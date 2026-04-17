using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class Zombie : CharacterBody2D
{
	private const float MoveSpeedMin = 55f;
	private const float MoveSpeedMax = 95f;
	private const float IdleTimeMin = 1f;
	private const float IdleTimeMax = 3f;
	private const float DriftAngleMax = 25f;
	private const float NearTileThreshold = 1.5f;

	private enum SeekState { Walking, Idle, Dying }

	[Signal]
	public delegate void KilledEventHandler();

	[Signal]
	public delegate void AdventureAttackRequestedEventHandler(ulong zombieId);

	public TileMapLayer BuildingLayer { get; set; }
	public bool IsDying => _state == SeekState.Dying;
	public ulong AdventureZombieId { get; set; }

	private AnimatedSprite2D _sprite;
	private RandomNumberGenerator _rng = new();
	private SeekState _state = SeekState.Idle;
	private Vector2 _moveDir;
	private float _stateTimer;
	private float _moveSpeed;

	private static TileMapLayer _cachedLayer;
	private static List<Vector2> _cachedCellWorldPositions;
	private static ulong _cacheBuiltAtMsec;
	private const ulong CacheTTLMsec = 5000;

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_moveSpeed = _rng.RandfRange(MoveSpeedMin, MoveSpeedMax);
		InputPickable = true;
		EnterIdle();
	}

	public override void _InputEvent(Viewport viewport, InputEvent @event, int shapeIdx)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			if (AdventureZombieId != 0)
			{
				EmitSignal(SignalName.AdventureAttackRequested, AdventureZombieId);
			}
			else
			{
				Die();
			}
			viewport.SetInputAsHandled();
		}
	}

	public void Die()
	{
		if (_state == SeekState.Dying)
			return;

		_state = SeekState.Dying;
		Velocity = Vector2.Zero;

		string suffix;
		if (Mathf.Abs(_moveDir.X) >= Mathf.Abs(_moveDir.Y))
			suffix = _moveDir.X < 0 ? "left" : "right";
		else
			suffix = _rng.Randi() % 2 == 0 ? "left" : "right";

		_sprite.Play($"death_{suffix}");
		_sprite.AnimationFinished += OnDeathAnimationFinished;
	}

	private void OnDeathAnimationFinished()
	{
		EmitSignal(SignalName.Killed);
		QueueFree();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_state == SeekState.Dying)
			return;

		if (AdventureZombieId != 0)
			return;

		_stateTimer -= (float)delta;

		switch (_state)
		{
			case SeekState.Walking:
				Velocity = _moveDir * _moveSpeed;
				MoveAndSlide();

				if (GetSlideCollisionCount() > 0)
					EnterIdle();
				break;

			case SeekState.Idle:
				Velocity = Vector2.Zero;
				if (_stateTimer <= 0)
					EnterWalking();
				break;
		}
	}

	private void EnterWalking()
	{
		if (!SeekNearestBuilding())
		{
			_stateTimer = _rng.RandfRange(IdleTimeMin, IdleTimeMax);
			return;
		}
		_state = SeekState.Walking;
		PlayDirectionalAnim("walk", _moveDir);
	}

	private void EnterIdle()
	{
		_state = SeekState.Idle;
		_stateTimer = _rng.RandfRange(IdleTimeMin, IdleTimeMax);
		PlayDirectionalAnim("idle", _moveDir);
	}

	private bool SeekNearestBuilding()
	{
		if (BuildingLayer is null)
			return false;

		ulong now = Time.GetTicksMsec();
		if (_cachedLayer != BuildingLayer || _cachedCellWorldPositions is null || now - _cacheBuiltAtMsec > CacheTTLMsec)
		{
			_cachedLayer = BuildingLayer;
			_cachedCellWorldPositions = new List<Vector2>();
			Vector2 mapScale = BuildingLayer.GetParent<Node2D>().Scale;
			foreach (Vector2I cell in BuildingLayer.GetUsedCells())
			{
				Vector2 localPos = BuildingLayer.MapToLocal(cell);
				_cachedCellWorldPositions.Add(localPos * mapScale);
			}
			_cacheBuiltAtMsec = now;
		}

		var cellsCached = _cachedCellWorldPositions;
		if (cellsCached.Count == 0)
			return false;

		float bestDistSq = float.MaxValue;
		foreach (var worldPos in cellsCached)
		{
			float distSq = Position.DistanceSquaredTo(worldPos);
			if (distSq < bestDistSq)
				bestDistSq = distSq;
		}

		float threshold = bestDistSq * NearTileThreshold * NearTileThreshold;
		var candidates = new List<Vector2>();
		foreach (var worldPos in cellsCached)
		{
			if (Position.DistanceSquaredTo(worldPos) <= threshold)
				candidates.Add(worldPos);
		}

		if (candidates.Count == 0)
			return false;

		Vector2 target = candidates[_rng.RandiRange(0, candidates.Count - 1)];
		Vector2 toTarget = target - Position;
		if (toTarget.LengthSquared() < 1f)
			return false;

		float driftRad = Mathf.DegToRad(_rng.RandfRange(-DriftAngleMax, DriftAngleMax));
		_moveDir = toTarget.Normalized().Rotated(driftRad);
		return true;
	}

	private void PlayDirectionalAnim(string prefix, Vector2 dir)
	{
		string suffix;
		if (Mathf.Abs(dir.X) >= Mathf.Abs(dir.Y))
			suffix = dir.X < 0 ? "left" : "right";
		else
			suffix = dir.Y < 0 ? "up" : "down";

		string anim = $"{prefix}_{suffix}";
		if (_sprite.Animation != anim)
			_sprite.Play(anim);
	}
}
