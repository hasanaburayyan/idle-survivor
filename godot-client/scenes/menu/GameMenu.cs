using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;

public partial class GameMenu : CanvasLayer
{
	private PanelContainer _panel;
	private TabBar _tabBar;
	private Control _generalTab;
	private Control _socialTab;

	// No-guild controls
	private VBoxContainer _noGuildContainer;
	private LineEdit _guildNameInput;
	private Button _createGuildButton;
	private VBoxContainer _invitesList;

	// In-guild controls
	private VBoxContainer _inGuildContainer;
	private Label _guildNameLabel;
	private VBoxContainer _membersList;
	private VBoxContainer _guildResourcesList;
	private LineEdit _invitePlayerInput;
	private Button _inviteButton;
	private Button _leaveGuildButton;
	private Button _disbandGuildButton;
	private Button _enterSessionButton;
	private Button _leaveSessionButton;

	[Signal]
	public delegate void GuildSessionChangedEventHandler(bool inSession);

	private bool _isOpen;

	public override void _Ready()
	{
		Layer = 10;
		Visible = false;
		_isOpen = false;

		_panel = new PanelContainer();
		_panel.AnchorsPreset = (int)Control.LayoutPreset.Center;
		_panel.AnchorLeft = 0.1f;
		_panel.AnchorRight = 0.9f;
		_panel.AnchorTop = 0.1f;
		_panel.AnchorBottom = 0.9f;
		_panel.OffsetLeft = 0;
		_panel.OffsetRight = 0;
		_panel.OffsetTop = 0;
		_panel.OffsetBottom = 0;
		_panel.GrowHorizontal = Control.GrowDirection.Both;
		_panel.GrowVertical = Control.GrowDirection.Both;
		AddChild(_panel);

		var mainVBox = new VBoxContainer();
		mainVBox.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		mainVBox.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		_panel.AddChild(mainVBox);

		_tabBar = new TabBar();
		_tabBar.AddTab("General");
		_tabBar.AddTab("Social");
		_tabBar.TabChanged += OnTabChanged;
		mainVBox.AddChild(_tabBar);

		var tabContent = new MarginContainer();
		tabContent.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		tabContent.AddThemeConstantOverride("margin_left", 16);
		tabContent.AddThemeConstantOverride("margin_right", 16);
		tabContent.AddThemeConstantOverride("margin_top", 16);
		tabContent.AddThemeConstantOverride("margin_bottom", 16);
		mainVBox.AddChild(tabContent);

		_generalTab = new VBoxContainer();
		var notYetLabel = new Label();
		notYetLabel.Text = "Not yet implemented";
		notYetLabel.HorizontalAlignment = HorizontalAlignment.Center;
		notYetLabel.VerticalAlignment = VerticalAlignment.Center;
		notYetLabel.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		_generalTab.AddChild(notYetLabel);
		tabContent.AddChild(_generalTab);

		_socialTab = new VBoxContainer();
		_socialTab.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		_socialTab.Visible = false;
		tabContent.AddChild(_socialTab);

		BuildNoGuildUI();
		BuildInGuildUI();

		RefreshSocialTab();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("menu_toggle"))
		{
			if (_isOpen)
				CloseMenu();
			else
				OpenMenu(0);
			GetViewport().SetInputAsHandled();
		}
		else if (@event.IsActionPressed("social_toggle"))
		{
			if (_isOpen && _tabBar.CurrentTab == 1)
				CloseMenu();
			else
				OpenMenu(1);
			GetViewport().SetInputAsHandled();
		}
	}

	private void OpenMenu(int tab)
	{
		_isOpen = true;
		Visible = true;
		_tabBar.CurrentTab = tab;
		ShowTab(tab);
	}

	private void CloseMenu()
	{
		_isOpen = false;
		Visible = false;
	}

	private void OnTabChanged(long tab)
	{
		ShowTab((int)tab);
	}

	private void ShowTab(int tab)
	{
		_generalTab.Visible = tab == 0;
		_socialTab.Visible = tab == 1;
		if (tab == 1)
			RefreshSocialTab();
	}

	private void BuildNoGuildUI()
	{
		_noGuildContainer = new VBoxContainer();
		_noGuildContainer.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;

		var createHeader = new Label();
		createHeader.Text = "Create a Guild";
		createHeader.AddThemeFontSizeOverride("font_size", 28);
		createHeader.HorizontalAlignment = HorizontalAlignment.Center;
		_noGuildContainer.AddChild(createHeader);

		var createRow = new HBoxContainer();
		_guildNameInput = new LineEdit();
		_guildNameInput.PlaceholderText = "Guild name...";
		_guildNameInput.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		createRow.AddChild(_guildNameInput);

		_createGuildButton = new Button();
		_createGuildButton.Text = "Create";
		_createGuildButton.Pressed += OnCreateGuildPressed;
		createRow.AddChild(_createGuildButton);
		_noGuildContainer.AddChild(createRow);

		_noGuildContainer.AddChild(new HSeparator());

		var invitesHeader = new Label();
		invitesHeader.Text = "Pending Invites";
		invitesHeader.AddThemeFontSizeOverride("font_size", 24);
		invitesHeader.HorizontalAlignment = HorizontalAlignment.Center;
		_noGuildContainer.AddChild(invitesHeader);

		var invitesScroll = new ScrollContainer();
		invitesScroll.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		_invitesList = new VBoxContainer();
		_invitesList.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		invitesScroll.AddChild(_invitesList);
		_noGuildContainer.AddChild(invitesScroll);

		_socialTab.AddChild(_noGuildContainer);
	}

	private void BuildInGuildUI()
	{
		_inGuildContainer = new VBoxContainer();
		_inGuildContainer.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;

		_guildNameLabel = new Label();
		_guildNameLabel.AddThemeFontSizeOverride("font_size", 32);
		_guildNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_inGuildContainer.AddChild(_guildNameLabel);

		_inGuildContainer.AddChild(new HSeparator());

		// Session buttons
		var sessionRow = new HBoxContainer();
		sessionRow.Alignment = BoxContainer.AlignmentMode.Center;
		_enterSessionButton = new Button();
		_enterSessionButton.Text = "Enter Guild Hall";
		_enterSessionButton.Pressed += OnEnterSessionPressed;
		sessionRow.AddChild(_enterSessionButton);

		_leaveSessionButton = new Button();
		_leaveSessionButton.Text = "Leave Guild Hall";
		_leaveSessionButton.Pressed += OnLeaveSessionPressed;
		sessionRow.AddChild(_leaveSessionButton);
		_inGuildContainer.AddChild(sessionRow);

		_inGuildContainer.AddChild(new HSeparator());

		// Members
		var membersHeader = new Label();
		membersHeader.Text = "Members";
		membersHeader.AddThemeFontSizeOverride("font_size", 24);
		membersHeader.HorizontalAlignment = HorizontalAlignment.Center;
		_inGuildContainer.AddChild(membersHeader);

		var membersScroll = new ScrollContainer();
		membersScroll.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		membersScroll.CustomMinimumSize = new Godot.Vector2(0, 120);
		_membersList = new VBoxContainer();
		_membersList.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		membersScroll.AddChild(_membersList);
		_inGuildContainer.AddChild(membersScroll);

		// Invite row
		var inviteRow = new HBoxContainer();
		_invitePlayerInput = new LineEdit();
		_invitePlayerInput.PlaceholderText = "Player name to invite...";
		_invitePlayerInput.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		inviteRow.AddChild(_invitePlayerInput);
		_inviteButton = new Button();
		_inviteButton.Text = "Invite";
		_inviteButton.Pressed += OnInvitePressed;
		inviteRow.AddChild(_inviteButton);
		_inGuildContainer.AddChild(inviteRow);

		_inGuildContainer.AddChild(new HSeparator());

		// Guild resources
		var resourcesHeader = new Label();
		resourcesHeader.Text = "Guild Treasury";
		resourcesHeader.AddThemeFontSizeOverride("font_size", 24);
		resourcesHeader.HorizontalAlignment = HorizontalAlignment.Center;
		_inGuildContainer.AddChild(resourcesHeader);

		var resourcesScroll = new ScrollContainer();
		resourcesScroll.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		resourcesScroll.CustomMinimumSize = new Godot.Vector2(0, 80);
		_guildResourcesList = new VBoxContainer();
		_guildResourcesList.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		resourcesScroll.AddChild(_guildResourcesList);
		_inGuildContainer.AddChild(resourcesScroll);

		_inGuildContainer.AddChild(new HSeparator());

		// Action buttons
		var actionsRow = new HBoxContainer();
		actionsRow.Alignment = BoxContainer.AlignmentMode.Center;
		_leaveGuildButton = new Button();
		_leaveGuildButton.Text = "Leave Guild";
		_leaveGuildButton.Pressed += OnLeaveGuildPressed;
		actionsRow.AddChild(_leaveGuildButton);

		_disbandGuildButton = new Button();
		_disbandGuildButton.Text = "Disband Guild";
		_disbandGuildButton.Pressed += OnDisbandGuildPressed;
		actionsRow.AddChild(_disbandGuildButton);
		_inGuildContainer.AddChild(actionsRow);

		_socialTab.AddChild(_inGuildContainer);
	}

	private void RefreshSocialTab()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		var membership = conn.Db.GuildMember.PlayerId.Find(localId);

		if (membership is null)
		{
			_noGuildContainer.Visible = true;
			_inGuildContainer.Visible = false;
			RefreshInvites();
		}
		else
		{
			_noGuildContainer.Visible = false;
			_inGuildContainer.Visible = true;
			RefreshGuildView(membership);
		}
	}

	private void RefreshInvites()
	{
		foreach (var child in _invitesList.GetChildren())
			child.QueueFree();

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		foreach (var invite in conn.Db.GuildInvite.InviteeId.Filter(localId))
		{
			var guild = conn.Db.Guild.Id.Find(invite.GuildId);
			var guildName = guild?.Name ?? "Unknown Guild";

			var inviter = conn.Db.Player.Identity.Find(invite.InviterId);
			var inviterName = inviter?.DisplayName ?? "Unknown";

			var row = new HBoxContainer();
			var label = new Label();
			label.Text = $"{guildName} (from {inviterName})";
			label.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
			row.AddChild(label);

			var acceptBtn = new Button();
			acceptBtn.Text = "Join";
			var capturedInviteId = invite.Id;
			acceptBtn.Pressed += () =>
			{
				conn.Reducers.JoinGuild(capturedInviteId);
				CallDeferred(nameof(DeferredRefreshSocial));
			};
			row.AddChild(acceptBtn);
			_invitesList.AddChild(row);
		}

		if (_invitesList.GetChildCount() == 0)
		{
			var noInvites = new Label();
			noInvites.Text = "No pending invites";
			noInvites.HorizontalAlignment = HorizontalAlignment.Center;
			noInvites.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
			_invitesList.AddChild(noInvites);
		}
	}

	private void RefreshGuildView(GuildMember membership)
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		var guild = conn.Db.Guild.Id.Find(membership.GuildId);
		_guildNameLabel.Text = guild?.Name ?? "Unknown Guild";

		bool isFounder = guild?.FounderId == localId;
		_disbandGuildButton.Visible = isFounder;

		_enterSessionButton.Visible = !membership.InSession;
		_leaveSessionButton.Visible = membership.InSession;

		// Members list
		foreach (var child in _membersList.GetChildren())
			child.QueueFree();

		foreach (var member in conn.Db.GuildMember.GuildId.Filter(membership.GuildId))
		{
			var player = conn.Db.Player.Identity.Find(member.PlayerId);
			var row = new HBoxContainer();

			var nameLabel = new Label();
			nameLabel.Text = player?.DisplayName ?? "Unknown";
			nameLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
			row.AddChild(nameLabel);

			var statusLabel = new Label();
			if (player?.Online == true)
			{
				statusLabel.Text = member.InSession ? "In Hall" : "Online";
				statusLabel.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.3f));
			}
			else
			{
				statusLabel.Text = "Offline";
				statusLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
			}
			row.AddChild(statusLabel);
			_membersList.AddChild(row);
		}

		// Guild resources
		foreach (var child in _guildResourcesList.GetChildren())
			child.QueueFree();

		foreach (var resource in conn.Db.GuildResourceTracker.GuildId.Filter(membership.GuildId))
		{
			var row = new HBoxContainer();
			var nameLabel = new Label();
			nameLabel.Text = resource.Type.ToString();
			nameLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
			row.AddChild(nameLabel);

			var amountLabel = new Label();
			amountLabel.Text = resource.Amount.ToString();
			row.AddChild(amountLabel);
			_guildResourcesList.AddChild(row);
		}

		if (_guildResourcesList.GetChildCount() == 0)
		{
			var noResources = new Label();
			noResources.Text = "No guild resources yet";
			noResources.HorizontalAlignment = HorizontalAlignment.Center;
			noResources.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
			_guildResourcesList.AddChild(noResources);
		}
	}

	private void DeferredRefreshSocial()
	{
		RefreshSocialTab();
	}

	private void OnCreateGuildPressed()
	{
		var name = _guildNameInput.Text.Trim();
		if (string.IsNullOrEmpty(name)) return;

		SpacetimeNetworkManager.Instance.Conn.Reducers.CreateGuild(name);
		_guildNameInput.Text = "";
		CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnInvitePressed()
	{
		var playerName = _invitePlayerInput.Text.Trim();
		if (string.IsNullOrEmpty(playerName)) return;

		var conn = SpacetimeNetworkManager.Instance.Conn;
		SpacetimeDB.Types.Player targetPlayer = null;
		foreach (var p in conn.Db.Player.Iter())
		{
			if (p.DisplayName == playerName)
			{
				targetPlayer = p;
				break;
			}
		}

		if (targetPlayer is null)
		{
			GD.PrintErr($"Player '{playerName}' not found");
			return;
		}

		conn.Reducers.InviteToGuild(targetPlayer.Identity);
		_invitePlayerInput.Text = "";
	}

	private void OnLeaveGuildPressed()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var membership = conn.Db.GuildMember.PlayerId.Find(SpacetimeNetworkManager.Instance.LocalIdentity);
		if (membership?.InSession == true)
		{
			conn.Reducers.LeaveGuildSession();
			EmitSignal(SignalName.GuildSessionChanged, false);
		}
		conn.Reducers.LeaveGuild();
		CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnDisbandGuildPressed()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var membership = conn.Db.GuildMember.PlayerId.Find(SpacetimeNetworkManager.Instance.LocalIdentity);
		if (membership?.InSession == true)
		{
			EmitSignal(SignalName.GuildSessionChanged, false);
		}
		conn.Reducers.DisbandGuild();
		CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnEnterSessionPressed()
	{
		SpacetimeNetworkManager.Instance.Conn.Reducers.EnterGuildSession();
		EmitSignal(SignalName.GuildSessionChanged, true);
		CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnLeaveSessionPressed()
	{
		SpacetimeNetworkManager.Instance.Conn.Reducers.LeaveGuildSession();
		EmitSignal(SignalName.GuildSessionChanged, false);
		CallDeferred(nameof(DeferredRefreshSocial));
	}
}
