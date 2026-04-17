using Godot;
using SpacetimeDB;
using SpacetimeDB.Types;
using System;
using System.Collections.Generic;

public partial class UpgradesManager : Node
{
	[Signal]
	public delegate void CloseRequestedEventHandler();

	private Control _popup;
	private PanelContainer _modal;
	private VBoxContainer _rowsContainer;
	private Label _moneyLabel;
	private bool _isOpen;

	public Control Popup => _popup;
	public PanelContainer ModalPanel => _modal;

	private static readonly (UpgradeType Type, string Name, string Effect)[] Entries = new (UpgradeType, string, string)[]
	{
		(UpgradeType.AttackSpeed,    "Attack Speed",    "Swing faster. -5% attack cooldown per level (min 25%)."),
		(UpgradeType.KillsPerClick,  "Kills Per Click", "+1 additional zombie per swing per level."),
		(UpgradeType.ZombieDensity,  "Zombie Density",  "+20 more zombies roaming the shelter per level."),
		(UpgradeType.LootMultiplier, "Loot Multiplier", "+1 bonus resource per kill and per scavenge per level."),
	};

	public void Init(CanvasLayer popupLayer)
	{
		BuildPopup(popupLayer);

		var conn = SpacetimeNetworkManager.Instance.Conn;
		conn.Db.PlayerUpgrade.OnInsert += OnUpgradeChanged;
		conn.Db.PlayerUpgrade.OnUpdate += OnUpgradeUpdated;
		conn.Db.ResourceTracker.OnUpdate += OnResourceTrackerUpdated;
		conn.Db.ResourceTracker.OnInsert += OnResourceTrackerChanged;
	}

	public override void _ExitTree()
	{
		var conn = SpacetimeNetworkManager.Instance?.Conn;
		if (conn is null) return;
		conn.Db.PlayerUpgrade.OnInsert -= OnUpgradeChanged;
		conn.Db.PlayerUpgrade.OnUpdate -= OnUpgradeUpdated;
		conn.Db.ResourceTracker.OnUpdate -= OnResourceTrackerUpdated;
		conn.Db.ResourceTracker.OnInsert -= OnResourceTrackerChanged;
	}

	public void SetOpen(bool open)
	{
		_isOpen = open;
		if (open) Refresh();
	}

	private void OnUpgradeChanged(EventContext ctx, PlayerUpgrade row)
	{
		if (_isOpen && row.Owner == SpacetimeNetworkManager.Instance.LocalIdentity)
			CallDeferred(nameof(Refresh));
	}

	private void OnUpgradeUpdated(EventContext ctx, PlayerUpgrade oldRow, PlayerUpgrade newRow)
	{
		if (_isOpen && newRow.Owner == SpacetimeNetworkManager.Instance.LocalIdentity)
			CallDeferred(nameof(Refresh));
	}

	private void OnResourceTrackerChanged(EventContext ctx, SpacetimeDB.Types.ResourceTracker row)
	{
		if (_isOpen && row.Owner == SpacetimeNetworkManager.Instance.LocalIdentity && row.Type == ResourceType.Money)
			CallDeferred(nameof(Refresh));
	}

	private void OnResourceTrackerUpdated(EventContext ctx, SpacetimeDB.Types.ResourceTracker oldRow, SpacetimeDB.Types.ResourceTracker newRow)
	{
		if (_isOpen && newRow.Owner == SpacetimeNetworkManager.Instance.LocalIdentity && newRow.Type == ResourceType.Money)
			CallDeferred(nameof(Refresh));
	}

	public void Refresh()
	{
		if (_rowsContainer is null) return;

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		ulong money = 0;
		foreach (var r in conn.Db.ResourceTracker.Owner.Filter(localId))
		{
			if (r.Type == ResourceType.Money) { money = r.Amount; break; }
		}
		_moneyLabel.Text = $"Money: {money}";

		foreach (var child in _rowsContainer.GetChildren())
			child.QueueFree();

		foreach (var entry in Entries)
		{
			uint level = 0;
			foreach (var row in conn.Db.PlayerUpgrade.ByUpgradeOwnerType.Filter((Owner: localId, Type: entry.Type)))
			{
				level = row.Level;
				break;
			}

			ulong cost = NextCost(level);

			var rowBox = new VBoxContainer();
			rowBox.AddThemeConstantOverride("separation", 2);

			var top = new HBoxContainer();
			top.AddThemeConstantOverride("separation", 8);

			var name = new Label();
			name.Text = $"{entry.Name}  [Lv {level}]";
			name.AddThemeFontSizeOverride("font_size", 18);
			name.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
			top.AddChild(name);

			var costLabel = new Label();
			costLabel.Text = $"{cost} Money";
			costLabel.AddThemeColorOverride("font_color", money >= cost ? new Color(0.9f, 0.9f, 0.3f) : new Color(0.6f, 0.6f, 0.6f));
			top.AddChild(costLabel);

			var buyBtn = new Button();
			buyBtn.Text = "Buy";
			buyBtn.CustomMinimumSize = new Vector2(80, 28);
			buyBtn.Disabled = money < cost;
			var capturedType = entry.Type;
			buyBtn.Pressed += () =>
			{
				conn.Reducers.PurchaseUpgrade(capturedType);
				CallDeferred(nameof(Refresh));
			};
			top.AddChild(buyBtn);

			rowBox.AddChild(top);

			var desc = new Label();
			desc.Text = entry.Effect;
			desc.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.65f));
			desc.AddThemeFontSizeOverride("font_size", 14);
			rowBox.AddChild(desc);

			_rowsContainer.AddChild(rowBox);
			_rowsContainer.AddChild(new HSeparator());
		}
	}

	public static ulong NextCost(uint currentLevel) =>
		(ulong)Math.Floor(10.0 * Math.Pow(1.5, currentLevel));

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
		panel.CustomMinimumSize = new Vector2(520, 440);
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
		var title = new Label();
		title.Text = "Upgrades";
		title.AddThemeFontSizeOverride("font_size", 22);
		title.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		header.AddChild(title);
		var closeBtn = new Button();
		closeBtn.Text = "X";
		closeBtn.CustomMinimumSize = new Vector2(32, 32);
		closeBtn.Pressed += () => EmitSignal(SignalName.CloseRequested);
		header.AddChild(closeBtn);
		outerVbox.AddChild(header);

		_moneyLabel = new Label();
		_moneyLabel.Text = "Money: 0";
		_moneyLabel.AddThemeFontSizeOverride("font_size", 16);
		_moneyLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.3f));
		outerVbox.AddChild(_moneyLabel);

		outerVbox.AddChild(new HSeparator());

		var scroll = new ScrollContainer();
		scroll.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		outerVbox.AddChild(scroll);

		_rowsContainer = new VBoxContainer();
		_rowsContainer.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		_rowsContainer.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(_rowsContainer);
	}
}
