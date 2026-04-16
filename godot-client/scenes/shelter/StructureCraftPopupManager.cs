using Godot;
using SpacetimeDB;
using SpacetimeDB.Types;
using System.Linq;

public partial class StructureCraftPopupManager : Node
{
	[Signal]
	public delegate void CloseRequestedEventHandler();

	private Control _popup;
	private PanelContainer _modalPanel;
	private Label _titleLabel;
	private VBoxContainer _recipeList;
	private ulong? _openStructureDefId;

	public Control Popup => _popup;
	public PanelContainer ModalPanel => _modalPanel;

	public void Init(CanvasLayer popupLayer)
	{
		BuildPopup(popupLayer);
	}

	public void Open(ulong structureDefinitionId)
	{
		_openStructureDefId = structureDefinitionId;
		Refresh();
	}

	public void Close()
	{
		_openStructureDefId = null;
	}

	public void Refresh()
	{
		foreach (var child in _recipeList.GetChildren())
			child.QueueFree();

		if (_openStructureDefId is not ulong defId) return;

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var def = conn.Db.StructureDefinition.Id.Find(defId);
		_titleLabel.Text = def != null ? $"{def.Name} — Recipes" : "Crafting Station";

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

			_recipeList.AddChild(row);
		}

		if (_recipeList.GetChildCount() == 0)
		{
			var empty = new Label();
			empty.Text = "No recipes available";
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			empty.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
			_recipeList.AddChild(empty);
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
		_modalPanel = panel;

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 12);
		panel.AddChild(vbox);

		var header = new HBoxContainer();
		_titleLabel = new Label();
		_titleLabel.Text = "Crafting Station";
		_titleLabel.AddThemeFontSizeOverride("font_size", 22);
		_titleLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		header.AddChild(_titleLabel);

		var closeBtn = new Button();
		closeBtn.Text = "X";
		closeBtn.CustomMinimumSize = new Vector2(32, 32);
		closeBtn.Pressed += () => EmitSignal(SignalName.CloseRequested);
		header.AddChild(closeBtn);
		vbox.AddChild(header);

		var sep = new HSeparator();
		vbox.AddChild(sep);

		var scroll = new ScrollContainer();
		scroll.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		vbox.AddChild(scroll);

		_recipeList = new VBoxContainer();
		_recipeList.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		_recipeList.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(_recipeList);
	}
}
