using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;

public partial class PartyPanel : PanelContainer
{
	private static readonly Color GreenOnline = new(0.3f, 1f, 0.3f);
	private static readonly Color MutedGray = new(0.6f, 0.6f, 0.6f);
	private static readonly Color LeaderGold = new(0.9f, 0.85f, 0.4f);

	private VBoxContainer _root;
	private VBoxContainer _noPartyContainer;
	private VBoxContainer _inPartyContainer;
	private Label _headerLabel;
	private VBoxContainer _membersList;
	private VBoxContainer _invitesList;
	private Button _createPartyButton;
	private Button _leaveButton;
	private Button _multiplayerActivitiesButton;
	private PopupMenu _activitiesMenu;

	public override void _Ready()
	{
		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0.12f, 0.12f, 0.15f, 0.9f);
		panelStyle.CornerRadiusTopLeft = 6;
		panelStyle.CornerRadiusTopRight = 6;
		panelStyle.CornerRadiusBottomLeft = 6;
		panelStyle.CornerRadiusBottomRight = 6;
		panelStyle.ContentMarginLeft = 10;
		panelStyle.ContentMarginTop = 8;
		panelStyle.ContentMarginRight = 10;
		panelStyle.ContentMarginBottom = 8;
		AddThemeStyleboxOverride("panel", panelStyle);

		SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;

		_root = new VBoxContainer();
		_root.AddThemeConstantOverride("separation", 6);
		AddChild(_root);

		BuildNoPartyUI();
		BuildInPartyUI();

		var conn = SpacetimeNetworkManager.Instance.Conn;
		conn.Db.Party.OnInsert += OnPartyInsert;
		conn.Db.Party.OnUpdate += OnPartyUpdate;
		conn.Db.Party.OnDelete += OnPartyDelete;
		conn.Db.PartyMember.OnInsert += OnPartyMemberInsert;
		conn.Db.PartyMember.OnDelete += OnPartyMemberDelete;
		conn.Db.PartyInvite.OnInsert += OnPartyInviteInsert;
		conn.Db.PartyInvite.OnDelete += OnPartyInviteDelete;
		conn.Db.Player.OnUpdate += OnPlayerUpdate;

