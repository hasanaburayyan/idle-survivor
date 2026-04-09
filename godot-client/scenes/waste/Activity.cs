using Godot;
using System;
using SpacetimeDB;
using SpacetimeDB.Types;
using System.Linq;

public partial class Activity : HBoxContainer
{	
	public Button ActivateButton;
	public Label CostLabel;

	private ulong trackingId;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		ActivateButton = GetNode<Button>("%ActivateButton");
		CostLabel = GetNode<Label>("%CostLabel");

		ActivateButton.Pressed += OnActivatePressed;
	}

	public void InitActivityTracking(ulong id) {
		trackingId = id;
		var conn = SpacetimeNetworkManager.Instance.Conn;

		var activity = conn.Db.Activity.Id.Find(id);
		Format(activity);

		conn.Db.Activity.OnUpdate += (EventContext ctx, SpacetimeDB.Types.Activity oldActivity, SpacetimeDB.Types.Activity newActivity) => {
			if (newActivity.Id != trackingId) {
				return;
			}
			GD.Print($"Updating label: {newActivity}");
			Format(newActivity);
		};
	}

	private void Format(SpacetimeDB.Types.Activity activity) {
		var conn = SpacetimeNetworkManager.Instance.Conn;

		ActivateButton.Text = activity.Type.ToString();
		CostLabel.Text = "Cost: ";

		foreach (var cost in activity.Cost) {
			CostLabel.Text += $"{cost.Amount} {cost.Type},";
			// var resource = conn.Db.ResourceTracker.ByOwnerAndType.Filter((Owner: SpacetimeNetworkManager.Instance.LocalIdentity, Type: cost.Type)).First();
			// if (resource.Amount < cost.Amount) {
			// 	ActivateButton.Disabled = true;
			// }
		}

		CostLabel.Text = CostLabel.Text.TrimSuffix(",");

		if (activity.Cost.Count == 0) {
			CostLabel.Text = "";
		}
	}

	private void OnActivatePressed() {
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var activity = conn.Db.Activity.Id.Find(trackingId);
		GD.Print($"Activity {activity}");

		conn.Reducers.ActivityOnInterval(SpacetimeNetworkManager.Instance.LocalIdentity, activity.Type, 100, false);
	}
}
