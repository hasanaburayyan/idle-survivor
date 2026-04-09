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

    [SpacetimeDB.Reducer]
    public static void AddResourceToPlayer(ReducerContext ctx, Identity playerId, ResourceType type, ulong amount) {
        var existingResources = ctx.Db.ResourceTracker.by_owner_and_type.Filter((Owner: playerId, Type: type));

        if (!existingResources.Any()) {
            ctx.Db.ResourceTracker.Insert(new ResourceTracker{
                Owner = playerId,
                Type = type,
                Amount = amount
            });

            return;
        }

        var existingResource = existingResources.First();
        existingResource.Amount = existingResource.Amount + amount;
        ctx.Db.ResourceTracker.Id.Update(existingResource);
    }
}