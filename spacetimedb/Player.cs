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
            Location = LocationType.Shelter
        });

        SetStat(ctx, ctx.Sender, StatType.Health, 5);
        SetStat(ctx, ctx.Sender, StatType.MaxHealth, 5);
        SetStat(ctx, ctx.Sender, StatType.Strength, 1);
        SetStat(ctx, ctx.Sender, StatType.Intelligence, 1);
        SetStat(ctx, ctx.Sender, StatType.Perception, 1);
        SetStat(ctx, ctx.Sender, StatType.Wit, 1);
        SetStat(ctx, ctx.Sender, StatType.Endurance, 1);
        SetStat(ctx, ctx.Sender, StatType.Dexterity, 1);
        SetStat(ctx, ctx.Sender, StatType.KillSpeed, 1);

        ctx.Db.PlayerLevel.Insert(new PlayerLevel
        {
            Owner = ctx.Sender,
            Level = 0,
            Xp = 0,
            AvailableSkillPoints = 0
        });

        StartShelterSchedules(ctx, ctx.Sender);

        ctx.Db.Activity.Insert(new Activity{
            Participant = ctx.Sender,
            Type = ActivityType.Scavenge,
            Cost = [],
            DurationMs = 500,
            RequiredLocation = LocationType.Shelter,
            RequiredLevel = null,
            RequiredStructure = null,
            RequiredSkillId = null,
            Level = 1
        });

        var woodSkillId = FindSkillIdByName(ctx, "Unlock Wood Gathering");
        ctx.Db.Activity.Insert(new Activity{
            Participant = ctx.Sender,
            Type = ActivityType.ChopWood,
            Cost = [],
            DurationMs = 3000,
            RequiredLocation = LocationType.Shelter,
            RequiredLevel = null,
            RequiredStructure = null,
            RequiredSkillId = woodSkillId,
            Level = 1
        });

        var metalSkillId = FindSkillIdByName(ctx, "Unlock Metal Gathering");
        ctx.Db.Activity.Insert(new Activity{
            Participant = ctx.Sender,
            Type = ActivityType.Mine,
            Cost = [],
            DurationMs = 3000,
            RequiredLocation = LocationType.Shelter,
            RequiredLevel = null,
            RequiredStructure = null,
            RequiredSkillId = metalSkillId,
            Level = 1
        });
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

        GrantExperience(ctx, ctx.Sender, 1);
    }

    [SpacetimeDB.Reducer]
    public static void AckKillLoot(ReducerContext ctx, ulong lootId)
    {
        ctx.Db.KillLoot.Id.Delete(lootId);
    }
}
