using Godot;
using System;
using SpacetimeDB;
using SpacetimeDB.Types;

/// <summary>
/// Guild and invites UI (formerly GameMenu "Social" tab). Friends list is not implemented yet.
/// </summary>
public partial class SocialPanel : VBoxContainer
{
	private VBoxContainer _noGuildContainer;
	private LineEdit _guildNameInput;
	private Button _createGuildButton;
	private VBoxContainer _invitesList;

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

	public override void _Ready()
	{
		SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
		SizeFlagsVertical = SizeFlags.Fill | SizeFlags.Expand;

		var friendsHeader = new Label();
		friendsHeader.Text = "Friends";
		friendsHeader.AddThemeFontSizeOverride("font_size", 24);
		friendsHeader.HorizontalAlignment = HorizontalAlignment.Center;
		AddChild(friendsHeader);

		var friendsPlaceholder = new Label();
		friendsPlaceholder.Text = "Coming soon";
		friendsPlaceholder.HorizontalAlignment = HorizontalAlignment.Center;
		friendsPlaceholder.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.65f));
		AddChild(friendsPlaceholder);

		AddChild(new HSeparator());

		BuildNoGuildUI();
		BuildInGuildUI();

		RefreshSocialTab();
	}

	private void BuildNoGuildUI()
	{
		_noGuildContainer = new VBoxContainer();
		_noGuildContainer.SizeFlagsVertical = SizeFlags.Fill | SizeFlags.Expand;

		var createHeader = new Label();
		createHeader.Text = "Create a Guild";
		createHeader.AddThemeFontSizeOverride("font_size", 28);
		createHeader.HorizontalAlignment = HorizontalAlignment.Center;
		_noGuildContainer.AddChild(createHeader);

		var createRow = new HBoxContainer();
		_guildNameInput = new LineEdit();
		_guildNameInput.PlaceholderText = "Guild name...";
		_guildNameInput.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
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
		invitesScroll.SizeFlagsVertical = SizeFlags.Fill | SizeFlags.Expand;
		_invitesList = new VBoxContainer();
		_invitesList.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
		invitesScroll.AddChild(_invitesList);
		_noGuildContainer.AddChild(invitesScroll);

		AddChild(_noGuildContainer);
	}

	private void BuildInGuildUI()
	{
		_inGuildContainer = new VBoxContainer();
		_inGuildContainer.SizeFlagsVertical = SizeFlags.Fill | SizeFlags.Expand;

		_guildNameLabel = new Label();
		_guildNameLabel.AddThemeFontSizeOverride("font_size", 32);
		_guildNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_inGuildContainer.AddChild(_guildNameLabel);

		_inGuildContainer.AddChild(new HSeparator());

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

		var membersHeader = new Label();
		membersHeader.Text = "Members";
		membersHeader.AddThemeFontSizeOverride("font_size", 24);
		membersHeader.HorizontalAlignment = HorizontalAlignment.Center;
		_inGuildContainer.AddChild(membersHeader);

		var membersScroll = new ScrollContainer();
		membersScroll.SizeFlagsVertical = SizeFlags.Fill | SizeFlags.Expand;
		membersScroll.CustomMinimumSize = new Vector2(0, 120);
		_membersList = new VBoxContainer();
		_membersList.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
		membersScroll.AddChild(_membersList);
		_inGuildContainer.AddChild(membersScroll);

		var inviteRow = new HBoxContainer();
		_invitePlayerInput = new LineEdit();
		_invitePlayerInput.PlaceholderText = "Player name to invite...";
		_invitePlayerInput.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
		inviteRow.AddChild(_invitePlayerInput);
		_inviteButton = new Button();
		_inviteButton.Text = "Invite";
		_inviteButton.Pressed += OnInvitePressed;
		inviteRow.AddChild(_inviteButton);
		_inGuildContainer.AddChild(inviteRow);

		_inGuildContainer.AddChild(new HSeparator());

		var resourcesHeader = new Label();
		resourcesHeader.Text = "Guild Treasury";
		resourcesHeader.AddThemeFontSizeOverride("font_size", 24);
		resourcesHeader.HorizontalAlignment = HorizontalAlignment.Center;
		_inGuildContainer.AddChild(resourcesHeader);

		var resourcesScroll = new ScrollContainer();
		resourcesScroll.SizeFlagsVertical = SizeFlags.Fill | SizeFlags.Expand;
		resourcesScroll.CustomMinimumSize = new Vector2(0, 80);
		_guildResourcesList = new VBoxContainer();
		_guildResourcesList.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
		resourcesScroll.AddChild(_guildResourcesList);
		_inGuildContainer.AddChild(resourcesScroll);

		_inGuildContainer.AddChild(new HSeparator());

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

		AddChild(_inGuildContainer);
	}

	public void RefreshSocialTab()
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
			label.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
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

		foreach (var child in _membersList.GetChildren())
			child.QueueFree();

		foreach (var member in conn.Db.GuildMember.GuildId.Filter(membership.GuildId))
		{
			var player = conn.Db.Player.Identity.Find(member.PlayerId);
			var row = new HBoxContainer();

			var nameLabel = new Label();
			nameLabel.Text = player?.DisplayName ?? "Unknown";
			nameLabel.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
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

		foreach (var child in _guildResourcesList.GetChildren())
			child.QueueFree();

		foreach (var resource in conn.Db.GuildResourceTracker.GuildId.Filter(membership.GuildId))
		{
			var row = new HBoxContainer();
			var nameLabel = new Label();
			nameLabel.Text = resource.Type.ToString();
			nameLabel.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
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
