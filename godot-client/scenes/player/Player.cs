using Godot;
using SpacetimeDB;
using SpacetimeDB.Types;
using System;

public partial class Player : CharacterBody2D
{
	private const int FrameWidth = 148;
	private const int FrameHeight = 96;
	private const float MoveSpeed = 120f;
	private const float IdlePauseMin = 2f;
	private const float IdlePauseMax = 5f;
	private const float ActionChance = 0.15f;

	private static readonly string[] SheetPaths = {
		"res://assets/player/IDLE.png",
		"res://assets/player/RUN.png",
		"res://assets/player/DASH.png",
		"res://assets/player/HURT.png",
		"res://assets/player/ATTACK 1.png",
		"res://assets/player/ATTACK 2.png",
		"res://assets/player/ATTACK 3.png",
	};

	private static readonly string[] AnimNames = {
		"idle", "run", "dash", "hurt", "attack1", "attack2", "attack3"
	};

	private enum AnimState { Moving, Idle, Action }

	public Label DisplayNameLabel;
	public Label ActivityLabel;
	private AnimatedSprite2D _sprite;
	private Identity? _activityIdentity;
	private bool _activityLabelShown;
	private string _activityLabelText = "";
	private AnimState _state = AnimState.Moving;
	private Vector2 _moveDir = Vector2.Right;
	private float _stateTimer;
	private RandomNumberGenerator _rng = new();
	private bool _animationsLoaded;

	public override void _Ready()
	{
		DisplayNameLabel = GetNode<Label>("%DisplayName");
		ActivityLabel = GetNode<Label>("%ActivityLabel");
		ActivityLabel.Visible = false;
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		LoadAnimations();
		_sprite.AnimationFinished += OnAnimationFinished;
		EnterMoving();
	}

	public override void _ExitTree()
	{
		UnbindActivityDisplay();
		base._ExitTree();
	}

	public void BindActivityDisplay(Identity playerId)
	{
		UnbindActivityDisplay();
		var conn = SpacetimeNetworkManager.Instance?.Conn;
		if (conn is null)
			return;
		_activityIdentity = playerId;
		conn.Db.ActiveTask.OnInsert += OnActiveTaskInserted;
		conn.Db.ActiveTask.OnDelete += OnActiveTaskDeleted;

		var existing = conn.Db.ActiveTask.Participant.Find(playerId);
		if (existing is null)
			ClearActivityLabel();
		else
			SetActivityLabel(FormatActivityLine(existing.Type));
	}

	private void UnbindActivityDisplay()
	{
		if (!_activityIdentity.HasValue)
			return;

		var conn = SpacetimeNetworkManager.Instance?.Conn;
		if (conn is not null)
		{
			conn.Db.ActiveTask.OnInsert -= OnActiveTaskInserted;
			conn.Db.ActiveTask.OnDelete -= OnActiveTaskDeleted;
		}
		_activityIdentity = null;
		_activityLabelShown = false;
		_activityLabelText = "";
		if (ActivityLabel is not null)
			ActivityLabel.Visible = false;
	}

	private void OnActiveTaskInserted(EventContext ctx, ActiveTask row)
	{
		if (!_activityIdentity.HasValue || row.Participant != _activityIdentity.Value)
			return;
		SetActivityLabel(FormatActivityLine(row.Type));
	}

	private void OnActiveTaskDeleted(EventContext ctx, ActiveTask row)
	{
		if (!_activityIdentity.HasValue || row.Participant != _activityIdentity.Value)
			return;
		ClearActivityLabel();
	}

	private void SetActivityLabel(string text)
	{
		if (ActivityLabel is null)
			return;
		if (_activityLabelShown && _activityLabelText == text)
			return;
		ActivityLabel.Text = text;
		ActivityLabel.Visible = true;
		_activityLabelText = text;
		_activityLabelShown = true;
	}

	private void ClearActivityLabel()
	{
		if (ActivityLabel is null || !_activityLabelShown)
			return;
		ActivityLabel.Visible = false;
		_activityLabelShown = false;
		_activityLabelText = "";
	}

	private static string FormatActivityLine(ActivityType type) => type switch
	{
		ActivityType.Scavenge => "Scavenging...",
		ActivityType.LootBigWood => "Looting...",
		ActivityType.CarbLoad => "Carb loading...",
		ActivityType.Study => "Studying...",
		ActivityType.Focus => "Focusing...",
		ActivityType.BuildShelter => "Building shelter...",
		ActivityType.Salvage => "Salvaging...",
		_ => $"{type}..."
	};

