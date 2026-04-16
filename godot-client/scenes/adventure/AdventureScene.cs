using Godot;
using SpacetimeDB;
using SpacetimeDB.Types;
using System;
using System.Collections.Generic;

public partial class AdventureScene : Node2D
{
	private const float PLAYER_MOVE_SPEED = 200f;
	private const float POSITION_SEND_INTERVAL = 0.1f;
	private const float AUTO_ATTACK_INTERVAL = 1.5f;
	private const float AUTO_ATTACK_RANGE = 150f;
	private const float ZOMBIE_LERP_SPEED = 10f;
	private const float PLAYER_LERP_SPEED = 10f;
	private const float RETURN_DELAY = 4f;

	private PackedScene _playerScene = GD.Load<PackedScene>("uid://cl6yviutw6arx");
	private PackedScene _zombieScene = GD.Load<PackedScene>("uid://cklegshx4bjbl");
	private PackedScene _shelterScene = GD.Load<PackedScene>("uid://dt6dxcbysqucx");

	private Node2D _worldRoot;
	private SubViewport _subViewport;
	private Camera2D _camera;
	private Label _waveLabel;
	private ProgressBar _healthBar;
	private Label _healthLabel;
	private Label _zombiesLabel;
	private PanelContainer _resultsPanel;
	private Label _resultTitle;
	private Label _resultDetails;
	private Button _returnButton;

	private Player _localPlayerNode;
	private ulong _localAdventureId;
	private Dictionary<Identity, Player> _playerNodes = new();
	private Dictionary<ulong, Zombie> _zombieNodes = new();
	private Dictionary<ulong, Vector2> _zombieTargetPositions = new();
	private Dictionary<Identity, Vector2> _playerTargetPositions = new();
	private Dictionary<ulong, ProgressBar> _zombieHealthBars = new();
	private Dictionary<Identity, ProgressBar> _playerHealthBars = new();

	private float _positionSendTimer;
	private float _autoAttackTimer;
	private bool _adventureEnded;
	private float _returnTimer;
	private bool _autoKillEnabled;

	public override void _Ready()
	{
		_worldRoot = GetNode<Node2D>("%WorldRoot");
		_subViewport = GetNode<SubViewport>("%SubViewport");
		_camera = GetNode<Camera2D>("%Camera2D");
		_waveLabel = GetNode<Label>("%WaveLabel");
		_healthBar = GetNode<ProgressBar>("%HealthBar");
		_healthLabel = GetNode<Label>("%HealthLabel");
		_zombiesLabel = GetNode<Label>("%ZombiesLabel");
		_resultsPanel = GetNode<PanelContainer>("%ResultsPanel");
		_resultTitle = GetNode<Label>("%ResultTitle");
		_resultDetails = GetNode<Label>("%ResultDetails");
		_returnButton = GetNode<Button>("%ReturnButton");

		_returnButton.Pressed += OnReturnPressed;

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		_autoKillEnabled = HasSkillByName(conn, localId, "Auto Kill Zombie");

		var localParticipant = conn.Db.AdventureParticipant.PlayerId.Find(localId);
		if (localParticipant is null)
		{
			GD.PrintErr("AdventureScene: no participant found for local player, returning to shelter");
			GetTree().ChangeSceneToPacked(_shelterScene);
			return;
		}

		_localAdventureId = localParticipant.AdventureId;

		foreach (var p in conn.Db.AdventureParticipant.AdventureId.Filter(_localAdventureId))
			SpawnPlayerNode(p);

		foreach (var z in conn.Db.AdventureZombie.AdventureId.Filter(_localAdventureId))
			SpawnZombieNode(z);

		conn.Db.AdventureParticipant.OnInsert += OnParticipantInsert;
		conn.Db.AdventureParticipant.OnUpdate += OnParticipantUpdate;
		conn.Db.AdventureParticipant.OnDelete += OnParticipantDelete;
		conn.Db.AdventureZombie.OnInsert += OnZombieInsert;
		conn.Db.AdventureZombie.OnUpdate += OnZombieUpdate;
		conn.Db.AdventureZombie.OnDelete += OnZombieDelete;
		conn.Db.Adventure.OnUpdate += OnAdventureUpdate;

		RefreshHud();
	}

