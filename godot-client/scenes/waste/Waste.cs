using Godot;
using SpacetimeDB;
using SpacetimeDB.Types;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Waste : Node2D
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

	private Label _locationLabel;
	private Button _travelWasteButton;
	private Button _travelShelterButton;
	private Button _travelGuildHallButton;

	private PanelContainer _craftingPanel;
	private VBoxContainer _craftingList;

	private struct CraftingProgress
	{
		public ProgressBar Bar;
		public ulong LocalStartUsec;
		public double InitialPercent;
		public ulong RemainingUsec;
	}
	private readonly Dictionary<ActivityType, CraftingProgress> _craftingProgressBars = new();

	private Dictionary<ulong, Activity> _activityNodes = new();

	private ColorRect _popupBackdrop;
	private Control _characterPopup;
	private Control _inventoryPopup;
	private Control _socialPopup;
	private Control _systemPopup;

	private PanelContainer _modalCharacter;
	private PanelContainer _modalInventory;
	private PanelContainer _modalSocial;
	private PanelContainer _modalSystem;

	private Button _btnCharacter;
	private Button _btnInventory;
	private Button _btnSocial;
	private Button _btnSystem;

	private int? _openPopupIndex;

	private FlowContainer _relevantResourcesBar;
	private FlowContainer _relevantStatsBar;
	private PanelContainer _relevantResourcesPanel;
	private Label _relevantResourcesTitle;
	private Label _relevantStatsTitle;

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
		_systemPopup = GetNode<Control>("%SystemPopup");

		_modalCharacter = GetNode<PanelContainer>("CanvasLayerPopups/CharacterPopup/CenterCharacter/CharacterPanel");
		_modalInventory = GetNode<PanelContainer>("CanvasLayerPopups/InventoryPopup/CenterInventory/InventoryPanel");
		_modalSocial = GetNode<PanelContainer>("CanvasLayerPopups/SocialPopup/CenterSocial/SocialPanel");
		_modalSystem = GetNode<PanelContainer>("CanvasLayerPopups/SystemPopup/CenterSystem/SystemPanel");

		_btnCharacter = GetNode<Button>("%CharacterMenuButton");
		_btnInventory = GetNode<Button>("%InventoryMenuButton");
		_btnSocial = GetNode<Button>("%SocialMenuButton");
		_btnSystem = GetNode<Button>("%SystemMenuButton");

		_btnCharacter.Pressed += () => TogglePopup(0);
		_btnInventory.Pressed += () => TogglePopup(1);
		_btnSocial.Pressed += () => TogglePopup(2);
		_btnSystem.Pressed += () => TogglePopup(3);

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

		_guildSocialPanel.GuildSessionChanged += OnGuildSessionChanged;

		conn.Db.Player.OnUpdate += OnPlayerUpdate;

		conn.Db.PlayerShelter.OnInsert += OnPlayerShelterInsert;
		conn.Db.PlayerStructure.OnInsert += OnPlayerStructureInsert;
		conn.Db.KillLoot.OnInsert += OnKillLootInsert;

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
	}

	public override void _Process(double delta)
	{
		foreach (var (type, cp) in _craftingProgressBars)
		{
			if (!IsInstanceValid(cp.Bar)) continue;
			if (cp.InitialPercent >= 100.0) { cp.Bar.Value = 100; continue; }
			var elapsed = Time.GetTicksUsec() - cp.LocalStartUsec;
			var span = 100.0 - cp.InitialPercent;
			var pct = elapsed / (double)cp.RemainingUsec * span;
			cp.Bar.Value = Math.Clamp(cp.InitialPercent + pct, 0.0, 100.0);
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (_openPopupIndex is null) return;
		if (@event is not InputEventMouseButton mb || !mb.Pressed) return;

		var shell = GetOpenModalShell();
		if (shell is null) return;
		if (shell.GetGlobalRect().HasPoint(mb.GlobalPosition))
			return;

		CloseAllPopups();
		GetViewport().SetInputAsHandled();
	}

	private PanelContainer? GetOpenModalShell() => _openPopupIndex switch
	{
		0 => _modalCharacter,
		1 => _modalInventory,
		2 => _modalSocial,
		3 => _modalSystem,
		_ => null,
	};

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel"))
		{
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
			if (_openPopupIndex == 2)
				CloseAllPopups();
			else
				OpenPopup(2);
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
		_socialPopup.Visible = index == 2;
		_systemPopup.Visible = index == 3;

		switch (index)
		{
			case 0:
				_characterProfilePanel.RefreshOnOpen();
				break;
			case 2:
				_guildSocialPanel.RefreshOnOpen();
				break;
		}
	}

	private void CloseAllPopups()
	{
		_openPopupIndex = null;
		_popupBackdrop.Visible = false;
		_characterPopup.Visible = false;
		_inventoryPopup.Visible = false;
		_socialPopup.Visible = false;
		_systemPopup.Visible = false;
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

	private static Color LocationPlayfieldColor(LocationType loc) => loc switch
	{
		LocationType.Waste => new Color(0.45f, 0.45f, 0.48f),
		LocationType.Shelter => new Color(0.42f, 0.30f, 0.22f),
		LocationType.GuildHall => new Color(0.72f, 0.62f, 0.28f),
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
			case ActivityType.CarbLoad:
				set.Add(StatType.Dexterity);
				set.Add(StatType.Endurance);
				set.Add(StatType.Intelligence);
				set.Add(StatType.Perception);
				set.Add(StatType.Wit);
				set.Add(StatType.Strength);
				break;
			case ActivityType.Focus:
				set.Add(StatType.Perception);
				break;
			case ActivityType.Study:
				set.Add(StatType.Intelligence);
				break;
			case ActivityType.Salvage:
				set.Add(StatType.Dexterity);
				set.Add(StatType.Wit);
				break;
			case ActivityType.SearchFood:
				set.Add(StatType.Perception);
				break;
			case ActivityType.SearchFabric:
				set.Add(StatType.Intelligence);
				break;
			case ActivityType.SearchMetal:
				set.Add(StatType.Strength);
				break;
			case ActivityType.SearchMoney:
				set.Add(StatType.Wit);
				break;
			case ActivityType.SearchParts:
				set.Add(StatType.Dexterity);
				break;
			case ActivityType.SearchWood:
				set.Add(StatType.Endurance);
				break;
			case ActivityType.TrainStrength:
				set.Add(StatType.Strength);
				break;
			case ActivityType.TrainWit:
				set.Add(StatType.Wit);
				break;
			case ActivityType.TrainEndurance:
				set.Add(StatType.Endurance);
				break;
			case ActivityType.TrainDexterity:
				set.Add(StatType.Dexterity);
				break;
			case ActivityType.LootBigWood:
			case ActivityType.BuildShelter:
			case ActivityType.BuildDumbbells:
			case ActivityType.BuildBookshelf:
			case ActivityType.BuildDartBoard:
			case ActivityType.BuildMeditationNook:
			case ActivityType.BuildStairStepper:
			case ActivityType.BuildPingPongTable:
				break;
		}
	}

	private static List<StatType> ComputeRelevantStatTypes(DbConnection conn, SpacetimeDB.Identity localId, LocationType loc)
	{
		var set = new HashSet<StatType>();

		if (loc == LocationType.Waste)
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

	private static void AddActivityOutputResourceTypes(ActivityType activityType, HashSet<ResourceType> set)
	{
		switch (activityType)
		{
			case ActivityType.Salvage:
				set.Add(ResourceType.Metal);
				set.Add(ResourceType.Money);
				break;
		}
	}

	private static List<ResourceType> ComputeRelevantResourceTypes(DbConnection conn, SpacetimeDB.Identity localId, LocationType loc)
	{
		var set = new HashSet<ResourceType>();

		if (loc == LocationType.Waste)
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
			AddActivityOutputResourceTypes(activity.Type, set);
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

		_travelWasteButton.Visible = loc != LocationType.Waste;
		_travelShelterButton.Visible = loc != LocationType.Shelter
			&& conn.Db.PlayerShelter.Owner.Find(localId) is not null;
		_travelGuildHallButton.Visible = loc != LocationType.GuildHall
			&& conn.Db.GuildMember.PlayerId.Find(localId) is not null;

		bool isShelterLoc = loc == LocationType.Shelter || loc == LocationType.GuildHall;
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

		// Visibility (location + unlock criteria) is applied in Activity.Format via InitActivityTracking.
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
				buildBtn.CustomMinimumSize = new Vector2(80, 28);
				var capturedId = definition.Id;
				buildBtn.Pressed += () => conn.Reducers.BuildStructure(capturedId);
				row.AddChild(buildBtn);
			}

			_craftingList.AddChild(row);
		}

		var activeTask = conn.Db.ActiveTask.Participant.Find(localId);
		_craftingProgressBars.Clear();

		foreach (var activity in conn.Db.Activity.Participant.Filter(localId))
		{
			if (!Activity.IsCraftableActivityType(activity.Type))
				continue;

			var outer = new VBoxContainer();
			outer.AddThemeConstantOverride("separation", 4);

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);

			var nameLabel = new Label();
			nameLabel.Text = Activity.GetActivityDisplayName(activity.Type);
			nameLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
			row.AddChild(nameLabel);

			var costLabel = new Label();
			costLabel.Text = string.Join(", ", activity.Cost.Select(c => $"{c.Amount} {c.Type}"));
			costLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
			row.AddChild(costLabel);

			var effectiveMs = Activity.GetEffectiveDurationMs(activity, conn, localId);
			var durationSec = effectiveMs / 1000.0;
			var durationLabel = new Label();
			durationLabel.Text = durationSec >= 1.0 ? $"{durationSec:F0}s" : $"{durationSec:F1}s";
			durationLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
			row.AddChild(durationLabel);

			bool isBuilding = activeTask is not null && activeTask.Type == activity.Type;
			if (isBuilding)
			{
				row.AddChild(new Label
				{
					Text = "Building...",
					Modulate = new Color(1f, 0.85f, 0.3f)
				});
			}
			else
			{
				var buildBtn = new Button();
				buildBtn.Text = "Build";
				buildBtn.CustomMinimumSize = new Vector2(80, 28);
				var capturedType = activity.Type;
				buildBtn.Pressed += () => conn.Reducers.StartActivity(capturedType);
				row.AddChild(buildBtn);
			}

			outer.AddChild(row);

			if (isBuilding)
			{
				var fillStyle = new StyleBoxFlat();
				fillStyle.BgColor = new Color(0.3f, 0.75f, 0.35f);
				fillStyle.SetCornerRadiusAll(3);

				var bgStyle = new StyleBoxFlat();
				bgStyle.BgColor = new Color(0.15f, 0.15f, 0.18f);
				bgStyle.SetCornerRadiusAll(3);

				var progressBar = new ProgressBar();
				progressBar.MinValue = 0;
				progressBar.MaxValue = 100;
				progressBar.ShowPercentage = false;
				progressBar.CustomMinimumSize = new Vector2(0, 12);
				progressBar.AddThemeStyleboxOverride("fill", fillStyle);
				progressBar.AddThemeStyleboxOverride("background", bgStyle);

				var startUs = (long)activeTask.StartedAt.MicrosecondsSinceUnixEpoch;
				var endUs = (long)activeTask.CompletesAt.MicrosecondsSinceUnixEpoch;
				var totalUs = endUs - startUs;

				if (totalUs > 0)
				{
					var wallNowUs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000L;
					var alreadyUs = Math.Max(0L, Math.Min(wallNowUs - startUs, totalUs));
					var initialPct = alreadyUs / (double)totalUs * 100.0;
					progressBar.Value = Math.Clamp(initialPct, 0.0, 100.0);

					_craftingProgressBars[activity.Type] = new CraftingProgress
					{
						Bar = progressBar,
						LocalStartUsec = Time.GetTicksUsec(),
						InitialPercent = initialPct,
						RemainingUsec = (ulong)Math.Max(1, totalUs - alreadyUs)
					};
				}
				else
				{
					progressBar.Value = 100;
				}

				outer.AddChild(progressBar);
			}

			_craftingList.AddChild(outer);
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
		if (structure.Owner == SpacetimeNetworkManager.Instance.LocalIdentity)
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

		if (Activity.IsCraftableActivityType(task.Type))
			RefreshCraftingMenu();
	}

	private void OnActiveTaskDelete(EventContext ctx, SpacetimeDB.Types.ActiveTask task)
	{
		if (task.Participant != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;

		RefreshLocationUI();
	}

	private void OnActivityInsert(EventContext ctx, SpacetimeDB.Types.Activity activity)
	{
		if (activity.Participant != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;

		AddActivityNode(activity);
		RefreshRelevantLocationContext();

		if (Activity.IsCraftableActivityType(activity.Type))
			RefreshCraftingMenu();
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

		if (Activity.IsCraftableActivityType(activity.Type))
			RefreshCraftingMenu();
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
		zombie.Killed += () => OnZombieKilled(zombie.Position);
		_worldRoot.AddChild(zombie);
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