	private void LoadAnimations()
	{
		var frames = new SpriteFrames();

		for (int i = 0; i < SheetPaths.Length; i++)
		{
			var tex = GD.Load<Texture2D>(SheetPaths[i]);
			if (tex == null)
			{
				GD.PrintErr($"Failed to load sprite sheet: {SheetPaths[i]}");
				continue;
			}

			var animName = AnimNames[i];
			if (animName != "default")
				frames.AddAnimation(animName);

			frames.SetAnimationSpeed(animName, 8.0);
			frames.SetAnimationLoop(animName, animName == "idle" || animName == "run");

			int frameCount = tex.GetWidth() / FrameWidth;
			for (int f = 0; f < frameCount; f++)
			{
				var atlas = new AtlasTexture();
				atlas.Atlas = tex;
				atlas.Region = new Rect2(f * FrameWidth, 0, FrameWidth, FrameHeight);
				frames.AddFrame(animName, atlas);
			}
		}

		if (frames.HasAnimation("default"))
			frames.RemoveAnimation("default");

		_sprite.SpriteFrames = frames;
		_animationsLoaded = true;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_animationsLoaded) return;

		_stateTimer -= (float)delta;

		switch (_state)
		{
			case AnimState.Moving:
				ProcessMoving(delta);
				break;
			case AnimState.Idle:
				if (_stateTimer <= 0)
					TransitionFromIdle();
				break;
			case AnimState.Action:
				break;
		}
	}

	private void ProcessMoving(double delta)
	{
		var viewport = GetViewportRect();
		float halfW = FrameWidth * 0.5f;
		float halfH = FrameHeight * 0.5f;
		float leftBound = halfW;
		float rightBound = viewport.Size.X - halfW;
		float topBound = halfH;
		float bottomBound = viewport.Size.Y - halfH;

		Position += _moveDir * MoveSpeed * (float)delta;

		if (Position.X >= rightBound)
		{
			Position = new Vector2(rightBound, Position.Y);
			_moveDir.X *= -1f;
			_sprite.FlipH = _moveDir.X < 0f;
		}
		else if (Position.X <= leftBound)
		{
			Position = new Vector2(leftBound, Position.Y);
			_moveDir.X *= -1f;
			_sprite.FlipH = _moveDir.X < 0f;
		}

		if (Position.Y >= bottomBound)
		{
			Position = new Vector2(Position.X, bottomBound);
			_moveDir.Y *= -1f;
		}
		else if (Position.Y <= topBound)
		{
			Position = new Vector2(Position.X, topBound);
			_moveDir.Y *= -1f;
		}

		if (_stateTimer <= 0)
		{
			if (_rng.Randf() < ActionChance)
				EnterAction();
			else
				EnterIdle();
		}
	}

	private void EnterMoving()
	{
		_state = AnimState.Moving;
		_stateTimer = _rng.RandfRange(3f, 7f);
		_moveDir = RandomUnitDirection();
		_sprite.FlipH = _moveDir.X < 0f;
		PlayAnim("run");
	}

	private Vector2 RandomUnitDirection()
	{
		for (int i = 0; i < 8; i++)
		{
			var v = new Vector2(_rng.RandfRange(-1f, 1f), _rng.RandfRange(-1f, 1f));
			if (v.LengthSquared() > 0.0001f)
				return v.Normalized();
		}
		return Vector2.Right;
	}

	private void EnterIdle()
	{
		_state = AnimState.Idle;
		_stateTimer = _rng.RandfRange(IdlePauseMin, IdlePauseMax);
		PlayAnim("idle");
	}

	private void EnterAction()
	{
		_state = AnimState.Action;
		string[] actions = { "attack1", "attack2", "attack3", "dash" };
		var pick = actions[_rng.RandiRange(0, actions.Length - 1)];
		PlayAnim(pick);
	}

	private void TransitionFromIdle()
	{
		if (_rng.Randf() < ActionChance)
			EnterAction();
		else
			EnterMoving();
	}

	private void OnAnimationFinished()
	{
		if (_state == AnimState.Action)
			EnterMoving();
	}

	private void PlayAnim(string name)
	{
		if (_sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(name))
			_sprite.Play(name);
	}

	public new void SetName(string name)
	{
		DisplayNameLabel.Text = name;
	}
}
