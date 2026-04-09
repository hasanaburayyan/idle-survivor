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
    Dexterity
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

}
