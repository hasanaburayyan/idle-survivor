using Godot;
using SpacetimeDB.Types;
using System;
using System.Collections.Generic;

public partial class Waste : Node2D
{
	private SpacetimeDB.Types.Player player;
	private Marker2D PlayerSpawnPosition;
	private VBoxContainer LeftSide;
	private PlayerStatsPanel StatsPanel;
	private VBoxContainer ActivitiesPanel;
	private Player _localPlayerNode;
	private GameMenu _gameMenu;

	private PackedScene PlayerScene = GD.Load<PackedScene>("uid://cl6yviutw6arx");
	private PackedScene ResourceTrackingScene = GD.Load<PackedScene>("uid://bmw2ixd8nj1t8");
	private PackedScene ActivityScene = GD.Load<PackedScene>("uid://bjckoiwufesye");
	private PackedScene GameMenuScene = GD.Load<PackedScene>("res://scenes/menu/GameMenu.tscn");

	private Dictionary<SpacetimeDB.Identity, Player> _guildMemberSprites = new();
	private bool _inGuildSession;
	private RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		PlayerSpawnPosition = GetNode<Marker2D>("%PlayerSpawnLocation");
		LeftSide = GetNode<VBoxContainer>("%LeftSide");
		StatsPanel = GetNode<PlayerStatsPanel>("%PlayerStatsPanel");
		ActivitiesPanel = GetNode<VBoxContainer>("%Activities");

		player = SpacetimeNetworkManager.Instance.Conn.Db.Player.Identity.Find(SpacetimeNetworkManager.Instance.LocalIdentity);

		_localPlayerNode = PlayerScene.Instantiate<Player>();
		_localPlayerNode.Position = PlayerSpawnPosition.Position;
		_localPlayerNode.ZIndex = 10;
		AddChild(_localPlayerNode);
		_localPlayerNode.SetName(player.DisplayName);

		StatsPanel.InitStats(SpacetimeNetworkManager.Instance.LocalIdentity);

		var conn = SpacetimeNetworkManager.Instance.Conn;

		var playerResources = conn.Db.ResourceTracker.Owner.Filter(SpacetimeNetworkManager.Instance.LocalIdentity);
		foreach (var resource in playerResources)
		{
			var resourceTracker = ResourceTrackingScene.Instantiate<ResourceTracker>();
			resourceTracker.Name = resource.Id.ToString();
			LeftSide.AddChild(resourceTracker);
			resourceTracker.InitResourceTracking(resource.Id);
		}

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

		_gameMenu = GameMenuScene.Instantiate<GameMenu>();
		AddChild(_gameMenu);
		_gameMenu.GuildSessionChanged += OnGuildSessionChanged;

		conn.Db.GuildMember.OnUpdate += OnGuildMemberUpdate;
		conn.Db.Player.OnUpdate += OnPlayerUpdate;
	}

	private void OnGuildSessionChanged(bool inSession)
	{
		_inGuildSession = inSession;
		if (inSession)
			SpawnGuildMembers();
		else
			DespawnAllGuildMembers();
	}

	private void SpawnGuildMembers()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		var membership = conn.Db.GuildMember.PlayerId.Find(localId);
		if (membership is null) return;

		foreach (var member in conn.Db.GuildMember.GuildId.Filter(membership.GuildId))
		{
			if (member.PlayerId == localId) continue;
			if (!member.InSession) continue;

			var memberPlayer = conn.Db.Player.Identity.Find(member.PlayerId);
			if (memberPlayer is null || !memberPlayer.Online) continue;

			SpawnMemberSprite(member.PlayerId, memberPlayer.DisplayName);
		}
	}

	private void SpawnMemberSprite(SpacetimeDB.Identity playerId, string displayName)
	{
		if (_guildMemberSprites.ContainsKey(playerId)) return;

		var sprite = PlayerScene.Instantiate<Player>();
		var viewport = GetViewportRect();
		float x = _rng.RandfRange(100, viewport.Size.X - 100);
		sprite.Position = new Vector2(x, PlayerSpawnPosition.Position.Y);
		sprite.ZIndex = 0;
		AddChild(sprite);
		sprite.SetName(displayName);
		_guildMemberSprites[playerId] = sprite;
	}

	private void DespawnMemberSprite(SpacetimeDB.Identity playerId)
	{
		if (_guildMemberSprites.TryGetValue(playerId, out var sprite))
		{
			sprite.QueueFree();
			_guildMemberSprites.Remove(playerId);
		}
	}

	private void DespawnAllGuildMembers()
	{
		foreach (var kvp in _guildMemberSprites)
			kvp.Value.QueueFree();
		_guildMemberSprites.Clear();
	}

	private void OnGuildMemberUpdate(EventContext ctx, SpacetimeDB.Types.GuildMember oldMember, SpacetimeDB.Types.GuildMember newMember)
	{
		if (!_inGuildSession) return;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		if (newMember.PlayerId == localId) return;

		var myMembership = SpacetimeNetworkManager.Instance.Conn.Db.GuildMember.PlayerId.Find(localId);
		if (myMembership is null || myMembership.GuildId != newMember.GuildId) return;

		if (!oldMember.InSession && newMember.InSession)
		{
			var memberPlayer = SpacetimeNetworkManager.Instance.Conn.Db.Player.Identity.Find(newMember.PlayerId);
			if (memberPlayer?.Online == true)
				SpawnMemberSprite(newMember.PlayerId, memberPlayer.DisplayName);
		}
		else if (oldMember.InSession && !newMember.InSession)
		{
			DespawnMemberSprite(newMember.PlayerId);
		}
	}

	private void OnPlayerUpdate(EventContext ctx, SpacetimeDB.Types.Player oldPlayer, SpacetimeDB.Types.Player newPlayer)
	{
		if (!_inGuildSession) return;
		if (newPlayer.Identity == SpacetimeNetworkManager.Instance.LocalIdentity) return;

		if (oldPlayer.Online && !newPlayer.Online)
			DespawnMemberSprite(newPlayer.Identity);
	}

	private void OnResourceTrackerInsert(EventContext ctx, SpacetimeDB.Types.ResourceTracker tracker)
	{
		if (tracker.Owner != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;

		var resourceTracker = ResourceTrackingScene.Instantiate<ResourceTracker>();
		resourceTracker.Name = tracker.Id.ToString();
		LeftSide.AddChild(resourceTracker);
		resourceTracker.InitResourceTracking(tracker.Id);
	}

	private void OnActivityInsert(EventContext ctx, SpacetimeDB.Types.Activity activity)
	{
		if (activity.Participant != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;

		var activitySelection = ActivityScene.Instantiate<Activity>();
		activitySelection.Name = activity.Id.ToString();
		ActivitiesPanel.AddChild(activitySelection);
		activitySelection.InitActivityTracking(activity.Id);
	}
}
