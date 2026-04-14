using Godot;
using System;
using System.Collections.Generic;
using SpacetimeDB;
using SpacetimeDB.ClientApi;
using SpacetimeDB.Types;
using System.Linq;

public partial class Activity : VBoxContainer
{
	private static bool IsLocationValid(LocationType? required, LocationType playerLoc) =>
		required is null ||
		required == playerLoc ||
		(required == LocationType.Shelter && playerLoc == LocationType.GuildHall);

	private Button ActivateButton;
	private Label CostLabel;
	private Label DurationLabel;
	private Control UpgradeRow;
	private Button UpgradeButton;
	private Label UpgradeCostLabel;
	private ProgressBar ProgressBar;

	private ulong trackingId;
	private SpacetimeDB.Types.ActiveTask? currentTask;

	private static int s_autoRepeatCount;

	private bool _autoRepeatArmed;

	private SpacetimeDB.Identity _trackedIdentity;

	private bool _progressAnimating;
	private ulong _progressLocalStartUsec;
	private double _progressInitialPercent;
	private ulong _progressRemainingUsec;

	private bool _startActivityPending;

	private bool _playerStatHandlersRegistered;
	private bool _resourceHandlersRegistered;
	private bool _playerLevelHandlersRegistered;

	public override void _Ready()
	{
		ActivateButton = GetNode<Button>("%ActivateButton");
		CostLabel = GetNode<Label>("%CostLabel");
		DurationLabel = GetNode<Label>("%DurationLabel");
		UpgradeRow = GetNode<Control>("%UpgradeRow");
		UpgradeButton = GetNode<Button>("%UpgradeButton");
		UpgradeCostLabel = GetNode<Label>("%UpgradeCostLabel");
		ProgressBar = GetNode<ProgressBar>("%ProgressBar");

		ActivateButton.Pressed += OnActivatePressed;
		ActivateButton.GuiInput += OnActivateButtonGuiInput;
		UpgradeButton.Pressed += OnUpgradePressed;

		ProgressBar.Visible = true;
		ProgressBar.Value = 0;
	}

	public override void _ExitTree()
	{
		var conn = SpacetimeNetworkManager.Instance?.Conn;
		if (conn != null)
		{
			conn.Reducers.OnStartActivity -= OnStartActivityReducerInstance;
			conn.Reducers.OnUpgradeActivity -= OnUpgradeActivityReducer;

			conn.Db.Activity.OnUpdate -= OnTrackedActivityUpdate;
			conn.Db.ActiveTask.OnInsert -= OnTrackedActiveTaskInsert;
			conn.Db.ActiveTask.OnDelete -= OnTrackedActiveTaskDelete;
		}

		if (_playerStatHandlersRegistered)
		{
			if (conn != null)
			{
				conn.Db.PlayerStat.OnInsert -= OnLocalPlayerStatInsert;
				conn.Db.PlayerStat.OnUpdate -= OnLocalPlayerStatUpdate;
			}
			_playerStatHandlersRegistered = false;
		}

		if (_resourceHandlersRegistered)
		{
			if (conn != null)
			{
				conn.Db.ResourceTracker.OnInsert -= OnLocalResourceTrackerChange;
				conn.Db.ResourceTracker.OnUpdate -= OnLocalResourceTrackerChangeUpdate;
			}
			_resourceHandlersRegistered = false;
		}

		if (_playerLevelHandlersRegistered)
		{
			if (conn != null)
			{
				conn.Db.PlayerLevel.OnInsert -= OnLocalPlayerLevelInsert;
				conn.Db.PlayerLevel.OnUpdate -= OnLocalPlayerLevelUpdate;
			}
			_playerLevelHandlersRegistered = false;
		}

		if (_autoRepeatArmed)
		{
			_autoRepeatArmed = false;
			s_autoRepeatCount = Math.Max(0, s_autoRepeatCount - 1);
		}
		_startActivityPending = false;
		base._ExitTree();
	}

