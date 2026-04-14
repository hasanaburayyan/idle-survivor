using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;

public partial class GuildSocialPanel : VBoxContainer
{
	private static readonly Color GoldAccent = new(0.9f, 0.85f, 0.4f);
	private static readonly Color OfficerBlue = new(0.4f, 0.6f, 1.0f);
	private static readonly Color MutedGray = new(0.6f, 0.6f, 0.6f);

	// No-guild UI
	private ScrollContainer _noGuildContainer;
	private LineEdit _guildNameInput;
	private Button _createGuildButton;
	private VBoxContainer _invitesList;
	private LineEdit _guildSearchInput;
	private VBoxContainer _guildSearchList;
	private PanelContainer _pendingRequestPanel;
	private Label _pendingRequestLabel;
	private Button _cancelRequestButton;

	// In-guild UI
	private VBoxContainer _inGuildContainer;
	private Label _guildNameLabel;
	private VBoxContainer _membersList;
	private VBoxContainer _guildResourcesList;
	private HBoxContainer _inviteRow;
	private LineEdit _invitePlayerInput;
	private Button _inviteButton;
	private Button _leaveGuildButton;
	private Button _disbandGuildButton;
	private Button _enterSessionButton;
	private Button _leaveSessionButton;

	// Admin panel
	private PanelContainer _adminPanelContainer;
	private VBoxContainer _joinRequestsList;

	[Signal]
	public delegate void GuildSessionChangedEventHandler(bool inSession);