	public override void _ExitTree()
	{
		var conn = SpacetimeNetworkManager.Instance?.Conn;
		if (conn is null) return;
		conn.Db.AdventureParticipant.OnInsert -= OnParticipantInsert;
		conn.Db.AdventureParticipant.OnUpdate -= OnParticipantUpdate;
		conn.Db.AdventureParticipant.OnDelete -= OnParticipantDelete;
		conn.Db.AdventureZombie.OnInsert -= OnZombieInsert;
		conn.Db.AdventureZombie.OnUpdate -= OnZombieUpdate;
		conn.Db.AdventureZombie.OnDelete -= OnZombieDelete;
		conn.Db.Adventure.OnUpdate -= OnAdventureUpdate;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_adventureEnded)
		{
			_returnTimer -= (float)delta;
			if (_returnTimer <= 0f)
			{
				_resultsPanel.Visible = true;
			}
			InterpolateEntities((float)delta);
			return;
		}

		HandlePlayerInput((float)delta);
		HandleAutoAttack((float)delta);
		InterpolateEntities((float)delta);
	}

	private void HandlePlayerInput(float delta)
	{
		if (_localPlayerNode is null) return;

		var input = Vector2.Zero;
		if (Input.IsActionPressed("move_up")) input.Y -= 1;
		if (Input.IsActionPressed("move_down")) input.Y += 1;
		if (Input.IsActionPressed("move_left")) input.X -= 1;
		if (Input.IsActionPressed("move_right")) input.X += 1;

		if (input.LengthSquared() > 0)
		{
			input = input.Normalized();
			_localPlayerNode.Position += input * PLAYER_MOVE_SPEED * delta;

			var adventure = SpacetimeNetworkManager.Instance.Conn.Db.Adventure.Id.Find(_localAdventureId);
			if (adventure is not null)
			{
				float x = Mathf.Clamp(_localPlayerNode.Position.X, 0, adventure.ArenaWidth);
				float y = Mathf.Clamp(_localPlayerNode.Position.Y, 0, adventure.ArenaHeight);
				_localPlayerNode.Position = new Vector2(x, y);
			}
		}

		_localPlayerNode.SetAdventureDirection(input);

		_camera.Position = _localPlayerNode.Position;

		_positionSendTimer -= delta;
		if (_positionSendTimer <= 0f)
		{
			_positionSendTimer = POSITION_SEND_INTERVAL;
			SpacetimeNetworkManager.Instance.Conn.Reducers.UpdateAdventurePosition(
				_localPlayerNode.Position.X,
				_localPlayerNode.Position.Y
			);
		}
	}

	private void HandleAutoAttack(float delta)
	{
		if (!_autoKillEnabled || _localPlayerNode is null) return;

		_autoAttackTimer -= delta;
		if (_autoAttackTimer > 0f) return;
		_autoAttackTimer = AUTO_ATTACK_INTERVAL;

		ulong nearestId = 0;
		float nearestDistSq = float.MaxValue;
		foreach (var (id, zombie) in _zombieNodes)
		{
			if (zombie.IsDying) continue;
			float dSq = _localPlayerNode.Position.DistanceSquaredTo(zombie.Position);
			if (dSq < nearestDistSq)
			{
				nearestDistSq = dSq;
				nearestId = id;
			}
		}

		if (nearestId != 0 && nearestDistSq <= AUTO_ATTACK_RANGE * AUTO_ATTACK_RANGE)
		{
			SendPositionAndAttack(nearestId);
		}
	}

	private void InterpolateEntities(float delta)
	{
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		foreach (var (identity, node) in _playerNodes)
		{
			if (identity == localId) continue;
			if (_playerTargetPositions.TryGetValue(identity, out var target))
				node.Position = node.Position.Lerp(target, PLAYER_LERP_SPEED * delta);
		}

		foreach (var (id, node) in _zombieNodes)
		{
			if (_zombieTargetPositions.TryGetValue(id, out var target))
				node.Position = node.Position.Lerp(target, ZOMBIE_LERP_SPEED * delta);
		}
	}

	// ── Participant callbacks ───────────────────────────────────

	private void SpawnPlayerNode(AdventureParticipant p)
	{
		if (_playerNodes.ContainsKey(p.PlayerId)) return;

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var playerData = conn.Db.Player.Identity.Find(p.PlayerId);
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		var node = _playerScene.Instantiate<Player>();
		node.AdventureMode = true;
		node.IsLocal = p.PlayerId == localId;
		node.InputPickable = false;
		node.Position = new Vector2(p.PosX, p.PosY);
		node.ZIndex = 10;
		_worldRoot.AddChild(node);
		node.SetName(playerData?.DisplayName ?? "Player");

		var hpBar = new ProgressBar();
		hpBar.Position = new Vector2(-25, -25);
		hpBar.Size = new Vector2(50, 5);
		hpBar.MaxValue = p.MaxHealth;
		hpBar.Value = p.Health;
		hpBar.ShowPercentage = false;
		hpBar.MouseFilter = Control.MouseFilterEnum.Ignore;
		node.AddChild(hpBar);
		_playerHealthBars[p.PlayerId] = hpBar;

		if (p.PlayerId == localId)
		{
			_localPlayerNode = node;
			_camera.Position = node.Position;

			node.AutoKillEnabled = _autoKillEnabled;
			node.KillRequested += OnLocalPlayerKillRequested;
		}

		_playerNodes[p.PlayerId] = node;
		_playerTargetPositions[p.PlayerId] = new Vector2(p.PosX, p.PosY);
	}

	private void OnParticipantInsert(EventContext ctx, AdventureParticipant p)
	{
		if (p.AdventureId != _localAdventureId) return;
		CallDeferred(nameof(DeferredSpawnPlayer), p.PlayerId.ToString(), p.PosX, p.PosY);
	}

	private void DeferredSpawnPlayer(string playerIdStr, float posX, float posY)
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		foreach (var p in conn.Db.AdventureParticipant.AdventureId.Filter(_localAdventureId))
		{
			if (p.PlayerId.ToString() == playerIdStr)
			{
				SpawnPlayerNode(p);
				break;
			}
		}
	}

	private void OnParticipantUpdate(EventContext ctx, AdventureParticipant oldP, AdventureParticipant newP)
	{
		if (newP.AdventureId != _localAdventureId) return;

		_playerTargetPositions[newP.PlayerId] = new Vector2(newP.PosX, newP.PosY);

		if (_playerHealthBars.TryGetValue(newP.PlayerId, out var hpBar))
			hpBar.Value = newP.Health;

		if (!newP.Alive && oldP.Alive)
		{
			if (_playerNodes.TryGetValue(newP.PlayerId, out var node))
				node.Modulate = new Color(1f, 0.3f, 0.3f, 0.5f);
		}

		if (newP.PlayerId == SpacetimeNetworkManager.Instance.LocalIdentity)
			CallDeferred(nameof(DeferredRefreshHud));
	}

	private void OnParticipantDelete(EventContext ctx, AdventureParticipant p)
	{
		if (_playerNodes.TryGetValue(p.PlayerId, out var node))
		{
			node.QueueFree();
			_playerNodes.Remove(p.PlayerId);
		}
		_playerTargetPositions.Remove(p.PlayerId);
		_playerHealthBars.Remove(p.PlayerId);

		if (p.PlayerId == SpacetimeNetworkManager.Instance.LocalIdentity)
			_localPlayerNode = null;
	}

	// ── Zombie callbacks ────────────────────────────────────────

	private void SpawnZombieNode(AdventureZombie z)
	{
		if (_zombieNodes.ContainsKey(z.Id)) return;
		if (!z.Alive) return;

		var node = _zombieScene.Instantiate<Zombie>();
		node.AdventureZombieId = z.Id;
		node.Position = new Vector2(z.PosX, z.PosY);
		node.ZIndex = 5;
		node.InputPickable = true;
		node.AdventureAttackRequested += OnZombieClickAttack;
		_worldRoot.AddChild(node);

		var hpBar = new ProgressBar();
		hpBar.Position = new Vector2(-10, -18);
		hpBar.Size = new Vector2(20, 3);
		hpBar.Scale = new Vector2(1f / 3f, 1f / 3f);
		hpBar.MaxValue = z.Health;
		hpBar.Value = z.Health;
		hpBar.ShowPercentage = false;
		hpBar.MouseFilter = Control.MouseFilterEnum.Ignore;
		node.AddChild(hpBar);
		_zombieHealthBars[z.Id] = hpBar;

		_zombieNodes[z.Id] = node;
		_zombieTargetPositions[z.Id] = new Vector2(z.PosX, z.PosY);
	}

	private void OnZombieInsert(EventContext ctx, AdventureZombie z)
	{
		if (z.AdventureId != _localAdventureId) return;
		CallDeferred(nameof(DeferredSpawnZombie), (long)z.Id);
	}

	private void DeferredSpawnZombie(long zombieIdLong)
	{
		ulong zombieId = (ulong)zombieIdLong;
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var z = conn.Db.AdventureZombie.Id.Find(zombieId);
		if (z is AdventureZombie zombie)
			SpawnZombieNode(zombie);
	}

	private void OnZombieUpdate(EventContext ctx, AdventureZombie oldZ, AdventureZombie newZ)
	{
		if (newZ.AdventureId != _localAdventureId) return;

		_zombieTargetPositions[newZ.Id] = new Vector2(newZ.PosX, newZ.PosY);

		if (_zombieHealthBars.TryGetValue(newZ.Id, out var hpBar))
			hpBar.Value = newZ.Health;

		if (!newZ.Alive && oldZ.Alive)
		{
			if (_zombieNodes.TryGetValue(newZ.Id, out var node))
			{
				node.Die();
				_zombieNodes.Remove(newZ.Id);
				_zombieTargetPositions.Remove(newZ.Id);
				_zombieHealthBars.Remove(newZ.Id);
			}
		}

		CallDeferred(nameof(DeferredRefreshHud));
	}

	private void OnZombieDelete(EventContext ctx, AdventureZombie z)
	{
		if (_zombieNodes.TryGetValue(z.Id, out var node))
		{
			if (!node.IsDying)
				node.QueueFree();
			_zombieNodes.Remove(z.Id);
		}
		_zombieTargetPositions.Remove(z.Id);
		_zombieHealthBars.Remove(z.Id);
	}

	// ── Adventure state callback ────────────────────────────────

	private void OnAdventureUpdate(EventContext ctx, Adventure oldA, Adventure newA)
	{
		if (newA.Id != _localAdventureId) return;

		CallDeferred(nameof(DeferredRefreshHud));

		if ((newA.State == AdventureState.Completed || newA.State == AdventureState.Failed)
			&& oldA.State != newA.State)
		{
			CallDeferred(nameof(DeferredShowResults), (byte)newA.State, (int)newA.CurrentWave);
		}
	}

	private void DeferredShowResults(byte stateByte, int wavesReached)
	{
		_adventureEnded = true;
		_returnTimer = RETURN_DELAY;

		var state = (AdventureState)stateByte;
		_resultTitle.Text = state == AdventureState.Completed ? "Victory!" : "Defeated";

		uint wavesCompleted = wavesReached > 0 ? (uint)(wavesReached - 1) : 0;
		ulong xp = (ulong)wavesCompleted * 10;
		_resultDetails.Text = $"Waves survived: {wavesCompleted}\nXP earned: {xp}";
	}

	private void OnReturnPressed()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		if (conn.Db.AdventureParticipant.PlayerId.Find(localId) is not null)
		{
			try { conn.Reducers.LeaveAdventure(); } catch { }
		}
		GetTree().ChangeSceneToPacked(_shelterScene);
	}

	// ── Click / auto attack ─────────────────────────────────────

	private void SendPositionAndAttack(ulong zombieId)
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		if (_localPlayerNode is not null)
			conn.Reducers.UpdateAdventurePosition(_localPlayerNode.Position.X, _localPlayerNode.Position.Y);
		conn.Reducers.AdventureAttackZombie(zombieId);
	}

	private void OnZombieClickAttack(ulong zombieId)
	{
		SendPositionAndAttack(zombieId);
	}

	private void OnLocalPlayerKillRequested()
	{
		if (_localPlayerNode is null) return;

		ulong nearestId = 0;
		float nearestDistSq = float.MaxValue;
		foreach (var (id, zombie) in _zombieNodes)
		{
			if (zombie.IsDying) continue;
			float dSq = _localPlayerNode.Position.DistanceSquaredTo(zombie.Position);
			if (dSq < nearestDistSq)
			{
				nearestDistSq = dSq;
				nearestId = id;
			}
		}

		if (nearestId != 0 && nearestDistSq <= AUTO_ATTACK_RANGE * AUTO_ATTACK_RANGE)
		{
			SendPositionAndAttack(nearestId);
		}
	}

	// ── HUD ─────────────────────────────────────────────────────

	private void RefreshHud()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		var adventure = conn.Db.Adventure.Id.Find(_localAdventureId);
		if (adventure is not null)
		{
			_waveLabel.Text = $"Wave {adventure.CurrentWave}";
			uint remaining = adventure.WaveZombiesRemaining;
			_zombiesLabel.Text = $"Zombies: {remaining}";
		}

		var participant = conn.Db.AdventureParticipant.PlayerId.Find(localId);
		if (participant is not null)
		{
			_healthBar.MaxValue = participant.MaxHealth;
			_healthBar.Value = participant.Health;
			_healthLabel.Text = $"HP: {participant.Health} / {participant.MaxHealth}";
		}
	}

	private void DeferredRefreshHud() => RefreshHud();

	private static bool HasSkillByName(DbConnection conn, Identity owner, string skillName)
	{
		var def = conn.Db.SkillDefinition.Name.Find(skillName);
		if (def is null) return false;
		return conn.Db.PlayerSkill.BySkillOwnerDef
			.Filter((Owner: owner, SkillDefinitionId: def.Id)).GetEnumerator().MoveNext();
	}
}
