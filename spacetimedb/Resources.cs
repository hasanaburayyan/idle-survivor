using System.Numerics;
using SpacetimeDB;

[SpacetimeDB.Type]
public enum ResourceType: byte {
    Food,
    Money,
    Wood,
    Metal,
    Fabric,
    Parts
}

public static partial class Module {
    [SpacetimeDB.Table(Accessor = "ResourceTracker", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "by_owner_and_type", Columns = new [] {"Owner", "Type"})]
    public partial struct ResourceTracker {

        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;
        [SpacetimeDB.Index.BTree]
        public Identity Owner;
        public ResourceType Type;

        public ulong Amount;        
    }

    private const double GuildTaxRate = 0.20;

    [SpacetimeDB.Reducer]
    public static void AddResourceToPlayer(ReducerContext ctx, Identity playerId, ResourceType type, ulong amount) {
        var playerAmount = amount;

        if (ctx.Db.GuildMember.PlayerId.Find(playerId) is GuildMember member)
        {
            var taxAmount = (ulong)(amount * GuildTaxRate);
            if (taxAmount < 1 && amount > 0) taxAmount = 1;

            AddResourceToGuild(ctx, member.GuildId, type, taxAmount);
        }

        var existingResources = ctx.Db.ResourceTracker.by_owner_and_type.Filter((Owner: playerId, Type: type));

        if (!existingResources.Any()) {
            ctx.Db.ResourceTracker.Insert(new ResourceTracker{
                Owner = playerId,
                Type = type,
                Amount = playerAmount
            });

            return;
        }

        var existingResource = existingResources.First();
        existingResource.Amount = existingResource.Amount + playerAmount;
        ctx.Db.ResourceTracker.Id.Update(existingResource);
    }

    [SpacetimeDB.Reducer]
    public static void DebugGrantMoney(ReducerContext ctx) {
        AddResourceToPlayer(ctx, ctx.Sender, ResourceType.Money, 1000);
    }

    [SpacetimeDB.Reducer]
    public static void AddResourceToPlayerByPlayerName(ReducerContext ctx, string name, string resourceType, ulong amount) {
        if (!Enum.TryParse<ResourceType>(resourceType, true, out var type)) {
            throw new Exception($"Unknown resource type '{resourceType}'. Valid: {string.Join(", ", Enum.GetNames<ResourceType>())}");
        }
        var player = ctx.Db.Player.Iter().FirstOrDefault(p => p.DisplayName == name);
        if (player.Identity == default) {
            throw new Exception($"No player found with name '{name}'");
        }
        AddResourceToPlayer(ctx, player.Identity, type, amount);
    }

}