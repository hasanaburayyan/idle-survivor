using SpacetimeDB;

public static partial class Module {
    [SpacetimeDB.Table(Accessor = "KillLoot", Public = true)]
    public partial struct KillLoot
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public Identity Owner;

        public ResourceType Resource;
        public ulong Amount;
    }

    [SpacetimeDB.Table(Accessor = "Player", Public = true)]
    public partial struct Player
    {
        [SpacetimeDB.PrimaryKey]
        public Identity Identity;
        public string DisplayName;
        public string? Email;
        public bool Online;
        public LocationType Location;
    }

    [SpacetimeDB.Reducer]
    public static void CreatePlayer(ReducerContext ctx) {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) != null) {
            throw new Exception("Cannot create a second player");
        }
        var random = new Random();
        var length = random.Next(5, 16);
        var displayName = new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", length)
            .Select(s => s[random.Next(s.Length)]).ToArray());

        ctx.Db.Player.Insert(new Player{
            Identity = ctx.Sender,
            DisplayName = displayName,
            Online = true,
            Location = LocationType.Waste
        });

        SetStat(ctx, ctx.Sender, StatType.Health, 5);
        SetStat(ctx, ctx.Sender, StatType.MaxHealth, 5);
        SetStat(ctx, ctx.Sender, StatType.Strength, 1);
        SetStat(ctx, ctx.Sender, StatType.Intelligence, 1);
        SetStat(ctx, ctx.Sender, StatType.Perception, 1);
        SetStat(ctx, ctx.Sender, StatType.Wit, 1);
        SetStat(ctx, ctx.Sender, StatType.Endurance, 1);
        SetStat(ctx, ctx.Sender, StatType.Dexterity, 1);

        StartWasteSchedules(ctx, ctx.Sender);

        ctx.Db.Activity.Insert(new Activity{
            Participant = ctx.Sender,
            Type = ActivityType.Scavenge,
            Cost = [],
            DurationMs = 500,
            RequiredLocation = LocationType.Waste,
            UnlockCriteria = [],
            Level = 1
        });

        ctx.Db.Activity.Insert(new Activity{
            Participant = ctx.Sender,
            Type = ActivityType.LootBigWood,
            Cost = [],
            DurationMs = 500,
            RequiredLocation = LocationType.Waste,
            UnlockCriteria = [],
            Level = 1
        });

        ctx.Db.Activity.Insert(new Activity {
            Participant = ctx.Sender,
            Type = ActivityType.CarbLoad,
            Cost = [
                new ActivityCost{Type = ResourceType.Food, Amount = 60}
            ],
            DurationMs = 2000,
            RequiredLocation = null,
            UnlockCriteria = [],
            Level = 1
        });

        ctx.Db.Activity.Insert(new Activity {
            Participant = ctx.Sender,
            Type = ActivityType.BuildShelter,
            Cost = [
                new ActivityCost{Type = ResourceType.Wood, Amount = 50},
                new ActivityCost{Type = ResourceType.Metal, Amount = 30},
                new ActivityCost{Type = ResourceType.Fabric, Amount = 20}
            ],
            DurationMs = 10_000,
            RequiredLocation = LocationType.Waste,
            UnlockCriteria = [],
            Level = 1
        });

        EnsureSearchActivities(ctx, ctx.Sender);
    }

    [SpacetimeDB.Reducer]
    public static void SetName(ReducerContext ctx, string name)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
        {
            throw new Exception("Player not found");
        }

        player.DisplayName = name;
        ctx.Db.Player.Identity.Update(player);
    }

    [SpacetimeDB.Reducer]
    public static void KillZombie(ReducerContext ctx)
    {
        var current = GetStat(ctx, ctx.Sender, StatType.ZombiesKilled);
        SetStat(ctx, ctx.Sender, StatType.ZombiesKilled, current + 1);

        var random = new Random();
        var values = Enum.GetValues<ResourceType>();
        var resourceType = values[random.Next(values.Length)];
        var level = GetActivityLevel(ctx, ctx.Sender, ActivityType.Scavenge);
        var amount = GetScavengeAmountForResource(ctx, ctx.Sender, resourceType) + level;
        AddResourceToPlayer(ctx, ctx.Sender, resourceType, amount);

        ctx.Db.KillLoot.Insert(new KillLoot
        {
            Id = 0,
            Owner = ctx.Sender,
            Resource = resourceType,
            Amount = amount
        });
    }

    [SpacetimeDB.Reducer]
    public static void AckKillLoot(ReducerContext ctx, ulong lootId)
    {
        ctx.Db.KillLoot.Id.Delete(lootId);
    }
}
