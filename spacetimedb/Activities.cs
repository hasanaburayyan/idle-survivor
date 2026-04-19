using SpacetimeDB;

[SpacetimeDB.Type]
public enum ActivityType : byte
{
    Scavenge,
    ChopWood,
    Mine,
    GatherFabric,
}

[SpacetimeDB.Type]
public partial struct ActivityCost {
    public ResourceType Type;
    public ulong Amount;
}

public static partial class Module
{
    [SpacetimeDB.Table(Accessor = "Activity", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "by_activity_participant_type", Columns = new [] {"Participant", "Type"})]
    public partial struct Activity {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;
        [SpacetimeDB.Index.BTree]
        public Identity Participant;
        public ActivityType Type;
        public List<ActivityCost> Cost;
        public ulong DurationMs;
        public LocationType? RequiredLocation;
        public uint? RequiredLevel;
        public string? RequiredStructure;
        public ulong? RequiredSkillId;
        public ulong? RequiredSkillTreeNodeId;
        [SpacetimeDB.Default(1)]
        public uint Level;
    }

    [SpacetimeDB.Table(Accessor = "ActiveTask", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "by_active_participant_type", Columns = new[] { "Participant", "Type" })]
    public partial struct ActiveTask {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;
        [SpacetimeDB.Index.BTree]
        public Identity Participant;
        public ActivityType Type;
        public Timestamp StartedAt;
        public Timestamp CompletesAt;
    }

    [SpacetimeDB.Table(Accessor = "TaskCompletion", Scheduled = nameof(CompleteTask))]
    public partial struct TaskCompletion {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong ScheduleId;
        public ScheduleAt ScheduledAt;
        [SpacetimeDB.Index.BTree]
        public Identity Participant;
        public ActivityType Type;
    }

    [SpacetimeDB.Table(Accessor = "ActivitySchedule", Public = true, Scheduled = nameof(ProcessScheduledActivity))]
    public partial struct ActivitySchedule
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong ScheduleId;
        public SpacetimeDB.ScheduleAt ScheduledAt;
        public ActivityType type;
        public ulong? IntervalMilliseconds;

