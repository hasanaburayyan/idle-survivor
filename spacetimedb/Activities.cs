using SpacetimeDB;

[SpacetimeDB.Type]
public enum ActivityType : byte
{
    Scavenge,
    LootBigWood,
    CarbLoad,
    Study,
    Focus,
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
    }

    [SpacetimeDB.Table(Accessor = "ActiveTask", Public = true)]
    public partial struct ActiveTask {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;
        [SpacetimeDB.Unique]
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

    [SpacetimeDB.Reducer]
    public static void ProcessScheduledActivity(ReducerContext ctx, ActivitySchedule arg)
    {
        Log.Info("Processing a activity");
        switch (arg.type)
        {
            case ActivityType.Scavenge:
                Scavenge(ctx, arg.Participant);
                break;
            case ActivityType.LootBigWood:
                LootBigWood(ctx, arg.Participant);
                break;
            case ActivityType.CarbLoad:
                CarbLoad(ctx, arg.Participant);
                break;
            case ActivityType.Study:
                Study(ctx, arg.Participant);
                break;
            case ActivityType.Focus:
                Focus(ctx, arg.Participant);
                break;
            default:
                throw new Exception("Unknown activity type");
        }

        if (arg.IntervalMilliseconds is not null)
        {
            ctx.Db.ActivitySchedule.Insert(new ActivitySchedule
            {
                IntervalMilliseconds = arg.IntervalMilliseconds,
                Participant = arg.Participant,
                ScheduledAt = new ScheduleAt.Time(ctx.Timestamp + TimeSpan.FromMilliseconds(arg.IntervalMilliseconds.Value)),
                type = arg.type
            });
        }
    }


    [SpacetimeDB.Reducer]
    public static void Scavenge(ReducerContext ctx, Identity participant)
    {
        var random = new Random();
        var values = Enum.GetValues<ResourceType>();
        var resourceType = values[random.Next(values.Length)];

        var amount = (ulong)1;
        switch (resourceType) {
            case ResourceType.Food:
                amount = (ulong)GetStat(ctx, participant, StatType.Perception);
                break;
            case ResourceType.Fabric:
                amount = (ulong)GetStat(ctx, participant, StatType.Intelligence);
                break;
            case ResourceType.Metal:
                amount = (ulong)GetStat(ctx, participant, StatType.Strength);
                break;
            case ResourceType.Money:
                amount = (ulong)GetStat(ctx, participant, StatType.Wit);
                break;
            case ResourceType.Parts:
                amount = (ulong)GetStat(ctx, participant, StatType.Dexterity);
                break;
            case ResourceType.Wood:
                amount = (ulong)GetStat(ctx, participant, StatType.Endurance);
                break;
        }
        AddResourceToPlayer(ctx, participant, resourceType, amount);

        if (ctx.Db.GuildMember.PlayerId.Find(participant) is GuildMember scavengerMember
            && scavengerMember.InSession)
        {
            foreach (var member in ctx.Db.GuildMember.GuildId.Filter(scavengerMember.GuildId))
            {
                if (member.PlayerId == participant) continue;
                if (!member.InSession) continue;
                AddResourceToPlayer(ctx, member.PlayerId, resourceType, amount);
            }
        }
    }

    [SpacetimeDB.Reducer]
    public static void LootBigWood(ReducerContext ctx, Identity participant) {
        AddResourceToPlayer(ctx, participant, ResourceType.Food, 100);
    }

    [SpacetimeDB.Reducer]
    public static void Focus(ReducerContext ctx, Identity participant) {
        var currentPerception = GetStat(ctx, participant, StatType.Perception);
        SetStat(ctx, participant, StatType.Perception, currentPerception + 2);
    }

    private static void CarbLoad(ReducerContext ctx, Identity participant) {
        var totalStats = (ulong)(
            GetStat(ctx, participant, StatType.Dexterity) +
            GetStat(ctx, participant, StatType.Endurance) +
            GetStat(ctx, participant, StatType.Intelligence) +
            GetStat(ctx, participant, StatType.Perception) +
            GetStat(ctx, participant, StatType.Wit) +
            GetStat(ctx, participant, StatType.Strength)
        );
        var upgradeCost = totalStats * 10;

        var playerFood = ctx.Db.ResourceTracker.by_owner_and_type.Filter((Owner: participant, Type: ResourceType.Food)).First();

        if (playerFood.Amount < upgradeCost) {
            throw new Exception("Player has insufficient resources for upgrade");
        }

        // Choose a random stat to upgrade
        var random = new Random();
        var stats = Enum.GetValues<StatType>();
        var statToUpgrade = stats[random.Next(stats.Length)];
        while (statToUpgrade == StatType.Health || statToUpgrade == StatType.MaxHealth) 
        {
            statToUpgrade = stats[random.Next(stats.Length)];
        }

        // Upgrade the stat
        SetStat(ctx, participant, statToUpgrade, GetStat(ctx, participant, statToUpgrade) + 1);

        var food = ctx.Db.ResourceTracker.by_owner_and_type.Filter((Owner: participant, Type: ResourceType.Food)).First();
        food.Amount -= upgradeCost;
        ctx.Db.ResourceTracker.Id.Update(food);

        var carbActivity = ctx.Db.Activity.by_activity_participant_type.Filter((Participant: participant, Type: ActivityType.CarbLoad)).First();
        carbActivity.Cost = [
            new ActivityCost{
                Amount =  upgradeCost + 10,
                Type = ResourceType.Food
            }
        ];

        ctx.Db.Activity.Id.Update(carbActivity);
    }

    private static void CarbLoadReward(ReducerContext ctx, Identity participant) {
        var random = new Random();
        var stats = Enum.GetValues<StatType>();
        var statToUpgrade = stats[random.Next(stats.Length)];
        while (statToUpgrade == StatType.Health || statToUpgrade == StatType.MaxHealth)
        {
            statToUpgrade = stats[random.Next(stats.Length)];
        }
        SetStat(ctx, participant, statToUpgrade, GetStat(ctx, participant, statToUpgrade) + 1);

        var totalStats = (ulong)(
            GetStat(ctx, participant, StatType.Dexterity) +
            GetStat(ctx, participant, StatType.Endurance) +
            GetStat(ctx, participant, StatType.Intelligence) +
            GetStat(ctx, participant, StatType.Perception) +
            GetStat(ctx, participant, StatType.Wit) +
            GetStat(ctx, participant, StatType.Strength)
        );
        var nextCost = totalStats * 10;

        var carbActivity = ctx.Db.Activity.by_activity_participant_type
            .Filter((Participant: participant, Type: ActivityType.CarbLoad)).First();
        carbActivity.Cost = [new ActivityCost { Amount = nextCost, Type = ResourceType.Food }];
        ctx.Db.Activity.Id.Update(carbActivity);
    }

    [SpacetimeDB.Reducer]
    public static void ActivityOnInterval(ReducerContext ctx, Identity participant, ActivityType type, ulong interval, bool reoccuring)
    {
        var i = ctx.Db.ActivitySchedule.Insert(new ActivitySchedule
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
    public static void StartActiveSchedules(ReducerContext ctx, Identity participant) {
        ActivityOnInterval(ctx, participant, ActivityType.Scavenge, 5000, true);
        ActivityOnInterval(ctx, participant, ActivityType.LootBigWood, 20_000, true);
    }

    private static void Study(ReducerContext ctx, Identity participant) {
        SetStat(ctx, participant, StatType.Intelligence,
            GetStat(ctx, participant, StatType.Intelligence) + 3);
    }

    private static ulong GetEffectiveDurationMs(ulong baseDurationMs, int statValue)
    {
        var effective = baseDurationMs / (1.0 + statValue * 0.1);
        return Math.Max(250, (ulong)effective);
    }

    private static void UpdateActivityDuration(ReducerContext ctx, Identity participant, ActivityType type, StatType stat)
    {
        var activity = ctx.Db.Activity.by_activity_participant_type
            .Filter((Participant: participant, Type: type)).First();
        activity.DurationMs = GetEffectiveDurationMs(activity.DurationMs, GetStat(ctx, participant, stat));
        ctx.Db.Activity.Id.Update(activity);
    }

    [SpacetimeDB.Reducer]
    public static void StartActivity(ReducerContext ctx, ActivityType type) {
        if (ctx.Db.ActiveTask.Participant.Find(ctx.Sender) is ActiveTask) {
            throw new Exception("Already doing an activity");
        }

        var activity = ctx.Db.Activity.by_activity_participant_type
            .Filter((Participant: ctx.Sender, Type: type)).First();

        foreach (var cost in activity.Cost) {
            var resource = ctx.Db.ResourceTracker.by_owner_and_type
                .Filter((Owner: ctx.Sender, Type: cost.Type)).First();
            if (resource.Amount < cost.Amount) {
                throw new Exception($"Insufficient {cost.Type}");
            }
        }

        foreach (var cost in activity.Cost) {
            var resource = ctx.Db.ResourceTracker.by_owner_and_type
                .Filter((Owner: ctx.Sender, Type: cost.Type)).First();
            resource.Amount -= cost.Amount;
            ctx.Db.ResourceTracker.Id.Update(resource);
        }

        var durationMs = activity.DurationMs;
        if (type == ActivityType.Study)
            durationMs = GetEffectiveDurationMs(durationMs, GetStat(ctx, ctx.Sender, StatType.Intelligence));
        else if (type == ActivityType.Focus)
            durationMs = GetEffectiveDurationMs(durationMs, GetStat(ctx, ctx.Sender, StatType.Perception));

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
            case ActivityType.LootBigWood:
                LootBigWood(ctx, task.Participant);
                break;
            case ActivityType.CarbLoad:
                CarbLoadReward(ctx, task.Participant);
                break;
            case ActivityType.Study:
                Study(ctx, task.Participant);
                UpdateActivityDuration(ctx, task.Participant, ActivityType.Study, StatType.Intelligence);
                break;
            case ActivityType.Focus:
                Focus(ctx, task.Participant);
                UpdateActivityDuration(ctx, task.Participant, ActivityType.Focus, StatType.Perception);
                break;
            default:
                throw new Exception("Unknown activity type");
        }

        if (ctx.Db.ActiveTask.Participant.Find(task.Participant) is ActiveTask active) {
            ctx.Db.ActiveTask.Id.Delete(active.Id);
        }
    }
}