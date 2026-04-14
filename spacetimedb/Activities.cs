using SpacetimeDB;

[SpacetimeDB.Type]
public enum ActivityType : byte
{
    Scavenge,
    LootBigWood,
    CarbLoad,
    Study,
    Focus,
    BuildShelter,
    Salvage,
    SearchFood,
    SearchMoney,
    SearchWood,
    SearchMetal,
    SearchFabric,
    SearchParts,
    TrainStrength,
    TrainWit,
    TrainEndurance,
    TrainDexterity,
    BuildDumbbells,
    BuildBookshelf,
    BuildDartBoard,
    BuildMeditationNook,
    BuildStairStepper,
    BuildPingPongTable,
}

[SpacetimeDB.Type]
public partial struct ActivityCost {
    public ResourceType Type;
    public ulong Amount;
}

[SpacetimeDB.Type]
public partial struct ActivityUnlockCriterion
{
    public StatType Stat;
    public int MinValue;
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
        public List<ActivityUnlockCriterion> UnlockCriteria;
        [SpacetimeDB.Default(1)]
        public uint Level;
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

    /// <summary>Superlinear: base * currentLevel^2 (current level before upgrade).</summary>
    private static ulong ScaledUpgradeCost(ulong baseAmount, uint level) =>
        baseAmount * (ulong)level * (ulong)level;

    /// <summary>Must match client preview in godot-client/scenes/waste/Activity.cs</summary>
    private static void AppendNextUpgradeCosts(ActivityType type, uint currentLevel, List<ActivityCost> dest)
    {
        void Add(ResourceType rt, ulong baseAmt) =>
            dest.Add(new ActivityCost { Type = rt, Amount = ScaledUpgradeCost(baseAmt, currentLevel) });

        switch (type)
        {
            case ActivityType.Scavenge:
                Add(ResourceType.Food, 5);
                Add(ResourceType.Money, 5);
                Add(ResourceType.Wood, 5);
                Add(ResourceType.Metal, 5);
                Add(ResourceType.Fabric, 5);
                Add(ResourceType.Parts, 5);
                break;
            case ActivityType.LootBigWood:
                Add(ResourceType.Money, 12);
                break;
            case ActivityType.SearchFood:
                Add(ResourceType.Metal, 10);
                break;
            case ActivityType.SearchMoney:
                Add(ResourceType.Wood, 10);
                break;
            case ActivityType.SearchWood:
                Add(ResourceType.Fabric, 10);
                break;
            case ActivityType.SearchMetal:
                Add(ResourceType.Food, 10);
                break;
            case ActivityType.SearchFabric:
                Add(ResourceType.Parts, 10);
                break;
            case ActivityType.SearchParts:
                Add(ResourceType.Money, 10);
                break;
            case ActivityType.Salvage:
                Add(ResourceType.Food, 8);
                Add(ResourceType.Wood, 8);
                Add(ResourceType.Fabric, 8);
                break;
            case ActivityType.Study:
                Add(ResourceType.Food, 15);
                break;
            case ActivityType.Focus:
                Add(ResourceType.Parts, 15);
                break;
            case ActivityType.CarbLoad:
                Add(ResourceType.Money, 14);
                break;
            case ActivityType.TrainStrength:
                Add(ResourceType.Fabric, 15);
                break;
            case ActivityType.TrainWit:
                Add(ResourceType.Metal, 15);
                break;
            case ActivityType.TrainEndurance:
                Add(ResourceType.Money, 15);
                break;
            case ActivityType.TrainDexterity:
                Add(ResourceType.Food, 15);
                break;
            default:
                break;
        }
    }