		RefreshAll();
	}

	// ── Build UI ────────────────────────────────────────────────

	private void BuildNoPartyUI()
	{
		_noPartyContainer = new VBoxContainer();
		_noPartyContainer.AddThemeConstantOverride("separation", 6);

		var headerRow = new HBoxContainer();
		var title = new Label();
		title.Text = "Party";
		title.AddThemeFontSizeOverride("font_size", 16);
		title.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
		headerRow.AddChild(title);
		_noPartyContainer.AddChild(headerRow);

		_noPartyContainer.AddChild(new HSeparator());

		_createPartyButton = new Button();
		_createPartyButton.Text = "Create Party";
		_createPartyButton.CustomMinimumSize = new Vector2(0, 30);
		_createPartyButton.Pressed += OnCreatePartyPressed;
		_noPartyContainer.AddChild(_createPartyButton);

		_invitesList = new VBoxContainer();
		_invitesList.AddThemeConstantOverride("separation", 4);
		_noPartyContainer.AddChild(_invitesList);

		_root.AddChild(_noPartyContainer);
	}

	private void BuildInPartyUI()
	{
		_inPartyContainer = new VBoxContainer();
		_inPartyContainer.AddThemeConstantOverride("separation", 4);

		var headerRow = new HBoxContainer();
		_headerLabel = new Label();
		_headerLabel.Text = "Party (1/4)";
		_headerLabel.AddThemeFontSizeOverride("font_size", 16);
		_headerLabel.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
		headerRow.AddChild(_headerLabel);

		_leaveButton = new Button();
		_leaveButton.Text = "Leave";
		_leaveButton.CustomMinimumSize = new Vector2(70, 26);
		_leaveButton.Pressed += OnLeavePressed;
		headerRow.AddChild(_leaveButton);
		_inPartyContainer.AddChild(headerRow);

		_inPartyContainer.AddChild(new HSeparator());

		_multiplayerActivitiesButton = new Button();
		_multiplayerActivitiesButton.Text = "Multiplayer Activities";
		_multiplayerActivitiesButton.CustomMinimumSize = new Vector2(0, 30);
		_multiplayerActivitiesButton.Visible = false;
		_multiplayerActivitiesButton.Pressed += OnMultiplayerActivitiesPressed;
		_inPartyContainer.AddChild(_multiplayerActivitiesButton);

		_activitiesMenu = new PopupMenu();
		_activitiesMenu.AddItem("Adventure", 0);
		_activitiesMenu.AddItem("Risky Business", 1);
		_activitiesMenu.IdPressed += OnActivityMenuItemSelected;
		AddChild(_activitiesMenu);

		_membersList = new VBoxContainer();
		_membersList.AddThemeConstantOverride("separation", 2);
		_inPartyContainer.AddChild(_membersList);

		_root.AddChild(_inPartyContainer);
	}

	// ── Refresh ─────────────────────────────────────────────────

	private void RefreshAll()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		var membership = conn.Db.PartyMember.PlayerId.Find(localId);

		if (membership is null)
		{
			_noPartyContainer.Visible = true;
			_inPartyContainer.Visible = false;
			RefreshInvites();
		}
		else
		{
			_noPartyContainer.Visible = false;
			_inPartyContainer.Visible = true;
			RefreshPartyView(membership);
		}
	}

	private void RefreshInvites()
	{
		foreach (var child in _invitesList.GetChildren())
			child.QueueFree();

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		foreach (var invite in conn.Db.PartyInvite.InviteeId.Filter(localId))
		{
			var inviter = conn.Db.Player.Identity.Find(invite.InviterId);
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 4);

			var label = new Label();
			label.Text = $"{inviter?.DisplayName ?? "Unknown"}'s party";
			label.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
			label.AddThemeFontSizeOverride("font_size", 13);
			row.AddChild(label);

			var capturedId = invite.Id;

			var acceptBtn = new Button();
			acceptBtn.Text = "Join";
			acceptBtn.CustomMinimumSize = new Vector2(50, 24);
			acceptBtn.Pressed += () =>
			{
				conn.Reducers.AcceptPartyInvite(capturedId);
				CallDeferred(nameof(DeferredRefresh));
			};
			row.AddChild(acceptBtn);

			var declineBtn = new Button();
			declineBtn.Text = "X";
			declineBtn.CustomMinimumSize = new Vector2(30, 24);
			declineBtn.Pressed += () =>
			{
				conn.Reducers.DeclinePartyInvite(capturedId);
				CallDeferred(nameof(DeferredRefresh));
			};
			row.AddChild(declineBtn);

			_invitesList.AddChild(row);
		}
	}

	private void RefreshPartyView(PartyMember localMembership)
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		var party = conn.Db.Party.Id.Find(localMembership.PartyId);
		if (party is null) return;

		var members = new List<PartyMember>();
		foreach (var m in conn.Db.PartyMember.PartyId.Filter(localMembership.PartyId))
			members.Add(m);

		bool isLeader = party.LeaderId == localId;
		_headerLabel.Text = $"Party ({members.Count}/4)";
		_leaveButton.Text = isLeader ? "Disband" : "Leave";
		_multiplayerActivitiesButton.Visible = isLeader;

		foreach (var child in _membersList.GetChildren())
			child.QueueFree();

		foreach (var member in members)
		{
			var player = conn.Db.Player.Identity.Find(member.PlayerId);
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 4);

			var statusDot = new Label();
			statusDot.Text = "\u25cf ";
			statusDot.AddThemeFontSizeOverride("font_size", 12);
			statusDot.AddThemeColorOverride("font_color", player?.Online == true ? GreenOnline : MutedGray);
			row.AddChild(statusDot);

			bool memberIsLeader = party.LeaderId == member.PlayerId;

			if (memberIsLeader)
			{
				var crownLabel = new Label();
				crownLabel.Text = "\u2605 ";
				crownLabel.AddThemeFontSizeOverride("font_size", 12);
				crownLabel.AddThemeColorOverride("font_color", LeaderGold);
				row.AddChild(crownLabel);
			}

			var nameLabel = new Label();
			nameLabel.Text = player?.DisplayName ?? "Unknown";
			nameLabel.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
			nameLabel.AddThemeFontSizeOverride("font_size", 13);
			if (memberIsLeader)
				nameLabel.AddThemeColorOverride("font_color", LeaderGold);
			row.AddChild(nameLabel);

			if (isLeader && member.PlayerId != localId)
			{
				var capturedId = member.PlayerId;
				var kickBtn = new Button();
				kickBtn.Text = "Kick";
				kickBtn.CustomMinimumSize = new Vector2(50, 22);
				kickBtn.Pressed += () =>
				{
					conn.Reducers.KickFromParty(capturedId);
					CallDeferred(nameof(DeferredRefresh));
				};
				row.AddChild(kickBtn);
			}

			_membersList.AddChild(row);
		}
	}

	// ── Table Callbacks ─────────────────────────────────────────

	private void OnPartyInsert(EventContext ctx, Party party) =>
		CallDeferred(nameof(DeferredRefresh));

	private void OnPartyUpdate(EventContext ctx, Party oldParty, Party newParty) =>
		CallDeferred(nameof(DeferredRefresh));

	private void OnPartyDelete(EventContext ctx, Party party) =>
		CallDeferred(nameof(DeferredRefresh));

	private void OnPartyMemberInsert(EventContext ctx, PartyMember member) =>
		CallDeferred(nameof(DeferredRefresh));

	private void OnPartyMemberDelete(EventContext ctx, PartyMember member) =>
		CallDeferred(nameof(DeferredRefresh));

	private void OnPartyInviteInsert(EventContext ctx, PartyInvite invite)
	{
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		if (invite.InviteeId == localId)
			CallDeferred(nameof(DeferredRefresh));
	}

	private void OnPartyInviteDelete(EventContext ctx, PartyInvite invite)
	{
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		if (invite.InviteeId == localId)
			CallDeferred(nameof(DeferredRefresh));
	}

	private void OnPlayerUpdate(EventContext ctx, SpacetimeDB.Types.Player oldPlayer, SpacetimeDB.Types.Player newPlayer)
	{
		if (oldPlayer.Online != newPlayer.Online || oldPlayer.DisplayName != newPlayer.DisplayName)
			CallDeferred(nameof(DeferredRefresh));
	}

	private void DeferredRefresh()
	{
		RefreshAll();
	}

	// ── Button Handlers ─────────────────────────────────────────

	private void OnCreatePartyPressed()
	{
		SpacetimeNetworkManager.Instance.Conn.Reducers.CreateParty();
		CallDeferred(nameof(DeferredRefresh));
	}

	private void OnLeavePressed()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		var membership = conn.Db.PartyMember.PlayerId.Find(localId);
		if (membership is null) return;

		var party = conn.Db.Party.Id.Find(membership.PartyId);
		if (party is not null && party.LeaderId == localId)
			conn.Reducers.DisbandParty();
		else
			conn.Reducers.LeaveParty();

		CallDeferred(nameof(DeferredRefresh));
	}

	private void OnMultiplayerActivitiesPressed()
	{
		var buttonRect = _multiplayerActivitiesButton.GetGlobalRect();
		_activitiesMenu.Position = new Vector2I(
			(int)buttonRect.Position.X,
			(int)buttonRect.End.Y
		);
		_activitiesMenu.ResetSize();
		_activitiesMenu.Popup();
	}

	private void OnActivityMenuItemSelected(long id)
	{
		switch (id)
		{
			case 0:
				SpacetimeNetworkManager.Instance.Conn.Reducers.StartAdventure();
				break;
			case 1:
				SpacetimeNetworkManager.Instance.Conn.Reducers.StartRiskyBusiness();
				break;
		}
	}
}
