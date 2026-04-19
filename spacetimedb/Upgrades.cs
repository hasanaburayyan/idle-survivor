using SpacetimeDB;

[SpacetimeDB.Type]
public enum StatType : byte {
    Health,
    MaxHealth,
    Strength,
    Intelligence,
    Perception,
    Wit,
    Endurance,
    Dexterity,
    ZombiesKilled,
    KillSpeed
}

[SpacetimeDB.Type]
public enum UpgradeType : byte {
    AttackSpeed,
    KillsPerClick,
    ZombieDensity,
    LootMultiplier
}

public static partial class Module {
    [SpacetimeDB.Table(Accessor = "PlayerStat", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "by_stat_owner_stat", Columns = new[] { "Owner", "Stat" })]
    public partial struct PlayerStat {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public Identity Owner;
        public StatType Stat;
        public int Value;
    }

    [SpacetimeDB.Table(Accessor = "PlayerUpgrade", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "by_upgrade_owner_type", Columns = new[] { "Owner", "Type" })]
    public partial struct PlayerUpgrade {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public Identity Owner;
        public UpgradeType Type;
        public uint Level;
    }

    public static void SetStat(ReducerContext ctx, Identity owner, StatType stat, int value) {
        var existing = ctx.Db.PlayerStat.by_stat_owner_stat.Filter((Owner: owner, Stat: stat));
        if (existing.Any()) {
            var row = existing.First();
            row.Value = value;
            ctx.Db.PlayerStat.Id.Update(row);
        } else {
            ctx.Db.PlayerStat.Insert(new PlayerStat {
                Owner = owner,
                Stat = stat,
                Value = value
            });
        }

    }

    public static int GetStat(ReducerContext ctx, Identity owner, StatType stat) {
        var existing = ctx.Db.PlayerStat.by_stat_owner_stat.Filter((Owner: owner, Stat: stat));
        return existing.Any() ? existing.First().Value : 0;
    }

    public static uint GetUpgradeLevel(ReducerContext ctx, Identity owner, UpgradeType type) {
        var existing = ctx.Db.PlayerUpgrade.by_upgrade_owner_type.Filter((Owner: owner, Type: type));
        return existing.Any() ? existing.First().Level : 0u;
    }

    // Classic idle curve: floor(10 * 1.5^level). L0=10, L5=75, L10=576, L20=22168.
    public static ulong NextUpgradeCost(uint currentLevel) {
        return (ulong)Math.Floor(10.0 * Math.Pow(1.5, currentLevel));
    }

    [SpacetimeDB.Reducer]
    public static void PurchaseUpgrade(ReducerContext ctx, UpgradeType type) {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is null)
            throw new Exception("Player not found");

        if (type != UpgradeType.LootMultiplier && !IsUpgradeUnlocked(ctx, ctx.Sender, type))
            throw new Exception($"{type} is not unlocked yet");

        var existing = ctx.Db.PlayerUpgrade.by_upgrade_owner_type
            .Filter((Owner: ctx.Sender, Type: type));

        uint currentLevel = existing.Any() ? existing.First().Level : 0u;
        ulong cost = NextUpgradeCost(currentLevel);

        var moneyRow = ctx.Db.ResourceTracker.by_owner_and_type
            .Filter((Owner: ctx.Sender, Type: ResourceType.Money));
        if (!moneyRow.Any() || moneyRow.First().Amount < cost)
            throw new Exception($"Insufficient Money (need {cost})");

        var money = moneyRow.First();
        money.Amount -= cost;
        ctx.Db.ResourceTracker.Id.Update(money);

        if (existing.Any()) {
            var row = existing.First();
            row.Level = currentLevel + 1;
            ctx.Db.PlayerUpgrade.Id.Update(row);
        } else {
            ctx.Db.PlayerUpgrade.Insert(new PlayerUpgrade {
                Id = 0,
                Owner = ctx.Sender,
                Type = type,
                Level = 1
            });
        }
    }
}
