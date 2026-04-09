using SpacetimeDB;

[SpacetimeDB.Type]
public enum ActivityType : byte
{
    Scavenge,
    LootBigWood,
    CarbLoad
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

        AddResourceToPlayer(ctx, participant, resourceType, 1);
    }

    [SpacetimeDB.Reducer]
    public static void LootBigWood(ReducerContext ctx, Identity participant) {
        AddResourceToPlayer(ctx, participant, ResourceType.Food, 100);
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
}