	public void InitActivityTracking(ulong id)
	{
		trackingId = id;
		var conn = SpacetimeNetworkManager.Instance.Conn;

		conn.Reducers.OnStartActivity -= OnStartActivityReducerInstance;
		conn.Reducers.OnStartActivity += OnStartActivityReducerInstance;
		conn.Reducers.OnUpgradeActivity -= OnUpgradeActivityReducer;
		conn.Reducers.OnUpgradeActivity += OnUpgradeActivityReducer;

		var activity = conn.Db.Activity.Id.Find(id);
		Format(activity);

		_trackedIdentity = SpacetimeNetworkManager.Instance.LocalIdentity;

		conn.Db.Activity.OnUpdate += OnTrackedActivityUpdate;

		conn.Db.PlayerStat.OnInsert += OnLocalPlayerStatInsert;
		conn.Db.PlayerStat.OnUpdate += OnLocalPlayerStatUpdate;
		_playerStatHandlersRegistered = true;

		conn.Db.ResourceTracker.OnInsert += OnLocalResourceTrackerChange;
		conn.Db.ResourceTracker.OnUpdate += OnLocalResourceTrackerChangeUpdate;
		_resourceHandlersRegistered = true;

		conn.Db.PlayerLevel.OnInsert += OnLocalPlayerLevelInsert;
		conn.Db.PlayerLevel.OnUpdate += OnLocalPlayerLevelUpdate;
		_playerLevelHandlersRegistered = true;

		foreach (var task in conn.Db.ActiveTask.Participant.Filter(_trackedIdentity))
		{
			if (activity is not null && task.Type == activity.Type)
			{
				OnActiveTaskStarted(task);
				break;
			}
		}

		conn.Db.ActiveTask.OnInsert += OnTrackedActiveTaskInsert;
		conn.Db.ActiveTask.OnDelete += OnTrackedActiveTaskDelete;
	}

	private void OnTrackedActivityUpdate(SpacetimeDB.Types.EventContext ctx, SpacetimeDB.Types.Activity oldActivity, SpacetimeDB.Types.Activity newActivity)
	{
		if (newActivity.Id != trackingId) return;
		Format(newActivity);
	}

	private void OnTrackedActiveTaskInsert(SpacetimeDB.Types.EventContext ctx, SpacetimeDB.Types.ActiveTask task)
	{
		if (task.Participant != _trackedIdentity) return;
		OnActiveTaskStarted(task);
	}

	private void OnTrackedActiveTaskDelete(SpacetimeDB.Types.EventContext ctx, SpacetimeDB.Types.ActiveTask task)
	{
		if (task.Participant != _trackedIdentity) return;
		if (currentTask is null || currentTask?.Id != task.Id) return;
		OnActiveTaskFinished(task);
	}

