using SpacetimeDB;

[SpacetimeDB.Type]
public enum AdventureState : byte
{
    WaitingToStart,
    InProgress,
    BetweenWaves,
    Completed,
    Failed
}

public static partial class Module
{
    private const float DEFAULT_ARENA_WIDTH = 2000f;
    private const float DEFAULT_ARENA_HEIGHT = 1500f;
    private const float ZOMBIE_CONTACT_RADIUS = 40f;
    private const float PLAYER_ATTACK_RANGE = 150f;
    private const int TICK_MS = 200;
    private const int BETWEEN_WAVES_MS = 3000;
    private const float ZOMBIE_SEPARATION_RADIUS = 30f;
    private const float ZOMBIE_SEPARATION_FORCE = 15f;

    // ── Tables ──────────────────────────────────────────────────────

    [SpacetimeDB.Table(Accessor = "Adventure", Public = true)]
    public partial struct Adventure
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        public ulong PartyId;
        public uint CurrentWave;
        public uint WaveZombiesRemaining;
        public uint WaveZombiesTotal;
        public AdventureState State;
        public Timestamp StartedAt;
        public Timestamp WaveStartedAt;
        public float ArenaWidth;
        public float ArenaHeight;
    }

    [SpacetimeDB.Table(Accessor = "AdventureParticipant", Public = true)]
    public partial struct AdventureParticipant
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public ulong AdventureId;

        [SpacetimeDB.Unique]
        public Identity PlayerId;

        public float PosX;
        public float PosY;
        public int Health;
        public int MaxHealth;
        public bool Alive;
    }

    [SpacetimeDB.Table(Accessor = "AdventureZombie", Public = true)]
    public partial struct AdventureZombie
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public ulong AdventureId;

        public float PosX;
        public float PosY;
        public int Health;
        public int MaxHealth;
        public float Speed;
        public bool Alive;
    }

    [SpacetimeDB.Table(Accessor = "AdventureTick", Scheduled = nameof(ProcessAdventureTick))]
    public partial struct AdventureTick
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong ScheduleId;

        public ScheduleAt ScheduledAt;
        public ulong AdventureId;
    }

    [SpacetimeDB.Table(Accessor = "AdventureReward", Public = true)]
    public partial struct AdventureReward
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public ulong AdventureId;

        [SpacetimeDB.Index.BTree]
        public Identity PlayerId;

        public ulong XpGranted;
        public uint WavesCompleted;
    }

    // ── Reducers ────────────────────────────────────────────────────

    [SpacetimeDB.Reducer]
    public static void StartAdventure(ReducerContext ctx)
    {
        if (ctx.Db.PartyMember.PlayerId.Find(ctx.Sender) is not PartyMember membership)
            throw new Exception("You must be in a party to start an adventure");

        if (ctx.Db.Party.Id.Find(membership.PartyId) is not Party party)
            throw new Exception("Party not found");

        if (party.LeaderId != ctx.Sender)
            throw new Exception("Only the party leader can start an adventure");

        var members = new List<PartyMember>();
        foreach (var m in ctx.Db.PartyMember.PartyId.Filter(membership.PartyId))
        {
            if (ctx.Db.AdventureParticipant.PlayerId.Find(m.PlayerId) is not null)
                throw new Exception("A party member is already in an adventure");
            members.Add(m);
        }

        var adventure = ctx.Db.Adventure.Insert(new Adventure
        {
            Id = 0,
            PartyId = membership.PartyId,
            CurrentWave = 0,
            WaveZombiesRemaining = 0,
            WaveZombiesTotal = 0,
            State = AdventureState.WaitingToStart,
            StartedAt = ctx.Timestamp,
            WaveStartedAt = ctx.Timestamp,
            ArenaWidth = DEFAULT_ARENA_WIDTH,
            ArenaHeight = DEFAULT_ARENA_HEIGHT
        });

        float centerX = DEFAULT_ARENA_WIDTH / 2f;
        float centerY = DEFAULT_ARENA_HEIGHT / 2f;

        foreach (var m in members)
        {
            int maxHp = Math.Max(1, GetStat(ctx, m.PlayerId, StatType.MaxHealth));
            ctx.Db.AdventureParticipant.Insert(new AdventureParticipant
            {
                Id = 0,
                AdventureId = adventure.Id,
                PlayerId = m.PlayerId,
                PosX = centerX,
                PosY = centerY,
                Health = maxHp,
                MaxHealth = maxHp,
                Alive = true
            });
        }

        ctx.Db.AdventureTick.Insert(new AdventureTick
        {
            ScheduleId = 0,
            ScheduledAt = new ScheduleAt.Time(ctx.Timestamp + TimeSpan.FromSeconds(2)),
            AdventureId = adventure.Id
        });
    }

    [SpacetimeDB.Reducer]
    public static void UpdateAdventurePosition(ReducerContext ctx, float posX, float posY)
    {
        if (ctx.Db.AdventureParticipant.PlayerId.Find(ctx.Sender) is not AdventureParticipant participant)
            throw new Exception("You are not in an adventure");

        if (!participant.Alive)
            return;

        if (ctx.Db.Adventure.Id.Find(participant.AdventureId) is not Adventure adventure)
            return;

        float clampedX = Math.Clamp(posX, 0f, adventure.ArenaWidth);
        float clampedY = Math.Clamp(posY, 0f, adventure.ArenaHeight);

        ctx.Db.AdventureParticipant.Id.Update(participant with
        {
            PosX = clampedX,
            PosY = clampedY
        });
    }

    [SpacetimeDB.Reducer]
    public static void AdventureAttackZombie(ReducerContext ctx, ulong zombieId)
    {
        if (ctx.Db.AdventureParticipant.PlayerId.Find(ctx.Sender) is not AdventureParticipant participant)
            throw new Exception("You are not in an adventure");

        if (!participant.Alive)
            throw new Exception("You are dead");

        if (ctx.Db.AdventureZombie.Id.Find(zombieId) is not AdventureZombie zombie)
            throw new Exception("Zombie not found");

        if (!zombie.Alive)
            return;

        if (zombie.AdventureId != participant.AdventureId)
            throw new Exception("Zombie is not in your adventure");

        float dx = participant.PosX - zombie.PosX;
        float dy = participant.PosY - zombie.PosY;
        float distSq = dx * dx + dy * dy;
        if (distSq > PLAYER_ATTACK_RANGE * PLAYER_ATTACK_RANGE)
            throw new Exception("Zombie is out of range");

        int damage = Math.Max(1, GetStat(ctx, ctx.Sender, StatType.Strength));
        int newHealth = zombie.Health - damage;

        if (newHealth <= 0)
        {
            ctx.Db.AdventureZombie.Id.Update(zombie with { Health = 0, Alive = false });

            if (ctx.Db.Adventure.Id.Find(participant.AdventureId) is Adventure adventure)
            {
                ctx.Db.Adventure.Id.Update(adventure with
                {
                    WaveZombiesRemaining = adventure.WaveZombiesRemaining > 0
                        ? adventure.WaveZombiesRemaining - 1
                        : 0
                });
            }
        }
        else
        {
            ctx.Db.AdventureZombie.Id.Update(zombie with { Health = newHealth });
        }
    }

    [SpacetimeDB.Reducer]
    public static void LeaveAdventure(ReducerContext ctx)
    {
        if (ctx.Db.AdventureParticipant.PlayerId.Find(ctx.Sender) is not AdventureParticipant participant)
            throw new Exception("You are not in an adventure");

        ctx.Db.AdventureParticipant.Id.Update(participant with { Alive = false });

        CheckAllParticipantsDead(ctx, participant.AdventureId);
    }

    // ── Server Tick ─────────────────────────────────────────────────

    [SpacetimeDB.Reducer]
    public static void ProcessAdventureTick(ReducerContext ctx, AdventureTick tick)
    {
        if (ctx.Db.Adventure.Id.Find(tick.AdventureId) is not Adventure adventure)
            return;

        if (adventure.State == AdventureState.Completed || adventure.State == AdventureState.Failed)
            return;

        switch (adventure.State)
        {
            case AdventureState.WaitingToStart:
            {
                uint wave = 1;
                ctx.Db.Adventure.Id.Update(adventure with
                {
                    State = AdventureState.InProgress,
                    CurrentWave = wave,
                    WaveStartedAt = ctx.Timestamp
                });
                SpawnWave(ctx, adventure.Id, wave);
                break;
            }
            case AdventureState.InProgress:
            {
                TickGameplay(ctx, adventure);
                break;
            }
            case AdventureState.BetweenWaves:
            {
                long elapsedMs = (ctx.Timestamp.MicrosecondsSinceUnixEpoch - adventure.WaveStartedAt.MicrosecondsSinceUnixEpoch) / 1000;
                if (elapsedMs >= BETWEEN_WAVES_MS)
                {
                    uint nextWave = adventure.CurrentWave + 1;
                    ctx.Db.Adventure.Id.Update(adventure with
                    {
                        State = AdventureState.InProgress,
                        CurrentWave = nextWave,
                        WaveStartedAt = ctx.Timestamp
                    });
                    SpawnWave(ctx, adventure.Id, nextWave);
                }
                break;
            }
        }

        ScheduleNextTick(ctx, adventure.Id);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static void ScheduleNextTick(ReducerContext ctx, ulong adventureId)
    {
        ctx.Db.AdventureTick.Insert(new AdventureTick
        {
            ScheduleId = 0,
            ScheduledAt = new ScheduleAt.Time(ctx.Timestamp + TimeSpan.FromMilliseconds(TICK_MS)),
            AdventureId = adventureId
        });
    }

    private static void SpawnWave(ReducerContext ctx, ulong adventureId, uint waveNumber)
    {
        if (ctx.Db.Adventure.Id.Find(adventureId) is not Adventure adventure)
            return;

        int count = (int)(3 + waveNumber * 2);
        float speed = 40f + waveNumber * 5f;
        int hp = (int)(waveNumber + 1);

        var rng = new Random();

        for (int i = 0; i < count; i++)
        {
            float posX, posY;
            int edge = rng.Next(4);
            switch (edge)
            {
                case 0: // top
                    posX = (float)(rng.NextDouble() * adventure.ArenaWidth);
                    posY = 0f;
                    break;
                case 1: // bottom
                    posX = (float)(rng.NextDouble() * adventure.ArenaWidth);
                    posY = adventure.ArenaHeight;
                    break;
                case 2: // left
                    posX = 0f;
                    posY = (float)(rng.NextDouble() * adventure.ArenaHeight);
                    break;
                default: // right
                    posX = adventure.ArenaWidth;
                    posY = (float)(rng.NextDouble() * adventure.ArenaHeight);
                    break;
            }

            ctx.Db.AdventureZombie.Insert(new AdventureZombie
            {
                Id = 0,
                AdventureId = adventureId,
                PosX = posX,
                PosY = posY,
                Health = hp,
                MaxHealth = hp,
                Speed = speed,
                Alive = true
            });
        }

        ctx.Db.Adventure.Id.Update(adventure with
        {
            WaveZombiesRemaining = (uint)count,
            WaveZombiesTotal = (uint)count
        });
    }

    private static void TickGameplay(ReducerContext ctx, Adventure adventure)
    {
        var alivePlayers = new List<AdventureParticipant>();
        foreach (var p in ctx.Db.AdventureParticipant.AdventureId.Filter(adventure.Id))
            if (p.Alive)
                alivePlayers.Add(p);

        if (alivePlayers.Count == 0)
        {
            EndAdventure(ctx, adventure.Id, AdventureState.Failed);
            return;
        }

        bool anyZombieAlive = false;
        foreach (var zombie in ctx.Db.AdventureZombie.AdventureId.Filter(adventure.Id))
        {
            if (!zombie.Alive)
                continue;

            if (alivePlayers.Count == 0)
                break;

            anyZombieAlive = true;

            float nearestDistSq = float.MaxValue;
            AdventureParticipant nearestPlayer = alivePlayers[0];
            foreach (var player in alivePlayers)
            {
                float dx = player.PosX - zombie.PosX;
                float dy = player.PosY - zombie.PosY;
                float dSq = dx * dx + dy * dy;
                if (dSq < nearestDistSq)
                {
                    nearestDistSq = dSq;
                    nearestPlayer = player;
                }
            }

            float dirX = nearestPlayer.PosX - zombie.PosX;
            float dirY = nearestPlayer.PosY - zombie.PosY;
            float dist = MathF.Sqrt(dirX * dirX + dirY * dirY);

            float newX = zombie.PosX;
            float newY = zombie.PosY;

            if (dist > 1f)
            {
                float step = zombie.Speed * (TICK_MS / 1000f);
                newX += (dirX / dist) * step;
                newY += (dirY / dist) * step;
                newX = Math.Clamp(newX, 0f, adventure.ArenaWidth);
                newY = Math.Clamp(newY, 0f, adventure.ArenaHeight);
            }

            ctx.Db.AdventureZombie.Id.Update(zombie with { PosX = newX, PosY = newY });

            for (int i = alivePlayers.Count - 1; i >= 0; i--)
            {
                var player = alivePlayers[i];
                float cdx = player.PosX - newX;
                float cdy = player.PosY - newY;
                float contactDistSq = cdx * cdx + cdy * cdy;
                if (contactDistSq < ZOMBIE_CONTACT_RADIUS * ZOMBIE_CONTACT_RADIUS)
                {
                    int newHp = player.Health - 1;
                    if (newHp <= 0)
                    {
                        ctx.Db.AdventureParticipant.Id.Update(player with { Health = 0, Alive = false });
                        alivePlayers.RemoveAt(i);
                    }
                    else
                    {
                        var updated = player with { Health = newHp };
                        ctx.Db.AdventureParticipant.Id.Update(updated);
                        alivePlayers[i] = updated;
                    }
                    break;
                }
            }
        }

        if (alivePlayers.Count == 0)
        {
            EndAdventure(ctx, adventure.Id, AdventureState.Failed);
            return;
        }

        var zombiePositions = new List<(ulong Id, float X, float Y)>();
        foreach (var z in ctx.Db.AdventureZombie.AdventureId.Filter(adventure.Id))
            if (z.Alive) zombiePositions.Add((z.Id, z.PosX, z.PosY));

        for (int i = 0; i < zombiePositions.Count; i++)
        {
            float pushX = 0, pushY = 0;
            var a = zombiePositions[i];
            for (int j = 0; j < zombiePositions.Count; j++)
            {
                if (i == j) continue;
                var b = zombiePositions[j];
                float dx = a.X - b.X, dy = a.Y - b.Y;
                float dSq = dx * dx + dy * dy;
                if (dSq < ZOMBIE_SEPARATION_RADIUS * ZOMBIE_SEPARATION_RADIUS && dSq > 0.01f)
                {
                    float d = MathF.Sqrt(dSq);
                    float overlap = ZOMBIE_SEPARATION_RADIUS - d;
                    pushX += (dx / d) * overlap * 0.5f;
                    pushY += (dy / d) * overlap * 0.5f;
                }
            }
            if (pushX != 0 || pushY != 0)
            {
                float nx = Math.Clamp(a.X + pushX, 0f, adventure.ArenaWidth);
                float ny = Math.Clamp(a.Y + pushY, 0f, adventure.ArenaHeight);
                if (ctx.Db.AdventureZombie.Id.Find(a.Id) is AdventureZombie z)
                    ctx.Db.AdventureZombie.Id.Update(z with { PosX = nx, PosY = ny });
            }
        }

        var freshAdventure = ctx.Db.Adventure.Id.Find(adventure.Id);
        if (freshAdventure is Adventure adv && adv.WaveZombiesRemaining == 0 && !anyZombieAlive)
        {
            ctx.Db.Adventure.Id.Update(adv with
            {
                State = AdventureState.BetweenWaves,
                WaveStartedAt = ctx.Timestamp
            });
        }
    }

    private static void CheckAllParticipantsDead(ReducerContext ctx, ulong adventureId)
    {
        foreach (var p in ctx.Db.AdventureParticipant.AdventureId.Filter(adventureId))
            if (p.Alive) return;

        EndAdventure(ctx, adventureId, AdventureState.Failed);
    }

    private static void EndAdventure(ReducerContext ctx, ulong adventureId, AdventureState state)
    {
        if (ctx.Db.Adventure.Id.Find(adventureId) is not Adventure adventure)
            return;

        if (adventure.State == AdventureState.Completed || adventure.State == AdventureState.Failed)
            return;

        ctx.Db.Adventure.Id.Update(adventure with { State = state });

        uint wavesCompleted = adventure.CurrentWave > 0 ? adventure.CurrentWave - 1 : 0;
        if (state == AdventureState.Completed)
            wavesCompleted = adventure.CurrentWave;

        ulong xpReward = (ulong)wavesCompleted * 10;

        foreach (var p in ctx.Db.AdventureParticipant.AdventureId.Filter(adventureId))
        {
            ctx.Db.AdventureReward.Insert(new AdventureReward
            {
                Id = 0,
                AdventureId = adventureId,
                PlayerId = p.PlayerId,
                XpGranted = xpReward,
                WavesCompleted = wavesCompleted
            });

            if (xpReward > 0)
                GrantExperience(ctx, p.PlayerId, xpReward);
        }

        foreach (var z in ctx.Db.AdventureZombie.AdventureId.Filter(adventureId))
            ctx.Db.AdventureZombie.Id.Delete(z.Id);

        foreach (var p in ctx.Db.AdventureParticipant.AdventureId.Filter(adventureId))
            ctx.Db.AdventureParticipant.Id.Delete(p.Id);
    }

    public static void HandleAdventureDisconnect(ReducerContext ctx, Identity playerId)
    {
        if (ctx.Db.AdventureParticipant.PlayerId.Find(playerId) is AdventureParticipant participant)
        {
            ctx.Db.AdventureParticipant.Id.Update(participant with { Alive = false });
            CheckAllParticipantsDead(ctx, participant.AdventureId);
        }
    }
}
