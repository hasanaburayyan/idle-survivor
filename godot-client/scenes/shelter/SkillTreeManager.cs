using Godot;
using SpacetimeDB;
using SpacetimeDB.Types;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class SkillTreeManager : Node
{
	[Signal]
	public delegate void CloseRequestedEventHandler();

	private Control _popup;
	private PanelContainer _modal;
	private SkillTreeCanvas _canvas;
	private HBoxContainer _resourcesRow;
	private readonly Dictionary<ResourceType, Label> _resourceLabels = new();
	private bool _isOpen;

	private const float CanvasOriginX = 400f;
	private const float CanvasOriginY = 320f;
	private const float NodeRadius = 38f;

	public Control Popup => _popup;
	public PanelContainer ModalPanel => _modal;

	public void Init(CanvasLayer popupLayer)
	{
		BuildPopup(popupLayer);

		var conn = SpacetimeNetworkManager.Instance.Conn;
		conn.Db.SkillTreeNode.OnInsert += OnNodeChanged;
		conn.Db.PlayerSkillTreeUnlock.OnInsert += OnUnlockInserted;
		conn.Db.PlayerSkillTreeUnlock.OnUpdate += OnUnlockUpdated;
		conn.Db.ResourceTracker.OnInsert += OnResourceChanged;
		conn.Db.ResourceTracker.OnUpdate += OnResourceUpdated;
	}

	public override void _ExitTree()
	{
		var conn = SpacetimeNetworkManager.Instance?.Conn;
		if (conn is null) return;
		conn.Db.SkillTreeNode.OnInsert -= OnNodeChanged;
		conn.Db.PlayerSkillTreeUnlock.OnInsert -= OnUnlockInserted;
		conn.Db.PlayerSkillTreeUnlock.OnUpdate -= OnUnlockUpdated;
		conn.Db.ResourceTracker.OnInsert -= OnResourceChanged;
		conn.Db.ResourceTracker.OnUpdate -= OnResourceUpdated;
	}

	public void SetOpen(bool open)
	{
		_isOpen = open;
		if (open) Redraw();
	}

	private void OnNodeChanged(EventContext ctx, SkillTreeNode row)
	{
		if (_isOpen) CallDeferred(nameof(Redraw));
	}

	private void OnUnlockInserted(EventContext ctx, PlayerSkillTreeUnlock row)
	{
		if (_isOpen && row.Owner == SpacetimeNetworkManager.Instance.LocalIdentity)
			CallDeferred(nameof(Redraw));
	}

	private void OnUnlockUpdated(EventContext ctx, PlayerSkillTreeUnlock oldRow, PlayerSkillTreeUnlock newRow)
	{
		if (_isOpen && newRow.Owner == SpacetimeNetworkManager.Instance.LocalIdentity)
			CallDeferred(nameof(Redraw));
	}

	private void OnResourceChanged(EventContext ctx, SpacetimeDB.Types.ResourceTracker row)
	{
		if (!_isOpen) return;
		if (row.Owner != SpacetimeNetworkManager.Instance.LocalIdentity) return;
		CallDeferred(nameof(Redraw));
	}

	private void OnResourceUpdated(EventContext ctx, SpacetimeDB.Types.ResourceTracker oldRow, SpacetimeDB.Types.ResourceTracker newRow)
	{
		if (!_isOpen) return;
		if (newRow.Owner != SpacetimeNetworkManager.Instance.LocalIdentity) return;
		CallDeferred(nameof(Redraw));
	}

	public void Redraw()
	{
		if (_canvas is null) return;

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		var resources = new Dictionary<ResourceType, ulong>();
		foreach (var r in conn.Db.ResourceTracker.Owner.Filter(localId))
			resources[r.Type] = r.Amount;

		RefreshResourcesRow(resources);

		_canvas.RefreshNodes(conn, localId, resources, NodeRadius, CanvasOriginX, CanvasOriginY);
		_canvas.QueueRedraw();
	}

	private static readonly ResourceType[] HeaderResourceOrder = new[]
	{
		ResourceType.Money, ResourceType.Wood, ResourceType.Metal, ResourceType.Fabric,
		ResourceType.Food,  ResourceType.Parts,
	};

	private static readonly Dictionary<ResourceType, Color> ResourceColors = new()
	{
		{ ResourceType.Money,  new Color(0.90f, 0.90f, 0.30f) },
		{ ResourceType.Wood,   new Color(0.55f, 0.80f, 0.45f) },
		{ ResourceType.Metal,  new Color(0.75f, 0.78f, 0.85f) },
		{ ResourceType.Fabric, new Color(0.85f, 0.65f, 0.85f) },
		{ ResourceType.Food,   new Color(0.90f, 0.60f, 0.35f) },
		{ ResourceType.Parts,  new Color(0.60f, 0.85f, 0.95f) },
	};

	private void RefreshResourcesRow(Dictionary<ResourceType, ulong> resources)
	{
		foreach (var rt in HeaderResourceOrder)
		{
			bool visible = rt == ResourceType.Money || resources.ContainsKey(rt);
			if (!_resourceLabels.TryGetValue(rt, out var label))
			{
				label = new Label();
				label.AddThemeFontSizeOverride("font_size", 16);
				if (ResourceColors.TryGetValue(rt, out var color))
					label.AddThemeColorOverride("font_color", color);
				_resourcesRow.AddChild(label);
				_resourceLabels[rt] = label;
			}
			ulong amount = resources.TryGetValue(rt, out var v) ? v : 0UL;
			label.Text = $"{rt}: {amount}";
			label.Visible = visible;
		}
	}

	private void BuildPopup(CanvasLayer popupLayer)
	{
		_popup = new Control();
		_popup.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_popup.Visible = false;
		popupLayer.AddChild(_popup);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_popup.AddChild(center);

		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(820, 720);
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.12f, 0.12f, 0.15f, 0.95f);
		style.CornerRadiusTopLeft = 8;
		style.CornerRadiusTopRight = 8;
		style.CornerRadiusBottomLeft = 8;
		style.CornerRadiusBottomRight = 8;
		style.ContentMarginLeft = 16;
		style.ContentMarginRight = 16;
		style.ContentMarginTop = 16;
		style.ContentMarginBottom = 16;
		panel.AddThemeStyleboxOverride("panel", style);
		center.AddChild(panel);
		_modal = panel;

		var outerVbox = new VBoxContainer();
		outerVbox.AddThemeConstantOverride("separation", 10);
		panel.AddChild(outerVbox);

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 12);
		var title = new Label();
		title.Text = "Skill Tree";
		title.AddThemeFontSizeOverride("font_size", 22);
		title.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		header.AddChild(title);

		_resourcesRow = new HBoxContainer();
		_resourcesRow.AddThemeConstantOverride("separation", 12);
		header.AddChild(_resourcesRow);

		var closeBtn = new Button();
		closeBtn.Text = "X";
		closeBtn.CustomMinimumSize = new Vector2(32, 32);
		closeBtn.Pressed += () => EmitSignal(SignalName.CloseRequested);
		header.AddChild(closeBtn);
		outerVbox.AddChild(header);

		outerVbox.AddChild(new HSeparator());

		_canvas = new SkillTreeCanvas();
		_canvas.CustomMinimumSize = new Vector2(780, 640);
		_canvas.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		_canvas.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		outerVbox.AddChild(_canvas);
	}
}

