using Godot;
using SpacetimeDB;
using SpacetimeDB.Types;
using StdbPlayer = SpacetimeDB.Types.Player;

/// <summary>
/// Display name editor (SetName reducer) for the Character popup.
/// </summary>
public partial class CharacterProfilePanel : PanelContainer
{
	private static readonly Color GoldAccent = new(0.9f, 0.85f, 0.4f);

	private Label _currentNameLabel;
	private LineEdit _nameInput;
	private Button _saveNameButton;

	public override void _Ready()
	{
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 8);
		AddChild(vbox);

		var header = new Label();
		header.Text = "Profile";
		header.AddThemeFontSizeOverride("font_size", 18);
		header.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(header);
		vbox.AddChild(new HSeparator());

		var currentRow = new HBoxContainer();
		currentRow.AddThemeConstantOverride("separation", 8);
		var cur = new Label();
		cur.Text = "Display Name:";
		currentRow.AddChild(cur);
		_currentNameLabel = new Label();
		_currentNameLabel.AddThemeColorOverride("font_color", GoldAccent);
		_currentNameLabel.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
		currentRow.AddChild(_currentNameLabel);
		vbox.AddChild(currentRow);

		var editRow = new HBoxContainer();
		_nameInput = new LineEdit();
		_nameInput.PlaceholderText = "Enter new name...";
		_nameInput.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
		editRow.AddChild(_nameInput);
		_saveNameButton = new Button();
		_saveNameButton.Text = "Save";
		_saveNameButton.CustomMinimumSize = new Vector2(140, 36);
		_saveNameButton.Pressed += OnSaveNamePressed;
		editRow.AddChild(_saveNameButton);
		vbox.AddChild(editRow);

		var conn = SpacetimeNetworkManager.Instance.Conn;
		conn.Db.Player.OnUpdate += OnPlayerUpdate;

		RefreshProfileUI();
	}

	public void RefreshOnOpen()
	{
		RefreshProfileUI();
	}

	private void RefreshProfileUI()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		var player = conn.Db.Player.Identity.Find(localId);
		_currentNameLabel.Text = player?.DisplayName ?? "Unknown";
	}

	private void OnPlayerUpdate(EventContext ctx, StdbPlayer oldPlayer, StdbPlayer newPlayer)
	{
		if (newPlayer.Identity == SpacetimeNetworkManager.Instance.LocalIdentity)
			RefreshProfileUI();
	}

	private void OnSaveNamePressed()
	{
		var newName = _nameInput.Text.Trim();
		if (string.IsNullOrEmpty(newName)) return;
		SpacetimeNetworkManager.Instance.Conn.Reducers.SetName(newName);
		_nameInput.Text = "";
	}
}
