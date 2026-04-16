using Godot;
using SpacetimeDB;
using SpacetimeDB.Types;
using System;
using System.Collections.Generic;

public partial class RiskyBusinessScene : Node2D
{
	private const float RETURN_DELAY = 4f;

	private PackedScene _playerScene = GD.Load<PackedScene>("uid://cl6yviutw6arx");
	private PackedScene _shelterScene = GD.Load<PackedScene>("uid://dt6dxcbysqucx");

	private Node2D _worldRoot;
	private SubViewport _subViewport;
	private Camera2D _camera;

	private Label _timerLabel;
	private Label _lootLabel;
	private Label _multiplierLabel;
	private Label _recoveredLootLabel;
	private Button _lootSafelyButton;
	private Button _greedButton;
	private Button _secureButton;

	private PanelContainer _resultsPanel;
	private Label _resultTitle;
	private Label _resultDetails;
	private Button _returnButton;

	private ulong _localRbId;
	private Dictionary<Identity, Player> _playerNodes = new();
	private Dictionary<Identity, Label> _playerLootLabels = new();
	private bool _gameEnded;
	private float _returnTimer;
	private bool _stunned;

	public override void _Ready()
	{
		_worldRoot = GetNode<Node2D>("%WorldRoot");
		_subViewport = GetNode<SubViewport>("%SubViewport");
		_camera = GetNode<Camera2D>("%Camera2D");

		_timerLabel = GetNode<Label>("%TimerLabel");
		_lootLabel = GetNode<Label>("%LootLabel");
		_multiplierLabel = GetNode<Label>("%MultiplierLabel");
		_recoveredLootLabel = GetNode<Label>("%RecoveredLootLabel");

		_lootSafelyButton = GetNode<Button>("%LootSafelyButton");
		_greedButton = GetNode<Button>("%GreedButton");
		_secureButton = GetNode<Button>("%SecureButton");

		_resultsPanel = GetNode<PanelContainer>("%ResultsPanel");
		_resultTitle = GetNode<Label>("%ResultTitle");
		_resultDetails = GetNode<Label>("%ResultDetails");
		_returnButton = GetNode<Button>("%ReturnButton");

		_lootSafelyButton.Pressed += OnLootSafelyPressed;
		_greedButton.Pressed += OnGreedPressed;
		_secureButton.Pressed += OnSecurePressed;
		_returnButton.Pressed += OnReturnPressed;

		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		var localParticipant = conn.Db.RiskyBusinessParticipant.PlayerId.Find(localId);
		if (localParticipant is null)
		{
			GD.PrintErr("RiskyBusinessScene: no participant found for local player, returning to shelter");
			GetTree().ChangeSceneToPacked(_shelterScene);
			return;
		}

		_localRbId = localParticipant.RiskyBusinessId;

		foreach (var p in conn.Db.RiskyBusinessParticipant.RiskyBusinessId.Filter(_localRbId))
			SpawnPlayerNode(p);

		conn.Db.RiskyBusinessParticipant.OnInsert += OnParticipantInsert;
		conn.Db.RiskyBusinessParticipant.OnUpdate += OnParticipantUpdate;
		conn.Db.RiskyBusinessParticipant.OnDelete += OnParticipantDelete;
		conn.Db.RiskyBusiness.OnUpdate += OnRiskyBusinessUpdate;

		RefreshHud();
	}

	public override void _ExitTree()
	{
		var conn = SpacetimeNetworkManager.Instance?.Conn;
		if (conn is null) return;
		conn.Db.RiskyBusinessParticipant.OnInsert -= OnParticipantInsert;
		conn.Db.RiskyBusinessParticipant.OnUpdate -= OnParticipantUpdate;
		conn.Db.RiskyBusinessParticipant.OnDelete -= OnParticipantDelete;
		conn.Db.RiskyBusiness.OnUpdate -= OnRiskyBusinessUpdate;
	}

	public override void _Process(double delta)
	{
		if (_gameEnded)
		{
			_returnTimer -= (float)delta;
			if (_returnTimer <= 0f)
				_resultsPanel.Visible = true;
			return;
		}

		UpdateTimerDisplay();
	}

	private void UpdateTimerDisplay()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var rb = conn.Db.RiskyBusiness.Id.Find(_localRbId);
		if (rb is null) return;