public partial class SkillTreeCanvas : Control
{
	private SkillTreeInner _inner;
	private bool _dragging;
	private Vector2 _dragStartMouse;
	private Vector2 _dragStartInnerPos;

	public override void _Ready()
	{
		ClipContents = true;
		MouseFilter = MouseFilterEnum.Stop;
		_inner = new SkillTreeInner();
		_inner.MouseFilter = MouseFilterEnum.Ignore;
		_inner.Size = new Vector2(4000, 4000);
		_inner.Position = Vector2.Zero;
		AddChild(_inner);
	}

	public void RefreshNodes(DbConnection conn, Identity localId, Dictionary<ResourceType, ulong> resources,
							 float nodeRadius, float originX, float originY)
	{
		if (_inner is null) return;
		_inner.RefreshNodes(conn, localId, resources, nodeRadius, originX, originY);
	}

	public new void QueueRedraw()
	{
		_inner?.QueueRedraw();
	}

	public override void _GuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
		{
			if (mb.Pressed && !_dragging)
			{
				_dragging = true;
				_dragStartMouse = mb.GlobalPosition;
				_dragStartInnerPos = _inner.Position;
				AcceptEvent();
			}
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (!_dragging) return;

		if (@event is InputEventMouseMotion mm)
		{
			Vector2 delta = mm.GlobalPosition - _dragStartMouse;
			_inner.Position = _dragStartInnerPos + delta;
			GetViewport().SetInputAsHandled();
		}
		else if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
		{
			_dragging = false;
			GetViewport().SetInputAsHandled();
		}
	}
}

public partial class SkillTreeInner : Control
{
	private record NodeVisual(ulong Id, ulong? VisualParentId, Vector2 Center, bool Owned, bool Visible);
	private readonly List<NodeVisual> _visuals = new();
	private readonly Dictionary<ulong, Button> _buttons = new();

