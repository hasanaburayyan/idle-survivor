using Godot;
using System;
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
	private ProgressBar ProgressBar;

	private ulong trackingId;
	private SpacetimeDB.Types.ActiveTask? currentTask;

	private static Activity s_autoRepeatActivity;

	private bool _autoRepeatArmed;

	public override void _Ready()
	{
		ActivateButton = GetNode<Button>("%ActivateButton");
		CostLabel = GetNode<Label>("%CostLabel");
		DurationLabel = GetNode<Label>("%DurationLabel");
		ProgressBar = GetNode<ProgressBar>("%ProgressBar");

		ActivateButton.Pressed += OnActivatePressed;
		ActivateButton.GuiInput += OnActivateButtonGuiInput;
	}

	public override void _ExitTree()
	{
		if (ReferenceEquals(s_autoRepeatActivity, this))
		{
			s_autoRepeatActivity = null;
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

		var activity = conn.Db.Activity.Id.Find(id);
		Format(activity);

		conn.Db.Activity.OnUpdate += (SpacetimeDB.Types.EventContext ctx, SpacetimeDB.Types.Activity oldActivity, SpacetimeDB.Types.Activity newActivity) =>
		{
			if (newActivity.Id != trackingId) return;
			Format(newActivity);
		};

		var identity = SpacetimeNetworkManager.Instance.LocalIdentity;
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

		if (currentTask == null)
		{
			RequestStartActivity();
		}
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

		if (task.Type == activity.Type)
		{
			ActivateButton.Disabled = true;
			ProgressBar.Visible = true;
			ProgressBar.Value = 0;
		}
		else
		{
			ActivateButton.Disabled = true;
			ProgressBar.Visible = false;
		}
	}

	private void OnActiveTaskFinished(SpacetimeDB.Types.ActiveTask finishedTask)
	{
		currentTask = null;
		ActivateButton.Disabled = false;
		ProgressBar.Visible = false;
		ProgressBar.Value = 0;

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var activity = conn.Db.Activity.Id.Find(trackingId);
		if (_autoRepeatArmed
			&& ReferenceEquals(s_autoRepeatActivity, this)
			&& finishedTask.Type == activity.Type)
		{
			var player = conn.Db.Player.Identity.Find(SpacetimeNetworkManager.Instance.LocalIdentity);
			if (player is not null && IsLocationValid(activity.RequiredLocation, player.Location))
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
		var player = conn.Db.Player.Identity.Find(SpacetimeNetworkManager.Instance.LocalIdentity);
		if (activity is not null && player is not null && IsLocationValid(activity.RequiredLocation, player.Location))
			RequestStartActivity();
	}

	private void RefreshFormat()
	{
		var conn = SpacetimeNetworkManager.Instance?.Conn;
		if (conn == null) return;
		if (conn.Db.Activity.Id.Find(trackingId) is SpacetimeDB.Types.Activity row)
		{
			Format(row);
		}
	}

	public override void _Process(double delta)
	{
		if (currentTask is not SpacetimeDB.Types.ActiveTask task) return;

		var activity = SpacetimeNetworkManager.Instance.Conn.Db.Activity.Id.Find(trackingId);
		if (task.Type != activity.Type) return;

		var startUs = task.StartedAt.MicrosecondsSinceUnixEpoch;
		var endUs = task.CompletesAt.MicrosecondsSinceUnixEpoch;
		var totalUs = (double)(endUs - startUs);
		if (totalUs <= 0)
		{
			ProgressBar.Value = 100;
			return;
		}

		var nowUs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L;
		var elapsedUs = (double)(nowUs - startUs);
		var progress = Math.Clamp(elapsedUs / totalUs * 100.0, 0.0, 100.0);
		ProgressBar.Value = progress;
	}

	private void Format(SpacetimeDB.Types.Activity activity)
	{
		ActivateButton.Text = activity.Type.ToString() + (_autoRepeatArmed ? " (AUTO)" : "");

		CostLabel.Text = "Cost: ";
		foreach (var cost in activity.Cost)
		{
			CostLabel.Text += $"{cost.Amount} {cost.Type},";
		}
		CostLabel.Text = CostLabel.Text.TrimSuffix(",");
		if (activity.Cost.Count == 0)
		{
			CostLabel.Text = "";
		}

		var seconds = activity.DurationMs / 1000.0;
		DurationLabel.Text = seconds >= 1.0 ? $"{seconds:F0}s" : $"{seconds:F1}s";
	}

	private void OnActivatePressed()
	{
		RequestStartActivity();
	}

	private void RequestStartActivity()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var activity = conn.Db.Activity.Id.Find(trackingId);
		conn.Reducers.StartActivity(activity.Type);
	}
}