	private void OnLocalPlayerStatInsert(SpacetimeDB.Types.EventContext ctx, SpacetimeDB.Types.PlayerStat row)
	{
		if (row.Owner != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;
		RefreshFormat();
	}

	private void OnLocalPlayerStatUpdate(SpacetimeDB.Types.EventContext ctx, SpacetimeDB.Types.PlayerStat oldRow, SpacetimeDB.Types.PlayerStat newRow)
	{
		if (newRow.Owner != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;
		RefreshFormat();
	}

	private void OnLocalResourceTrackerChange(SpacetimeDB.Types.EventContext ctx, SpacetimeDB.Types.ResourceTracker row)
	{
		if (row.Owner != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;
		RefreshFormat();
	}

	private void OnLocalResourceTrackerChangeUpdate(SpacetimeDB.Types.EventContext ctx, SpacetimeDB.Types.ResourceTracker oldRow, SpacetimeDB.Types.ResourceTracker newRow)
	{
		if (newRow.Owner != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;
		RefreshFormat();
	}

	private void OnLocalPlayerLevelInsert(SpacetimeDB.Types.EventContext ctx, SpacetimeDB.Types.PlayerLevel row)
	{
		if (row.Owner != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;
		RefreshFormat();
	}

	private void OnLocalPlayerLevelUpdate(SpacetimeDB.Types.EventContext ctx, SpacetimeDB.Types.PlayerLevel oldRow, SpacetimeDB.Types.PlayerLevel newRow)
	{
		if (newRow.Owner != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;
		RefreshFormat();
	}

	private void OnUpgradeActivityReducer(SpacetimeDB.Types.ReducerEventContext ctx, ActivityType type)
	{
		var conn = SpacetimeNetworkManager.Instance?.Conn;
		if (conn == null) return;
		if (conn.Db.Activity.Id.Find(trackingId) is not SpacetimeDB.Types.Activity row || row.Type != type)
			return;
		RefreshFormat();
	}

	private void OnActivateButtonGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Right)
		{
			ToggleAutoRepeat();
			ActivateButton.GetViewport().SetInputAsHandled();
		}
	}

	private void ToggleAutoRepeat()
	{
		if (_autoRepeatArmed)
		{
			_autoRepeatArmed = false;
			s_autoRepeatCount = Math.Max(0, s_autoRepeatCount - 1);
			RefreshFormat();
			return;
		}

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		int maxSlots = GetMaxAutoSlots(conn, localId);
		if (s_autoRepeatCount >= maxSlots)
			return;

		_autoRepeatArmed = true;
		s_autoRepeatCount++;
		RefreshFormat();

		var row = conn.Db.Activity.Id.Find(trackingId);
		bool alreadyRunningThis = currentTask != null && currentTask?.Type == row?.Type;
		if (!alreadyRunningThis && row is not null && ActivityMeetsUnlockCriteria(conn, localId, row))
			RequestStartActivity();
	}

	private void OnStartActivityReducerInstance(SpacetimeDB.Types.ReducerEventContext ctx, SpacetimeDB.Types.ActivityType type)
	{
		if (!_autoRepeatArmed) return;
		var conn = SpacetimeNetworkManager.Instance?.Conn;
		if (conn == null) return;
		if (conn.Db.Activity.Id.Find(trackingId) is not SpacetimeDB.Types.Activity activity
			|| activity.Type != type)
			return;

		switch (ctx.Event.Status)
		{
			case Status.Failed:
			case Status.OutOfEnergy:
				_startActivityPending = false;
				_autoRepeatArmed = false;
				s_autoRepeatCount = Math.Max(0, s_autoRepeatCount - 1);
				RefreshFormat();
				break;
		}
	}

	private void OnActiveTaskStarted(SpacetimeDB.Types.ActiveTask task)
	{
		var activity = SpacetimeNetworkManager.Instance.Conn.Db.Activity.Id.Find(trackingId);
		if (activity is null || task.Type != activity.Type)
			return;

		_startActivityPending = false;
		currentTask = task;
		RefreshFormat();

		var startUs = (long)task.StartedAt.MicrosecondsSinceUnixEpoch;
		var endUs = (long)task.CompletesAt.MicrosecondsSinceUnixEpoch;
		var totalUs = endUs - startUs;
		if (totalUs <= 0)
		{
			_progressAnimating = false;
			ProgressBar.Value = 100;
			return;
		}

		var wallNowUs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L;
		var alreadyUs = Math.Max(0L, Math.Min(wallNowUs - startUs, totalUs));
		_progressInitialPercent = alreadyUs / (double)totalUs * 100.0;
		_progressRemainingUsec = (ulong)Math.Max(1, totalUs - alreadyUs);
		_progressLocalStartUsec = Time.GetTicksUsec();
		_progressAnimating = true;
		ProgressBar.Value = Math.Clamp(_progressInitialPercent, 0.0, 100.0);
	}

	private void OnActiveTaskFinished(SpacetimeDB.Types.ActiveTask finishedTask)
	{
		currentTask = null;
		_progressAnimating = false;
		ProgressBar.Value = 0;
		RefreshFormat();

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var activity = conn.Db.Activity.Id.Find(trackingId);
		if (activity is null)
			return;

		if (_autoRepeatArmed && finishedTask.Type == activity.Type)
		{
			var player = conn.Db.Player.Identity.Find(SpacetimeNetworkManager.Instance.LocalIdentity);
			if (player is not null
				&& IsLocationValid(activity.RequiredLocation, player.Location)
				&& ActivityMeetsUnlockCriteria(conn, SpacetimeNetworkManager.Instance.LocalIdentity, activity))
			{
				RequestStartActivity();
			}
		}
	}

	public void TryResumeAutoRepeat()
	{
		if (!_autoRepeatArmed || currentTask != null)
			return;
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var activity = conn.Db.Activity.Id.Find(trackingId);
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		var player = conn.Db.Player.Identity.Find(localId);
		if (activity is not null && player is not null && IsLocationValid(activity.RequiredLocation, player.Location)
			&& ActivityMeetsUnlockCriteria(conn, localId, activity))
			RequestStartActivity();
	}

	public void RefreshFormat()
	{
		var conn = SpacetimeNetworkManager.Instance?.Conn;
		if (conn == null) return;
		if (conn.Db.Activity.Id.Find(trackingId) is not SpacetimeDB.Types.Activity row)
		{
			DisarmAutoRepeatIfThis();
			Visible = false;
			return;
		}
		Format(row);
	}

	public override void _Process(double delta)
	{
		if (!_progressAnimating || currentTask is not SpacetimeDB.Types.ActiveTask task)
			return;

		var activity = SpacetimeNetworkManager.Instance.Conn.Db.Activity.Id.Find(trackingId);
		if (activity is null || task.Type != activity.Type)
			return;

		if (_progressInitialPercent >= 100.0)
		{
			ProgressBar.Value = 100;
			return;
		}

		var monoElapsedUsec = Time.GetTicksUsec() - _progressLocalStartUsec;
		var span = 100.0 - _progressInitialPercent;
		var deltaP = monoElapsedUsec / (double)_progressRemainingUsec * span;
		ProgressBar.Value = Math.Clamp(_progressInitialPercent + deltaP, 0.0, 100.0);
	}

	private static ulong ScaledUpgradeCostPreview(ulong baseAmount, uint level) =>
		baseAmount * (ulong)level * (ulong)level;

	private static void AppendNextUpgradeCostsPreview(ActivityType type, uint currentLevel, List<ActivityCost> dest)
	{
		void Add(ResourceType rt, ulong baseAmt) =>
			dest.Add(new ActivityCost { Type = rt, Amount = ScaledUpgradeCostPreview(baseAmt, currentLevel) });

		switch (type)
		{
			case ActivityType.Scavenge:
				Add(ResourceType.Food, 5);
				Add(ResourceType.Money, 5);
				Add(ResourceType.Wood, 5);
				Add(ResourceType.Metal, 5);
				Add(ResourceType.Fabric, 5);
				Add(ResourceType.Parts, 5);
				break;
			case ActivityType.ChopWood:
				Add(ResourceType.Food, 8);
				Add(ResourceType.Metal, 4);
				break;
			case ActivityType.Mine:
				Add(ResourceType.Wood, 8);
				Add(ResourceType.Food, 4);
				break;
			default:
				break;
		}
	}

	private static ulong GetResourceAmount(DbConnection conn, SpacetimeDB.Identity owner, ResourceType type)
	{
		foreach (var row in conn.Db.ResourceTracker.ByOwnerAndType.Filter((Owner: owner, Type: type)))
			return row.Amount;
		return 0;
	}

	private static bool PlayerCanAfford(DbConnection conn, SpacetimeDB.Identity owner, List<ActivityCost> costs)
	{
		foreach (var c in costs)
		{
			if (GetResourceAmount(conn, owner, c.Type) < c.Amount)
				return false;
		}
		return true;
	}

	public static string GetActivityDisplayName(ActivityType type) => type switch
	{
		ActivityType.Scavenge => "Scavenge",
		ActivityType.ChopWood => "Chop Wood",
		ActivityType.Mine => "Mine",
		_ => type.ToString()
	};

	private static int GetMaxAutoSlots(DbConnection conn, SpacetimeDB.Identity owner)
	{
		int slots = 0;
		foreach (var skillDef in conn.Db.SkillDefinition.Iter())
		{
			if (!skillDef.Name.StartsWith("Auto-")) continue;
			if (conn.Db.PlayerSkill.BySkillOwnerDef
				.Filter((Owner: owner, SkillDefinitionId: skillDef.Id)).Any())
				slots++;
		}
		return slots;
	}

	private static uint GetPlayerLevel(DbConnection conn, SpacetimeDB.Identity owner)
	{
		var pl = conn.Db.PlayerLevel.Owner.Find(owner);
		return pl?.Level ?? 0;
	}

	public static bool ActivityMeetsUnlockCriteria(DbConnection conn, SpacetimeDB.Identity owner, SpacetimeDB.Types.Activity activity)
	{
		if (activity.RequiredLevel is uint reqLevel)
		{
			if (GetPlayerLevel(conn, owner) < reqLevel)
				return false;
		}

		if (activity.RequiredStructure is string reqStruct)
		{
			bool found = false;
			foreach (var def in conn.Db.StructureDefinition.Iter())
			{
				if (def.Name != reqStruct) continue;
				if (conn.Db.PlayerStructure.ByOwnerAndDefinition
					.Filter((Owner: owner, DefinitionId: def.Id)).Any())
				{
					found = true;
				}
				break;
			}
			if (!found) return false;
		}

		if (activity.RequiredSkillId is ulong reqSkillId)
		{
			if (!conn.Db.PlayerSkill.BySkillOwnerDef
				.Filter((Owner: owner, SkillDefinitionId: reqSkillId)).Any())
				return false;
		}

		return true;
	}

	private static string GetUnlockRequirementText(SpacetimeDB.Types.Activity activity)
	{
		var parts = new List<string>();
		if (activity.RequiredLevel is uint reqLevel)
			parts.Add($"Level {reqLevel}");
		if (activity.RequiredStructure is string reqStruct)
			parts.Add($"Structure: {reqStruct}");
		if (activity.RequiredSkillId is ulong)
			parts.Add("Skill required");
		return parts.Count > 0 ? "Requires: " + string.Join(", ", parts) : "";
	}

	private void DisarmAutoRepeatIfThis()
	{
		if (_autoRepeatArmed)
		{
			_autoRepeatArmed = false;
			_startActivityPending = false;
			s_autoRepeatCount = Math.Max(0, s_autoRepeatCount - 1);
		}
	}

	private void Format(SpacetimeDB.Types.Activity activity)
	{
		if (activity.Type == ActivityType.Scavenge)
		{
			DisarmAutoRepeatIfThis();
			Visible = false;
			return;
		}

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		var player = conn.Db.Player.Identity.Find(localId);
		bool locOk = player is not null && IsLocationValid(activity.RequiredLocation, player.Location);

		if (!locOk)
		{
			DisarmAutoRepeatIfThis();
			Visible = false;
			return;
		}

		Visible = true;

		bool unlocked = ActivityMeetsUnlockCriteria(conn, localId, activity);

		if (!unlocked)
		{
			DisarmAutoRepeatIfThis();
			var lockText = GetUnlockRequirementText(activity);
			ActivateButton.Text = $"{GetActivityDisplayName(activity.Type)} [LOCKED]";
			ActivateButton.Disabled = true;
			CostLabel.Text = lockText;
			DurationLabel.Text = "";
			UpgradeRow.Visible = false;
			ProgressBar.Value = 0;
			return;
		}

		var title = $"{GetActivityDisplayName(activity.Type)} Lv{activity.Level}" + (_autoRepeatArmed ? " (AUTO)" : "");
		ActivateButton.Text = title;
		ActivateButton.Disabled = false;

		var costParts = new List<string>();
		if (activity.Cost.Count > 0)
			costParts.Add("Cost: " + string.Join(", ", activity.Cost.Select(c => $"{c.Amount} {c.Type}")));
		CostLabel.Text = string.Join(" — ", costParts);

		var seconds = activity.DurationMs / 1000.0;
		DurationLabel.Text = seconds >= 1.0 ? $"{seconds:F0}s" : $"{seconds:F1}s";

		UpgradeRow.Visible = true;
		var upgradeCosts = new List<ActivityCost>();
		AppendNextUpgradeCostsPreview(activity.Type, activity.Level, upgradeCosts);
		if (upgradeCosts.Count == 0)
		{
			UpgradeCostLabel.Text = "";
			UpgradeButton.Disabled = true;
		}
		else
		{
			UpgradeCostLabel.Text = "Next: " + string.Join(", ", upgradeCosts.Select(c => $"{c.Amount} {c.Type}"));
			UpgradeButton.Disabled = !PlayerCanAfford(conn, localId, upgradeCosts);
		}
	}

	private void OnActivatePressed()
	{
		if (currentTask != null)
			return;
		RequestStartActivity();
	}

	private void OnUpgradePressed()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		if (conn.Db.Activity.Id.Find(trackingId) is not SpacetimeDB.Types.Activity activity)
			return;
		conn.Reducers.UpgradeActivity(activity.Type);
	}

	private void RequestStartActivity()
	{
		if (_startActivityPending)
			return;

		_startActivityPending = true;
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var activity = conn.Db.Activity.Id.Find(trackingId);
		conn.Reducers.StartActivity(activity.Type);
	}
}
