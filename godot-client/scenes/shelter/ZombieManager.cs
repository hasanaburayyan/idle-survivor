using Godot;
using SpacetimeDB;
using SpacetimeDB.Types;
using System;
using System.Collections.Generic;

public partial class ZombieManager : Node
{
	[Signal]
	public delegate void LootReceivedEventHandler(Vector2 position, string text);

	private Node2D _worldRoot;
	private TileMapLayer _buildingLayer;
	private Vector2 _mapScale;
	private PackedScene _zombieScene = GD.Load<PackedScene>("uid://cklegshx4bjbl");

	private RandomNumberGenerator _rng = new();
	private Rect2 _spawnOuterRect;
	private Rect2 _spawnExclusion;
	private Color _currentZombieTint = Colors.White;
	private readonly Queue<Vector2> _pendingKillPositions = new();

	private Player _localPlayerNode;

	public void Init(Node2D worldRoot, TileMapLayer buildingLayer, Vector2 mapScale,
		Rect2 spawnOuterRect, Rect2 spawnExclusion, Player localPlayerNode)
	{
		_worldRoot = worldRoot;
		_buildingLayer = buildingLayer;
		_mapScale = mapScale;
		_spawnOuterRect = spawnOuterRect;
		_spawnExclusion = spawnExclusion;
		_localPlayerNode = localPlayerNode;

		var conn = SpacetimeNetworkManager.Instance.Conn;
		conn.Db.KillLoot.OnInsert += OnKillLootInsert;

		for (int i = 0; i < 100; i++)
			SpawnZombie();
	}

	public override void _ExitTree()
	{
		var conn = SpacetimeNetworkManager.Instance?.Conn;
		if (conn != null)
			conn.Db.KillLoot.OnInsert -= OnKillLootInsert;
	}

	public void SetTint(Color tint)
	{
		_currentZombieTint = tint;
		foreach (var child in _worldRoot.GetChildren())
			if (child is Zombie z)
				z.Modulate = tint;
	}

	public void OnPlayerKillRequested()
	{
		var alive = new List<Zombie>();
		foreach (var child in _worldRoot.GetChildren())
		{
			if (child is Zombie z && !z.IsDying)
				alive.Add(z);
		}
		if (alive.Count == 0)
			return;

		var playerPos = _localPlayerNode.Position;
		alive.Sort((a, b) =>
			a.Position.DistanceSquaredTo(playerPos).CompareTo(b.Position.DistanceSquaredTo(playerPos)));

		int pool = Math.Min(alive.Count, 10);
		var target = alive[_rng.RandiRange(0, pool - 1)];
		target.Die();
	}

	private void SpawnZombie()
	{
		Vector2 point;
		do
		{
			point = new Vector2(
				_rng.RandfRange(_spawnOuterRect.Position.X, _spawnOuterRect.End.X),
				_rng.RandfRange(_spawnOuterRect.Position.Y, _spawnOuterRect.End.Y)
			);
		} while (_spawnExclusion.HasPoint(point));

		var zombie = _zombieScene.Instantiate<Zombie>();
		zombie.Position = point * _mapScale;
		zombie.BuildingLayer = _buildingLayer;
		zombie.Modulate = _currentZombieTint;
		zombie.Killed += () => OnZombieKilled(zombie.Position);
		_worldRoot.AddChild(zombie);
	}

	private void OnZombieKilled(Vector2 deathPosition)
	{
		_pendingKillPositions.Enqueue(deathPosition);
		SpacetimeNetworkManager.Instance.Conn.Reducers.KillZombie();
		SpawnZombie();
	}

	private void OnKillLootInsert(EventContext ctx, KillLoot loot)
	{
		if (loot.Owner != SpacetimeNetworkManager.Instance.LocalIdentity)
			return;

		var pos = _pendingKillPositions.Count > 0
			? _pendingKillPositions.Dequeue()
			: _localPlayerNode.Position;

		EmitSignal(SignalName.LootReceived, pos, $"+{loot.Amount} {loot.Resource}");
		SpacetimeNetworkManager.Instance.Conn.Reducers.AckKillLoot(loot.Id);
	}
}
