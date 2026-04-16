using Godot;
using SpacetimeDB;
using SpacetimeDB.Types;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Shelter : Node2D
{
	private SpacetimeDB.Types.Player player;
	private Marker2D _playerSpawnPosition;
	private Node2D _worldRoot;
	private SubViewport _subViewport;
	private Camera2D _camera;
	private PlayfieldBackground _playfieldBackground;
	private VBoxContainer _leftSide;
	private PlayerStatsPanel _statsPanel;
	private VBoxContainer _activitiesPanel;
	private Player _localPlayerNode;
	private GuildSocialPanel _guildSocialPanel;
	private FriendsSocialPanel _friendsSocialPanel;
	private CharacterProfilePanel _characterProfilePanel;

	private PackedScene _playerScene = GD.Load<PackedScene>("uid://cl6yviutw6arx");
	private PackedScene _resourceTrackingScene = GD.Load<PackedScene>("uid://bmw2ixd8nj1t8");
	private PackedScene _activityScene = GD.Load<PackedScene>("uid://bjckoiwufesye");
	private PackedScene _adventureScene = GD.Load<PackedScene>("res://scenes/adventure/adventure.tscn");
	private PackedScene _riskyBusinessScene = GD.Load<PackedScene>("res://scenes/risky_business/risky_business.tscn");

	private ZombieManager _zombieManager;
	private GuildMemberManager _guildMemberManager;

	private Label _locationLabel;
	private Button _travelShelterButton;
	private Button _travelGuildHallButton;
	private Button _travelWastesButton;

	private StructureManager _structureManager;

	private Dictionary<ulong, Activity> _activityNodes = new();

	private ColorRect _popupBackdrop;
	private Control _characterPopup;
	private Control _inventoryPopup;
	private Control _socialPopup;
	private Control _skillsPopup;
	private Control _systemPopup;

	private PanelContainer _modalCharacter;
	private PanelContainer _modalInventory;
	private PanelContainer _modalSocial;
	private PanelContainer _modalSkills;
	private PanelContainer _modalSystem;

	private StructureCraftPopupManager _structureCraftPopupManager;

	private Button _btnCharacter;
	private Button _btnInventory;
	private Button _btnGearInventory;
	private Button _btnSocial;
	private Button _btnSkills;
	private Button _btnSystem;

	private GearInventoryManager _gearInventoryManager;

	private int? _openPopupIndex;

	private FlowContainer _relevantResourcesBar;
	private FlowContainer _relevantStatsBar;
	private PanelContainer _relevantResourcesPanel;
	private Label _relevantResourcesTitle;
	private Label _relevantStatsTitle;

	private Label _levelLabel;
	private ProgressBar _xpProgressBar;
	private Label _xpLabel;
	private Label _skillPointsLabel;

	private SkillsManager _skillsManager;

	private Rect2 _spawnOuterRect;
	private Rect2 _spawnExclusion;
	private Vector2 _mapScale;
	private TileMapLayer _buildingLayer;

	public override void _Ready()
	{
		_worldRoot = GetNode<Node2D>("%WorldRoot");
		_subViewport = GetNode<SubViewport>("%SubViewport");
		_playfieldBackground = GetNode<PlayfieldBackground>("%PlayfieldBackground");

		_camera = new Camera2D();
		_camera.Enabled = true;
		_worldRoot.AddChild(_camera);
		_playerSpawnPosition = GetNode<Marker2D>("%PlayerSpawnLocation");
		_leftSide = GetNode<VBoxContainer>("%LeftSide");
		_statsPanel = GetNode<PlayerStatsPanel>("%PlayerStatsPanel");
		_activitiesPanel = GetNode<VBoxContainer>("%Activities");
		_guildSocialPanel = GetNode<GuildSocialPanel>("%GuildSocialPanel");
		_friendsSocialPanel = GetNode<FriendsSocialPanel>("%FriendsSocialPanel");
		_characterProfilePanel = GetNode<CharacterProfilePanel>("%CharacterProfilePanel");

		_popupBackdrop = GetNode<ColorRect>("%PopupBackdrop");
		_characterPopup = GetNode<Control>("%CharacterPopup");
		_inventoryPopup = GetNode<Control>("%InventoryPopup");
		_socialPopup = GetNode<Control>("%SocialPopup");
		_skillsPopup = GetNode<Control>("%SkillsPopup");
		_systemPopup = GetNode<Control>("%SystemPopup");

		_modalCharacter = GetNode<PanelContainer>("CanvasLayerPopups/CharacterPopup/CenterCharacter/CharacterPanel");
		_modalInventory = GetNode<PanelContainer>("CanvasLayerPopups/InventoryPopup/CenterInventory/InventoryPanel");
		_modalSocial = GetNode<PanelContainer>("CanvasLayerPopups/SocialPopup/CenterSocial/SocialPanel");
		_modalSkills = GetNode<PanelContainer>("CanvasLayerPopups/SkillsPopup/CenterSkills/SkillsPanel");
		_modalSystem = GetNode<PanelContainer>("CanvasLayerPopups/SystemPopup/CenterSystem/SystemPanel");

		_btnCharacter = GetNode<Button>("%CharacterMenuButton");
		_btnInventory = GetNode<Button>("%InventoryMenuButton");
		_btnGearInventory = GetNode<Button>("%GearInventoryMenuButton");
		_btnSocial = GetNode<Button>("%SocialMenuButton");
		_btnSkills = GetNode<Button>("%SkillsMenuButton");
		_btnSystem = GetNode<Button>("%SystemMenuButton");

		_btnCharacter.Pressed += () => TogglePopup(0);
		_btnInventory.Pressed += () => TogglePopup(1);
		_btnGearInventory.Pressed += () => TogglePopup(2);
		_btnSocial.Pressed += () => TogglePopup(3);
		_btnSkills.Pressed += () => TogglePopup(4);
		_btnSystem.Pressed += () => TogglePopup(5);

		_structureCraftPopupManager = new StructureCraftPopupManager();
		AddChild(_structureCraftPopupManager);
		_structureCraftPopupManager.Init(GetNode<CanvasLayer>("CanvasLayerPopups"));
		_structureCraftPopupManager.CloseRequested += CloseAllPopups;
		_gearInventoryManager = new GearInventoryManager();
		AddChild(_gearInventoryManager);
		_gearInventoryManager.Init(GetNode<CanvasLayer>("CanvasLayerPopups"));
		_gearInventoryManager.CloseRequested += CloseAllPopups;

		_btnSkills.Visible = false;

		_levelLabel = GetNode<Label>("%LevelLabel");
		_xpProgressBar = GetNode<ProgressBar>("%XpProgressBar");
		_xpLabel = GetNode<Label>("%XpLabel");
		_skillPointsLabel = GetNode<Label>("%SkillPointsLabel");

		var debugLevelUpBtn = GetNode<Button>("%DebugLevelUpButton");
		debugLevelUpBtn.Pressed += () =>
		{
			SpacetimeNetworkManager.Instance?.Conn?.Reducers.DebugLevelUp();
		};

		_skillsManager = new SkillsManager();
		AddChild(_skillsManager);
		_skillsManager.Init(GetNode<Label>("%SkillsAvailableLabel"), GetNode<VBoxContainer>("%SkillsList"));

		SetProcessInput(true);

		var conn = SpacetimeNetworkManager.Instance.Conn;
		player = conn.Db.Player.Identity.Find(SpacetimeNetworkManager.Instance.LocalIdentity);

		_localPlayerNode = _playerScene.Instantiate<Player>();
		_localPlayerNode.IsLocal = true;
		AlignSpawnToPlayfield();
		_localPlayerNode.ZIndex = 10;
		_worldRoot.AddChild(_localPlayerNode);
		_localPlayerNode.SetName(player.DisplayName);
		_localPlayerNode.BindActivityDisplay(SpacetimeNetworkManager.Instance.LocalIdentity);
		_localPlayerNode.AutoKillEnabled = HasSkillByName(conn, SpacetimeNetworkManager.Instance.LocalIdentity, "Auto Kill Zombie");
		_localPlayerNode.KillRequested += () => _zombieManager?.OnPlayerKillRequested();

		var groundLayer = _worldRoot.GetNode<Node2D>("PlayfieldMap/TileMapLayer");
		_mapScale = _worldRoot.GetNode<Node2D>("PlayfieldMap").Scale;
		_buildingLayer = _worldRoot.GetNode<TileMapLayer>("PlayfieldMap/BuildingMapLayer");

		var markerN = groundLayer.GetNode<Marker2D>("ZombieSpawnN");
		var markerE = groundLayer.GetNode<Marker2D>("ZombieSpawnE");
		var markerS = groundLayer.GetNode<Marker2D>("ZombieSpawnS");
		var markerW = groundLayer.GetNode<Marker2D>("ZombieSpawnW");
		_spawnExclusion = new Rect2(
			new Vector2(markerW.Position.X, markerN.Position.Y),
			new Vector2(markerE.Position.X - markerW.Position.X, markerS.Position.Y - markerN.Position.Y)
		);

		var area = groundLayer.GetNode<Area2D>("Area2D");
		var colShape = area.GetNode<CollisionShape2D>("CollisionShape2D");
		var rectShape = (RectangleShape2D)colShape.Shape;
		Vector2 halfExtents = rectShape.Size * 0.5f * colShape.Scale * area.Scale;
		Vector2 center = colShape.Position * area.Scale;
		_spawnOuterRect = new Rect2(center - halfExtents, halfExtents * 2f);

		_zombieManager = new ZombieManager();
		AddChild(_zombieManager);
		_zombieManager.Init(_worldRoot, _buildingLayer, _mapScale, _spawnOuterRect, _spawnExclusion, _localPlayerNode);
		_zombieManager.LootReceived += SpawnFloatingLoot;

		_guildMemberManager = new GuildMemberManager();
		AddChild(_guildMemberManager);
		_guildMemberManager.Init(_worldRoot, _playerSpawnPosition);

		_statsPanel.InitStats(SpacetimeNetworkManager.Instance.LocalIdentity);

		var playerResources = conn.Db.ResourceTracker.Owner.Filter(SpacetimeNetworkManager.Instance.LocalIdentity);
		foreach (var resource in playerResources)
		{
			var resourceTracker = _resourceTrackingScene.Instantiate<ResourceTracker>();
			resourceTracker.Name = resource.Id.ToString();
			_leftSide.AddChild(resourceTracker);
			resourceTracker.InitResourceTracking(resource.Id);
		}

		var playerActivities = conn.Db.Activity.Participant.Filter(SpacetimeNetworkManager.Instance.LocalIdentity);
		foreach (var activity in playerActivities)
		{
			AddActivityNode(activity);
		}

		conn.Db.ResourceTracker.OnInsert += OnResourceTrackerInsert;
		conn.Db.ResourceTracker.OnUpdate += OnLocalResourceTrackerUpdate;
		conn.Db.PlayerStat.OnInsert += OnLocalPlayerStatInsert;
		conn.Db.PlayerStat.OnUpdate += OnLocalPlayerStatUpdate;
		conn.Db.Activity.OnInsert += OnActivityInsert;
		conn.Db.Activity.OnDelete += OnActivityDelete;
		conn.Db.ActiveTask.OnInsert += OnActiveTaskInsert;
		conn.Db.ActiveTask.OnDelete += OnActiveTaskDelete;

		conn.Db.PlayerLevel.OnInsert += OnPlayerLevelChange;
		conn.Db.PlayerLevel.OnUpdate += OnPlayerLevelUpdate;
		conn.Db.PlayerSkill.OnInsert += OnPlayerSkillInsert;

		_guildSocialPanel.GuildSessionChanged += _guildMemberManager.OnGuildSessionChanged;

		conn.Db.Player.OnUpdate += OnPlayerUpdate;

		conn.Db.PlayerShelter.OnInsert += OnPlayerShelterInsert;

		conn.Db.AdventureParticipant.OnInsert += OnAdventureParticipantInsert;
		conn.Db.RiskyBusinessParticipant.OnInsert += OnRiskyBusinessParticipantInsert;

		GetViewport().SizeChanged += OnViewportSizeChanged;
		CallDeferred(nameof(FitCameraToPlayfield));

		BuildLocationBar();
		_relevantResourcesBar = GetNode<FlowContainer>("%RelevantResourcesBar");
		_relevantStatsBar = GetNode<FlowContainer>("%RelevantStatsBar");
		_relevantResourcesPanel = GetNode<PanelContainer>("%RelevantResourcesPanel");
		_relevantResourcesTitle = GetNode<Label>("%RelevantResourcesTitle");
		_relevantStatsTitle = GetNode<Label>("%RelevantStatsTitle");
		var craftingPanel = GetNode<PanelContainer>("%CraftingPanel");
		var craftingList = GetNode<VBoxContainer>("%CraftingList");
		_structureManager = new StructureManager();
		AddChild(_structureManager);
		_structureManager.Init(_worldRoot, _buildingLayer, _mapScale, _subViewport, _camera, craftingPanel, craftingList);
		_structureManager.StructureClicked += OpenStructureCraftMenu;
		_structureManager.PlacementStarted += CloseAllPopups;

		RefreshLocationUI();
		RefreshXpBar();
	}

	public override void _Process(double delta)
	{
		_structureManager?.ProcessPlacement();
	}

	public override void _Input(InputEvent @event)
	{
		if (_structureManager != null && _structureManager.InPlacementMode)
		{
			if (@event is InputEventMouseButton mb && mb.Pressed)
			{
				if (mb.ButtonIndex == MouseButton.Left)
				{
					_structureManager.ConfirmPlacement();
					GetViewport().SetInputAsHandled();
					return;
				}
				if (mb.ButtonIndex == MouseButton.Right)
				{
					_structureManager.CancelPlacement();
					GetViewport().SetInputAsHandled();
					return;
				}
			}
			if (@event.IsActionPressed("ui_cancel"))
			{
				_structureManager.CancelPlacement();
				GetViewport().SetInputAsHandled();
				return;
			}
			return;
		}

		if (_openPopupIndex is null) return;
		if (@event is not InputEventMouseButton popupMb || !popupMb.Pressed) return;

		var shell = GetOpenModalShell();
		if (shell is null) return;
		if (shell.GetGlobalRect().HasPoint(popupMb.GlobalPosition))
			return;

		CloseAllPopups();
		GetViewport().SetInputAsHandled();
	}

	private PanelContainer? GetOpenModalShell() => _openPopupIndex switch
	{
		0 => _modalCharacter,
		1 => _modalInventory,
		2 => _gearInventoryManager.ModalPanel,
		3 => _modalSocial,
		4 => _modalSkills,
		5 => _modalSystem,
		6 => _structureCraftPopupManager.ModalPanel,
		_ => null,
	};

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			if (_structureManager != null && _structureManager.InPlacementMode)
			{
				_structureManager.CancelPlacement();
				GetViewport().SetInputAsHandled();
				return;
			}
			if (_openPopupIndex is not null)
			{
				CloseAllPopups();
				GetViewport().SetInputAsHandled();
			}
			return;
		}

		if (@event.IsActionPressed("menu_toggle"))
		{
			if (_openPopupIndex == 0)
				CloseAllPopups();
			else
				OpenPopup(0);
			GetViewport().SetInputAsHandled();
		}
		else if (@event.IsActionPressed("social_toggle"))
		{
			if (_openPopupIndex == 3)
				CloseAllPopups();
			else
				OpenPopup(3);
			GetViewport().SetInputAsHandled();
		}
	}

	private void TogglePopup(int index)
	{
		if (_openPopupIndex == index)
			CloseAllPopups();
		else
			OpenPopup(index);
	}

	private void OpenPopup(int index)
	{
		_openPopupIndex = index;
		_popupBackdrop.Visible = true;
		_characterPopup.Visible = index == 0;
		_inventoryPopup.Visible = index == 1;
		_gearInventoryManager.Popup.Visible = index == 2;
		_socialPopup.Visible = index == 3;
		_skillsPopup.Visible = index == 4;
		_systemPopup.Visible = index == 5;
		_structureCraftPopupManager.Popup.Visible = index == 6;

		switch (index)
		{
			case 0:
				_characterProfilePanel.RefreshOnOpen();
				break;
			case 2:
				_gearInventoryManager.SetOpen(true);
				break;
			case 3:
				_friendsSocialPanel.RefreshOnOpen();
				_guildSocialPanel.RefreshOnOpen();
				break;
			case 4:
				_skillsManager.SetOpen(true);
				break;
			case 6:
				_structureCraftPopupManager.Refresh();
				break;
		}
	}

	private void CloseAllPopups()
	{
		_openPopupIndex = null;
		_popupBackdrop.Visible = false;
		_characterPopup.Visible = false;
		_inventoryPopup.Visible = false;
		_gearInventoryManager.Popup.Visible = false;
		_gearInventoryManager.SetOpen(false);
		_socialPopup.Visible = false;
		_skillsPopup.Visible = false;
		_systemPopup.Visible = false;
		_structureCraftPopupManager.Popup.Visible = false;
		_structureCraftPopupManager.Close();
	}

	private void OnViewportSizeChanged()
	{
		FitCameraToPlayfield();
		AlignSpawnToPlayfield();
	}

	private void FitCameraToPlayfield()
	{
		if (_subViewport is null || _camera is null) return;
		var viewportSize = _subViewport.Size;
		if (viewportSize.X <= 0 || viewportSize.Y <= 0) return;

		var playfield = _worldRoot.GetNode<Node2D>("PlayfieldMap");
		var groundLayer = playfield.GetNode<TileMapLayer>("TileMapLayer");
		Vector2 mapScale = playfield.Scale;

		var usedRect = groundLayer.GetUsedRect();
		var tileSize = groundLayer.TileSet.TileSize;

		var worldMin = new Vector2(usedRect.Position.X * tileSize.X, usedRect.Position.Y * tileSize.Y) * mapScale;
		var worldMax = new Vector2(usedRect.End.X * tileSize.X, usedRect.End.Y * tileSize.Y) * mapScale;
		var worldSize = worldMax - worldMin;
		var worldCenter = (worldMin + worldMax) / 2f;

		float zoomX = viewportSize.X / worldSize.X;
		float zoomY = viewportSize.Y / worldSize.Y;
		float zoom = Mathf.Min(zoomX, zoomY);

		_camera.Zoom = new Vector2(zoom, zoom);
		_camera.Position = worldCenter;
	}

	private void AlignSpawnToPlayfield()
	{
		var groundLayer = _worldRoot.GetNode<TileMapLayer>("PlayfieldMap/TileMapLayer");
		Vector2 mapScale = groundLayer.GetParent<Node2D>().Scale;
		Vector2 spawnWorld = groundLayer.MapToLocal(new Vector2I(20, 12)) * mapScale;

		_playerSpawnPosition.Position = spawnWorld;
		if (_localPlayerNode is not null && IsInstanceValid(_localPlayerNode) && !_localPlayerNode.IsInsideTree())
			_localPlayerNode.Position = spawnWorld;
	}

	private void BuildLocationBar()
	{
		_locationLabel = GetNode<Label>("%LocationLabel");
		_travelShelterButton = GetNode<Button>("%TravelShelterButton");
		_travelGuildHallButton = GetNode<Button>("%TravelGuildHallButton");
		_travelWastesButton = GetNode<Button>("%TravelWastesButton");

		_travelShelterButton.Pressed += () => SpacetimeNetworkManager.Instance.Conn.Reducers.Travel(LocationType.Shelter);
		_travelGuildHallButton.Pressed += () => SpacetimeNetworkManager.Instance.Conn.Reducers.Travel(LocationType.GuildHall);
		_travelGuildHallButton.Disabled = true;
		_travelGuildHallButton.TooltipText = "Coming soon";
		_travelWastesButton.Pressed += () => SpacetimeNetworkManager.Instance.Conn.Reducers.Travel(LocationType.Wastes);
	}


	private static string LocationDisplayName(LocationType loc) => loc switch
	{
		LocationType.Shelter => "Shelter",
		LocationType.GuildHall => "Guild Hall",
		LocationType.Wastes => "The Wastes",
		_ => loc.ToString()
	};

	private static Color LocationPlayfieldColor(LocationType loc) => loc switch
	{
		LocationType.Shelter => new Color(0.42f, 0.30f, 0.22f),
		LocationType.GuildHall => new Color(0.72f, 0.62f, 0.28f),
		LocationType.Wastes => new Color(0.45f, 0.22f, 0.20f),
		_ => new Color(0.35f, 0.35f, 0.38f),
	};

	private static bool IsLocationValid(LocationType? required, LocationType playerLoc) =>
		required is null ||
		required == playerLoc ||
		(required == LocationType.Shelter && playerLoc == LocationType.GuildHall);

	private static ulong GetResourceAmount(DbConnection conn, SpacetimeDB.Identity owner, ResourceType type)
	{
		foreach (var row in conn.Db.ResourceTracker.ByOwnerAndType.Filter((Owner: owner, Type: type)))
			return row.Amount;
		return 0;
	}

	private static int GetStatValue(DbConnection conn, SpacetimeDB.Identity owner, StatType stat)
	{
		foreach (var row in conn.Db.PlayerStat.ByStatOwnerStat.Filter((Owner: owner, Stat: stat)))
			return row.Value;
		return 0;
	}

	private static void AddActivityRelevantStats(ActivityType activityType, HashSet<StatType> set)
	{
		switch (activityType)
		{
			case ActivityType.Scavenge:
				set.Add(StatType.Perception);
				set.Add(StatType.Intelligence);
				set.Add(StatType.Strength);
				set.Add(StatType.Wit);
				set.Add(StatType.Dexterity);
				set.Add(StatType.Endurance);
				break;
			case ActivityType.ChopWood:
				set.Add(StatType.Endurance);
				break;
			case ActivityType.Mine:
				set.Add(StatType.Strength);
				break;
		}
	}

	private static List<StatType> ComputeRelevantStatTypes(DbConnection conn, SpacetimeDB.Identity localId, LocationType loc)
	{
		var set = new HashSet<StatType>();

		if (loc == LocationType.Shelter)
			AddActivityRelevantStats(ActivityType.Scavenge, set);

		foreach (var activity in conn.Db.Activity.Participant.Filter(localId))
		{
			if (!IsLocationValid(activity.RequiredLocation, loc))
				continue;
			AddActivityRelevantStats(activity.Type, set);
		}

		var ordered = new List<StatType>();
		foreach (StatType st in Enum.GetValues<StatType>())
		{
			if (set.Contains(st))
				ordered.Add(st);
		}
		return ordered;
	}

	private static List<ResourceType> ComputeRelevantResourceTypes(DbConnection conn, SpacetimeDB.Identity localId, LocationType loc)
	{
		var set = new HashSet<ResourceType>();

		if (loc == LocationType.Shelter)
		{
			foreach (ResourceType r in Enum.GetValues<ResourceType>())
				set.Add(r);
		}

		foreach (var activity in conn.Db.Activity.Participant.Filter(localId))
		{
			if (!IsLocationValid(activity.RequiredLocation, loc))
				continue;
			foreach (var c in activity.Cost)
				set.Add(c.Type);
		}

		if (loc == LocationType.Shelter || loc == LocationType.GuildHall)
		{
			foreach (var def in conn.Db.StructureDefinition.Iter())
			{
				foreach (var c in def.Cost)
					set.Add(c.Type);
			}
		}

		var ordered = new List<ResourceType>();
		foreach (ResourceType rt in Enum.GetValues<ResourceType>())
		{
			if (set.Contains(rt))
				ordered.Add(rt);
		}
		return ordered;
	}

	private void RefreshRelevantLocationContext()
	{
		if (_relevantResourcesBar is null || _relevantStatsBar is null || _relevantResourcesPanel is null)
			return;

		foreach (var child in _relevantResourcesBar.GetChildren())
			child.QueueFree();
		foreach (var child in _relevantStatsBar.GetChildren())
			child.QueueFree();

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		var player = conn.Db.Player.Identity.Find(localId);
		if (player is null)
		{
			_relevantResourcesPanel.Visible = false;
			return;
		}

		var loc = player.Location;
		var resourceTypes = ComputeRelevantResourceTypes(conn, localId, loc);
		var statTypes = ComputeRelevantStatTypes(conn, localId, loc);

		_relevantResourcesTitle.Visible = resourceTypes.Count > 0;
		_relevantStatsTitle.Visible = statTypes.Count > 0;
		_relevantResourcesPanel.Visible = resourceTypes.Count > 0 || statTypes.Count > 0;

		foreach (var rt in resourceTypes)
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 4);

			var nameLabel = new Label();
			nameLabel.Text = rt.ToString();
			nameLabel.AddThemeFontSizeOverride("font_size", 20);

			var amountLabel = new Label();
			amountLabel.Text = GetResourceAmount(conn, localId, rt).ToString();
			amountLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.4f));
			amountLabel.AddThemeFontSizeOverride("font_size", 20);

			row.AddChild(nameLabel);
			row.AddChild(amountLabel);
			_relevantResourcesBar.AddChild(row);
		}

		foreach (var st in statTypes)
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 4);

			var nameLabel = new Label();
			nameLabel.Text = st.ToString();
			nameLabel.AddThemeFontSizeOverride("font_size", 20);

			var valueLabel = new Label();
			valueLabel.Text = GetStatValue(conn, localId, st).ToString();
			valueLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.4f));
			valueLabel.AddThemeFontSizeOverride("font_size", 20);

			row.AddChild(nameLabel);
			row.AddChild(valueLabel);
			_relevantStatsBar.AddChild(row);
		}
	}

	private void OnLocalResourceTrackerUpdate(EventContext ctx, SpacetimeDB.Types.ResourceTracker oldRow, SpacetimeDB.Types.ResourceTracker newRow)
	{
		if (newRow.Owner != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;
		RefreshRelevantLocationContext();
	}

	private void OnLocalPlayerStatInsert(EventContext ctx, SpacetimeDB.Types.PlayerStat row)
	{
		if (row.Owner != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;
		RefreshRelevantLocationContext();
	}

	private void OnLocalPlayerStatUpdate(EventContext ctx, SpacetimeDB.Types.PlayerStat oldRow, SpacetimeDB.Types.PlayerStat newRow)
	{
		if (newRow.Owner != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;
		RefreshRelevantLocationContext();
	}

	private void RefreshLocationUI()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		var currentPlayer = conn.Db.Player.Identity.Find(localId);
		if (currentPlayer is null) return;

		var loc = currentPlayer.Location;
		_locationLabel.Text = $"Location: {LocationDisplayName(loc)}";
		_playfieldBackground.SetColor(LocationPlayfieldColor(loc));

		var playfield = _worldRoot.GetNode<Node2D>("PlayfieldMap");
		bool isWastes = loc == LocationType.Wastes;
		playfield.Modulate = isWastes ? new Color(1.0f, 0.7f, 0.7f) : Colors.White;

		var zombieTint = isWastes ? new Color(1.0f, 0.6f, 0.6f) : Colors.White;
		_zombieManager?.SetTint(zombieTint);

		_travelShelterButton.Visible = loc != LocationType.Shelter;
		_travelGuildHallButton.Visible = true;
		_travelWastesButton.Visible = loc != LocationType.Wastes && HasSkillByName(conn, localId, "Unlock Wastes");

		bool isShelterLoc = loc == LocationType.Shelter || loc == LocationType.GuildHall || loc == LocationType.Wastes;
		_structureManager?.SetCraftingPanelVisible(isShelterLoc);
		if (isShelterLoc)
			_structureManager?.RefreshCraftingMenu();

		RefreshActivityVisibility();
		RefreshRelevantLocationContext();
	}

	private void RefreshActivityVisibility()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		foreach (var (id, node) in _activityNodes)
		{
			if (conn.Db.Activity.Id.Find(id) is null)
				node.Visible = false;
			else
			{
				node.RefreshFormat();
				if (node.Visible)
					node.TryResumeAutoRepeat();
			}
		}
	}

	private void AddActivityNode(SpacetimeDB.Types.Activity activity)
	{
		var activitySelection = _activityScene.Instantiate<Activity>();
		activitySelection.Name = activity.Id.ToString();
		_activitiesPanel.AddChild(activitySelection);
		activitySelection.InitActivityTracking(activity.Id);
		_activityNodes[activity.Id] = activitySelection;
	}

	private static bool HasSkillByName(DbConnection conn, SpacetimeDB.Identity owner, string skillName)
	{
		var def = conn.Db.SkillDefinition.Name.Find(skillName);
		if (def is null) return false;
		return conn.Db.PlayerSkill.BySkillOwnerDef
			.Filter((Owner: owner, SkillDefinitionId: def.Id)).Any();
	}

	private static ulong XpForNextLevel(uint level) =>
		(ulong)Math.Floor(20.0 * Math.Pow(1.5, level));

	private void RefreshXpBar()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		var pl = conn.Db.PlayerLevel.Owner.Find(localId);

		uint level = pl?.Level ?? 0;
		ulong xp = pl?.Xp ?? 0;
		uint sp = pl?.AvailableSkillPoints ?? 0;

		var needed = XpForNextLevel(level);
		_levelLabel.Text = $"Lv {level}";
		_xpProgressBar.Value = needed > 0 ? (double)xp / needed * 100.0 : 0;
		_xpLabel.Text = $"{xp} / {needed} XP";
		_skillPointsLabel.Text = sp > 0 ? $"SP: {sp}" : "";

		if (level >= 1 && !_btnSkills.Visible)
		{
			_btnSkills.Visible = true;
			FlashButton(_btnSkills);
		}
	}

	private void FlashButton(Button btn)
	{
		var tween = CreateTween();
		tween.SetLoops(4);
		tween.TweenProperty(btn, "modulate", new Color(1f, 0.85f, 0.2f, 1f), 0.25);
		tween.TweenProperty(btn, "modulate", Colors.White, 0.25);
	}

	private void OnPlayerLevelChange(EventContext ctx, SpacetimeDB.Types.PlayerLevel row)
	{
		if (row.Owner != SpacetimeNetworkManager.Instance.LocalIdentity) return;
		RefreshXpBar();
		RefreshActivityVisibility();
	}

	private void OnPlayerLevelUpdate(EventContext ctx, SpacetimeDB.Types.PlayerLevel oldRow, SpacetimeDB.Types.PlayerLevel newRow)
	{
		if (newRow.Owner != SpacetimeNetworkManager.Instance.LocalIdentity) return;
		RefreshXpBar();
		RefreshActivityVisibility();
		if (_openPopupIndex == 4) _skillsManager.RefreshIfOpen();
	}

	private void OnPlayerSkillInsert(EventContext ctx, SpacetimeDB.Types.PlayerSkill row)
	{
		if (row.Owner != SpacetimeNetworkManager.Instance.LocalIdentity) return;
		RefreshXpBar();
		RefreshActivityVisibility();
		if (_openPopupIndex == 4) _skillsManager.RefreshIfOpen();

		var conn = SpacetimeNetworkManager.Instance.Conn;
		if (_localPlayerNode != null)
			_localPlayerNode.AutoKillEnabled = HasSkillByName(conn, row.Owner, "Auto Kill Zombie");

		if (HasSkillByName(conn, row.Owner, "Unlock Wastes"))
			RefreshLocationUI();
	}

	private void OnPlayerUpdate(EventContext ctx, SpacetimeDB.Types.Player oldPlayer, SpacetimeDB.Types.Player newPlayer)
	{
		if (newPlayer.Identity != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;

		if (oldPlayer.DisplayName != newPlayer.DisplayName)
			_localPlayerNode.SetName(newPlayer.DisplayName);

		if (oldPlayer.Location != newPlayer.Location)
		{
			RefreshLocationUI();

			bool wasGuildHall = oldPlayer.Location == LocationType.GuildHall;
			bool isGuildHall = newPlayer.Location == LocationType.GuildHall;
			if (!wasGuildHall && isGuildHall)
				_guildMemberManager.OnGuildSessionChanged(true);
			else if (wasGuildHall && !isGuildHall)
				_guildMemberManager.OnGuildSessionChanged(false);
		}
	}

	private void OnPlayerShelterInsert(EventContext ctx, SpacetimeDB.Types.PlayerShelter shelter)
	{
		if (shelter.Owner == SpacetimeNetworkManager.Instance.LocalIdentity)
			RefreshLocationUI();
	}

	private void OnResourceTrackerInsert(EventContext ctx, SpacetimeDB.Types.ResourceTracker tracker)
	{
		if (tracker.Owner != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;

		var resourceTracker = _resourceTrackingScene.Instantiate<ResourceTracker>();
		resourceTracker.Name = tracker.Id.ToString();
		_leftSide.AddChild(resourceTracker);
		resourceTracker.InitResourceTracking(tracker.Id);
		RefreshRelevantLocationContext();
	}

	private void OnActiveTaskInsert(EventContext ctx, SpacetimeDB.Types.ActiveTask task)
	{
		if (task.Participant != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;
	}

	private void OnActiveTaskDelete(EventContext ctx, SpacetimeDB.Types.ActiveTask task)
	{
		if (task.Participant != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;
	}

	private void OnActivityInsert(EventContext ctx, SpacetimeDB.Types.Activity activity)
	{
		if (activity.Participant != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;

		AddActivityNode(activity);
		RefreshRelevantLocationContext();
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
		RefreshRelevantLocationContext();
	}

	private void OnAdventureParticipantInsert(EventContext ctx, SpacetimeDB.Types.AdventureParticipant participant)
	{
		if (participant.PlayerId != SpacetimeNetworkManager.Instance.LocalIdentity) return;
		CallDeferred(nameof(DeferredTransitionToAdventure));
	}

	private void DeferredTransitionToAdventure()
	{
		GetTree().ChangeSceneToPacked(_adventureScene);
	}

	private void OnRiskyBusinessParticipantInsert(EventContext ctx, SpacetimeDB.Types.RiskyBusinessParticipant participant)
	{
		if (participant.PlayerId != SpacetimeNetworkManager.Instance.LocalIdentity) return;
		CallDeferred(nameof(DeferredTransitionToRiskyBusiness));
	}

	private void DeferredTransitionToRiskyBusiness()
	{
		GetTree().ChangeSceneToPacked(_riskyBusinessScene);
	}

	private void OpenStructureCraftMenu(ulong structureDefinitionId)
	{
		_structureCraftPopupManager.Open(structureDefinitionId);
		OpenPopup(6);
	}

	private void SpawnFloatingLoot(Vector2 worldPos, string text)
	{
		var label = new Label();
		label.Text = text;
		label.Position = worldPos - new Vector2(40, 30);
		label.ZIndex = 100;
		label.AddThemeFontSizeOverride("font_size", 48);
		label.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.2f));
		label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
		label.AddThemeConstantOverride("outline_size", 8);
		_worldRoot.AddChild(label);

		var tween = label.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(label, "position:y", worldPos.Y - 80, 1.0);
		tween.TweenProperty(label, "modulate:a", 0.0f, 1.0);
		tween.SetParallel(false);
		tween.TweenCallback(Callable.From(label.QueueFree));
	}
}