        [SpacetimeDB.Index.BTree]
        public Identity Participant;
    }

    private static uint GetActivityLevel(ReducerContext ctx, Identity participant, ActivityType type)
    {
        var row = ctx.Db.Activity.by_activity_participant_type
            .Filter((Participant: participant, Type: type)).First();
        return row.Level;
    }

    private static ulong ScaledUpgradeCost(ulong baseAmount, uint level) =>
        baseAmount * (ulong)level * (ulong)level;

    private static void AppendNextUpgradeCosts(ActivityType type, uint currentLevel, List<ActivityCost> dest)
    {
        void Add(ResourceType rt, ulong baseAmt) =>
            dest.Add(new ActivityCost { Type = rt, Amount = ScaledUpgradeCost(baseAmt, currentLevel) });

        switch (type)
        {
            case ActivityType.Scavenge:
                // Early-game ramp: widen the cost list gradually so the first upgrade
                // is reachable in ~60s and the coupon-collector wall kicks in past L4.
                if (currentLevel <= 1)
                {
                    Add(ResourceType.Food, 5);
                }
                else if (currentLevel == 2)
                {
                    Add(ResourceType.Food, 10);
                    Add(ResourceType.Money, 10);
                }
                else if (currentLevel == 3)
                {
                    Add(ResourceType.Food, 15);
                    Add(ResourceType.Money, 15);
                    Add(ResourceType.Wood, 10);
                }
                else
                {
                    Add(ResourceType.Food, 5);
                    Add(ResourceType.Money, 5);
                    Add(ResourceType.Wood, 5);
                    Add(ResourceType.Metal, 5);
                    Add(ResourceType.Fabric, 5);
                    Add(ResourceType.Parts, 5);
                }
                break;
            case ActivityType.ChopWood:
                Add(ResourceType.Food, 5);
                Add(ResourceType.Wood, 3);
                break;
            case ActivityType.Mine:
                Add(ResourceType.Wood, 8);
                Add(ResourceType.Food, 4);
                break;
            default:
                break;
        }
    }

    private static void ValidateActivityAccessible(ReducerContext ctx, Identity participant, Player player, Activity activity)
    {
        if (!IsLocationValid(activity.RequiredLocation, player.Location))
            throw new Exception("This activity is not available at your current location");

        if (activity.RequiredLevel is uint reqLevel)
        {
            if (ctx.Db.PlayerLevel.Owner.Find(participant) is not PlayerLevel pl || pl.Level < reqLevel)
                throw new Exception($"Requires level {reqLevel}");
        }

        if (activity.RequiredStructure is string reqStruct)
        {
            bool hasStructure = false;
            foreach (var def in ctx.Db.StructureDefinition.Iter())
            {
                if (def.Name != reqStruct) continue;
                if (ctx.Db.PlayerStructure.by_owner_and_definition
                    .Filter((Owner: participant, DefinitionId: def.Id)).Any())
                {
                    hasStructure = true;
                }
                break;
            }
            if (!hasStructure)
                throw new Exception($"Requires structure: {reqStruct}");
        }

        if (activity.RequiredSkillId is ulong reqSkillId)
        {
            if (!ctx.Db.PlayerSkill.by_skill_owner_def
                .Filter((Owner: participant, SkillDefinitionId: reqSkillId)).Any())
                throw new Exception("Requires a skill you haven't learned");
        }

        if (activity.RequiredSkillTreeNodeId is ulong reqTreeNodeId)
        {
            if (GetPlayerSkillTreeLevel(ctx, participant, reqTreeNodeId) < 1)
                throw new Exception("Requires a skill tree node you haven't unlocked");
        }
    }

    private static void DeductActivityCosts(ReducerContext ctx, Identity participant, List<ActivityCost> costs)
    {
        foreach (var cost in costs)
        {
            var resource = ctx.Db.ResourceTracker.by_owner_and_type
                .Filter((Owner: participant, Type: cost.Type)).First();
            if (resource.Amount < cost.Amount)
                throw new Exception($"Insufficient {cost.Type}");
        }

        foreach (var cost in costs)
        {
            var resource = ctx.Db.ResourceTracker.by_owner_and_type
                .Filter((Owner: participant, Type: cost.Type)).First();
            resource.Amount -= cost.Amount;
            ctx.Db.ResourceTracker.Id.Update(resource);
        }
    }

    [SpacetimeDB.Reducer]
    public static void UpgradeActivity(ReducerContext ctx, ActivityType type)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
            throw new Exception("Player not found");

        var activity = ctx.Db.Activity.by_activity_participant_type
            .Filter((Participant: ctx.Sender, Type: type)).First();

        ValidateActivityAccessible(ctx, ctx.Sender, player, activity);

        var costs = new List<ActivityCost>();
        AppendNextUpgradeCosts(type, activity.Level, costs);
        if (costs.Count == 0)
            throw new Exception("This activity has no upgrades");

        DeductActivityCosts(ctx, ctx.Sender, costs);

        activity.Level += 1;
        ctx.Db.Activity.Id.Update(activity);
    }

    [SpacetimeDB.Reducer]
    public static void ProcessScheduledActivity(ReducerContext ctx, ActivitySchedule arg)
    {
        switch (arg.type)
        {
            case ActivityType.Scavenge:
                Scavenge(ctx, arg.Participant);
                break;
            case ActivityType.ChopWood:
                ChopWoodReward(ctx, arg.Participant);
                break;
            case ActivityType.Mine:
                MineReward(ctx, arg.Participant);
                break;
            case ActivityType.GatherFabric:
                GatherFabricReward(ctx, arg.Participant);
                break;
            default:
                throw new Exception("Unknown scheduled activity type");
        }

        if (arg.IntervalMilliseconds is not null)
        {
            ulong nextInterval;
            if (arg.type == ActivityType.ChopWood || arg.type == ActivityType.Mine || arg.type == ActivityType.GatherFabric)
            {
                var activityRow = ctx.Db.Activity.by_activity_participant_type
                    .Filter((Participant: arg.Participant, Type: arg.type)).FirstOrDefault();
                ulong baseMs = activityRow.Id != 0 ? activityRow.DurationMs : arg.IntervalMilliseconds.Value;
                nextInterval = GetEffectiveActivityDurationMs(ctx, arg.Participant, arg.type, baseMs);
            }
            else
            {
                nextInterval = arg.IntervalMilliseconds.Value;
            }

            ctx.Db.ActivitySchedule.Insert(new ActivitySchedule
            {
                IntervalMilliseconds = nextInterval,
                Participant = arg.Participant,
                ScheduledAt = new ScheduleAt.Time(ctx.Timestamp + TimeSpan.FromMilliseconds(nextInterval)),
                type = arg.type
            });
        }
    }

    private static StatType GetScavengeStat(ResourceType resourceType) => resourceType switch
    {
        ResourceType.Food => StatType.Perception,
        ResourceType.Fabric => StatType.Intelligence,
        ResourceType.Metal => StatType.Strength,
        ResourceType.Money => StatType.Wit,
        ResourceType.Parts => StatType.Dexterity,
        ResourceType.Wood => StatType.Endurance,
        _ => StatType.Perception
    };

    private static ulong GetScavengeAmountForResource(ReducerContext ctx, Identity participant, ResourceType resourceType)
    {
        var statValue = (ulong)GetStat(ctx, participant, GetScavengeStat(resourceType));
        // Wit → Money channel is doubled so Money flows fast enough to fuel the upgrade shop.
        if (resourceType == ResourceType.Money)
            statValue *= 2;
        return statValue;
    }

    public static void EnsureActivityLevels(ReducerContext ctx, Identity participant)
    {
        foreach (var act in ctx.Db.Activity.Participant.Filter(participant))
        {
            if (act.Level != 0)
                continue;
            ctx.Db.Activity.Id.Update(act with { Level = 1u });
        }
    }

    [SpacetimeDB.Reducer]
    public static void Scavenge(ReducerContext ctx, Identity participant)
    {
        var random = new Random();
        var pool = GetScavengePool(ctx, participant);
        var resourceType = pool[random.Next(pool.Count)];

        var level = GetActivityLevel(ctx, participant, ActivityType.Scavenge);
        var lootBonus = (ulong)GetUpgradeLevel(ctx, participant, UpgradeType.LootMultiplier);
        var amount = GetScavengeAmountForResource(ctx, participant, resourceType) + level + lootBonus;
        AddResourceToPlayer(ctx, participant, resourceType, amount);

        GrantExperience(ctx, participant, 1);
    }

    private static void ChopWoodReward(ReducerContext ctx, Identity participant)
    {
        var skillLevel = GetSkillTreeLevelForEffect(ctx, participant,
            SkillTreeEffectKind.UpgradeActivity, (uint)(byte)ActivityType.ChopWood);
        var baseAmount = (ulong)Math.Max(1, GetStat(ctx, participant, StatType.Endurance));
        AddResourceToPlayer(ctx, participant, ResourceType.Wood, baseAmount * 3 + skillLevel);

        GrantExperience(ctx, participant, 2);
    }

    private static void MineReward(ReducerContext ctx, Identity participant)
    {
        var skillLevel = GetSkillTreeLevelForEffect(ctx, participant,
            SkillTreeEffectKind.UpgradeActivity, (uint)(byte)ActivityType.Mine);
        var baseAmount = (ulong)Math.Max(1, GetStat(ctx, participant, StatType.Strength));
        AddResourceToPlayer(ctx, participant, ResourceType.Metal, baseAmount * 3 + skillLevel);

        GrantExperience(ctx, participant, 3);
    }

    private static void GatherFabricReward(ReducerContext ctx, Identity participant)
    {
        var skillLevel = GetSkillTreeLevelForEffect(ctx, participant,
            SkillTreeEffectKind.UpgradeActivity, (uint)(byte)ActivityType.GatherFabric);
        var baseAmount = (ulong)Math.Max(1, GetStat(ctx, participant, StatType.Intelligence));
        AddResourceToPlayer(ctx, participant, ResourceType.Fabric, baseAmount * 3 + skillLevel);

        GrantExperience(ctx, participant, 3);
    }

    [SpacetimeDB.Reducer]
    public static void ActivityOnInterval(ReducerContext ctx, Identity participant, ActivityType type, ulong interval, bool reoccuring)
    {
        ctx.Db.ActivitySchedule.Insert(new ActivitySchedule
        {
            IntervalMilliseconds = reoccuring ? interval : null,
            Participant = participant,
            ScheduledAt = new ScheduleAt.Time(ctx.Timestamp + TimeSpan.FromMilliseconds(interval)),
            type = type
        });
    }

    [SpacetimeDB.Reducer]
    public static void RemoveAllScheduledEventsForParticipant(ReducerContext ctx, Identity participant) {
        var scheduledEvents = ctx.Db.ActivitySchedule.Participant.Filter(participant) ?? throw new Exception("No scheduled events to remove");
        foreach (var scheduledEvent in scheduledEvents) {
            ctx.Db.ActivitySchedule.Delete(scheduledEvent);
        }
    }

    [SpacetimeDB.Reducer]
    public static void StartShelterSchedules(ReducerContext ctx, Identity participant) {
        ActivityOnInterval(ctx, participant, ActivityType.Scavenge, 5000, true);
        ScheduleAutoActivityIfEnabled(ctx, participant, ActivityType.ChopWood);
        ScheduleAutoActivityIfEnabled(ctx, participant, ActivityType.Mine);
        ScheduleAutoActivityIfEnabled(ctx, participant, ActivityType.GatherFabric);
    }

    private static void ScheduleAutoActivityIfEnabled(ReducerContext ctx, Identity participant, ActivityType type)
    {
        if (!HasAutoActivity(ctx, participant, type)) return;
        var activityRow = ctx.Db.Activity.by_activity_participant_type
            .Filter((Participant: participant, Type: type)).FirstOrDefault();
        ulong baseMs = activityRow.Id != 0 ? activityRow.DurationMs : 3000UL;
        var interval = GetEffectiveActivityDurationMs(ctx, participant, type, baseMs);
        ActivityOnInterval(ctx, participant, type, interval, true);
    }

    [SpacetimeDB.Reducer]
    public static void StartActivity(ReducerContext ctx, ActivityType type) {
        foreach (var existing in ctx.Db.ActiveTask.by_active_participant_type
            .Filter((Participant: ctx.Sender, Type: type)))
        {
            ctx.Db.ActiveTask.Id.Delete(existing.Id);
        }
        foreach (var pending in ctx.Db.TaskCompletion.Participant.Filter(ctx.Sender))
        {
            if (pending.Type == type)
                ctx.Db.TaskCompletion.ScheduleId.Delete(pending.ScheduleId);
        }

        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
            throw new Exception("Player not found");

        var activity = ctx.Db.Activity.by_activity_participant_type
            .Filter((Participant: ctx.Sender, Type: type)).First();

        ValidateActivityAccessible(ctx, ctx.Sender, player, activity);
        DeductActivityCosts(ctx, ctx.Sender, activity.Cost);

        var durationMs = GetEffectiveActivityDurationMs(ctx, ctx.Sender, activity.Type, activity.DurationMs);
        var completesAt = ctx.Timestamp + TimeSpan.FromMilliseconds(durationMs);

        ctx.Db.ActiveTask.Insert(new ActiveTask {
            Participant = ctx.Sender,
            Type = type,
            StartedAt = ctx.Timestamp,
            CompletesAt = completesAt
        });

        ctx.Db.TaskCompletion.Insert(new TaskCompletion {
            Participant = ctx.Sender,
            Type = type,
            ScheduledAt = new ScheduleAt.Time(completesAt)
        });
    }

    [SpacetimeDB.Reducer]
    public static void CompleteTask(ReducerContext ctx, TaskCompletion task) {
        switch (task.Type) {
            case ActivityType.Scavenge:
                Scavenge(ctx, task.Participant);
                break;
            case ActivityType.ChopWood:
                ChopWoodReward(ctx, task.Participant);
                break;
            case ActivityType.Mine:
                MineReward(ctx, task.Participant);
                break;
            case ActivityType.GatherFabric:
                GatherFabricReward(ctx, task.Participant);
                break;
            default:
                throw new Exception("Unknown activity type");
        }

        foreach (var active in ctx.Db.ActiveTask.by_active_participant_type
            .Filter((Participant: task.Participant, Type: task.Type)))
        {
            ctx.Db.ActiveTask.Id.Delete(active.Id);
        }
    }
}