	private static readonly Color NodeFill         = new Color(0.06f, 0.06f, 0.08f);
	private static readonly Color OutlineDefault   = new Color(0.90f, 0.90f, 0.92f);
	private static readonly Color OutlineAffordable = new Color(1.00f, 0.85f, 0.20f);
	private static readonly Color OutlineMaxed     = new Color(0.55f, 0.55f, 0.58f);
	private static readonly Color TextColor        = new Color(0.95f, 0.95f, 0.95f);

	public void RefreshNodes(DbConnection conn, Identity localId, Dictionary<ResourceType, ulong> resources,
							 float nodeRadius, float originX, float originY)
	{
		_visuals.Clear();

		var levels = new Dictionary<ulong, uint>();
		foreach (var u in conn.Db.PlayerSkillTreeUnlock.Owner.Filter(localId))
			levels[u.NodeId] = u.Level;

		uint unlockedTiers = 0;
		foreach (var node in conn.Db.SkillTreeNode.Iter())
		{
			if (node.EffectKind != SkillTreeEffectKind.ScavengeUnlock) continue;
			if (levels.TryGetValue(node.Id, out var lvl) && lvl >= 1) unlockedTiers++;
		}

		var seenIds = new HashSet<ulong>();

		foreach (var node in conn.Db.SkillTreeNode.Iter())
		{
			uint currentLevel = levels.TryGetValue(node.Id, out var lv) ? lv : 0u;
			uint prereqLevel = 0;
			bool hasPrereq = node.PrerequisiteNodeId is not null;
			if (hasPrereq)
				levels.TryGetValue(node.PrerequisiteNodeId.Value, out prereqLevel);

			ulong? visualParentId = node.VisualPrerequisiteNodeId ?? node.PrerequisiteNodeId;
			bool prereqMet = !hasPrereq || prereqLevel >= node.PrerequisiteMinLevel;
			bool revealed = !hasPrereq || prereqMet;

			if (!revealed)
			{
				_visuals.Add(new NodeVisual(node.Id, visualParentId, default, false, false));
				if (_buttons.TryGetValue(node.Id, out var oldBtn))
				{
					oldBtn.QueueFree();
					_buttons.Remove(node.Id);
				}
				continue;
			}

			seenIds.Add(node.Id);

			var center = new Vector2(node.PosX + originX, node.PosY + originY);
			bool isOwned = currentLevel >= 1;
			_visuals.Add(new NodeVisual(node.Id, visualParentId, center, isOwned, true));

			uint maxLevel = ComputeEffectiveMaxLevel(node, unlockedTiers);
			bool atMax = currentLevel >= maxLevel;
			var costs = atMax
				? new List<(ResourceType Type, ulong Amount)>()
				: ComputeNextLevelCost(node, currentLevel);

			bool canAfford = true;
			foreach (var (t, a) in costs)
			{
				ulong have = resources.TryGetValue(t, out var v) ? v : 0UL;
				if (have < a) { canAfford = false; break; }
			}

			bool purchaseBlocked = atMax || !canAfford || (!prereqMet && !isOwned);

			if (!_buttons.TryGetValue(node.Id, out var btn))
			{
				btn = new Button();
				btn.AddThemeFontSizeOverride("font_size", 11);
				btn.ClipText = false;
				var capturedId = node.Id;
				btn.Pressed += () => OnNodeButtonPressed(capturedId);
				_buttons[node.Id] = btn;
				AddChild(btn);
			}

			string labelText = node.BaseMaxLevel <= 1
				? node.Name
				: $"{node.Name}\nLv {currentLevel}/{maxLevel}";
			if (btn.Text != labelText) btn.Text = labelText;

			string tooltip = $"{node.Name}\n{node.Tooltip}";
			if (!prereqMet && !isOwned)
				tooltip += $"\n[Requires previous node at level {node.PrerequisiteMinLevel}]";
			else if (atMax)
				tooltip += "\n[Max Level]";
			else
			{
				tooltip += "\nCost: ";
				var parts = new List<string>();
				foreach (var (t, a) in costs) parts.Add($"{a} {t}");
				tooltip += string.Join(" + ", parts);
				if (node.BaseMaxLevel > 1)
					tooltip += $"\n(Lv {currentLevel} → {currentLevel + 1})";
				if (!canAfford)
					tooltip += "\n[Not enough resources]";
			}
			if (btn.TooltipText != tooltip) btn.TooltipText = tooltip;

			float diameter = nodeRadius * 2.2f;
			var targetSize = new Vector2(diameter, diameter);
			if (btn.CustomMinimumSize != targetSize)
			{
				btn.CustomMinimumSize = targetSize;
				btn.Size = targetSize;
			}
			var targetPos = center - targetSize * 0.5f;
			if (btn.Position != targetPos) btn.Position = targetPos;

			bool canPurchaseNow = !purchaseBlocked;
			Color outline;
			int outlineWidth;
			if (atMax) { outline = OutlineMaxed; outlineWidth = 2; }
			else if (canPurchaseNow) { outline = OutlineAffordable; outlineWidth = 3; }
			else { outline = OutlineDefault; outlineWidth = 2; }

			var style = new StyleBoxFlat();
			style.BgColor = NodeFill;
			style.CornerRadiusTopLeft = (int)nodeRadius;
			style.CornerRadiusTopRight = (int)nodeRadius;
			style.CornerRadiusBottomLeft = (int)nodeRadius;
			style.CornerRadiusBottomRight = (int)nodeRadius;
			style.BorderColor = outline;
			style.BorderWidthLeft = outlineWidth;
			style.BorderWidthRight = outlineWidth;
			style.BorderWidthTop = outlineWidth;
			style.BorderWidthBottom = outlineWidth;
			btn.AddThemeStyleboxOverride("normal", style);
			btn.AddThemeStyleboxOverride("hover", style);
			btn.AddThemeStyleboxOverride("pressed", style);
			btn.AddThemeStyleboxOverride("disabled", style);
			btn.AddThemeColorOverride("font_color", TextColor);

			// Keep the button enabled so tooltips still show even when the purchase is blocked;
			// the Pressed handler itself no-ops when the action is invalid.
			btn.Disabled = false;
			btn.SetMeta("blocked", purchaseBlocked);
		}

		var stale = _buttons.Keys.Where(id => !seenIds.Contains(id)).ToList();
		foreach (var id in stale)
		{
			_buttons[id].QueueFree();
			_buttons.Remove(id);
		}
	}

