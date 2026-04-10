using Godot;
using System;
using SpacetimeDB;
using SpacetimeDB.Types;
using System.Linq;

public partial class Activity : VBoxContainer
{
	private Button ActivateButton;
	private Label CostLabel;
	private Label DurationLabel;
	private ProgressBar ProgressBar;

	private ulong trackingId;
	private SpacetimeDB.Types.ActiveTask? currentTask;

	public override void _Ready()
	{
		ActivateButton = GetNode<Button>("%ActivateButton");
		CostLabel = GetNode<Label>("%CostLabel");
		DurationLabel = GetNode<Label>("%DurationLabel");
		ProgressBar = GetNode<ProgressBar>("%ProgressBar");

		ActivateButton.Pressed += OnActivatePressed;
	}

	public void InitActivityTracking(ulong id) {
		trackingId = id;
		var conn = SpacetimeNetworkManager.Instance.Conn;

		var activity = conn.Db.Activity.Id.Find(id);
		Format(activity);

		conn.Db.Activity.OnUpdate += (EventContext ctx, SpacetimeDB.Types.Activity oldActivity, SpacetimeDB.Types.Activity newActivity) => {
			if (newActivity.Id != trackingId) return;
			Format(newActivity);
		};

		var identity = SpacetimeNetworkManager.Instance.LocalIdentity;
		var existingTask = conn.Db.ActiveTask.Participant.Find(identity);
		if (existingTask is SpacetimeDB.Types.ActiveTask task) {
			OnActiveTaskStarted(task);
		}

		conn.Db.ActiveTask.OnInsert += (EventContext ctx, SpacetimeDB.Types.ActiveTask task) => {
			if (task.Participant != identity) return;
			OnActiveTaskStarted(task);
		};

		conn.Db.ActiveTask.OnDelete += (EventContext ctx, SpacetimeDB.Types.ActiveTask task) => {
			if (task.Participant != identity) return;
			OnActiveTaskFinished();
		};
	}

	private void OnActiveTaskStarted(SpacetimeDB.Types.ActiveTask task) {
		currentTask = task;
		var activity = SpacetimeNetworkManager.Instance.Conn.Db.Activity.Id.Find(trackingId);

		if (task.Type == activity.Type) {
			ActivateButton.Disabled = true;
			ProgressBar.Visible = true;
			ProgressBar.Value = 0;
		} else {
			ActivateButton.Disabled = true;
			ProgressBar.Visible = false;
		}
	}

	private void OnActiveTaskFinished() {
		currentTask = null;
		ActivateButton.Disabled = false;
		ProgressBar.Visible = false;
		ProgressBar.Value = 0;
	}

	public override void _Process(double delta) {
		if (currentTask is not SpacetimeDB.Types.ActiveTask task) return;

		var activity = SpacetimeNetworkManager.Instance.Conn.Db.Activity.Id.Find(trackingId);
		if (task.Type != activity.Type) return;

		var startUs = task.StartedAt.MicrosecondsSinceUnixEpoch;
		var endUs = task.CompletesAt.MicrosecondsSinceUnixEpoch;
		var totalUs = (double)(endUs - startUs);
		if (totalUs <= 0) {
			ProgressBar.Value = 100;
			return;
		}

		var nowUs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L;
		var elapsedUs = (double)(nowUs - startUs);
		var progress = Math.Clamp(elapsedUs / totalUs * 100.0, 0.0, 100.0);
		ProgressBar.Value = progress;
	}

	private void Format(SpacetimeDB.Types.Activity activity) {
		ActivateButton.Text = activity.Type.ToString();

		CostLabel.Text = "Cost: ";
		foreach (var cost in activity.Cost) {
			CostLabel.Text += $"{cost.Amount} {cost.Type},";
		}
		CostLabel.Text = CostLabel.Text.TrimSuffix(",");
		if (activity.Cost.Count == 0) {
			CostLabel.Text = "";
		}

		var seconds = activity.DurationMs / 1000.0;
		DurationLabel.Text = seconds >= 1.0 ? $"{seconds:F0}s" : $"{seconds:F1}s";
	}

	private void OnActivatePressed() {
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var activity = conn.Db.Activity.Id.Find(trackingId);
		conn.Reducers.StartActivity(activity.Type);
	}
}