		long elapsedMs = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
			- (long)(rb.StartedAt.MicrosecondsSinceUnixEpoch / 1000);
		int remainingSeconds = (int)rb.DurationSeconds - (int)(elapsedMs / 1000);
		if (remainingSeconds < 0) remainingSeconds = 0;
		_timerLabel.Text = $"Time: {remainingSeconds}";
	}

	// ── Player spawning ─────────────────────────────────────────

	private void SpawnPlayerNode(RiskyBusinessParticipant p)
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

		var lootLabel = new Label();
		lootLabel.Text = $"Loot: {p.CurrentLoot}  (x{p.LootMultiplier:F1})";
		lootLabel.HorizontalAlignment = HorizontalAlignment.Center;
		lootLabel.Position = new Vector2(-50, 20);
		lootLabel.Size = new Vector2(100, 20);
		lootLabel.AddThemeFontSizeOverride("font_size", 10);
		lootLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.4f));
		node.AddChild(lootLabel);
		_playerLootLabels[p.PlayerId] = lootLabel;

		_playerNodes[p.PlayerId] = node;
	}

	// ── Participant callbacks ───────────────────────────────────

	private void OnParticipantInsert(EventContext ctx, RiskyBusinessParticipant p)
	{
		if (p.RiskyBusinessId != _localRbId) return;
		CallDeferred(nameof(DeferredSpawnPlayer), p.PlayerId.ToString());
	}

	private void DeferredSpawnPlayer(string playerIdStr)
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		foreach (var p in conn.Db.RiskyBusinessParticipant.RiskyBusinessId.Filter(_localRbId))
		{
			if (p.PlayerId.ToString() == playerIdStr)
			{
				SpawnPlayerNode(p);
				break;
			}
		}
	}

	private void OnParticipantUpdate(EventContext ctx, RiskyBusinessParticipant oldP, RiskyBusinessParticipant newP)
	{
		if (newP.RiskyBusinessId != _localRbId) return;
		CallDeferred(nameof(DeferredRefreshHud));

		bool greedFail = newP.CurrentLoot == 0 && oldP.CurrentLoot > 0
			&& newP.LootMultiplier < oldP.LootMultiplier;

		if (greedFail)
		{
			CallDeferred(nameof(PlayGreedFailEffect), newP.PlayerId.ToString());
		}
		else if (newP.CurrentLoot < oldP.CurrentLoot && oldP.CurrentLoot > 0)
		{
			long securedAmount = (long)(oldP.CurrentLoot - newP.CurrentLoot);
			CallDeferred(nameof(SpawnFloatingSecureLabel), newP.PlayerId.ToString(), securedAmount);
		}
	}

	private void OnParticipantDelete(EventContext ctx, RiskyBusinessParticipant p)
	{
		_playerLootLabels.Remove(p.PlayerId);
		if (_playerNodes.TryGetValue(p.PlayerId, out var node))
		{
			node.QueueFree();
			_playerNodes.Remove(p.PlayerId);
		}

		if (p.PlayerId == SpacetimeNetworkManager.Instance.LocalIdentity)
		{
			if (!_gameEnded)
			{
				GetTree().ChangeSceneToPacked(_shelterScene);
			}
		}
	}

	// ── RiskyBusiness state callback ────────────────────────────

	private void OnRiskyBusinessUpdate(EventContext ctx, SpacetimeDB.Types.RiskyBusiness oldRb, SpacetimeDB.Types.RiskyBusiness newRb)
	{
		if (newRb.Id != _localRbId) return;

		CallDeferred(nameof(DeferredRefreshHud));

		if (newRb.State == RiskyBusinessState.Completed && oldRb.State != newRb.State)
		{
			CallDeferred(nameof(DeferredShowResults), (long)newRb.RecoveredLoot);
		}
	}

	private void DeferredShowResults(long recoveredLoot)
	{
		_gameEnded = true;
		_returnTimer = RETURN_DELAY;

		_lootSafelyButton.Disabled = true;
		_greedButton.Disabled = true;
		_secureButton.Disabled = true;

		_resultTitle.Text = "Risky Business Complete!";
		_resultDetails.Text = $"Total Recovered Loot: {recoveredLoot}";
	}

	// ── Visual effects ──────────────────────────────────────────

	private void SpawnFloatingSecureLabel(string playerIdStr, long amount)
	{
		Player node = null;
		foreach (var (id, n) in _playerNodes)
		{
			if (id.ToString() == playerIdStr) { node = n; break; }
		}
		if (node is null) return;

		int fontSize = Mathf.Clamp(16 + (int)(amount / 100), 16, 64);

		var label = new Label();
		label.Text = $"+{amount}";
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.Position = node.Position - new Vector2(40, 30);
		label.ZIndex = 100;
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.2f));
		label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0));
		label.AddThemeConstantOverride("outline_size", 8);
		_worldRoot.AddChild(label);

		var tween = label.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(label, "position:y", node.Position.Y - 80, 1.2);
		tween.TweenProperty(label, "modulate:a", 0.0f, 1.2);
		tween.SetParallel(false);
		tween.TweenCallback(Callable.From(label.QueueFree));
	}

	private void PlayGreedFailEffect(string playerIdStr)
	{
		Player node = null;
		foreach (var (id, n) in _playerNodes)
		{
			if (id.ToString() == playerIdStr) { node = n; break; }
		}
		if (node is null) return;

		bool isLocal = false;
		foreach (var (id, _) in _playerNodes)
		{
			if (id.ToString() == playerIdStr && id == SpacetimeNetworkManager.Instance.LocalIdentity)
			{ isLocal = true; break; }
		}

		node.Modulate = new Color(1f, 0.3f, 0.3f);

		if (isLocal)
		{
			_stunned = true;
			_lootSafelyButton.Disabled = true;
			_greedButton.Disabled = true;
			_secureButton.Disabled = true;
		}

		var tween = node.CreateTween();
		tween.TweenProperty(node, "rotation_degrees", 90.0f, 0.3);
		tween.TweenInterval(2.4);
		tween.TweenProperty(node, "rotation_degrees", 0.0f, 0.3);
		tween.TweenCallback(Callable.From(() =>
		{
			node.Modulate = Colors.White;
			if (isLocal)
			{
				_stunned = false;
				if (!_gameEnded)
				{
					_lootSafelyButton.Disabled = false;
					_greedButton.Disabled = false;
					_secureButton.Disabled = false;
				}
			}
		}));
	}

	// ── Button handlers ─────────────────────────────────────────

	private void OnLootSafelyPressed()
	{
		if (_stunned) return;
		SpacetimeNetworkManager.Instance.Conn.Reducers.RbLootSafely();
	}

	private void OnGreedPressed()
	{
		if (_stunned) return;
		SpacetimeNetworkManager.Instance.Conn.Reducers.RbGreedForLoot();
	}

	private void OnSecurePressed()
	{
		if (_stunned) return;
		SpacetimeNetworkManager.Instance.Conn.Reducers.RbSecureLoot();
	}

	private void OnReturnPressed()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;
		if (conn.Db.RiskyBusinessParticipant.PlayerId.Find(localId) is not null)
		{
			try { conn.Reducers.LeaveRiskyBusiness(); } catch { }
		}
		GetTree().ChangeSceneToPacked(_shelterScene);
	}

	// ── HUD ─────────────────────────────────────────────────────

	private void RefreshHud()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;
		var localId = SpacetimeNetworkManager.Instance.LocalIdentity;

		var rb = conn.Db.RiskyBusiness.Id.Find(_localRbId);
		if (rb is not null)
		{
			_recoveredLootLabel.Text = $"Recovered Loot: {rb.RecoveredLoot}";
		}

		var localParticipant = conn.Db.RiskyBusinessParticipant.PlayerId.Find(localId);
		if (localParticipant is not null)
		{
			_lootLabel.Text = $"Loot: {localParticipant.CurrentLoot}";
			_multiplierLabel.Text = $"Multiplier: x{localParticipant.LootMultiplier:F1}";
		}

		RefreshPlayerLootLabels();
	}

	private void RefreshPlayerLootLabels()
	{
		var conn = SpacetimeNetworkManager.Instance.Conn;

		foreach (var p in conn.Db.RiskyBusinessParticipant.RiskyBusinessId.Filter(_localRbId))
		{
			if (_playerLootLabels.TryGetValue(p.PlayerId, out var label))
				label.Text = $"Loot: {p.CurrentLoot}  (x{p.LootMultiplier:F1})";
		}
	}

	private void DeferredRefreshHud() => RefreshHud();
}
