using Godot;
using SpacetimeDB;
using SpacetimeDB.Types;
using System;
using System.Collections.Generic;

public partial class GearInventoryManager : Node
{
	[Signal]
	public delegate void CloseRequestedEventHandler();

	private Control _gearInventoryPopup;
	private PanelContainer _modalGearInventory;
	private GridContainer _inventoryGrid;
	private HBoxContainer _equipmentSlotsContainer;
	private VBoxContainer _chestPanel;
	private GridContainer _chestGrid;

	private bool _isOpen;

	public Control Popup => _gearInventoryPopup;
	public PanelContainer ModalPanel => _modalGearInventory;

	public void Init(CanvasLayer popupLayer)
	{
		BuildGearInventoryPopup(popupLayer);

		var conn = SpacetimeNetworkManager.Instance.Conn;
		conn.Db.InventoryItem.OnInsert += OnInventoryItemChanged;
		conn.Db.InventoryItem.OnDelete += OnInventoryItemDeleted;
		conn.Db.EquippedGear.OnInsert += OnEquippedGearChanged;
		conn.Db.EquippedGear.OnDelete += OnEquippedGearDeleted;
		conn.Db.ChestItem.OnInsert += OnChestItemChanged;
		conn.Db.ChestItem.OnDelete += OnChestItemDeleted;
	}

	public override void _ExitTree()
	{
		var conn = SpacetimeNetworkManager.Instance?.Conn;
		if (conn == null) return;
		conn.Db.InventoryItem.OnInsert -= OnInventoryItemChanged;
		conn.Db.InventoryItem.OnDelete -= OnInventoryItemDeleted;
		conn.Db.EquippedGear.OnInsert -= OnEquippedGearChanged;
		conn.Db.EquippedGear.OnDelete -= OnEquippedGearDeleted;
		conn.Db.ChestItem.OnInsert -= OnChestItemChanged;
		conn.Db.ChestItem.OnDelete -= OnChestItemDeleted;
	}

	public void SetOpen(bool open)
	{
		_isOpen = open;
		if (open) Refresh();
	}

	public void Refresh()
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

	private void OnInventoryItemChanged(EventContext ctx, SpacetimeDB.Types.InventoryItem item)
	{
		if (item.Owner != SpacetimeNetworkManager.Instance.LocalIdentity) return;
		if (_isOpen) Refresh();
	}

	private void OnInventoryItemDeleted(EventContext ctx, SpacetimeDB.Types.InventoryItem item)
	{
		if (item.Owner != SpacetimeNetworkManager.Instance.LocalIdentity) return;
		if (_isOpen) Refresh();
	}

	private void OnEquippedGearChanged(EventContext ctx, SpacetimeDB.Types.EquippedGear gear)
	{
		if (gear.Owner != SpacetimeNetworkManager.Instance.LocalIdentity) return;
		if (_isOpen) Refresh();
	}

	private void OnEquippedGearDeleted(EventContext ctx, SpacetimeDB.Types.EquippedGear gear)
	{
		if (gear.Owner != SpacetimeNetworkManager.Instance.LocalIdentity) return;
		if (_isOpen) Refresh();
	}

	private void OnChestItemChanged(EventContext ctx, SpacetimeDB.Types.ChestItem item)
	{
		if (_isOpen) Refresh();
	}

	private void OnChestItemDeleted(EventContext ctx, SpacetimeDB.Types.ChestItem item)
	{
		if (_isOpen) Refresh();
	}

	private void BuildGearInventoryPopup(CanvasLayer popupLayer)
	{
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
		closeBtn.Pressed += () => EmitSignal(SignalName.CloseRequested);
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
}
