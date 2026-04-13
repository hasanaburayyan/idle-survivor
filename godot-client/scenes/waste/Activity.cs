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

	private static Activity s_autoRepeatActivity;

	private bool _autoRepeatArmed;

	/// <summary>Client-side progress fill for the matching activity only; not driven by per-frame DB reads.</summary>
	private bool _progressAnimating;
	private ulong _progressLocalStartUsec;
	private double _progressInitialPercent;
	private ulong _progressRemainingUsec;

	private bool _playerStatHandlersRegistered;
	private bool _resourceHandlersRegistered;

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
		if (ReferenceEquals(s_autoRepeatActivity, this))
		{
			s_autoRepeatActivity = null;
		}

		var connForReducers = SpacetimeNetworkManager.Instance?.Conn;
		if (connForReducers != null)
		{
			connForReducers.Reducers.OnStartActivity -= OnStartActivityReducer;
			connForReducers.Reducers.OnUpgradeActivity -= OnUpgradeActivityReducer;
		}

		if (_playerStatHandlersRegistered)
		{
			var conn = SpacetimeNetworkManager.Instance?.Conn;
			if (conn != null)
			{
				conn.Db.PlayerStat.OnInsert -= OnLocalPlayerStatInsert;
				conn.Db.PlayerStat.OnUpdate -= OnLocalPlayerStatUpdate;
			}
			_playerStatHandlersRegistered = false;
		}

		if (_resourceHandlersRegistered)
		{
			var conn = SpacetimeNetworkManager.Instance?.Conn;
			if (conn != null)
			{
				conn.Db.ResourceTracker.OnInsert -= OnLocalResourceTrackerChange;
				conn.Db.ResourceTracker.OnUpdate -= OnLocalResourceTrackerChangeUpdate;
			}
			_resourceHandlersRegistered = false;
		}

		_autoRepeatArmed = false;
		base._ExitTree();
	}

	public void InitActivityTracking(ulong id)
	{
		trackingId = id;
		var conn = SpacetimeNetworkManager.Instance.Conn;

		conn.Reducers.OnStartActivity -= OnStartActivityReducer;
		conn.Reducers.OnStartActivity += OnStartActivityReducer;
		conn.Reducers.OnUpgradeActivity -= OnUpgradeActivityReducer;
		conn.Reducers.OnUpgradeActivity += OnUpgradeActivityReducer;

		var activity = conn.Db.Activity.Id.Find(id);
		Format(activity);

		conn.Db.Activity.OnUpdate += (SpacetimeDB.Types.EventContext ctx, SpacetimeDB.Types.Activity oldActivity, SpacetimeDB.Types.Activity newActivity) =>
		{
			if (newActivity.Id != trackingId) return;
			Format(newActivity);
		};

		var identity = SpacetimeNetworkManager.Instance.LocalIdentity;
		conn.Db.PlayerStat.OnInsert += OnLocalPlayerStatInsert;
		conn.Db.PlayerStat.OnUpdate += OnLocalPlayerStatUpdate;
		_playerStatHandlersRegistered = true;

		conn.Db.ResourceTracker.OnInsert += OnLocalResourceTrackerChange;
		conn.Db.ResourceTracker.OnUpdate += OnLocalResourceTrackerChangeUpdate;
		_resourceHandlersRegistered = true;

		var existingTask = conn.Db.ActiveTask.Participant.Find(identity);
		if (existingTask is SpacetimeDB.Types.ActiveTask task)
		{
			OnActiveTaskStarted(task);
		}

		conn.Db.ActiveTask.OnInsert += (SpacetimeDB.Types.EventContext ctx, SpacetimeDB.Types.ActiveTask task) =>
		{
			if (task.Participant != identity) return;
			OnActiveTaskStarted(task);
		};

		conn.Db.ActiveTask.OnDelete += (SpacetimeDB.Types.EventContext ctx, SpacetimeDB.Types.ActiveTask task) =>
		{
			if (task.Participant != identity) return;
			OnActiveTaskFinished(task);
		};
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
		if (_autoRepeatArmed && ReferenceEquals(s_autoRepeatActivity, this))
		{
			_autoRepeatArmed = false;
			s_autoRepeatActivity = null;
			RefreshFormat();
			return;
		}

		if (s_autoRepeatActivity != null && !ReferenceEquals(s_autoRepeatActivity, this))
		{
			s_autoRepeatActivity._autoRepeatArmed = false;
			s_autoRepeatActivity.RefreshFormat();
		}

		s_autoRepeatActivity = this;
		_autoRepeatArmed = true;
		RefreshFormat();

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var row = conn.Db.Activity.Id.Find(trackingId);
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		bool alreadyRunningThis = currentTask != null && currentTask?.Type == row?.Type;
		if (!alreadyRunningThis && row is not null && ActivityMeetsUnlockCriteria(conn, localId, row))
			RequestStartActivity();
	}

	private static void OnStartActivityReducer(SpacetimeDB.Types.ReducerEventContext ctx, SpacetimeDB.Types.ActivityType type)
	{
		switch (ctx.Event.Status)
		{
			case Status.Failed:
			case Status.OutOfEnergy:
				var row = s_autoRepeatActivity;
				if (row == null || !row._autoRepeatArmed) return;
				var conn = SpacetimeNetworkManager.Instance?.Conn;
				if (conn == null) return;
				if (conn.Db.Activity.Id.Find(row.trackingId) is not SpacetimeDB.Types.Activity activity
					|| activity.Type != type)
				{
					return;
				}

				row._autoRepeatArmed = false;
				s_autoRepeatActivity = null;
				row.RefreshFormat();
				break;
		}
	}

	private void OnActiveTaskStarted(SpacetimeDB.Types.ActiveTask task)
	{
		currentTask = task;
		var activity = SpacetimeNetworkManager.Instance.Conn.Db.Activity.Id.Find(trackingId);

		RefreshFormat();

		if (task.Type != activity.Type)
		{
			_progressAnimating = false;
			ProgressBar.Value = 0;
			return;
		}

		var startUs = (long)task.StartedAt.MicrosecondsSinceUnixEpoch;
		var endUs = (long)task.CompletesAt.MicrosecondsSinceUnixEpoch;
		var totalUs = endUs - startUs;
		if (totalUs <= 0)
		{
			_progressAnimating = false;
			ProgressBar.Value = 100;
			return;
		}

		// One-time wall-clock catch-up so mid-task joins and network delay align with server window;
		// animation deltas use monotonic time only.
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
		if (_autoRepeatArmed
			&& ReferenceEquals(s_autoRepeatActivity, this)
			&& finishedTask.Type == activity.Type)
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
		if (!_autoRepeatArmed || !ReferenceEquals(s_autoRepeatActivity, this) || currentTask != null)
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
		if (task.Type != activity.Type)
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

	private static bool ActivityTypeSupportsUpgrade(ActivityType type) =>
		type != ActivityType.BuildShelter;

	/// <summary>Must match spacetimedb/Activities.cs ScaledUpgradeCost.</summary>
	private static ulong ScaledUpgradeCostPreview(ulong baseAmount, uint level) =>
		baseAmount * (ulong)level * (ulong)level;

	/// <summary>Must match spacetimedb/Activities.cs AppendNextUpgradeCosts.</summary>
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
			case ActivityType.LootBigWood:
				Add(ResourceType.Money, 12);
				break;
			case ActivityType.SearchFood:
				Add(ResourceType.Metal, 10);
				break;
			case ActivityType.SearchMoney:
				Add(ResourceType.Wood, 10);
				break;
			case ActivityType.SearchWood:
				Add(ResourceType.Fabric, 10);
				break;
			case ActivityType.SearchMetal:
				Add(ResourceType.Food, 10);
				break;
			case ActivityType.SearchFabric:
				Add(ResourceType.Parts, 10);
				break;
			case ActivityType.SearchParts:
				Add(ResourceType.Money, 10);
				break;
			case ActivityType.Salvage:
				Add(ResourceType.Food, 8);
				Add(ResourceType.Wood, 8);
				Add(ResourceType.Fabric, 8);
				break;
			case ActivityType.Study:
				Add(ResourceType.Food, 15);
				break;
			case ActivityType.Focus:
				Add(ResourceType.Parts, 15);
				break;
			case ActivityType.CarbLoad:
				Add(ResourceType.Money, 14);
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

	private static string GetActivityDisplayName(ActivityType type) => type switch
	{
		ActivityType.Scavenge => "Scavenge",
		ActivityType.LootBigWood => "Loot Big Wood",
		ActivityType.CarbLoad => "Carb Load",
		ActivityType.Study => "Study",
		ActivityType.Focus => "Focus",
		ActivityType.BuildShelter => "Build Shelter",
		ActivityType.Salvage => "Salvage",
		ActivityType.SearchFood => "Search for Food",
		ActivityType.SearchMoney => "Search for Money",
		ActivityType.SearchWood => "Search for Wood",
		ActivityType.SearchMetal => "Search for Metal",
		ActivityType.SearchFabric => "Search for Fabric",
		ActivityType.SearchParts => "Search for Parts",
		_ => type.ToString()
	};

	private static int GetStatValue(DbConnection conn, SpacetimeDB.Identity owner, StatType stat)
	{
		foreach (var row in conn.Db.PlayerStat.ByStatOwnerStat.Filter((Owner: owner, Stat: stat)))
			return row.Value;
		return 0;
	}

	private static bool ActivityMeetsUnlockCriteria(DbConnection conn, SpacetimeDB.Identity owner, SpacetimeDB.Types.Activity activity)
	{
		foreach (var c in activity.UnlockCriteria)
		{
			if (GetStatValue(conn, owner, c.Stat) < c.MinValue)
				return false;
		}
		return true;
	}

	private void DisarmAutoRepeatIfThis()
	{
		if (_autoRepeatArmed && ReferenceEquals(s_autoRepeatActivity, this))
		{
			_autoRepeatArmed = false;
			s_autoRepeatActivity = null;
		}
	}

	private void Format(SpacetimeDB.Types.Activity activity)
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		var player = conn.Db.Player.Identity.Find(localId);
		bool locOk = player is not null && IsLocationValid(activity.RequiredLocation, player.Location);
		bool unlocked = ActivityMeetsUnlockCriteria(conn, localId, activity);
		bool show = locOk && unlocked;

		if (!show)
		{
			DisarmAutoRepeatIfThis();
			Visible = false;
			return;
		}

		Visible = true;

		var title = $"{GetActivityDisplayName(activity.Type)} Lv{activity.Level}" + (_autoRepeatArmed ? " (AUTO)" : "");
		ActivateButton.Text = title;

		ActivateButton.Disabled = false;

		var costParts = new List<string>();
		if (activity.Cost.Count > 0)
			costParts.Add("Cost: " + string.Join(", ", activity.Cost.Select(c => $"{c.Amount} {c.Type}")));
		CostLabel.Text = string.Join(" — ", costParts);

		var seconds = activity.DurationMs / 1000.0;
		DurationLabel.Text = seconds >= 1.0 ? $"{seconds:F0}s" : $"{seconds:F1}s";

		if (ActivityTypeSupportsUpgrade(activity.Type))
		{
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
		else
		{
			UpgradeRow.Visible = false;
		}
	}

	private void OnActivatePressed()
	{
		RequestStartActivity();
	}

	private void OnUpgradePressed()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		if (conn.Db.Activity.Id.Find(trackingId) is not SpacetimeDB.Types.Activity activity)
			return;
		if (!ActivityTypeSupportsUpgrade(activity.Type))
			return;
		conn.Reducers.UpgradeActivity(activity.Type);
	}

	private void RequestStartActivity()
	{
		if (s_autoRepeatActivity != null && !ReferenceEquals(s_autoRepeatActivity, this))
		{
			s_autoRepeatActivity._autoRepeatArmed = false;
			s_autoRepeatActivity.RefreshFormat();
			s_autoRepeatActivity = null;
		}

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var activity = conn.Db.Activity.Id.Find(trackingId);
		conn.Reducers.StartActivity(activity.Type);
	}
}
