using Godot;
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
	private AnimatedSprite2D _sprite;
	private AnimState _state = AnimState.Moving;
	private float _direction = 1f;
	private float _stateTimer;
	private RandomNumberGenerator _rng = new();
	private bool _animationsLoaded;

	public override void _Ready()
	{
		DisplayNameLabel = GetNode<Label>("%DisplayName");
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		LoadAnimations();
		_sprite.AnimationFinished += OnAnimationFinished;
		EnterMoving();
	}

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
		float margin = FrameWidth * 0.5f;
		float leftBound = margin;
		float rightBound = viewport.Size.X - margin;

		Position += new Vector2(_direction * MoveSpeed * (float)delta, 0);

		if (Position.X >= rightBound)
		{
			Position = new Vector2(rightBound, Position.Y);
			_direction = -1f;
			_sprite.FlipH = true;
		}
		else if (Position.X <= leftBound)
		{
			Position = new Vector2(leftBound, Position.Y);
			_direction = 1f;
			_sprite.FlipH = false;
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
		PlayAnim("run");
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
