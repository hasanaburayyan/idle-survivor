using SpacetimeDB;

public static partial class Module {
    [SpacetimeDB.Table(Accessor = "Player", Public = true)]
    public partial struct Player
    {
        [SpacetimeDB.PrimaryKey]
        public Identity Identity;
        public string DisplayName;
        public string? Email;
        public bool Online;
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
            Online = true
        });

        SetStat(ctx, ctx.Sender, StatType.Health, 5);
        SetStat(ctx, ctx.Sender, StatType.MaxHealth, 5);
        SetStat(ctx, ctx.Sender, StatType.Strength, 1);
        SetStat(ctx, ctx.Sender, StatType.Intelligence, 1);
        SetStat(ctx, ctx.Sender, StatType.Perception, 1);
        SetStat(ctx, ctx.Sender, StatType.Wit, 1);
        SetStat(ctx, ctx.Sender, StatType.Endurance, 1);
        SetStat(ctx, ctx.Sender, StatType.Dexterity, 1);

        StartActiveSchedules(ctx, ctx.Sender);

        // Give starting Activities
        ctx.Db.Activity.Insert(new Activity{
            Participant = ctx.Sender,
            Type = ActivityType.Scavenge,
            Cost = [],
            DurationMs = 500
        });

        ctx.Db.Activity.Insert(new Activity {
            Participant = ctx.Sender,
            Type = ActivityType.CarbLoad,
            Cost = [
                new ActivityCost{Type = ResourceType.Food, Amount = 60}
            ],
            DurationMs = 2000
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
}
