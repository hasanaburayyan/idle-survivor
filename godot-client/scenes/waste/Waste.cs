using Godot;
using SpacetimeDB.Types;
using System;

public partial class Waste : Node2D
{
	private SpacetimeDB.Types.Player player;
	private Marker2D PlayerSpawnPosition;
	private VBoxContainer LeftSide;
	private PlayerStatsPanel StatsPanel;
	private VBoxContainer ActivitiesPanel;


	private PackedScene PlayerScene = GD.Load<PackedScene>("uid://cl6yviutw6arx");
	private PackedScene ResourceTrackingScene = GD.Load<PackedScene>("uid://bmw2ixd8nj1t8");
	private PackedScene ActivityScene = GD.Load<PackedScene>("uid://bjckoiwufesye");

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		PlayerSpawnPosition = GetNode<Marker2D>("%PlayerSpawnLocation");
		LeftSide = GetNode<VBoxContainer>("%LeftSide");
		StatsPanel = GetNode<PlayerStatsPanel>("%PlayerStatsPanel");
		ActivitiesPanel = GetNode<VBoxContainer>("%Activities");

		GD.Print(SpacetimeNetworkManager.Instance.LocalIdentity);
		player = SpacetimeNetworkManager.Instance.Conn.Db.Player.Identity.Find(SpacetimeNetworkManager.Instance.LocalIdentity);

		GD.Print(player);

		// Create a new player scene
		var playerScene = PlayerScene.Instantiate<Player>();
		playerScene.Position = PlayerSpawnPosition.Position;
		AddChild(playerScene);
		playerScene.SetName(player.DisplayName);


		StatsPanel.InitStats(SpacetimeNetworkManager.Instance.LocalIdentity);

		var conn = SpacetimeNetworkManager.Instance.Conn;

		// seed initial resources 
		var playerResources = conn.Db.ResourceTracker.Owner.Filter(SpacetimeNetworkManager.Instance.LocalIdentity);
		foreach (var resource in playerResources)
		{
			var resourceTracker = ResourceTrackingScene.Instantiate<ResourceTracker>();
			resourceTracker.Name = resource.Id.ToString();
			LeftSide.AddChild(resourceTracker);
			resourceTracker.InitResourceTracking(resource.Id);
		}

		// seed initial activities
		var playerActivities = conn.Db.Activity.Participant.Filter(SpacetimeNetworkManager.Instance.LocalIdentity);
		foreach (var activity in playerActivities)
		{
			var activitySelection = ActivityScene.Instantiate<Activity>();
			activitySelection.Name = activity.Id.ToString();
			ActivitiesPanel.AddChild(activitySelection);
			activitySelection.InitActivityTracking(activity.Id);
		}

		conn.Db.ResourceTracker.OnInsert += OnResourceTrackerInsert;
		conn.Db.Activity.OnInsert += OnActivityInsert;
	}


	private void OnResourceTrackerInsert(EventContext ctx, SpacetimeDB.Types.ResourceTracker tracker)
	{
		if (tracker.Owner != SpacetimeNetworkManager.Instance.LocalIdentity)
		{
			return;
		}

		var resourceTracker = ResourceTrackingScene.Instantiate<ResourceTracker>();
		resourceTracker.Name = tracker.Id.ToString();
		LeftSide.AddChild(resourceTracker);
		resourceTracker.InitResourceTracking(tracker.Id);
	}

	private void OnActivityInsert(EventContext ctx, SpacetimeDB.Types.Activity activity)
	{
		if (activity.Participant != SpacetimeNetworkManager.Instance.LocalIdentity)
		{
			return;
		}

		var activitySelection = ActivityScene.Instantiate<Activity>();
		activitySelection.Name = activity.Id.ToString();
		ActivitiesPanel.AddChild(activitySelection);
		activitySelection.InitActivityTracking(activity.Id);
	}

	private void OnScavengePressed()
	{
		GD.Print("Scavenged Pressed");
		SpacetimeNetworkManager.Instance.Conn.Reducers.Scavenge(SpacetimeNetworkManager.Instance.LocalIdentity);
	}
}
