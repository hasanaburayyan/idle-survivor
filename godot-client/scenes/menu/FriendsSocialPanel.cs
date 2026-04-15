using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB;
using SpacetimeDB.Types;

public partial class FriendsSocialPanel : VBoxContainer
{
	private static readonly Color GreenOnline = new(0.3f, 1f, 0.3f);
	private static readonly Color MutedGray = new(0.6f, 0.6f, 0.6f);
	private static readonly Color AccentGold = new(0.9f, 0.85f, 0.4f);

	private LineEdit _addFriendInput;
	private Button _addFriendButton;
	private VBoxContainer _incomingRequestsList;
	private VBoxContainer _outgoingRequestsList;
	private VBoxContainer _friendsList;

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

	private static PanelContainer CreateSectionPanel()
	{
		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", CreateSubPanelStyle());
		panel.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
		return panel;
	}

	private static Label CreateSectionHeader(string text)
	{
		var label = new Label();
		label.Text = text;
		label.AddThemeFontSizeOverride("font_size", 16);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		return label;
	}

	private static Button CreateSmallButton(string text, Vector2? minSize = null)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = minSize ?? new Vector2(80, 28);
		return btn;
	}

	public override void _Ready()
	{
		SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
		AddThemeConstantOverride("separation", 8);

		BuildAddFriendSection();
		BuildIncomingRequestsSection();
		BuildOutgoingRequestsSection();
		BuildFriendsListSection();

		var conn = SpacetimeNetworkManager.Instance.Conn;
		conn.Db.FriendRequest.OnInsert += OnFriendRequestInsert;
		conn.Db.FriendRequest.OnDelete += OnFriendRequestDelete;
		conn.Db.Friendship.OnInsert += OnFriendshipInsert;
		conn.Db.Friendship.OnDelete += OnFriendshipDelete;
		conn.Db.Player.OnUpdate += OnPlayerUpdate;
		conn.Db.PartyMember.OnInsert += OnPartyMemberInsert;
		conn.Db.PartyMember.OnDelete += OnPartyMemberDelete;

		RefreshAll();
	}

	public void RefreshOnOpen()
	{
		RefreshAll();
	}

	// ── Build UI ────────────────────────────────────────────────

	private void BuildAddFriendSection()
	{
		var panel = CreateSectionPanel();
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);
		vbox.AddChild(CreateSectionHeader("Add Friend"));
		vbox.AddChild(new HSeparator());

		var row = new HBoxContainer();
		_addFriendInput = new LineEdit();
		_addFriendInput.PlaceholderText = "Player name...";
		_addFriendInput.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
		_addFriendInput.TextSubmitted += (_) => OnAddFriendPressed();
		row.AddChild(_addFriendInput);

		_addFriendButton = new Button();
		_addFriendButton.Text = "Send Request";
		_addFriendButton.CustomMinimumSize = new Vector2(140, 36);
		_addFriendButton.Pressed += OnAddFriendPressed;
		row.AddChild(_addFriendButton);

		vbox.AddChild(row);
		panel.AddChild(vbox);
		AddChild(panel);
	}

	private void BuildIncomingRequestsSection()
	{
		var panel = CreateSectionPanel();
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);
		vbox.AddChild(CreateSectionHeader("Incoming Requests"));
		vbox.AddChild(new HSeparator());

		_incomingRequestsList = new VBoxContainer();
		_incomingRequestsList.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
		_incomingRequestsList.AddThemeConstantOverride("separation", 4);
		vbox.AddChild(_incomingRequestsList);
		panel.AddChild(vbox);
		AddChild(panel);
	}

	private void BuildOutgoingRequestsSection()
	{
		var panel = CreateSectionPanel();
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);
		vbox.AddChild(CreateSectionHeader("Outgoing Requests"));
		vbox.AddChild(new HSeparator());

		_outgoingRequestsList = new VBoxContainer();
		_outgoingRequestsList.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
		_outgoingRequestsList.AddThemeConstantOverride("separation", 4);
		vbox.AddChild(_outgoingRequestsList);
		panel.AddChild(vbox);
		AddChild(panel);
	}

	private void BuildFriendsListSection()
	{
		var panel = CreateSectionPanel();
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);
		vbox.AddChild(CreateSectionHeader("Friends"));
		vbox.AddChild(new HSeparator());

		_friendsList = new VBoxContainer();
		_friendsList.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
		_friendsList.AddThemeConstantOverride("separation", 4);
		vbox.AddChild(_friendsList);
		panel.AddChild(vbox);
		AddChild(panel);
	}

	// ── Refresh Logic ───────────────────────────────────────────

	private void RefreshAll()
	{
		RefreshIncomingRequests();
		RefreshOutgoingRequests();
		RefreshFriendsList();
	}

	private void RefreshIncomingRequests()
	{
		foreach (var child in _incomingRequestsList.GetChildren())
			child.QueueFree();

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		foreach (var req in conn.Db.FriendRequest.ReceiverId.Filter(localId))
		{
			var sender = conn.Db.Player.Identity.Find(req.SenderId);
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 6);

			var nameLabel = new Label();
			nameLabel.Text = sender?.DisplayName ?? "Unknown";
			nameLabel.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
			row.AddChild(nameLabel);

			var capturedId = req.Id;

			var acceptBtn = CreateSmallButton("Accept");
			acceptBtn.Pressed += () =>
			{
				conn.Reducers.AcceptFriendRequest(capturedId);
				CallDeferred(nameof(DeferredRefresh));
			};
			row.AddChild(acceptBtn);

			var declineBtn = CreateSmallButton("Decline");
			declineBtn.Pressed += () =>
			{
				conn.Reducers.DeclineFriendRequest(capturedId);
				CallDeferred(nameof(DeferredRefresh));
			};
			row.AddChild(declineBtn);

			_incomingRequestsList.AddChild(row);
		}

		if (_incomingRequestsList.GetChildCount() == 0)
		{
			var empty = new Label();
			empty.Text = "No incoming requests";
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			empty.AddThemeColorOverride("font_color", MutedGray);
			_incomingRequestsList.AddChild(empty);
		}
	}

	private void RefreshOutgoingRequests()
	{
		foreach (var child in _outgoingRequestsList.GetChildren())
			child.QueueFree();

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		foreach (var req in conn.Db.FriendRequest.SenderId.Filter(localId))
		{
			var receiver = conn.Db.Player.Identity.Find(req.ReceiverId);
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 6);

			var nameLabel = new Label();
			nameLabel.Text = receiver?.DisplayName ?? "Unknown";
			nameLabel.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
			row.AddChild(nameLabel);

			var capturedId = req.Id;
			var cancelBtn = CreateSmallButton("Cancel");
			cancelBtn.Pressed += () =>
			{
				conn.Reducers.CancelFriendRequest(capturedId);
				CallDeferred(nameof(DeferredRefresh));
			};
			row.AddChild(cancelBtn);

			_outgoingRequestsList.AddChild(row);
		}

		if (_outgoingRequestsList.GetChildCount() == 0)
		{
			var empty = new Label();
			empty.Text = "No outgoing requests";
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			empty.AddThemeColorOverride("font_color", MutedGray);
			_outgoingRequestsList.AddChild(empty);
		}
	}

	private void RefreshFriendsList()
	{
		foreach (var child in _friendsList.GetChildren())
			child.QueueFree();

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		var friends = new List<(ulong FriendshipId, Identity FriendId)>();
		foreach (var f in conn.Db.Friendship.PlayerA.Filter(localId))
			friends.Add((f.Id, f.PlayerB));
		foreach (var f in conn.Db.Friendship.PlayerB.Filter(localId))
			friends.Add((f.Id, f.PlayerA));

		var localMembership = conn.Db.PartyMember.PlayerId.Find(localId);
		bool localInParty = localMembership is not null;

		foreach (var (friendshipId, friendId) in friends)
		{
			var player = conn.Db.Player.Identity.Find(friendId);
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 6);

			var statusDot = new Label();
			statusDot.Text = "\u25cf ";
			statusDot.AddThemeColorOverride("font_color", player?.Online == true ? GreenOnline : MutedGray);
			row.AddChild(statusDot);

			var nameLabel = new Label();
			nameLabel.Text = player?.DisplayName ?? "Unknown";
			nameLabel.SizeFlagsHorizontal = SizeFlags.Fill | SizeFlags.Expand;
			row.AddChild(nameLabel);

			if (player?.Online == true)
			{
				var onlineLabel = new Label();
				onlineLabel.Text = "Online";
				onlineLabel.AddThemeFontSizeOverride("font_size", 12);
				onlineLabel.AddThemeColorOverride("font_color", GreenOnline);
				row.AddChild(onlineLabel);
			}
			else
			{
				var offlineLabel = new Label();
				offlineLabel.Text = "Offline";
				offlineLabel.AddThemeFontSizeOverride("font_size", 12);
				offlineLabel.AddThemeColorOverride("font_color", MutedGray);
				row.AddChild(offlineLabel);
			}

			if (localInParty)
			{
				var friendInParty = conn.Db.PartyMember.PlayerId.Find(friendId) is not null;
				if (!friendInParty)
				{
					var capturedFriendId = friendId;
					var inviteBtn = CreateSmallButton("Invite", new Vector2(60, 24));
					inviteBtn.Pressed += () =>
					{
						conn.Reducers.InviteToParty(capturedFriendId);
						CallDeferred(nameof(DeferredRefresh));
					};
					row.AddChild(inviteBtn);
				}
			}

			var capturedFriendshipId = friendshipId;
			var removeBtn = CreateSmallButton("Remove", new Vector2(70, 24));
			removeBtn.Pressed += () =>
			{
				conn.Reducers.RemoveFriend(capturedFriendshipId);
				CallDeferred(nameof(DeferredRefresh));
			};
			row.AddChild(removeBtn);

			_friendsList.AddChild(row);
		}

		if (_friendsList.GetChildCount() == 0)
		{
			var empty = new Label();
			empty.Text = "No friends yet. Add someone above!";
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			empty.AddThemeColorOverride("font_color", MutedGray);
			_friendsList.AddChild(empty);
		}
	}

	// ── Table Callbacks ─────────────────────────────────────────

	private void OnFriendRequestInsert(EventContext ctx, FriendRequest req)
	{
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		if (req.SenderId == localId || req.ReceiverId == localId)
			CallDeferred(nameof(DeferredRefresh));
	}

	private void OnFriendRequestDelete(EventContext ctx, FriendRequest req)
	{
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		if (req.SenderId == localId || req.ReceiverId == localId)
			CallDeferred(nameof(DeferredRefresh));
	}

	private void OnFriendshipInsert(EventContext ctx, Friendship f)
	{
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		if (f.PlayerA == localId || f.PlayerB == localId)
			CallDeferred(nameof(DeferredRefresh));
	}

	private void OnFriendshipDelete(EventContext ctx, Friendship f)
	{
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		if (f.PlayerA == localId || f.PlayerB == localId)
			CallDeferred(nameof(DeferredRefresh));
	}

	private void OnPlayerUpdate(EventContext ctx, SpacetimeDB.Types.Player oldPlayer, SpacetimeDB.Types.Player newPlayer)
	{
		if (oldPlayer.Online != newPlayer.Online || oldPlayer.DisplayName != newPlayer.DisplayName)
			CallDeferred(nameof(DeferredRefresh));
	}

	private void OnPartyMemberInsert(EventContext ctx, PartyMember member)
	{
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		if (member.PlayerId == localId)
			CallDeferred(nameof(DeferredRefresh));
	}

	private void OnPartyMemberDelete(EventContext ctx, PartyMember member)
	{
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		if (member.PlayerId == localId)
			CallDeferred(nameof(DeferredRefresh));
	}

	private void DeferredRefresh()
	{
		RefreshAll();
	}

	// ── Button Handlers ─────────────────────────────────────────

	private void OnAddFriendPressed()
	{
		var name = _addFriendInput.Text.Trim();
		if (string.IsNullOrEmpty(name)) return;

		SpacetimeNetworkManager.Instance.Conn.Reducers.SendFriendRequest(name);
		_addFriendInput.Text = "";
		CallDeferred(nameof(DeferredRefresh));
	}
}