	private static StyleBoxFlat CreateSubPanelStyle()
	{
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.16f, 0.16f, 0.19f, 0.9f);
		style.CornerRadiusTopLeft = 6;
		style.CornerRadiusTopRight = 6;
		style.CornerRadiusBottomLeft = 6;
		style.CornerRadiusBottomRight = 6;
		style.ContentMarginLeft = 12;
		style.ContentMarginTop = 10;
		style.ContentMarginRight = 12;
		style.ContentMarginBottom = 10;
		return style;
	}

	private static Label CreateSectionHeader(string text)
	{
		var label = new Label();
		label.Text = text;
		label.AddThemeFontSizeOverride("font_size", 20);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		return label;
	}

	private static PanelContainer CreateSectionPanel()
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", CreateSubPanelStyle());
		panel.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		return panel;
	}

	private static Button CreateSmallButton(string text, Godot.Vector2? minSize = null)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = minSize ?? new Godot.Vector2(80, 28);
		return btn;
	}

	public override void _Ready()
	{
		SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
		SizeFlagsVertical = SizeFlags.Fill | SizeFlags.Expand;
		AddThemeConstantOverride("separation", 10);

		BuildNoGuildUI();
		BuildInGuildUI();

		var conn = SpacetimeNetworkManager.Instance.Conn;
		conn.Db.GuildResourceTracker.OnInsert += OnGuildResourceTrackerInsert;
		conn.Db.GuildResourceTracker.OnUpdate += OnGuildResourceTrackerUpdate;
		conn.Db.GuildMember.OnInsert += OnGuildMemberInsert;
		conn.Db.GuildMember.OnUpdate += OnGuildMemberUpdate;
		conn.Db.GuildMember.OnDelete += OnGuildMemberDelete;
		conn.Db.GuildInvite.OnInsert += OnGuildInviteInsert;
		conn.Db.GuildInvite.OnDelete += OnGuildInviteDelete;
		conn.Db.Guild.OnInsert += OnGuildInsert;
		conn.Db.Guild.OnUpdate += OnGuildUpdate;
		conn.Db.Guild.OnDelete += OnGuildDelete;
		conn.Db.Player.OnUpdate += OnPlayerUpdate;
		conn.Db.GuildJoinRequest.OnInsert += OnGuildJoinRequestInsert;
		conn.Db.GuildJoinRequest.OnDelete += OnGuildJoinRequestDelete;

		RefreshSocialTab();
	}

	/// <summary>Call when the Social popup is shown.</summary>
	public void RefreshOnOpen()
	{
		RefreshSocialTab();
	}

	// ── No-Guild UI ─────────────────────────────────────────────

	private void BuildNoGuildUI()
	{
		_noGuildContainer = new ScrollContainer();
		_noGuildContainer.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		_noGuildContainer.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;

		var noGuildVBox = new VBoxContainer();
		noGuildVBox.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		noGuildVBox.AddThemeConstantOverride("separation", 10);

		// Create guild section
		var createPanel = CreateSectionPanel();
		var createVBox = new VBoxContainer();
		createVBox.AddThemeConstantOverride("separation", 8);
		createVBox.AddChild(CreateSectionHeader("Create a Guild"));
		createVBox.AddChild(new HSeparator());

		var createRow = new HBoxContainer();
		_guildNameInput = new LineEdit();
		_guildNameInput.PlaceholderText = "Guild name...";
		_guildNameInput.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		createRow.AddChild(_guildNameInput);

		_createGuildButton = new Button();
		_createGuildButton.Text = "Create";
		_createGuildButton.CustomMinimumSize = new Godot.Vector2(140, 36);
		_createGuildButton.Pressed += OnCreateGuildPressed;
		createRow.AddChild(_createGuildButton);
		createVBox.AddChild(createRow);
		createPanel.AddChild(createVBox);
		noGuildVBox.AddChild(createPanel);

		// Guild search section
		var searchPanel = CreateSectionPanel();
		var searchVBox = new VBoxContainer();
		searchVBox.AddThemeConstantOverride("separation", 6);
		searchVBox.AddChild(CreateSectionHeader("Find a Guild"));
		searchVBox.AddChild(new HSeparator());

		var searchRow = new HBoxContainer();
		_guildSearchInput = new LineEdit();
		_guildSearchInput.PlaceholderText = "Search guilds by name...";
		_guildSearchInput.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		_guildSearchInput.TextChanged += OnGuildSearchTextChanged;
		searchRow.AddChild(_guildSearchInput);

		var searchButton = new Button();
		searchButton.Text = "Search";
		searchButton.CustomMinimumSize = new Godot.Vector2(140, 36);
		searchButton.Pressed += () => RefreshGuildSearch();
		searchRow.AddChild(searchButton);
		searchVBox.AddChild(searchRow);

		// Pending request indicator
		_pendingRequestPanel = CreateSectionPanel();
		var pendingRow = new HBoxContainer();
		pendingRow.AddThemeConstantOverride("separation", 8);
		_pendingRequestLabel = new Label();
		_pendingRequestLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		_pendingRequestLabel.AddThemeColorOverride("font_color", GoldAccent);
		pendingRow.AddChild(_pendingRequestLabel);
		_cancelRequestButton = new Button();
		_cancelRequestButton.Text = "Cancel";
		_cancelRequestButton.CustomMinimumSize = new Godot.Vector2(80, 28);
		_cancelRequestButton.Pressed += OnCancelJoinRequestPressed;
		pendingRow.AddChild(_cancelRequestButton);
		_pendingRequestPanel.AddChild(pendingRow);
		_pendingRequestPanel.Visible = false;
		searchVBox.AddChild(_pendingRequestPanel);

		_guildSearchList = new VBoxContainer();
		_guildSearchList.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		_guildSearchList.AddThemeConstantOverride("separation", 4);
		searchVBox.AddChild(_guildSearchList);
		searchPanel.AddChild(searchVBox);
		noGuildVBox.AddChild(searchPanel);

		// Invites section
		var invitesPanel = CreateSectionPanel();
		var invitesVBox = new VBoxContainer();
		invitesVBox.AddThemeConstantOverride("separation", 6);
		invitesVBox.AddChild(CreateSectionHeader("Pending Invites"));
		invitesVBox.AddChild(new HSeparator());

		_invitesList = new VBoxContainer();
		_invitesList.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		_invitesList.AddThemeConstantOverride("separation", 6);
		invitesVBox.AddChild(_invitesList);
		invitesPanel.AddChild(invitesVBox);
		noGuildVBox.AddChild(invitesPanel);

		_noGuildContainer.AddChild(noGuildVBox);
		AddChild(_noGuildContainer);
	}

	// ── In-Guild UI ─────────────────────────────────────────────

	private void BuildInGuildUI()
	{
		_inGuildContainer = new VBoxContainer();
		_inGuildContainer.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		_inGuildContainer.AddThemeConstantOverride("separation", 10);

		_guildNameLabel = new Label();
		_guildNameLabel.AddThemeFontSizeOverride("font_size", 28);
		_guildNameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_guildNameLabel.AddThemeColorOverride("font_color", GoldAccent);
		_inGuildContainer.AddChild(_guildNameLabel);

		// Session buttons
		var sessionPanel = CreateSectionPanel();
		var sessionRow = new HBoxContainer();
		sessionRow.Alignment = BoxContainer.AlignmentMode.Center;
		sessionRow.AddThemeConstantOverride("separation", 12);
		_enterSessionButton = new Button();
		_enterSessionButton.Text = "Enter Guild Hall";
		_enterSessionButton.CustomMinimumSize = new Godot.Vector2(140, 36);
		_enterSessionButton.Disabled = true;
		_enterSessionButton.TooltipText = "Coming soon";
		_enterSessionButton.Pressed += OnEnterSessionPressed;
		sessionRow.AddChild(_enterSessionButton);

		_leaveSessionButton = new Button();
		_leaveSessionButton.Text = "Leave Guild Hall";
		_leaveSessionButton.CustomMinimumSize = new Godot.Vector2(140, 36);
		_leaveSessionButton.Disabled = true;
		_leaveSessionButton.TooltipText = "Coming soon";
		_leaveSessionButton.Pressed += OnLeaveSessionPressed;
		sessionRow.AddChild(_leaveSessionButton);
		sessionPanel.AddChild(sessionRow);
		_inGuildContainer.AddChild(sessionPanel);

		// Members section
		var membersPanel = CreateSectionPanel();
		var membersVBox = new VBoxContainer();
		membersVBox.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		membersVBox.AddThemeConstantOverride("separation", 6);
		membersVBox.AddChild(CreateSectionHeader("Members"));
		membersVBox.AddChild(new HSeparator());

		var membersScroll = new ScrollContainer();
		membersScroll.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		membersScroll.CustomMinimumSize = new Godot.Vector2(0, 100);
		_membersList = new VBoxContainer();
		_membersList.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		_membersList.AddThemeConstantOverride("separation", 4);
		membersScroll.AddChild(_membersList);
		membersVBox.AddChild(membersScroll);

		_inviteRow = new HBoxContainer();
		_invitePlayerInput = new LineEdit();
		_invitePlayerInput.PlaceholderText = "Player name to invite...";
		_invitePlayerInput.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		_inviteRow.AddChild(_invitePlayerInput);
		_inviteButton = new Button();
		_inviteButton.Text = "Invite";
		_inviteButton.CustomMinimumSize = new Godot.Vector2(140, 36);
		_inviteButton.Pressed += OnInvitePressed;
		_inviteRow.AddChild(_inviteButton);
		membersVBox.AddChild(_inviteRow);
		membersPanel.AddChild(membersVBox);
		_inGuildContainer.AddChild(membersPanel);

		// Admin panel (join requests)
		_adminPanelContainer = CreateSectionPanel();
		var adminVBox = new VBoxContainer();
		adminVBox.AddThemeConstantOverride("separation", 6);
		adminVBox.AddChild(CreateSectionHeader("Join Requests"));
		adminVBox.AddChild(new HSeparator());

		var requestsScroll = new ScrollContainer();
		requestsScroll.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		requestsScroll.CustomMinimumSize = new Godot.Vector2(0, 60);
		_joinRequestsList = new VBoxContainer();
		_joinRequestsList.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		_joinRequestsList.AddThemeConstantOverride("separation", 4);
		requestsScroll.AddChild(_joinRequestsList);
		adminVBox.AddChild(requestsScroll);
		_adminPanelContainer.AddChild(adminVBox);
		_inGuildContainer.AddChild(_adminPanelContainer);

		// Treasury
		var treasuryPanel = CreateSectionPanel();
		var treasuryVBox = new VBoxContainer();
		treasuryVBox.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		treasuryVBox.AddThemeConstantOverride("separation", 6);
		treasuryVBox.AddChild(CreateSectionHeader("Guild Treasury"));
		treasuryVBox.AddChild(new HSeparator());

		var resourcesScroll = new ScrollContainer();
		resourcesScroll.SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		resourcesScroll.CustomMinimumSize = new Godot.Vector2(0, 60);
		_guildResourcesList = new VBoxContainer();
		_guildResourcesList.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
		_guildResourcesList.AddThemeConstantOverride("separation", 4);
		resourcesScroll.AddChild(_guildResourcesList);
		treasuryVBox.AddChild(resourcesScroll);
		treasuryPanel.AddChild(treasuryVBox);
		_inGuildContainer.AddChild(treasuryPanel);

		// Actions row
		var actionsRow = new HBoxContainer();
		actionsRow.Alignment = BoxContainer.AlignmentMode.Center;
		actionsRow.AddThemeConstantOverride("separation", 12);
		_leaveGuildButton = new Button();
		_leaveGuildButton.Text = "Leave Guild";
		_leaveGuildButton.CustomMinimumSize = new Godot.Vector2(140, 36);
		_leaveGuildButton.Pressed += OnLeaveGuildPressed;
		actionsRow.AddChild(_leaveGuildButton);

		_disbandGuildButton = new Button();
		_disbandGuildButton.Text = "Disband Guild";
		_disbandGuildButton.CustomMinimumSize = new Godot.Vector2(140, 36);
		_disbandGuildButton.Pressed += OnDisbandGuildPressed;
		actionsRow.AddChild(_disbandGuildButton);
		_inGuildContainer.AddChild(actionsRow);

		AddChild(_inGuildContainer);
	}

	// ── Refresh Logic ───────────────────────────────────────────

	private void RefreshSocialTab()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		var membership = conn.Db.GuildMember.PlayerId.Find(localId);

		if (membership is null)
		{
			_noGuildContainer.Visible = true;
			_inGuildContainer.Visible = false;
			RefreshNoGuildView();
		}
		else
		{
			_noGuildContainer.Visible = false;
			_inGuildContainer.Visible = true;
			RefreshGuildView(membership);
		}
	}

	private void RefreshNoGuildView()
	{
		RefreshInvites();
		RefreshGuildSearch();
		RefreshPendingRequest();
	}

	private void RefreshPendingRequest()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		var pending = conn.Db.GuildJoinRequest.RequesterId.Find(localId);

		if (pending is not null)
		{
			var guild = conn.Db.Guild.Id.Find(pending.GuildId);
			_pendingRequestLabel.Text = $"Request pending: {guild?.Name ?? "Unknown Guild"}";
			_pendingRequestPanel.Visible = true;
		}
		else
		{
			_pendingRequestPanel.Visible = false;
		}
	}

	private void RefreshGuildSearch()
	{
		foreach (var child in _guildSearchList.GetChildren())
			child.QueueFree();

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		var searchText = _guildSearchInput?.Text?.Trim() ?? "";

		var pendingRequest = conn.Db.GuildJoinRequest.RequesterId.Find(localId);
		bool hasPending = pendingRequest is not null;

		var guilds = conn.Db.Guild.Iter()
			.Where(g => searchText.Length == 0 || g.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
			.Take(20);

		foreach (var guild in guilds)
		{
			var memberCount = conn.Db.GuildMember.GuildId.Filter(guild.Id).Count();

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);

			var nameLabel = new Label();
			nameLabel.Text = guild.Name;
			nameLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
			row.AddChild(nameLabel);

			var countLabel = new Label();
			countLabel.Text = $"{memberCount} member{(memberCount != 1 ? "s" : "")}";
			countLabel.AddThemeColorOverride("font_color", MutedGray);
			row.AddChild(countLabel);

			if (hasPending && pendingRequest!.GuildId == guild.Id)
			{
				var pendingLabel = new Label();
				pendingLabel.Text = "Requested";
				pendingLabel.AddThemeColorOverride("font_color", GoldAccent);
				row.AddChild(pendingLabel);
			}
			else if (!hasPending)
			{
				var requestBtn = CreateSmallButton("Request to Join");
				var capturedGuildId = guild.Id;
				requestBtn.Pressed += () =>
				{
					conn.Reducers.RequestJoinGuild(capturedGuildId);
					CallDeferred(nameof(DeferredRefreshSocial));
				};
				row.AddChild(requestBtn);
			}

			_guildSearchList.AddChild(row);
		}

		if (_guildSearchList.GetChildCount() == 0)
		{
			var noResults = new Label();
			noResults.Text = searchText.Length > 0 ? "No guilds found" : "No guilds exist yet";
			noResults.HorizontalAlignment = HorizontalAlignment.Center;
			noResults.AddThemeColorOverride("font_color", MutedGray);
			_guildSearchList.AddChild(noResults);
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
			acceptBtn.CustomMinimumSize = new Godot.Vector2(80, 32);
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
			noInvites.AddThemeColorOverride("font_color", MutedGray);
			_invitesList.AddChild(noInvites);
		}
	}

	private void RefreshGuildView(GuildMember membership)
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		var guild = conn.Db.Guild.Id.Find(membership.GuildId);
		_guildNameLabel.Text = guild?.Name ?? "Unknown Guild";

		bool isOwner = membership.Role == GuildRole.Owner;
		bool isOfficerOrAbove = membership.Role == GuildRole.Officer || isOwner;

		_disbandGuildButton.Visible = isOwner;
		_leaveGuildButton.Visible = !isOwner;
		_inviteRow.Visible = isOfficerOrAbove;
		_adminPanelContainer.Visible = isOfficerOrAbove;

		var player = conn.Db.Player.Identity.Find(localId);
		bool inGuildHall = player?.Location == LocationType.GuildHall;
		_enterSessionButton.Visible = !inGuildHall;
		_leaveSessionButton.Visible = inGuildHall;

		RefreshMembersList(membership, isOfficerOrAbove);
		RefreshJoinRequests(membership, isOfficerOrAbove);
		RefreshTreasury(membership);
	}

	private void RefreshMembersList(GuildMember localMembership, bool isOfficerOrAbove)
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		foreach (var child in _membersList.GetChildren())
			child.QueueFree();

		foreach (var member in conn.Db.GuildMember.GuildId.Filter(localMembership.GuildId))
		{
			var player = conn.Db.Player.Identity.Find(member.PlayerId);
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 6);

			var nameLabel = new Label();
			nameLabel.Text = player?.DisplayName ?? "Unknown";
			nameLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
			row.AddChild(nameLabel);

			// Role badge
			if (member.Role != GuildRole.Member)
			{
				var roleLabel = new Label();
				roleLabel.Text = member.Role.ToString();
				roleLabel.AddThemeFontSizeOverride("font_size", 12);
				if (member.Role == GuildRole.Owner)
					roleLabel.AddThemeColorOverride("font_color", GoldAccent);
				else
					roleLabel.AddThemeColorOverride("font_color", OfficerBlue);
				row.AddChild(roleLabel);
			}

			// Online status
			var statusLabel = new Label();
			if (player?.Online == true)
			{
				var memberPlayer = conn.Db.Player.Identity.Find(member.PlayerId);
				statusLabel.Text = memberPlayer?.Location == LocationType.GuildHall ? "In Hall" : "Online";
				statusLabel.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.3f));
			}
			else
			{
				statusLabel.Text = "Offline";
				statusLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
			}
			row.AddChild(statusLabel);

			// Management buttons (only for other members, and only if caller is officer+)
			if (isOfficerOrAbove && member.PlayerId != localId && member.Role != GuildRole.Owner)
			{
				var capturedPlayerId = member.PlayerId;

				if (member.Role == GuildRole.Member)
				{
					var promoteBtn = CreateSmallButton("Promote");
					promoteBtn.Pressed += () =>
					{
						conn.Reducers.PromoteMember(capturedPlayerId);
						CallDeferred(nameof(DeferredRefreshSocial));
					};
					row.AddChild(promoteBtn);
				}
				else if (member.Role == GuildRole.Officer)
				{
					var demoteBtn = CreateSmallButton("Demote");
					demoteBtn.Pressed += () =>
					{
						conn.Reducers.DemoteMember(capturedPlayerId);
						CallDeferred(nameof(DeferredRefreshSocial));
					};
					row.AddChild(demoteBtn);
				}

				var kickBtn = CreateSmallButton("Kick");
				kickBtn.Pressed += () =>
				{
					conn.Reducers.KickMember(capturedPlayerId);
					CallDeferred(nameof(DeferredRefreshSocial));
				};
				row.AddChild(kickBtn);
			}

			_membersList.AddChild(row);
		}
	}

	private void RefreshJoinRequests(GuildMember localMembership, bool isOfficerOrAbove)
	{
		foreach (var child in _joinRequestsList.GetChildren())
			child.QueueFree();

		if (!isOfficerOrAbove) return;

		var conn = SpacetimeNetworkManager.Instance.Conn;

		foreach (var request in conn.Db.GuildJoinRequest.GuildId.Filter(localMembership.GuildId))
		{
			var requester = conn.Db.Player.Identity.Find(request.RequesterId);
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 6);

			var nameLabel = new Label();
			nameLabel.Text = requester?.DisplayName ?? "Unknown";
			nameLabel.SizeFlagsHorizontal = Control.SizeFlags.Fill | Control.SizeFlags.Expand;
			row.AddChild(nameLabel);

			var capturedRequestId = request.Id;

			var acceptBtn = CreateSmallButton("Accept");
			acceptBtn.Pressed += () =>
			{
				conn.Reducers.AcceptJoinRequest(capturedRequestId);
				CallDeferred(nameof(DeferredRefreshSocial));
			};
			row.AddChild(acceptBtn);

			var rejectBtn = CreateSmallButton("Reject");
			rejectBtn.Pressed += () =>
			{
				conn.Reducers.RejectJoinRequest(capturedRequestId);
				CallDeferred(nameof(DeferredRefreshSocial));
			};
			row.AddChild(rejectBtn);

			_joinRequestsList.AddChild(row);
		}

		if (_joinRequestsList.GetChildCount() == 0)
		{
			var noRequests = new Label();
			noRequests.Text = "No pending requests";
			noRequests.HorizontalAlignment = HorizontalAlignment.Center;
			noRequests.AddThemeColorOverride("font_color", MutedGray);
			_joinRequestsList.AddChild(noRequests);
		}
	}

	private void RefreshTreasury(GuildMember membership)
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;

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
			amountLabel.AddThemeColorOverride("font_color", GoldAccent);
			row.AddChild(amountLabel);
			_guildResourcesList.AddChild(row);
		}

		if (_guildResourcesList.GetChildCount() == 0)
		{
			var noResources = new Label();
			noResources.Text = "No guild resources yet";
			noResources.HorizontalAlignment = HorizontalAlignment.Center;
			noResources.AddThemeColorOverride("font_color", MutedGray);
			_guildResourcesList.AddChild(noResources);
		}
	}

	// ── Helpers ─────────────────────────────────────────────────

	private ulong? GetLocalGuildId()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		return conn.Db.GuildMember.PlayerId.Find(localId)?.GuildId;
	}

	// ── Table Callbacks ─────────────────────────────────────────

	private void OnGuildResourceTrackerInsert(EventContext ctx, GuildResourceTracker resource)
	{
		var guildId = GetLocalGuildId();
		if (guildId is not null && resource.GuildId == guildId)
			CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnGuildResourceTrackerUpdate(EventContext ctx, GuildResourceTracker oldResource, GuildResourceTracker newResource)
	{
		var guildId = GetLocalGuildId();
		if (guildId is not null && newResource.GuildId == guildId)
			CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnGuildMemberInsert(EventContext ctx, GuildMember member)
	{
		var guildId = GetLocalGuildId();
		if (guildId is not null && member.GuildId == guildId)
			CallDeferred(nameof(DeferredRefreshSocial));
		else if (member.PlayerId == SpacetimeNetworkManager.Instance.LocalIdentity)
			CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnGuildMemberUpdate(EventContext ctx, GuildMember oldMember, GuildMember newMember)
	{
		var guildId = GetLocalGuildId();
		if (guildId is not null && newMember.GuildId == guildId)
			CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnGuildMemberDelete(EventContext ctx, GuildMember member)
	{
		var guildId = GetLocalGuildId();
		if (guildId is not null && member.GuildId == guildId)
			CallDeferred(nameof(DeferredRefreshSocial));
		else if (member.PlayerId == SpacetimeNetworkManager.Instance.LocalIdentity)
			CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnGuildInviteInsert(EventContext ctx, GuildInvite invite)
	{
		if (invite.InviteeId == SpacetimeNetworkManager.Instance.LocalIdentity)
			CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnGuildInviteDelete(EventContext ctx, GuildInvite invite)
	{
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		if (invite.InviteeId == localId)
			CallDeferred(nameof(DeferredRefreshSocial));
		var guildId = GetLocalGuildId();
		if (guildId is not null && invite.GuildId == guildId)
			CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnGuildInsert(EventContext ctx, Guild guild)
	{
		if (GetLocalGuildId() is null)
			CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnGuildUpdate(EventContext ctx, Guild oldGuild, Guild newGuild)
	{
		var guildId = GetLocalGuildId();
		if (guildId is not null && newGuild.Id == guildId)
			CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnGuildDelete(EventContext ctx, Guild guild)
	{
		CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnPlayerUpdate(EventContext ctx, SpacetimeDB.Types.Player oldPlayer, SpacetimeDB.Types.Player newPlayer)
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var guildId = GetLocalGuildId();
		if (guildId is null) return;

		var memberEntry = conn.Db.GuildMember.PlayerId.Find(newPlayer.Identity);
		if (memberEntry is not null && memberEntry.GuildId == guildId)
			CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnGuildJoinRequestInsert(EventContext ctx, GuildJoinRequest request)
	{
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		if (request.RequesterId == localId)
		{
			CallDeferred(nameof(DeferredRefreshSocial));
			return;
		}
		var guildId = GetLocalGuildId();
		if (guildId is not null && request.GuildId == guildId)
			CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnGuildJoinRequestDelete(EventContext ctx, GuildJoinRequest request)
	{
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		if (request.RequesterId == localId)
		{
			CallDeferred(nameof(DeferredRefreshSocial));
			return;
		}
		var guildId = GetLocalGuildId();
		if (guildId is not null && request.GuildId == guildId)
			CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void DeferredRefreshSocial()
	{
		RefreshSocialTab();
	}

	// ── Button Handlers ─────────────────────────────────────────

	private void OnCreateGuildPressed()
	{
		var name = _guildNameInput.Text.Trim();
		if (string.IsNullOrEmpty(name)) return;

		SpacetimeNetworkManager.Instance.Conn.Reducers.CreateGuild(name);
		_guildNameInput.Text = "";
		CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnGuildSearchTextChanged(string newText)
	{
		RefreshGuildSearch();
	}

	private void OnCancelJoinRequestPressed()
	{
		SpacetimeNetworkManager.Instance.Conn.Reducers.CancelJoinRequest();
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
		var player = conn.Db.Player.Identity.Find(SpacetimeNetworkManager.Instance.LocalIdentity);
		if (player?.Location == LocationType.GuildHall)
		{
			conn.Reducers.Travel(LocationType.Shelter);
			EmitSignal(SignalName.GuildSessionChanged, false);
		}
		conn.Reducers.LeaveGuild();
		CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnDisbandGuildPressed()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var player = conn.Db.Player.Identity.Find(SpacetimeNetworkManager.Instance.LocalIdentity);
		if (player?.Location == LocationType.GuildHall)
		{
			EmitSignal(SignalName.GuildSessionChanged, false);
		}
		conn.Reducers.DisbandGuild();
		CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnEnterSessionPressed()
	{
		SpacetimeNetworkManager.Instance.Conn.Reducers.Travel(LocationType.GuildHall);
		EmitSignal(SignalName.GuildSessionChanged, true);
		CallDeferred(nameof(DeferredRefreshSocial));
	}

	private void OnLeaveSessionPressed()
	{
		SpacetimeNetworkManager.Instance.Conn.Reducers.Travel(LocationType.Shelter);
		EmitSignal(SignalName.GuildSessionChanged, false);
		CallDeferred(nameof(DeferredRefreshSocial));
	}
}