    private static void ValidateActivityAccessible(ReducerContext ctx, Identity participant, Player player, Activity activity)
    {
        if (!IsLocationValid(activity.RequiredLocation, player.Location))
            throw new Exception("This activity is not available at your current location");

        foreach (var criterion in activity.UnlockCriteria)
        {
            if (GetStat(ctx, participant, criterion.Stat) < criterion.MinValue)
                throw new Exception($"Requires {criterion.Stat} {criterion.MinValue}+");
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
        if (type == ActivityType.BuildShelter || BuildTypeToTrainType(type) is not null)
            throw new Exception("This activity cannot be upgraded");

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

    private static ulong GetScavengeAmountForResource(ReducerContext ctx, Identity participant, ResourceType resourceType) =>
        (ulong)GetStat(ctx, participant, GetScavengeStat(resourceType));

    private static ResourceType? ActivityTypeToSearchResource(ActivityType type) => type switch
    {
        ActivityType.SearchFood => ResourceType.Food,
        ActivityType.SearchMoney => ResourceType.Money,
        ActivityType.SearchWood => ResourceType.Wood,
        ActivityType.SearchMetal => ResourceType.Metal,
        ActivityType.SearchFabric => ResourceType.Fabric,
        ActivityType.SearchParts => ResourceType.Parts,
        _ => null
    };

    private static void SearchResourceReward(ReducerContext ctx, Identity participant, ResourceType resource, ActivityType searchType)
    {
        var level = GetActivityLevel(ctx, participant, searchType);
        var amount = GetScavengeAmountForResource(ctx, participant, resource) * 5 + level;
        AddResourceToPlayer(ctx, participant, resource, amount);
    }

    /// <summary>Backfill Level for rows created before the Level column existed (0 = unset).</summary>
    public static void EnsureActivityLevels(ReducerContext ctx, Identity participant)
    {
        foreach (var act in ctx.Db.Activity.Participant.Filter(participant))
        {
            if (act.Level != 0)
                continue;
            ctx.Db.Activity.Id.Update(act with { Level = 1u });
        }
    }

    public static void EnsureLootBigWoodActivity(ReducerContext ctx, Identity participant)
    {
        if (ctx.Db.Activity.by_activity_participant_type
            .Filter((Participant: participant, Type: ActivityType.LootBigWood)).Any())
            return;

        ctx.Db.Activity.Insert(new Activity
        {
            Participant = participant,
            Type = ActivityType.LootBigWood,
            Cost = [],
            DurationMs = 500,
            RequiredLocation = LocationType.Waste,
            UnlockCriteria = [],
            Level = 1
        });
    }

    public static void EnsureSearchActivities(ReducerContext ctx, Identity participant)
    {
        (ResourceType Resource, ActivityType Type)[] pairs =
        [
            (ResourceType.Food, ActivityType.SearchFood),
            (ResourceType.Money, ActivityType.SearchMoney),
            (ResourceType.Wood, ActivityType.SearchWood),
            (ResourceType.Metal, ActivityType.SearchMetal),
            (ResourceType.Fabric, ActivityType.SearchFabric),
            (ResourceType.Parts, ActivityType.SearchParts),
        ];

        foreach (var (resource, type) in pairs)
        {
            if (ctx.Db.Activity.by_activity_participant_type.Filter((Participant: participant, Type: type)).Any())
                continue;

            var stat = GetScavengeStat(resource);
            ctx.Db.Activity.Insert(new Activity
            {
                Participant = participant,
                Type = type,
                Cost = [],
                DurationMs = 500,
                RequiredLocation = LocationType.Waste,
                UnlockCriteria = [new ActivityUnlockCriterion { Stat = stat, MinValue = 5 }],
                Level = 1
            });
        }
    }

    [SpacetimeDB.Reducer]
    public static void Scavenge(ReducerContext ctx, Identity participant)
    {
        var random = new Random();
        var values = Enum.GetValues<ResourceType>();
        var resourceType = values[random.Next(values.Length)];

        var level = GetActivityLevel(ctx, participant, ActivityType.Scavenge);
        var amount = GetScavengeAmountForResource(ctx, participant, resourceType) + level;
        AddResourceToPlayer(ctx, participant, resourceType, amount);
    }

    [SpacetimeDB.Reducer]
    public static void LootBigWood(ReducerContext ctx, Identity participant) {
        var level = GetActivityLevel(ctx, participant, ActivityType.LootBigWood);
        AddResourceToPlayer(ctx, participant, ResourceType.Food, 100 + level);
    }

    [SpacetimeDB.Reducer]
    public static void Focus(ReducerContext ctx, Identity participant) {
        var level = (int)GetActivityLevel(ctx, participant, ActivityType.Focus);
        var currentPerception = GetStat(ctx, participant, StatType.Perception);
        SetStat(ctx, participant, StatType.Perception, currentPerception + 2 + level);
    }

    private static readonly StatType[] UpgradeableStats = {
        StatType.Strength,
        StatType.Intelligence,
        StatType.Perception,
        StatType.Wit,
        StatType.Endurance,
        StatType.Dexterity
    };

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

        var random = new Random();
        var statToUpgrade = UpgradeableStats[random.Next(UpgradeableStats.Length)];

        var carbLevel = (int)GetActivityLevel(ctx, participant, ActivityType.CarbLoad);
        SetStat(ctx, participant, statToUpgrade, GetStat(ctx, participant, statToUpgrade) + 1 + carbLevel);

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
        var statToUpgrade = UpgradeableStats[random.Next(UpgradeableStats.Length)];
        var carbLevel = (int)GetActivityLevel(ctx, participant, ActivityType.CarbLoad);
        SetStat(ctx, participant, statToUpgrade, GetStat(ctx, participant, statToUpgrade) + 1 + carbLevel);

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

    private static void SalvageReward(ReducerContext ctx, Identity participant) {
        var level = GetActivityLevel(ctx, participant, ActivityType.Salvage);
        var metalAmount = (ulong)Math.Max(1, GetStat(ctx, participant, StatType.Dexterity)) + level;
        var moneyAmount = (ulong)Math.Max(1, GetStat(ctx, participant, StatType.Wit)) + level;
        AddResourceToPlayer(ctx, participant, ResourceType.Metal, metalAmount);
        AddResourceToPlayer(ctx, participant, ResourceType.Money, moneyAmount);
    }

    private static void BuildShelterReward(ReducerContext ctx, Identity participant) {
        ctx.Db.PlayerShelter.Insert(new PlayerShelter
        {
            Owner = participant,
            Level = 1,
            BuiltAt = ctx.Timestamp
        });

        var buildActivity = ctx.Db.Activity.by_activity_participant_type
            .Filter((Participant: participant, Type: ActivityType.BuildShelter));
        if (buildActivity.Any())
        {
            ctx.Db.Activity.Id.Delete(buildActivity.First().Id);
        }

        InsertBuildStructureActivities(ctx, participant);
    }

    private static void BuildStructureActivityReward(ReducerContext ctx, Identity participant, ActivityType buildType)
    {
        if (BuildTypeToTrainType(buildType) is ActivityType trainType
            && !ctx.Db.Activity.by_activity_participant_type
                .Filter((Participant: participant, Type: trainType)).Any())
        {
            ctx.Db.Activity.Insert(new Activity
            {
                Participant = participant,
                Type = trainType,
                Cost = [],
                DurationMs = 5000,
                RequiredLocation = LocationType.Shelter,
                UnlockCriteria = [],
                Level = 1
            });
        }

        var build = ctx.Db.Activity.by_activity_participant_type
            .Filter((Participant: participant, Type: buildType));
        if (build.Any())
            ctx.Db.Activity.Id.Delete(build.First().Id);
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
    public static void StartWasteSchedules(ReducerContext ctx, Identity participant) {
        ActivityOnInterval(ctx, participant, ActivityType.Scavenge, 5000, true);
        ActivityOnInterval(ctx, participant, ActivityType.LootBigWood, 20_000, true);
    }

    private static void Study(ReducerContext ctx, Identity participant) {
        var level = (int)GetActivityLevel(ctx, participant, ActivityType.Study);
        SetStat(ctx, participant, StatType.Intelligence,
            GetStat(ctx, participant, StatType.Intelligence) + 2 + level);
    }

    private static void TrainStrengthReward(ReducerContext ctx, Identity participant) {
        var level = (int)GetActivityLevel(ctx, participant, ActivityType.TrainStrength);
        SetStat(ctx, participant, StatType.Strength,
            GetStat(ctx, participant, StatType.Strength) + 2 + level);
    }

    private static void TrainWitReward(ReducerContext ctx, Identity participant) {
        var level = (int)GetActivityLevel(ctx, participant, ActivityType.TrainWit);
        SetStat(ctx, participant, StatType.Wit,
            GetStat(ctx, participant, StatType.Wit) + 2 + level);
    }

    private static void TrainEnduranceReward(ReducerContext ctx, Identity participant) {
        var level = (int)GetActivityLevel(ctx, participant, ActivityType.TrainEndurance);
        SetStat(ctx, participant, StatType.Endurance,
            GetStat(ctx, participant, StatType.Endurance) + 2 + level);
    }

    private static void TrainDexterityReward(ReducerContext ctx, Identity participant) {
        var level = (int)GetActivityLevel(ctx, participant, ActivityType.TrainDexterity);
        SetStat(ctx, participant, StatType.Dexterity,
            GetStat(ctx, participant, StatType.Dexterity) + 2 + level);
    }

    private static ulong GetEffectiveDurationMs(ulong baseDurationMs, int statValue)
    {
        var effective = baseDurationMs / (1.0 + statValue * 0.1);
        return Math.Max(250, (ulong)effective);
    }

    [SpacetimeDB.Reducer]
    public static void StartActivity(ReducerContext ctx, ActivityType type) {
        if (ctx.Db.ActiveTask.Participant.Find(ctx.Sender) is ActiveTask existing)
        {
            ctx.Db.ActiveTask.Id.Delete(existing.Id);
            foreach (var pending in ctx.Db.TaskCompletion.Participant.Filter(ctx.Sender))
                ctx.Db.TaskCompletion.ScheduleId.Delete(pending.ScheduleId);
        }

        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
            throw new Exception("Player not found");

        var activity = ctx.Db.Activity.by_activity_participant_type
            .Filter((Participant: ctx.Sender, Type: type)).First();

        ValidateActivityAccessible(ctx, ctx.Sender, player, activity);
        DeductActivityCosts(ctx, ctx.Sender, activity.Cost);

        var durationMs = activity.DurationMs;
        StatType? durationScalingStat = type switch
        {
            ActivityType.Study => StatType.Intelligence,
            ActivityType.Focus => StatType.Perception,
            ActivityType.TrainStrength => StatType.Strength,
            ActivityType.TrainWit => StatType.Wit,
            ActivityType.TrainEndurance => StatType.Endurance,
            ActivityType.TrainDexterity => StatType.Dexterity,
            _ => null
        };
        if (durationScalingStat is StatType scaleStat)
            durationMs = GetEffectiveDurationMs(durationMs, GetStat(ctx, ctx.Sender, scaleStat));

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
                break;
            case ActivityType.Focus:
                Focus(ctx, task.Participant);
                break;
            case ActivityType.BuildShelter:
                BuildShelterReward(ctx, task.Participant);
                break;
            case ActivityType.Salvage:
                SalvageReward(ctx, task.Participant);
                break;
            case ActivityType.SearchFood:
            case ActivityType.SearchMoney:
            case ActivityType.SearchWood:
            case ActivityType.SearchMetal:
            case ActivityType.SearchFabric:
            case ActivityType.SearchParts:
                if (ActivityTypeToSearchResource(task.Type) is ResourceType searchResource)
                    SearchResourceReward(ctx, task.Participant, searchResource, task.Type);
                break;
            case ActivityType.TrainStrength:
                TrainStrengthReward(ctx, task.Participant);
                break;
            case ActivityType.TrainWit:
                TrainWitReward(ctx, task.Participant);
                break;
            case ActivityType.TrainEndurance:
                TrainEnduranceReward(ctx, task.Participant);
                break;
            case ActivityType.TrainDexterity:
                TrainDexterityReward(ctx, task.Participant);
                break;
            case ActivityType.BuildDumbbells:
            case ActivityType.BuildBookshelf:
            case ActivityType.BuildDartBoard:
            case ActivityType.BuildMeditationNook:
            case ActivityType.BuildStairStepper:
            case ActivityType.BuildPingPongTable:
                BuildStructureActivityReward(ctx, task.Participant, task.Type);
                break;
            default:
                throw new Exception("Unknown activity type");
        }

        if (ctx.Db.ActiveTask.Participant.Find(task.Participant) is ActiveTask active) {
            ctx.Db.ActiveTask.Id.Delete(active.Id);
        }
    }
}