	private void OnNodeButtonPressed(ulong nodeId)
	{
		if (_buttons.TryGetValue(nodeId, out var btn)
			&& btn.HasMeta("blocked")
			&& (bool)btn.GetMeta("blocked"))
			return;
		SpacetimeNetworkManager.Instance?.Conn?.Reducers.PurchaseSkillTreeNode(nodeId);
	}

	private static readonly ResourceType[] TierResources = new[]
	{
		ResourceType.Wood, ResourceType.Metal, ResourceType.Fabric,
		ResourceType.Food, ResourceType.Parts,
	};

	private static uint ComputeEffectiveMaxLevel(SkillTreeNode node, uint unlockedTiers)
	{
		if (node.BaseMaxLevel <= 1) return node.BaseMaxLevel;
		if (unlockedTiers <= node.BranchTier) return node.BaseMaxLevel;
		return node.BaseMaxLevel * (1 + unlockedTiers - node.BranchTier);
	}

	private static uint ComputeCostTier(SkillTreeNode node, uint currentLevel)
	{
		if (node.EffectKind == SkillTreeEffectKind.ScavengeUnlock)
			return node.BranchTier > 0 ? node.BranchTier - 1 : 0;
		uint nextLevel = currentLevel + 1;
		uint band = (nextLevel - 1) / 5;
		return node.BranchTier + band;
	}

	private static List<(ResourceType Type, ulong Amount)> ComputeNextLevelCost(SkillTreeNode node, uint currentLevel)
	{
		var costs = new List<(ResourceType, ulong)>();
		ulong perResource = (ulong)Math.Max(1.0, Math.Floor(node.BaseCost * Math.Pow(1.5, currentLevel)));

		costs.Add((ResourceType.Money, perResource));
		uint costTier = ComputeCostTier(node, currentLevel);
		for (uint i = 1; i <= costTier && i <= TierResources.Length; i++)
			costs.Add((TierResources[i - 1], perResource));

		return costs;
	}

	public override void _Draw()
	{
		var byId = new Dictionary<ulong, NodeVisual>();
		foreach (var v in _visuals) byId[v.Id] = v;

		foreach (var v in _visuals)
		{
			if (!v.Visible) continue;
			if (v.VisualParentId is not ulong parentId) continue;
			if (!byId.TryGetValue(parentId, out var parent) || !parent.Visible) continue;

			Color lineColor;
			if (v.Owned && parent.Owned) lineColor = new Color(0.95f, 0.78f, 0.25f);
			else if (parent.Owned) lineColor = new Color(0.85f, 0.85f, 0.85f);
			else lineColor = new Color(0.4f, 0.4f, 0.44f);

			DrawLine(parent.Center, v.Center, lineColor, 3f);
		}
	}
}
