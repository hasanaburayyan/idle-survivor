using Godot;
using SpacetimeDB.Types;
using System;
using System.Collections.Generic;
using System.Linq;

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
	private bool _inGuildHall;
	private RandomNumberGenerator _rng = new();

	private Label _locationLabel;
	private Button _travelWasteButton;
	private Button _travelShelterButton;
	private Button _travelGuildHallButton;

	private PanelContainer _craftingPanel;
	private VBoxContainer _craftingList;

	private Dictionary<ulong, Activity> _activityNodes = new();

	public override void _Ready()
	{
		PlayerSpawnPosition = GetNode<Marker2D>("%PlayerSpawnLocation");
		LeftSide = GetNode<VBoxContainer>("%LeftSide");
		StatsPanel = GetNode<PlayerStatsPanel>("%PlayerStatsPanel");
		ActivitiesPanel = GetNode<VBoxContainer>("%Activities");

		var conn = SpacetimeNetworkManager.Instance.Conn;
		player = conn.Db.Player.Identity.Find(SpacetimeNetworkManager.Instance.LocalIdentity);

		_localPlayerNode = PlayerScene.Instantiate<Player>();
		_localPlayerNode.Position = PlayerSpawnPosition.Position;
		_localPlayerNode.ZIndex = 10;
		AddChild(_localPlayerNode);
		_localPlayerNode.SetName(player.DisplayName);

		StatsPanel.InitStats(SpacetimeNetworkManager.Instance.LocalIdentity);

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
			AddActivityNode(activity);
		}

		conn.Db.ResourceTracker.OnInsert += OnResourceTrackerInsert;
		conn.Db.Activity.OnInsert += OnActivityInsert;
		conn.Db.Activity.OnDelete += OnActivityDelete;

		_gameMenu = GameMenuScene.Instantiate<GameMenu>();
		AddChild(_gameMenu);
		_gameMenu.GuildSessionChanged += OnGuildSessionChanged;

		conn.Db.Player.OnUpdate += OnPlayerUpdate;

		conn.Db.PlayerShelter.OnInsert += OnPlayerShelterInsert;
		conn.Db.PlayerStructure.OnInsert += OnPlayerStructureInsert;

		BuildLocationBar();
		BuildCraftingPanel();
		RefreshLocationUI();
	}

	private void BuildLocationBar()
	{
		var locationBar = GetNode<HBoxContainer>("%LocationBar");

		_locationLabel = GetNode<Label>("%LocationLabel");
		_travelWasteButton = GetNode<Button>("%TravelWasteButton");
		_travelShelterButton = GetNode<Button>("%TravelShelterButton");
		_travelGuildHallButton = GetNode<Button>("%TravelGuildHallButton");

		_travelWasteButton.Pressed += () => SpacetimeNetworkManager.Instance.Conn.Reducers.Travel(LocationType.Waste);
		_travelShelterButton.Pressed += () => SpacetimeNetworkManager.Instance.Conn.Reducers.Travel(LocationType.Shelter);
		_travelGuildHallButton.Pressed += () => SpacetimeNetworkManager.Instance.Conn.Reducers.Travel(LocationType.GuildHall);
	}

	private void BuildCraftingPanel()
	{
		_craftingPanel = GetNode<PanelContainer>("%CraftingPanel");
		_craftingList = GetNode<VBoxContainer>("%CraftingList");
		RefreshCraftingMenu();
	}

	private static string LocationDisplayName(LocationType loc) => loc switch
	{
		LocationType.Waste => "The Waste",
		LocationType.Shelter => "Shelter",
		LocationType.GuildHall => "Guild Hall",
		_ => loc.ToString()
	};

	private static bool IsLocationValid(LocationType? required, LocationType playerLoc) =>
		required is null ||
		required == playerLoc ||
		(required == LocationType.Shelter && playerLoc == LocationType.GuildHall);

	private void RefreshLocationUI()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		var currentPlayer = conn.Db.Player.Identity.Find(localId);
		if (currentPlayer is null) return;

		var loc = currentPlayer.Location;
		_locationLabel.Text = $"Location: {LocationDisplayName(loc)}";

		_travelWasteButton.Visible = loc != LocationType.Waste;
		_travelShelterButton.Visible = loc != LocationType.Shelter
			&& conn.Db.PlayerShelter.Owner.Find(localId) is not null;
		_travelGuildHallButton.Visible = loc != LocationType.GuildHall
			&& conn.Db.GuildMember.PlayerId.Find(localId) is not null;

		bool isShelterLoc = loc == LocationType.Shelter || loc == LocationType.GuildHall;
		_craftingPanel.Visible = isShelterLoc;
		if (isShelterLoc)
			RefreshCraftingMenu();

		RefreshActivityVisibility(loc);
	}

	private void RefreshActivityVisibility(LocationType loc)
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		foreach (var (id, node) in _activityNodes)
		{
			var activity = conn.Db.Activity.Id.Find(id);
			if (activity is not null)
			{
				node.Visible = IsLocationValid(activity.RequiredLocation, loc);
				if (node.Visible)
					node.TryResumeAutoRepeat();
			}
			else
				node.Visible = false;
		}
	}

	private void AddActivityNode(SpacetimeDB.Types.Activity activity)
	{
		var activitySelection = ActivityScene.Instantiate<Activity>();
		activitySelection.Name = activity.Id.ToString();
		ActivitiesPanel.AddChild(activitySelection);
		activitySelection.InitActivityTracking(activity.Id);
		_activityNodes[activity.Id] = activitySelection;

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var currentPlayer = conn.Db.Player.Identity.Find(SpacetimeNetworkManager.Instance.LocalIdentity);
		if (currentPlayer is not null)
			activitySelection.Visible = IsLocationValid(activity.RequiredLocation, currentPlayer.Location);
	}

	private void RefreshCraftingMenu()
	{
		foreach (var child in _craftingList.GetChildren())
			child.QueueFree();

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		foreach (var definition in conn.Db.StructureDefinition.Iter())
		{
			var alreadyBuilt = conn.Db.PlayerStructure.ByOwnerAndDefinition
				.Filter((Owner: localId, DefinitionId: definition.Id)).Any();

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);

			var nameLabel = new Label();
			nameLabel.Text = definition.Name;
			nameLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
			row.AddChild(nameLabel);

			var costLabel = new Label();
			var costParts = definition.Cost.Select(c => $"{c.Amount} {c.Type}");
			costLabel.Text = string.Join(", ", costParts);
			costLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
			row.AddChild(costLabel);

			if (alreadyBuilt)
			{
				var builtLabel = new Label();
				builtLabel.Text = "Built";
				builtLabel.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.3f));
				row.AddChild(builtLabel);
			}
			else
			{
				var buildBtn = new Button();
				buildBtn.Text = "Build";
				buildBtn.CustomMinimumSize = new Godot.Vector2(80, 28);
				var capturedId = definition.Id;
				buildBtn.Pressed += () => conn.Reducers.BuildStructure(capturedId);
				row.AddChild(buildBtn);
			}

			_craftingList.AddChild(row);
		}

		if (_craftingList.GetChildCount() == 0)
		{
			var empty = new Label();
			empty.Text = "No structures available";
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			empty.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
			_craftingList.AddChild(empty);
		}
	}

	private void OnGuildSessionChanged(bool inSession)
	{
		_inGuildHall = inSession;
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

			var memberPlayer = conn.Db.Player.Identity.Find(member.PlayerId);
			if (memberPlayer is null || !memberPlayer.Online) continue;
			if (memberPlayer.Location != LocationType.GuildHall) continue;

			SpawnMemberSprite(member.PlayerId, memberPlayer.DisplayName);
		}
	}

	private void SpawnMemberSprite(SpacetimeDB.Identity playerId, string displayName)
	{
		if (_guildMemberSprites.ContainsKey(playerId)) return;

		var sprite = PlayerScene.Instantiate<Player>();
		var viewport = GetViewportRect();
		float x = _rng.RandfRange(100, viewport.Size.X - 100);
		float y = PlayerSpawnPosition.Position.Y + _rng.RandfRange(-20, 20);
		sprite.Position = new Vector2(x, y);
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

	private void OnPlayerUpdate(EventContext ctx, SpacetimeDB.Types.Player oldPlayer, SpacetimeDB.Types.Player newPlayer)
	{
		if (newPlayer.Identity == SpacetimeNetworkManager.Instance.LocalIdentity)
		{
			if (oldPlayer.DisplayName != newPlayer.DisplayName)
				_localPlayerNode.SetName(newPlayer.DisplayName);

			if (oldPlayer.Location != newPlayer.Location)
			{
				RefreshLocationUI();

				bool wasGuildHall = oldPlayer.Location == LocationType.GuildHall;
				bool isGuildHall = newPlayer.Location == LocationType.GuildHall;
				if (!wasGuildHall && isGuildHall)
				{
					_inGuildHall = true;
					SpawnGuildMembers();
				}
				else if (wasGuildHall && !isGuildHall)
				{
					_inGuildHall = false;
					DespawnAllGuildMembers();
				}
			}
			return;
		}

		if (!_inGuildHall) return;

		if (oldPlayer.DisplayName != newPlayer.DisplayName
			&& _guildMemberSprites.TryGetValue(newPlayer.Identity, out var sprite))
		{
			sprite.SetName(newPlayer.DisplayName);
		}

		if (oldPlayer.Location != newPlayer.Location)
		{
			if (newPlayer.Location == LocationType.GuildHall && newPlayer.Online)
				SpawnMemberSprite(newPlayer.Identity, newPlayer.DisplayName);
			else
				DespawnMemberSprite(newPlayer.Identity);
		}

		if (oldPlayer.Online && !newPlayer.Online)
			DespawnMemberSprite(newPlayer.Identity);
	}

	private void OnPlayerShelterInsert(EventContext ctx, SpacetimeDB.Types.PlayerShelter shelter)
	{
		if (shelter.Owner == SpacetimeNetworkManager.Instance.LocalIdentity)
			RefreshLocationUI();
	}

	private void OnPlayerStructureInsert(EventContext ctx, SpacetimeDB.Types.PlayerStructure structure)
	{
		if (structure.Owner == SpacetimeNetworkManager.Instance.LocalIdentity)
			RefreshCraftingMenu();
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

		AddActivityNode(activity);
	}

	private void OnActivityDelete(EventContext ctx, SpacetimeDB.Types.Activity activity)
	{
		if (activity.Participant != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;

		if (_activityNodes.TryGetValue(activity.Id, out var node))
		{
			node.QueueFree();
			_activityNodes.Remove(activity.Id);
		}
	}
}
