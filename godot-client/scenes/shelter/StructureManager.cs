using Godot;
using SpacetimeDB;
using SpacetimeDB.Types;
using System.Collections.Generic;
using System.Linq;

public partial class StructureManager : Node
{
	private static readonly Vector2 StructureSize = new(64, 64);

	private Node2D _worldRoot;
	private TileMapLayer _buildingLayer;
	private Vector2 _mapScale;
	private SubViewport _subViewport;
	private Camera2D _camera;

	private bool _placementMode;
	private Node2D _placementGhost;
	private ulong _pendingDefinitionId;
	private string _pendingStructureName;
	private bool _pendingIndoorOnly;
	private bool _placementValid = true;

	private Dictionary<ulong, Node2D> _placedStructureNodes = new();

	private PanelContainer _craftingPanel;
	private VBoxContainer _craftingList;

	public bool InPlacementMode => _placementMode;

	public void Init(Node2D worldRoot, TileMapLayer buildingLayer, Vector2 mapScale,
		SubViewport subViewport, Camera2D camera, PanelContainer craftingPanel, VBoxContainer craftingList)
	{
		_worldRoot = worldRoot;
		_buildingLayer = buildingLayer;
		_mapScale = mapScale;
		_subViewport = subViewport;
		_camera = camera;
		_craftingPanel = craftingPanel;
		_craftingList = craftingList;

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		conn.Db.PlayerStructure.OnInsert += OnPlayerStructureInsert;

		foreach (var ps in conn.Db.PlayerStructure.Owner.Filter(localId))
			SpawnStructureNode(ps);
	}

	[Signal]
	public delegate void StructureClickedEventHandler(ulong structureDefinitionId);

	[Signal]
	public delegate void PlacementStartedEventHandler();

	public override void _ExitTree()
	{
		var conn = SpacetimeNetworkManager.Instance?.Conn;
		if (conn != null)
			conn.Db.PlayerStructure.OnInsert -= OnPlayerStructureInsert;
	}

	public void ProcessPlacement()
	{
		if (!_placementMode || _placementGhost == null) return;

		var mousePos = _subViewport.GetMousePosition();
		var worldPos = _camera.Position + (mousePos - (Vector2)_subViewport.Size / 2f) / _camera.Zoom;
		_placementGhost.Position = worldPos;

		if (_pendingIndoorOnly)
		{
			var localPos = worldPos / _mapScale;
			var tileCoord = _buildingLayer.LocalToMap(localPos);
			bool onBuilding = _buildingLayer.GetCellSourceId(tileCoord) != -1;
			_placementValid = onBuilding;
			_placementGhost.Modulate = onBuilding
				? new Color(1f, 1f, 1f, 0.6f)
				: new Color(1f, 0.2f, 0.2f, 0.6f);
		}
		else
		{
			_placementValid = true;
		}
	}

	public void EnterPlacementMode(ulong definitionId, string structureName, bool indoorOnly)
	{
		if (_placementMode) return;
		EmitSignal(SignalName.PlacementStarted);

		_placementMode = true;
		_pendingDefinitionId = definitionId;
		_pendingStructureName = structureName;
		_pendingIndoorOnly = indoorOnly;
		_placementValid = !indoorOnly;

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

	public void ConfirmPlacement()
	{
		if (!_placementMode || _placementGhost == null) return;
		if (!_placementValid) return;

		var pos = _placementGhost.Position;
		var conn = SpacetimeNetworkManager.Instance.Conn;
		conn.Reducers.BuildStructure(_pendingDefinitionId, (int)pos.X, (int)pos.Y);

		_placementGhost.QueueFree();
		_placementGhost = null;
		_placementMode = false;
	}

	public void CancelPlacement()
	{
		if (!_placementMode) return;

		if (_placementGhost != null)
		{
			_placementGhost.QueueFree();
			_placementGhost = null;
		}
		_placementMode = false;
	}

	public void RefreshCraftingMenu()
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
				var capturedIndoor = definition.IndoorOnly;
				buildBtn.Pressed += () => EnterPlacementMode(capturedId, capturedName, capturedIndoor);
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

	public void SetCraftingPanelVisible(bool visible)
	{
		_craftingPanel.Visible = visible;
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
		area.StructureClicked += () => EmitSignal(SignalName.StructureClicked, capturedDefId);

		_worldRoot.AddChild(area);
		_placedStructureNodes[ps.Id] = area;
	}

	private void OnPlayerStructureInsert(EventContext ctx, SpacetimeDB.Types.PlayerStructure structure)
	{
		if (structure.Owner != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;

		SpawnStructureNode(structure);
		RefreshCraftingMenu();
	}
}
