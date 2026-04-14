using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class Zombie : CharacterBody2D
{
	private const float MoveSpeedMin = 35f;
	private const float MoveSpeedMax = 65f;
	private const float IdleTimeMin = 1f;
	private const float IdleTimeMax = 3f;
	private const float DriftAngleMax = 25f;
	private const float NearTileThreshold = 1.5f;

	private enum SeekState { Walking, Idle }

	public TileMapLayer BuildingLayer { get; set; }

	private AnimatedSprite2D _sprite;
	private RandomNumberGenerator _rng = new();
	private SeekState _state = SeekState.Idle;
	private Vector2 _moveDir;
	private float _stateTimer;
	private float _moveSpeed;

	public override void _Ready()
	{
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		_moveSpeed = _rng.RandfRange(MoveSpeedMin, MoveSpeedMax);
		EnterIdle();
	}

	public override void _PhysicsProcess(double delta)
	{
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

		Array<Vector2I> cells = BuildingLayer.GetUsedCells();
		if (cells.Count == 0)
			return false;

		Vector2 mapScale = BuildingLayer.GetParent<Node2D>().Scale;

		float bestDistSq = float.MaxValue;
		var candidates = new List<Vector2>();

		foreach (Vector2I cell in cells)
		{
			Vector2 localPos = BuildingLayer.MapToLocal(cell);
			Vector2 worldPos = localPos * mapScale;
			float distSq = Position.DistanceSquaredTo(worldPos);
			if (distSq < bestDistSq)
				bestDistSq = distSq;
		}

		float threshold = bestDistSq * NearTileThreshold * NearTileThreshold;
		foreach (Vector2I cell in cells)
		{
			Vector2 localPos = BuildingLayer.MapToLocal(cell);
			Vector2 worldPos = localPos * mapScale;
			if (Position.DistanceSquaredTo(worldPos) <= threshold)
				candidates.Add(worldPos);
		}

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
