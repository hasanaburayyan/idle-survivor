using Godot;
using SpacetimeDB;
using SpacetimeDB.Types;
using System.Collections.Generic;

public partial class GuildMemberManager : Node
{
	private Node2D _worldRoot;
	private Marker2D _playerSpawnPosition;
	private PackedScene _playerScene = GD.Load<PackedScene>("uid://cl6yviutw6arx");
	private Dictionary<SpacetimeDB.Identity, Player> _memberSprites = new();
	private RandomNumberGenerator _rng = new();
	private bool _inGuildHall;

	public bool InGuildHall => _inGuildHall;

	public void Init(Node2D worldRoot, Marker2D playerSpawnPosition)
	{
		_worldRoot = worldRoot;
		_playerSpawnPosition = playerSpawnPosition;

		var conn = SpacetimeNetworkManager.Instance.Conn;
		conn.Db.Player.OnUpdate += OnPlayerUpdate;
	}

	public override void _ExitTree()
	{
		var conn = SpacetimeNetworkManager.Instance?.Conn;
		if (conn != null)
			conn.Db.Player.OnUpdate -= OnPlayerUpdate;
	}

	public void OnGuildSessionChanged(bool inSession)
	{
		_inGuildHall = inSession;
		if (inSession)
			SpawnGuildMembers();
		else
			DespawnAll();
	}

	public void HandlePlayerLocationChange(SpacetimeDB.Types.Player oldPlayer, SpacetimeDB.Types.Player newPlayer)
	{
		if (!_inGuildHall) return;

		if (oldPlayer.Location != newPlayer.Location)
		{
			if (newPlayer.Location == LocationType.GuildHall && newPlayer.Online)
				SpawnMemberSprite(newPlayer.Identity, newPlayer.DisplayName);
			else
				DespawnMemberSprite(newPlayer.Identity);
		}

		if (oldPlayer.Online && !newPlayer.Online)
			DespawnMemberSprite(newPlayer.Identity);
	}

	public void HandlePlayerNameChange(SpacetimeDB.Types.Player oldPlayer, SpacetimeDB.Types.Player newPlayer)
	{
		if (!_inGuildHall) return;
		if (oldPlayer.DisplayName != newPlayer.DisplayName
			&& _memberSprites.TryGetValue(newPlayer.Identity, out var sprite))
		{
			sprite.SetName(newPlayer.DisplayName);
		}
	}

	private void OnPlayerUpdate(EventContext ctx, SpacetimeDB.Types.Player oldPlayer, SpacetimeDB.Types.Player newPlayer)
	{
		if (newPlayer.Identity == SpacetimeNetworkManager.Instance.LocalIdentity)
			return;

		HandlePlayerNameChange(oldPlayer, newPlayer);
		HandlePlayerLocationChange(oldPlayer, newPlayer);
	}

	private void SpawnGuildMembers()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		var membership = conn.Db.GuildMember.PlayerId.Find(localId);
		if (membership is null) return;

		foreach (var member in conn.Db.GuildMember.GuildId.Filter(membership.GuildId))
		{
			if (member.PlayerId == localId) continue;

			var memberPlayer = conn.Db.Player.Identity.Find(member.PlayerId);
			if (memberPlayer is null || !memberPlayer.Online) continue;
			if (memberPlayer.Location != LocationType.GuildHall) continue;

			SpawnMemberSprite(member.PlayerId, memberPlayer.DisplayName);
		}
	}

	private void SpawnMemberSprite(SpacetimeDB.Identity playerId, string displayName)
	{
		if (_memberSprites.ContainsKey(playerId)) return;

		var sprite = _playerScene.Instantiate<Player>();
		var viewport = _worldRoot.GetViewport().GetVisibleRect();
		float margin = 80f;
		float x = _rng.RandfRange(margin, viewport.Size.X - margin);
		float y = _playerSpawnPosition.Position.Y + _rng.RandfRange(-20, 20);
		sprite.Position = new Vector2(x, y);
		sprite.ZIndex = 0;
		_worldRoot.AddChild(sprite);
		sprite.SetName(displayName);
		sprite.BindActivityDisplay(playerId);
		_memberSprites[playerId] = sprite;
	}

	private void DespawnMemberSprite(SpacetimeDB.Identity playerId)
	{
		if (_memberSprites.TryGetValue(playerId, out var sprite))
		{
			sprite.QueueFree();
			_memberSprites.Remove(playerId);
		}
	}

	private void DespawnAll()
	{
		foreach (var kvp in _memberSprites)
			kvp.Value.QueueFree();
		_memberSprites.Clear();
	}
}
