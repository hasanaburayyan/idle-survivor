using SpacetimeDB;

[SpacetimeDB.Type]
public enum RiskyBusinessState : byte
{
    WaitingToStart,
    InProgress,
    Completed
}

public static partial class Module
{
    private const float RB_ARENA_WIDTH = 800f;
    private const float RB_ARENA_HEIGHT = 400f;
    private const ulong RB_BASE_LOOT = 10;
    private const double RB_FAIL_CHANCE_PER_MULT = 5.0;
    private const float RB_PLAYER_SPACING = 120f;
    private const uint RB_DURATION_SECONDS = 60;

    // ── Tables ──────────────────────────────────────────────────────

    [SpacetimeDB.Table(Accessor = "RiskyBusiness", Public = true)]
    public partial struct RiskyBusiness
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        public ulong PartyId;
        public RiskyBusinessState State;
        public ulong RecoveredLoot;
        public Timestamp StartedAt;
        public uint DurationSeconds;
    }

    [SpacetimeDB.Table(Accessor = "RiskyBusinessParticipant", Public = true)]
    public partial struct RiskyBusinessParticipant
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public ulong RiskyBusinessId;

        [SpacetimeDB.Unique]
        public Identity PlayerId;

        public ulong CurrentLoot;
        public float LootMultiplier;
        public float PosX;
        public float PosY;
    }

    [SpacetimeDB.Table(Accessor = "RiskyBusinessTick", Scheduled = nameof(ProcessRiskyBusinessTick))]
    public partial struct RiskyBusinessTick
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong ScheduleId;

        public ScheduleAt ScheduledAt;
        public ulong RiskyBusinessId;
    }

    // ── Reducers ────────────────────────────────────────────────────

    [SpacetimeDB.Reducer]
    public static void StartRiskyBusiness(ReducerContext ctx)
    {
        if (ctx.Db.PartyMember.PlayerId.Find(ctx.Sender) is not PartyMember membership)
            throw new Exception("You must be in a party to start Risky Business");

        if (ctx.Db.Party.Id.Find(membership.PartyId) is not Party party)
            throw new Exception("Party not found");

        if (party.LeaderId != ctx.Sender)
            throw new Exception("Only the party leader can start Risky Business");

        var members = new List<PartyMember>();
        foreach (var m in ctx.Db.PartyMember.PartyId.Filter(membership.PartyId))
        {
            if (ctx.Db.AdventureParticipant.PlayerId.Find(m.PlayerId) is not null)
                throw new Exception("A party member is already in an adventure");
            if (ctx.Db.RiskyBusinessParticipant.PlayerId.Find(m.PlayerId) is not null)
                throw new Exception("A party member is already in Risky Business");
            members.Add(m);
        }

        var rb = ctx.Db.RiskyBusiness.Insert(new RiskyBusiness
        {
            Id = 0,
            PartyId = membership.PartyId,
            State = RiskyBusinessState.InProgress,
            RecoveredLoot = 0,
            StartedAt = ctx.Timestamp,
            DurationSeconds = RB_DURATION_SECONDS
        });

        float centerX = RB_ARENA_WIDTH / 2f;
        float centerY = RB_ARENA_HEIGHT / 2f;
        float totalWidth = (members.Count - 1) * RB_PLAYER_SPACING;
        float startX = centerX - totalWidth / 2f;

        for (int i = 0; i < members.Count; i++)
        {
            ctx.Db.RiskyBusinessParticipant.Insert(new RiskyBusinessParticipant
            {
                Id = 0,
                RiskyBusinessId = rb.Id,
                PlayerId = members[i].PlayerId,
                CurrentLoot = 0,
                LootMultiplier = 1.0f,
                PosX = startX + i * RB_PLAYER_SPACING,
                PosY = centerY
            });
        }

        ctx.Db.RiskyBusinessTick.Insert(new RiskyBusinessTick
        {
            ScheduleId = 0,
            ScheduledAt = new ScheduleAt.Time(ctx.Timestamp + TimeSpan.FromSeconds(RB_DURATION_SECONDS)),
            RiskyBusinessId = rb.Id
        });
    }

    [SpacetimeDB.Reducer]
    public static void RbLootSafely(ReducerContext ctx)
    {
        if (ctx.Db.RiskyBusinessParticipant.PlayerId.Find(ctx.Sender) is not RiskyBusinessParticipant participant)
            throw new Exception("You are not in Risky Business");

        if (ctx.Db.RiskyBusiness.Id.Find(participant.RiskyBusinessId) is not RiskyBusiness rb)
            throw new Exception("Session not found");

        if (rb.State != RiskyBusinessState.InProgress)
            throw new Exception("Game is not in progress");

        ulong loot = (ulong)Math.Floor(RB_BASE_LOOT * (double)participant.LootMultiplier);

        ctx.Db.RiskyBusinessParticipant.Id.Update(participant with
        {
            CurrentLoot = participant.CurrentLoot + loot,
            LootMultiplier = participant.LootMultiplier + 0.1f
        });
    }

    [SpacetimeDB.Reducer]
    public static void RbGreedForLoot(ReducerContext ctx)
    {
        if (ctx.Db.RiskyBusinessParticipant.PlayerId.Find(ctx.Sender) is not RiskyBusinessParticipant participant)
            throw new Exception("You are not in Risky Business");

        if (ctx.Db.RiskyBusiness.Id.Find(participant.RiskyBusinessId) is not RiskyBusiness rb)
            throw new Exception("Session not found");

        if (rb.State != RiskyBusinessState.InProgress)
            throw new Exception("Game is not in progress");

        double failChance = RB_FAIL_CHANCE_PER_MULT * participant.LootMultiplier;
        var seed = (int)(ctx.Timestamp.MicrosecondsSinceUnixEpoch % int.MaxValue);
        var rng = new Random(seed);
        double roll = rng.NextDouble() * 100.0;

        if (roll < failChance)
        {
            ulong secured = (ulong)Math.Floor(participant.CurrentLoot * 0.05);
            ctx.Db.RiskyBusinessParticipant.Id.Update(participant with
            {
                CurrentLoot = 0,
                LootMultiplier = 1.0f
            });
            ctx.Db.RiskyBusiness.Id.Update(rb with
            {
                RecoveredLoot = rb.RecoveredLoot + secured
            });
        }
        else
        {
            ulong loot = (ulong)Math.Floor(RB_BASE_LOOT * 10.0 * (double)participant.LootMultiplier);
            ctx.Db.RiskyBusinessParticipant.Id.Update(participant with
            {
                CurrentLoot = participant.CurrentLoot + loot,
                LootMultiplier = participant.LootMultiplier + 1.0f
            });
        }
    }

    [SpacetimeDB.Reducer]
    public static void RbSecureLoot(ReducerContext ctx)
    {
        if (ctx.Db.RiskyBusinessParticipant.PlayerId.Find(ctx.Sender) is not RiskyBusinessParticipant participant)
            throw new Exception("You are not in Risky Business");

        if (ctx.Db.RiskyBusiness.Id.Find(participant.RiskyBusinessId) is not RiskyBusiness rb)
            throw new Exception("Session not found");

        if (rb.State != RiskyBusinessState.InProgress)
            throw new Exception("Game is not in progress");

        if (participant.CurrentLoot == 0)
            return;

        ctx.Db.RiskyBusiness.Id.Update(rb with
        {
            RecoveredLoot = rb.RecoveredLoot + participant.CurrentLoot
        });

        ctx.Db.RiskyBusinessParticipant.Id.Update(participant with
        {
            CurrentLoot = 0
        });
    }

    [SpacetimeDB.Reducer]
    public static void LeaveRiskyBusiness(ReducerContext ctx)
    {
        if (ctx.Db.RiskyBusinessParticipant.PlayerId.Find(ctx.Sender) is not RiskyBusinessParticipant participant)
            throw new Exception("You are not in Risky Business");

        ctx.Db.RiskyBusinessParticipant.Id.Delete(participant.Id);

        CheckRbAllLeft(ctx, participant.RiskyBusinessId);
    }

    // ── Server Tick ─────────────────────────────────────────────────

    [SpacetimeDB.Reducer]
    public static void ProcessRiskyBusinessTick(ReducerContext ctx, RiskyBusinessTick tick)
    {
        if (ctx.Db.RiskyBusiness.Id.Find(tick.RiskyBusinessId) is not RiskyBusiness rb)
            return;

        if (rb.State == RiskyBusinessState.Completed)
            return;

        EndRiskyBusiness(ctx, rb.Id);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static void CheckRbAllLeft(ReducerContext ctx, ulong rbId)
    {
        foreach (var p in ctx.Db.RiskyBusinessParticipant.RiskyBusinessId.Filter(rbId))
            return;

        EndRiskyBusiness(ctx, rbId);
    }

    private static void EndRiskyBusiness(ReducerContext ctx, ulong rbId)
    {
        if (ctx.Db.RiskyBusiness.Id.Find(rbId) is not RiskyBusiness rb)
            return;

        if (rb.State == RiskyBusinessState.Completed)
            return;

        ulong totalSecured = rb.RecoveredLoot;
        foreach (var p in ctx.Db.RiskyBusinessParticipant.RiskyBusinessId.Filter(rbId))
        {
            totalSecured += p.CurrentLoot;
        }

        ctx.Db.RiskyBusiness.Id.Update(rb with
        {
            State = RiskyBusinessState.Completed,
            RecoveredLoot = totalSecured
        });

        foreach (var p in ctx.Db.RiskyBusinessParticipant.RiskyBusinessId.Filter(rbId))
            ctx.Db.RiskyBusinessParticipant.Id.Delete(p.Id);
    }

    public static void HandleRiskyBusinessDisconnect(ReducerContext ctx, Identity playerId)
    {
        if (ctx.Db.RiskyBusinessParticipant.PlayerId.Find(playerId) is not RiskyBusinessParticipant participant)
            return;

        ctx.Db.RiskyBusinessParticipant.Id.Delete(participant.Id);
        CheckRbAllLeft(ctx, participant.RiskyBusinessId);
    }
}
