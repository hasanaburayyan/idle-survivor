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
	private CharacterProfilePanel _characterProfilePanel;

	private PackedScene _playerScene = GD.Load<PackedScene>("uid://cl6yviutw6arx");
	private PackedScene _resourceTrackingScene = GD.Load<PackedScene>("uid://bmw2ixd8nj1t8");
	private PackedScene _activityScene = GD.Load<PackedScene>("uid://bjckoiwufesye");
	private PackedScene _zombieScene = GD.Load<PackedScene>("uid://cklegshx4bjbl");

	private Dictionary<SpacetimeDB.Identity, Player> _guildMemberSprites = new();
	private bool _inGuildHall;
	private RandomNumberGenerator _rng = new();
	private Color _currentZombieTint = Colors.White;

	private Label _locationLabel;
	private Button _travelShelterButton;
	private Button _travelGuildHallButton;
	private Button _travelWastesButton;

	private PanelContainer _craftingPanel;
	private VBoxContainer _craftingList;

	private bool _placementMode;
	private Node2D _placementGhost;
	private ulong _pendingDefinitionId;
	private string _pendingStructureName;

	private Dictionary<ulong, Node2D> _placedStructureNodes = new();
	private ulong? _openStructureDefId;

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

	private Control _structureCraftPopup;
	private PanelContainer _modalStructureCraft;
	private Label _structureCraftTitle;
	private VBoxContainer _structureCraftList;

	private Button _btnCharacter;
	private Button _btnInventory;
	private Button _btnGearInventory;
	private Button _btnSocial;
	private Button _btnSkills;
	private Button _btnSystem;

	private Control _gearInventoryPopup;
	private PanelContainer _modalGearInventory;
	private GridContainer _inventoryGrid;
	private HBoxContainer _equipmentSlotsContainer;
	private VBoxContainer _chestPanel;
	private GridContainer _chestGrid;

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

	private Label _skillsAvailableLabel;
	private VBoxContainer _skillsList;

	private Rect2 _spawnOuterRect;
	private Rect2 _spawnExclusion;
	private Vector2 _mapScale;
	private TileMapLayer _buildingLayer;
	private readonly Queue<Vector2> _pendingKillPositions = new();

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

		BuildStructureCraftPopup();
		BuildGearInventoryPopup();

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

		_skillsAvailableLabel = GetNode<Label>("%SkillsAvailableLabel");
		_skillsList = GetNode<VBoxContainer>("%SkillsList");

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
		_localPlayerNode.KillRequested += OnPlayerKillRequested;

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

		for (int i = 0; i < 100; i++)
			SpawnZombie();

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

		_guildSocialPanel.GuildSessionChanged += OnGuildSessionChanged;

		conn.Db.Player.OnUpdate += OnPlayerUpdate;

		conn.Db.PlayerShelter.OnInsert += OnPlayerShelterInsert;
		conn.Db.PlayerStructure.OnInsert += OnPlayerStructureInsert;
		conn.Db.KillLoot.OnInsert += OnKillLootInsert;

		conn.Db.InventoryItem.OnInsert += OnInventoryItemChanged;
		conn.Db.InventoryItem.OnDelete += OnInventoryItemDeleted;
		conn.Db.EquippedGear.OnInsert += OnEquippedGearChanged;
		conn.Db.EquippedGear.OnDelete += OnEquippedGearDeleted;
		conn.Db.ChestItem.OnInsert += OnChestItemChanged;
		conn.Db.ChestItem.OnDelete += OnChestItemDeleted;

		foreach (var ps in conn.Db.PlayerStructure.Owner.Filter(SpacetimeNetworkManager.Instance.LocalIdentity))
			SpawnStructureNode(ps);

		GetViewport().SizeChanged += OnViewportSizeChanged;
		CallDeferred(nameof(FitCameraToPlayfield));

		BuildLocationBar();
		_relevantResourcesBar = GetNode<FlowContainer>("%RelevantResourcesBar");
		_relevantStatsBar = GetNode<FlowContainer>("%RelevantStatsBar");
		_relevantResourcesPanel = GetNode<PanelContainer>("%RelevantResourcesPanel");
		_relevantResourcesTitle = GetNode<Label>("%RelevantResourcesTitle");
		_relevantStatsTitle = GetNode<Label>("%RelevantStatsTitle");
		BuildCraftingPanel();
		RefreshLocationUI();
		RefreshXpBar();
	}

	public override void _Process(double delta)
	{
		if (_placementMode && _placementGhost != null)
		{
			var mousePos = _subViewport.GetMousePosition();
			var worldPos = _camera.Position + (mousePos - (Vector2)_subViewport.Size / 2f) / _camera.Zoom;
			_placementGhost.Position = worldPos;
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (_placementMode)
		{
			if (@event is InputEventMouseButton mb && mb.Pressed)
			{
				if (mb.ButtonIndex == MouseButton.Left)
				{
					ConfirmPlacement();
					GetViewport().SetInputAsHandled();
					return;
				}
				if (mb.ButtonIndex == MouseButton.Right)
				{
					CancelPlacement();
					GetViewport().SetInputAsHandled();
					return;
				}
			}
			if (@event.IsActionPressed("ui_cancel"))
			{
				CancelPlacement();
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
		2 => _modalGearInventory,
		3 => _modalSocial,
		4 => _modalSkills,
		5 => _modalSystem,
		6 => _modalStructureCraft,
		_ => null,
	};

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
			if (_placementMode)
			{
				CancelPlacement();
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
		_gearInventoryPopup.Visible = index == 2;
		_socialPopup.Visible = index == 3;
		_skillsPopup.Visible = index == 4;
		_systemPopup.Visible = index == 5;
		_structureCraftPopup.Visible = index == 6;

		switch (index)
		{
			case 0:
				_characterProfilePanel.RefreshOnOpen();
				break;
			case 2:
				RefreshGearInventory();
				break;
			case 3:
				_guildSocialPanel.RefreshOnOpen();
				break;
			case 4:
				RefreshSkillsPanel();
				break;
			case 6:
				RefreshStructureCraftMenu();
				break;
		}
	}

	private void CloseAllPopups()
	{
		_openPopupIndex = null;
		_popupBackdrop.Visible = false;
		_characterPopup.Visible = false;
		_inventoryPopup.Visible = false;
		_gearInventoryPopup.Visible = false;
		_socialPopup.Visible = false;
		_skillsPopup.Visible = false;
		_systemPopup.Visible = false;
		_structureCraftPopup.Visible = false;
		_openStructureDefId = null;
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

	private void BuildCraftingPanel()
	{
		_craftingPanel = GetNode<PanelContainer>("%CraftingPanel");
		_craftingList = GetNode<VBoxContainer>("%CraftingList");
		RefreshCraftingMenu();
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

		_currentZombieTint = isWastes ? new Color(1.0f, 0.6f, 0.6f) : Colors.White;
		ApplyZombieTint(_currentZombieTint);

		_travelShelterButton.Visible = loc != LocationType.Shelter;
		_travelGuildHallButton.Visible = true;
		_travelWastesButton.Visible = loc != LocationType.Wastes && HasSkillByName(conn, localId, "Unlock Wastes");

		bool isShelterLoc = loc == LocationType.Shelter || loc == LocationType.GuildHall || loc == LocationType.Wastes;
		_craftingPanel.Visible = isShelterLoc;
		if (isShelterLoc)
			RefreshCraftingMenu();

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

			var entry = new VBoxContainer();
			entry.AddThemeConstantOverride("separation", 2);

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
				buildBtn.CustomMinimumSize = new Vector2(80, 28);
				var capturedId = definition.Id;
				var capturedName = definition.Name;
				buildBtn.Pressed += () => EnterPlacementMode(capturedId, capturedName);
				row.AddChild(buildBtn);
			}

			entry.AddChild(row);

			var recipeNames = conn.Db.CraftingRecipe.StructureDefinitionId
				.Filter(definition.Id)
				.Select(r => r.Name)
				.ToList();
			if (recipeNames.Count > 0)
			{
				var recipesLabel = new Label();
				recipesLabel.Text = $"Recipes: {string.Join(", ", recipeNames)}";
				recipesLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.7f, 0.9f));
				recipesLabel.AddThemeFontSizeOverride("font_size", 14);
				entry.AddChild(recipesLabel);
			}

			_craftingList.AddChild(entry);
		}

		if (_craftingList.GetChildCount() == 0)
		{
			var empty = new Label();
			empty.Text = "Nothing to craft";
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			empty.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
			_craftingList.AddChild(empty);
		}
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

	private static bool IsSkillExposed(DbConnection conn, SpacetimeDB.Identity owner, SpacetimeDB.Types.SkillDefinition skill)
	{
		bool hasAnyPrereq = skill.PrerequisiteSkillId is not null || skill.PrerequisiteSkillId2 is not null;
		if (!hasAnyPrereq) return true;

		if (skill.PrerequisiteSkillId is ulong p1
			&& conn.Db.PlayerSkill.BySkillOwnerDef.Filter((Owner: owner, SkillDefinitionId: p1)).Any())
			return true;
		if (skill.PrerequisiteSkillId2 is ulong p2
			&& conn.Db.PlayerSkill.BySkillOwnerDef.Filter((Owner: owner, SkillDefinitionId: p2)).Any())
			return true;

		return false;
	}

	private void RefreshSkillsPanel()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		var pl = conn.Db.PlayerLevel.Owner.Find(localId);
		uint availableSp = pl?.AvailableSkillPoints ?? 0;

		_skillsAvailableLabel.Text = $"Available Skill Points: {availableSp}";

		foreach (var child in _skillsList.GetChildren())
			child.QueueFree();

		foreach (var skill in conn.Db.SkillDefinition.Iter())
		{
			if (!IsSkillExposed(conn, localId, skill))
				continue;

			bool owned = conn.Db.PlayerSkill.BySkillOwnerDef
				.Filter((Owner: localId, SkillDefinitionId: skill.Id)).Any();

			bool meetsPrereq = true;
			if (skill.PrerequisiteSkillId is not null || skill.PrerequisiteSkillId2 is not null)
			{
				bool has1 = skill.PrerequisiteSkillId is not ulong r1
					|| conn.Db.PlayerSkill.BySkillOwnerDef.Filter((Owner: localId, SkillDefinitionId: r1)).Any();
				bool has2 = skill.PrerequisiteSkillId2 is not ulong r2
					|| conn.Db.PlayerSkill.BySkillOwnerDef.Filter((Owner: localId, SkillDefinitionId: r2)).Any();
				meetsPrereq = has1 || has2;
			}
			bool meetsLevel = skill.RequiredLevel is not uint reqLvl || (pl?.Level ?? 0) >= reqLvl;
			bool canAfford = availableSp >= skill.Cost;

			var outer = new VBoxContainer();
			outer.AddThemeConstantOverride("separation", 2);

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);

			var nameLabel = new Label();
			nameLabel.Text = skill.Name;
			nameLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
			nameLabel.AddThemeFontSizeOverride("font_size", 18);
			row.AddChild(nameLabel);

			var costLabel = new Label();
			costLabel.Text = $"{skill.Cost} SP";
			costLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
			row.AddChild(costLabel);

			if (owned)
			{
				var ownedLabel = new Label();
				ownedLabel.Text = "Learned";
				ownedLabel.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.3f));
				row.AddChild(ownedLabel);
			}
			else
			{
				var purchaseBtn = new Button();
				purchaseBtn.Text = "Learn";
				purchaseBtn.CustomMinimumSize = new Vector2(80, 28);
				purchaseBtn.Disabled = !meetsLevel || !meetsPrereq || !canAfford;
				var capturedId = skill.Id;
				purchaseBtn.Pressed += () =>
				{
					conn.Reducers.PurchaseSkill(capturedId);
					CallDeferred(nameof(RefreshSkillsPanel));
				};
				row.AddChild(purchaseBtn);
			}

			outer.AddChild(row);

			var descLabel = new Label();
			descLabel.Text = skill.Description;
			descLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
			descLabel.AddThemeFontSizeOverride("font_size", 14);
			outer.AddChild(descLabel);

			_skillsList.AddChild(outer);
		}
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
		if (_openPopupIndex == 4) RefreshSkillsPanel();
	}

	private void OnPlayerSkillInsert(EventContext ctx, SpacetimeDB.Types.PlayerSkill row)
	{
		if (row.Owner != SpacetimeNetworkManager.Instance.LocalIdentity) return;
		RefreshXpBar();
		RefreshActivityVisibility();
		if (_openPopupIndex == 4) RefreshSkillsPanel();

		var conn = SpacetimeNetworkManager.Instance.Conn;
		if (_localPlayerNode != null)
			_localPlayerNode.AutoKillEnabled = HasSkillByName(conn, row.Owner, "Auto Kill Zombie");

		if (HasSkillByName(conn, row.Owner, "Unlock Wastes"))
			RefreshLocationUI();
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

		var sprite = _playerScene.Instantiate<Player>();
		var viewport = _worldRoot.GetViewport().GetVisibleRect();
		float margin = 80f;
		float x = _rng.RandfRange(margin, viewport.Size.X - margin);
		float y = _playerSpawnPosition.Position.Y + _rng.RandfRange(-20, 20);
		sprite.Position = new Vector2(x, y);
		sprite.ZIndex = 0;
		_worldRoot.AddChild(sprite);
		sprite.SetName(displayName);
		sprite.BindActivityDisplay(playerId);
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
		if (structure.Owner != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;

		SpawnStructureNode(structure);
		RefreshCraftingMenu();
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

	private void SpawnZombie()
	{
		Vector2 point;
		do
		{
			point = new Vector2(
				_rng.RandfRange(_spawnOuterRect.Position.X, _spawnOuterRect.End.X),
				_rng.RandfRange(_spawnOuterRect.Position.Y, _spawnOuterRect.End.Y)
			);
		} while (_spawnExclusion.HasPoint(point));

		var zombie = _zombieScene.Instantiate<Zombie>();
		zombie.Position = point * _mapScale;
		zombie.BuildingLayer = _buildingLayer;
		zombie.Modulate = _currentZombieTint;
		zombie.Killed += () => OnZombieKilled(zombie.Position);
		_worldRoot.AddChild(zombie);
	}

	private void ApplyZombieTint(Color tint)
	{
		foreach (var child in _worldRoot.GetChildren())
			if (child is Zombie z)
				z.Modulate = tint;
	}

	private void OnPlayerKillRequested()
	{
		var alive = new System.Collections.Generic.List<Zombie>();
		foreach (var child in _worldRoot.GetChildren())
		{
			if (child is Zombie z && !z.IsDying)
				alive.Add(z);
		}
		if (alive.Count == 0)
			return;

		var playerPos = _localPlayerNode.Position;
		alive.Sort((a, b) =>
			a.Position.DistanceSquaredTo(playerPos).CompareTo(b.Position.DistanceSquaredTo(playerPos)));

		int pool = Math.Min(alive.Count, 10);
		var target = alive[_rng.RandiRange(0, pool - 1)];
		target.Die();
	}

	private void OnZombieKilled(Vector2 deathPosition)
	{
		_pendingKillPositions.Enqueue(deathPosition);
		SpacetimeNetworkManager.Instance.Conn.Reducers.KillZombie();
		SpawnZombie();
	}

	private void OnKillLootInsert(EventContext ctx, SpacetimeDB.Types.KillLoot loot)
	{
		if (loot.Owner != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;

		var pos = _pendingKillPositions.Count > 0
			? _pendingKillPositions.Dequeue()
			: _localPlayerNode.Position;

		SpawnFloatingLoot(pos, $"+{loot.Amount} {loot.Resource}");
		SpacetimeNetworkManager.Instance.Conn.Reducers.AckKillLoot(loot.Id);
	}

	private static readonly Vector2 StructureSize = new(64, 64);

	private void EnterPlacementMode(ulong definitionId, string structureName)
	{
		if (_placementMode) return;
		CloseAllPopups();

		_placementMode = true;
		_pendingDefinitionId = definitionId;
		_pendingStructureName = structureName;

		var assetPath = $"res://assets/structures/{structureName.Replace(" ", "_").ToLower()}.png";
		if (ResourceLoader.Exists(assetPath))
		{
			var sprite = new Sprite2D();
			sprite.Texture = GD.Load<Texture2D>(assetPath);
			sprite.Modulate = new Color(1f, 1f, 1f, 0.6f);
			_placementGhost = sprite;
		}
		else
		{
			var rect = new ColorRect();
			rect.Size = StructureSize;
			rect.Position = -StructureSize / 2f;
			rect.Color = new Color(0.2f, 0.4f, 0.9f, 0.6f);
			var container = new Node2D();
			container.AddChild(rect);
			_placementGhost = container;
		}

		_placementGhost.ZIndex = 50;
		_worldRoot.AddChild(_placementGhost);
	}

	private void ConfirmPlacement()
	{
		if (!_placementMode || _placementGhost == null) return;

		var pos = _placementGhost.Position;
		var conn = SpacetimeNetworkManager.Instance.Conn;
		conn.Reducers.BuildStructure(_pendingDefinitionId, (int)pos.X, (int)pos.Y);

		_placementGhost.QueueFree();
		_placementGhost = null;
		_placementMode = false;
	}

	private void CancelPlacement()
	{
		if (!_placementMode) return;

		if (_placementGhost != null)
		{
			_placementGhost.QueueFree();
			_placementGhost = null;
		}
		_placementMode = false;
	}

	private void SpawnStructureNode(SpacetimeDB.Types.PlayerStructure ps)
	{
		if (_placedStructureNodes.ContainsKey(ps.Id)) return;

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var def = conn.Db.StructureDefinition.Id.Find(ps.DefinitionId);
		string structureName = def?.Name ?? "Unknown";

		var area = new ClickableStructure();
		area.Position = new Vector2(ps.PosX, ps.PosY);
		area.ZIndex = 5;

		var collision = new CollisionShape2D();
		var shape = new RectangleShape2D();
		shape.Size = StructureSize;
		collision.Shape = shape;
		area.AddChild(collision);

		var assetPath = $"res://assets/structures/{structureName.Replace(" ", "_").ToLower()}.png";
		if (ResourceLoader.Exists(assetPath))
		{
			var sprite = new Sprite2D();
			sprite.Texture = GD.Load<Texture2D>(assetPath);
			area.AddChild(sprite);
		}
		else
		{
			var rect = new ColorRect();
			rect.Size = StructureSize;
			rect.Position = -StructureSize / 2f;
			rect.MouseFilter = Control.MouseFilterEnum.Ignore;
			area.AddChild(rect);

			var label = new Label();
			label.Text = structureName;
			label.HorizontalAlignment = HorizontalAlignment.Center;
			label.Position = new Vector2(-StructureSize.X / 2f, -StructureSize.Y / 2f - 20);
			label.Size = new Vector2(StructureSize.X, 20);
			label.AddThemeFontSizeOverride("font_size", 12);
			label.AddThemeColorOverride("font_color", Colors.White);
			label.MouseFilter = Control.MouseFilterEnum.Ignore;
			area.AddChild(label);
		}

		var capturedDefId = ps.DefinitionId;
		area.StructureClicked += () =>
		{
			if (!_placementMode)
				OpenStructureCraftMenu(capturedDefId);
		};

		_worldRoot.AddChild(area);
		_placedStructureNodes[ps.Id] = area;
	}

	private void BuildStructureCraftPopup()
	{
		var popupLayer = GetNode<CanvasLayer>("CanvasLayerPopups");

		_structureCraftPopup = new Control();
		_structureCraftPopup.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_structureCraftPopup.Visible = false;
		popupLayer.AddChild(_structureCraftPopup);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_structureCraftPopup.AddChild(center);

		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(500, 400);
		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0.12f, 0.12f, 0.15f, 0.95f);
		panelStyle.CornerRadiusTopLeft = 8;
		panelStyle.CornerRadiusTopRight = 8;
		panelStyle.CornerRadiusBottomLeft = 8;
		panelStyle.CornerRadiusBottomRight = 8;
		panelStyle.ContentMarginLeft = 16;
		panelStyle.ContentMarginRight = 16;
		panelStyle.ContentMarginTop = 16;
		panelStyle.ContentMarginBottom = 16;
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		center.AddChild(panel);
		_modalStructureCraft = panel;

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 12);
		panel.AddChild(vbox);

		var header = new HBoxContainer();
		_structureCraftTitle = new Label();
		_structureCraftTitle.Text = "Crafting Station";
		_structureCraftTitle.AddThemeFontSizeOverride("font_size", 22);
		_structureCraftTitle.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		header.AddChild(_structureCraftTitle);

		var closeBtn = new Button();
		closeBtn.Text = "X";
		closeBtn.CustomMinimumSize = new Vector2(32, 32);
		closeBtn.Pressed += CloseAllPopups;
		header.AddChild(closeBtn);
		vbox.AddChild(header);

		var sep = new HSeparator();
		vbox.AddChild(sep);

		var scroll = new ScrollContainer();
		scroll.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		vbox.AddChild(scroll);

		_structureCraftList = new VBoxContainer();
		_structureCraftList.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		_structureCraftList.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(_structureCraftList);
	}

	private void OpenStructureCraftMenu(ulong structureDefinitionId)
	{
		_openStructureDefId = structureDefinitionId;
		OpenPopup(6);
	}

	private void RefreshStructureCraftMenu()
	{
		foreach (var child in _structureCraftList.GetChildren())
			child.QueueFree();

		if (_openStructureDefId is not ulong defId) return;

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var def = conn.Db.StructureDefinition.Id.Find(defId);
		_structureCraftTitle.Text = def != null ? $"{def.Name} — Recipes" : "Crafting Station";

		foreach (var recipe in conn.Db.CraftingRecipe.StructureDefinitionId.Filter(defId))
		{
			var row = new VBoxContainer();
			row.AddThemeConstantOverride("separation", 4);

			var topRow = new HBoxContainer();
			topRow.AddThemeConstantOverride("separation", 8);

			var nameLabel = new Label();
			nameLabel.Text = recipe.Name;
			nameLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
			nameLabel.AddThemeFontSizeOverride("font_size", 18);
			if (recipe.IsGearRecipe)
				nameLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.7f, 1.0f));
			topRow.AddChild(nameLabel);

			var craftBtn = new Button();
			craftBtn.Text = "Craft";
			craftBtn.CustomMinimumSize = new Vector2(80, 28);
			var capturedRecipeId = recipe.Id;
			var isGear = recipe.IsGearRecipe;
			craftBtn.Pressed += () =>
			{
				if (isGear)
					conn.Reducers.CraftGear(capturedRecipeId);
				else
					conn.Reducers.CraftRecipe(capturedRecipeId);
			};
			topRow.AddChild(craftBtn);
			row.AddChild(topRow);

			var costParts = recipe.InputCost.Select(c => $"{c.Amount} {c.Type}");
			var detailLabel = new Label();
			if (recipe.IsGearRecipe)
			{
				string gearName = FindGearNameForRecipe(conn, recipe.Id);
				detailLabel.Text = $"Cost: {string.Join(", ", costParts)}  →  {gearName}";
			}
			else
			{
				detailLabel.Text = $"Cost: {string.Join(", ", costParts)}  →  {recipe.OutputAmount} {recipe.OutputResource}";
			}
			detailLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
			detailLabel.AddThemeFontSizeOverride("font_size", 14);
			row.AddChild(detailLabel);

			var rowSep = new HSeparator();
			row.AddChild(rowSep);

			_structureCraftList.AddChild(row);
		}

		if (_structureCraftList.GetChildCount() == 0)
		{
			var empty = new Label();
			empty.Text = "No recipes available";
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			empty.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
			_structureCraftList.AddChild(empty);
		}
	}

	private static string FindGearNameForRecipe(DbConnection conn, ulong recipeId)
	{
		foreach (var def in conn.Db.GearDefinition.Iter())
		{
			if (def.CraftingRecipeId == recipeId)
				return def.Name;
		}
		return "Gear";
	}

	private void OnInventoryItemChanged(EventContext ctx, SpacetimeDB.Types.InventoryItem item)
	{
		if (item.Owner != SpacetimeNetworkManager.Instance.LocalIdentity) return;
		if (_openPopupIndex == 2) RefreshGearInventory();
	}

	private void OnInventoryItemDeleted(EventContext ctx, SpacetimeDB.Types.InventoryItem item)
	{
		if (item.Owner != SpacetimeNetworkManager.Instance.LocalIdentity) return;
		if (_openPopupIndex == 2) RefreshGearInventory();
	}

	private void OnEquippedGearChanged(EventContext ctx, SpacetimeDB.Types.EquippedGear gear)
	{
		if (gear.Owner != SpacetimeNetworkManager.Instance.LocalIdentity) return;
		if (_openPopupIndex == 2) RefreshGearInventory();
	}

	private void OnEquippedGearDeleted(EventContext ctx, SpacetimeDB.Types.EquippedGear gear)
	{
		if (gear.Owner != SpacetimeNetworkManager.Instance.LocalIdentity) return;
		if (_openPopupIndex == 2) RefreshGearInventory();
	}

	private void OnChestItemChanged(EventContext ctx, SpacetimeDB.Types.ChestItem item)
	{
		if (_openPopupIndex == 2) RefreshGearInventory();
	}

	private void OnChestItemDeleted(EventContext ctx, SpacetimeDB.Types.ChestItem item)
	{
		if (_openPopupIndex == 2) RefreshGearInventory();
	}

	private void BuildGearInventoryPopup()
	{
		var popupLayer = GetNode<CanvasLayer>("CanvasLayerPopups");

		_gearInventoryPopup = new Control();
		_gearInventoryPopup.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_gearInventoryPopup.Visible = false;
		popupLayer.AddChild(_gearInventoryPopup);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_gearInventoryPopup.AddChild(center);

		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(560, 520);
		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0.12f, 0.12f, 0.15f, 0.95f);
		panelStyle.CornerRadiusTopLeft = 8;
		panelStyle.CornerRadiusTopRight = 8;
		panelStyle.CornerRadiusBottomLeft = 8;
		panelStyle.CornerRadiusBottomRight = 8;
		panelStyle.ContentMarginLeft = 16;
		panelStyle.ContentMarginRight = 16;
		panelStyle.ContentMarginTop = 16;
		panelStyle.ContentMarginBottom = 16;
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		center.AddChild(panel);
		_modalGearInventory = panel;

		var scroll = new ScrollContainer();
		scroll.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		panel.AddChild(scroll);

		var outerVbox = new VBoxContainer();
		outerVbox.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		outerVbox.AddThemeConstantOverride("separation", 12);
		scroll.AddChild(outerVbox);

		var header = new HBoxContainer();
		var title = new Label();
		title.Text = "Inventory";
		title.AddThemeFontSizeOverride("font_size", 22);
		title.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		header.AddChild(title);
		var closeBtn = new Button();
		closeBtn.Text = "X";
		closeBtn.CustomMinimumSize = new Vector2(32, 32);
		closeBtn.Pressed += CloseAllPopups;
		header.AddChild(closeBtn);
		outerVbox.AddChild(header);
		outerVbox.AddChild(new HSeparator());

		var equipLabel = new Label();
		equipLabel.Text = "Equipment";
		equipLabel.AddThemeFontSizeOverride("font_size", 18);
		equipLabel.HorizontalAlignment = HorizontalAlignment.Center;
		outerVbox.AddChild(equipLabel);

		_equipmentSlotsContainer = new HBoxContainer();
		_equipmentSlotsContainer.AddThemeConstantOverride("separation", 8);
		_equipmentSlotsContainer.Alignment = BoxContainer.AlignmentMode.Center;
		outerVbox.AddChild(_equipmentSlotsContainer);

		outerVbox.AddChild(new HSeparator());

		var invLabel = new Label();
		invLabel.Text = "Backpack (15 slots)";
		invLabel.AddThemeFontSizeOverride("font_size", 18);
		invLabel.HorizontalAlignment = HorizontalAlignment.Center;
		outerVbox.AddChild(invLabel);

		_inventoryGrid = new GridContainer();
		_inventoryGrid.Columns = 5;
		_inventoryGrid.AddThemeConstantOverride("h_separation", 6);
		_inventoryGrid.AddThemeConstantOverride("v_separation", 6);
		outerVbox.AddChild(_inventoryGrid);

		outerVbox.AddChild(new HSeparator());

		_chestPanel = new VBoxContainer();
		_chestPanel.AddThemeConstantOverride("separation", 8);
		_chestPanel.Visible = false;
		outerVbox.AddChild(_chestPanel);

		var chestLabel = new Label();
		chestLabel.Text = "Storage Chest";
		chestLabel.AddThemeFontSizeOverride("font_size", 18);
		chestLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_chestPanel.AddChild(chestLabel);

		_chestGrid = new GridContainer();
		_chestGrid.Columns = 5;
		_chestGrid.AddThemeConstantOverride("h_separation", 6);
		_chestGrid.AddThemeConstantOverride("v_separation", 6);
		_chestPanel.AddChild(_chestGrid);
	}

	private static string SlotDisplayName(GearSlot slot) => slot switch
	{
		GearSlot.Head => "Head",
		GearSlot.Chest => "Chest",
		GearSlot.Arms => "Arms",
		GearSlot.Legs => "Legs",
		GearSlot.Feet => "Feet",
		GearSlot.Weapon => "Weapon",
		_ => slot.ToString()
	};

	private void RefreshGearInventory()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		foreach (var child in _equipmentSlotsContainer.GetChildren())
			child.QueueFree();
		foreach (var child in _inventoryGrid.GetChildren())
			child.QueueFree();
		foreach (var child in _chestGrid.GetChildren())
			child.QueueFree();

		foreach (GearSlot slot in Enum.GetValues<GearSlot>())
		{
			SpacetimeDB.Types.EquippedGear? equipped = null;
			foreach (var eq in conn.Db.EquippedGear.ByEquipOwnerSlot.Filter((Owner: localId, Slot: slot)))
			{
				equipped = eq;
				break;
			}

			var slotBox = new VBoxContainer();
			slotBox.AddThemeConstantOverride("separation", 2);

			var slotLabel = new Label();
			slotLabel.Text = SlotDisplayName(slot);
			slotLabel.AddThemeFontSizeOverride("font_size", 12);
			slotLabel.HorizontalAlignment = HorizontalAlignment.Center;
			slotBox.AddChild(slotLabel);

			var cell = new PanelContainer();
			cell.CustomMinimumSize = new Vector2(80, 80);
			var cellStyle = new StyleBoxFlat();
			cellStyle.CornerRadiusTopLeft = 4;
			cellStyle.CornerRadiusTopRight = 4;
			cellStyle.CornerRadiusBottomLeft = 4;
			cellStyle.CornerRadiusBottomRight = 4;

			if (equipped is SpacetimeDB.Types.EquippedGear eq2)
			{
				cellStyle.BgColor = new Color(0.2f, 0.35f, 0.6f, 0.9f);
				cell.AddThemeStyleboxOverride("panel", cellStyle);

				var gearDef = conn.Db.GearDefinition.Id.Find(eq2.GearDefinitionId);
				var gearLabel = new Label();
				gearLabel.Text = gearDef?.Name ?? "???";
				gearLabel.AddThemeFontSizeOverride("font_size", 11);
				gearLabel.HorizontalAlignment = HorizontalAlignment.Center;
				gearLabel.VerticalAlignment = VerticalAlignment.Center;
				gearLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				cell.AddChild(gearLabel);

				var capturedSlot = slot;
				cell.GuiInput += (InputEvent ev) =>
				{
					if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
					{
						conn.Reducers.UnequipGear(capturedSlot);
					}
				};
				cell.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
				cell.TooltipText = BuildGearTooltip(conn, eq2.GearDefinitionId, eq2.CraftedBy) + "\nClick to unequip";
			}
			else
			{
				cellStyle.BgColor = new Color(0.15f, 0.15f, 0.18f, 0.9f);
				cell.AddThemeStyleboxOverride("panel", cellStyle);

				var emptyLabel = new Label();
				emptyLabel.Text = "Empty";
				emptyLabel.AddThemeFontSizeOverride("font_size", 11);
				emptyLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.4f, 0.4f));
				emptyLabel.HorizontalAlignment = HorizontalAlignment.Center;
				emptyLabel.VerticalAlignment = VerticalAlignment.Center;
				cell.AddChild(emptyLabel);
			}

			slotBox.AddChild(cell);
			_equipmentSlotsContainer.AddChild(slotBox);
		}

		var invItems = new List<SpacetimeDB.Types.InventoryItem>();
		foreach (var item in conn.Db.InventoryItem.Owner.Filter(localId))
			invItems.Add(item);

		for (int i = 0; i < 15; i++)
		{
			var cell = new PanelContainer();
			cell.CustomMinimumSize = new Vector2(90, 80);
			var cellStyle = new StyleBoxFlat();
			cellStyle.CornerRadiusTopLeft = 4;
			cellStyle.CornerRadiusTopRight = 4;
			cellStyle.CornerRadiusBottomLeft = 4;
			cellStyle.CornerRadiusBottomRight = 4;

			if (i < invItems.Count)
			{
				var item = invItems[i];
				cellStyle.BgColor = new Color(0.2f, 0.35f, 0.65f, 0.9f);
				cell.AddThemeStyleboxOverride("panel", cellStyle);

				var gearDef = conn.Db.GearDefinition.Id.Find(item.GearDefinitionId);

				var vbox = new VBoxContainer();
				vbox.Alignment = BoxContainer.AlignmentMode.Center;
				var nameLabel = new Label();
				nameLabel.Text = gearDef?.Name ?? "???";
				nameLabel.AddThemeFontSizeOverride("font_size", 11);
				nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
				nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
				vbox.AddChild(nameLabel);

				if (gearDef is not null)
				{
					var slotTag = new Label();
					slotTag.Text = SlotDisplayName(gearDef.Slot);
					slotTag.AddThemeFontSizeOverride("font_size", 10);
					slotTag.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
					slotTag.HorizontalAlignment = HorizontalAlignment.Center;
					vbox.AddChild(slotTag);
				}

				cell.AddChild(vbox);
				cell.TooltipText = BuildGearTooltip(conn, item.GearDefinitionId, item.CraftedBy);

				var capturedItemId = item.Id;
				var btnRow = new HBoxContainer();
				btnRow.Alignment = BoxContainer.AlignmentMode.Center;
				btnRow.AddThemeConstantOverride("separation", 2);

				var equipBtn = new Button();
				equipBtn.Text = "E";
				equipBtn.TooltipText = "Equip";
				equipBtn.CustomMinimumSize = new Vector2(24, 20);
				equipBtn.AddThemeFontSizeOverride("font_size", 10);
				equipBtn.Pressed += () => conn.Reducers.EquipGear(capturedItemId);
				btnRow.AddChild(equipBtn);

				SpacetimeDB.Types.StorageChest? playerChest = null;
				foreach (var ch in conn.Db.StorageChest.Owner.Filter(localId))
				{
					playerChest = ch;
					break;
				}
				if (playerChest is SpacetimeDB.Types.StorageChest chest)
				{
					var storeBtn = new Button();
					storeBtn.Text = "S";
					storeBtn.TooltipText = "Store in chest";
					storeBtn.CustomMinimumSize = new Vector2(24, 20);
					storeBtn.AddThemeFontSizeOverride("font_size", 10);
					var capturedChestId = chest.Id;
					storeBtn.Pressed += () => conn.Reducers.TransferToChest(capturedItemId, capturedChestId);
					btnRow.AddChild(storeBtn);
				}

				vbox.AddChild(btnRow);
			}
			else
			{
				cellStyle.BgColor = new Color(0.12f, 0.12f, 0.14f, 0.7f);
				cell.AddThemeStyleboxOverride("panel", cellStyle);

				var emptyLabel = new Label();
				emptyLabel.Text = "";
				cell.AddChild(emptyLabel);
			}

			_inventoryGrid.AddChild(cell);
		}

		SpacetimeDB.Types.StorageChest? ownedChest = null;
		foreach (var ch in conn.Db.StorageChest.Owner.Filter(localId))
		{
			ownedChest = ch;
			break;
		}

		if (ownedChest is SpacetimeDB.Types.StorageChest myChest)
		{
			_chestPanel.Visible = true;

			var chestItems = new List<SpacetimeDB.Types.ChestItem>();
			foreach (var ci in conn.Db.ChestItem.ChestId.Filter(myChest.Id))
				chestItems.Add(ci);

			for (int i = 0; i < myChest.Capacity; i++)
			{
				var cell = new PanelContainer();
				cell.CustomMinimumSize = new Vector2(90, 80);
				var cellStyle = new StyleBoxFlat();
				cellStyle.CornerRadiusTopLeft = 4;
				cellStyle.CornerRadiusTopRight = 4;
				cellStyle.CornerRadiusBottomLeft = 4;
				cellStyle.CornerRadiusBottomRight = 4;

				if (i < chestItems.Count)
				{
					var ci = chestItems[i];
					cellStyle.BgColor = new Color(0.5f, 0.35f, 0.15f, 0.9f);
					cell.AddThemeStyleboxOverride("panel", cellStyle);

					var gearDef = conn.Db.GearDefinition.Id.Find(ci.GearDefinitionId);
					var vbox = new VBoxContainer();
					vbox.Alignment = BoxContainer.AlignmentMode.Center;
					var nameLabel = new Label();
					nameLabel.Text = gearDef?.Name ?? "???";
					nameLabel.AddThemeFontSizeOverride("font_size", 11);
					nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
					nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
					vbox.AddChild(nameLabel);
					cell.AddChild(vbox);

					cell.TooltipText = BuildGearTooltip(conn, ci.GearDefinitionId, ci.CraftedBy) + "\nClick to retrieve";

					var capturedCiId = ci.Id;
					cell.GuiInput += (InputEvent ev) =>
					{
						if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
							conn.Reducers.TransferFromChest(capturedCiId);
					};
					cell.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
				}
				else
				{
					cellStyle.BgColor = new Color(0.12f, 0.12f, 0.14f, 0.7f);
					cell.AddThemeStyleboxOverride("panel", cellStyle);
				}

				_chestGrid.AddChild(cell);
			}
		}
		else
		{
			_chestPanel.Visible = false;
		}
	}

	private static string BuildGearTooltip(DbConnection conn, ulong gearDefId, SpacetimeDB.Identity craftedBy)
	{
		var def = conn.Db.GearDefinition.Id.Find(gearDefId);
		if (def is null) return "Unknown gear";

		var lines = new List<string> { def.Name, $"Slot: {SlotDisplayName(def.Slot)}" };

		foreach (var bonus in def.StatBonuses)
			lines.Add($"+{bonus.Value} {bonus.Stat}");
		if (def.HealthBonus > 0)
			lines.Add($"+{def.HealthBonus} Max Health");
		if (def.SetName is not null)
			lines.Add($"Set: {def.SetName}");

		var crafter = conn.Db.Player.Identity.Find(craftedBy);
		lines.Add($"Crafted by: {crafter?.DisplayName ?? "Unknown"}");

		return string.Join("\n", lines);
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
