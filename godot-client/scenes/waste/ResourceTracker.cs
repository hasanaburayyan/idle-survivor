using Godot;
using SpacetimeDB.Types;
using System;

public partial class ResourceTracker : HBoxContainer
{
	private Label NameLabel;
	private Label AmountLabel;

	private ulong trackingId;

	public override void _Ready()
	{
		NameLabel = GetNode<Label>("%NameLabel");
		AmountLabel = GetNode<Label>("%AmountLabel");
		AmountLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.4f));
	}

	public void InitResourceTracking(ulong id) {
		var conn = SpacetimeNetworkManager.Instance.Conn;

		// initial setup
		var resourcetracker = conn.Db.ResourceTracker.Id.Find(id);

		NameLabel.Text = resourcetracker.Type.ToString();
		AmountLabel.Text = resourcetracker.Amount.ToString();

		trackingId = id;

		conn.Db.ResourceTracker.OnUpdate += (EventContext ctx, SpacetimeDB.Types.ResourceTracker oldTracker, SpacetimeDB.Types.ResourceTracker newTracker) => {
			if (newTracker.Id != trackingId) {
				return;
			}

			NameLabel.Text = newTracker.Type.ToString();
			AmountLabel.Text = newTracker.Amount.ToString();
		};
	}
}
