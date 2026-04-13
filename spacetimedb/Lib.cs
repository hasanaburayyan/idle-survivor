using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Reducer(ReducerKind.ClientConnected)]
    public static void Connected(ReducerContext ctx)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player) {
            return;
        }

        player.Online = true;
        ctx.Db.Player.Identity.Update(player);

        if (player.Location == LocationType.Waste)
        {
            StartWasteSchedules(ctx, player.Identity);
        }
    }

    [SpacetimeDB.Reducer(ReducerKind.ClientDisconnected)]
    public static void Disconnected(ReducerContext ctx)
    {
        var player = ctx.Db.Player.Identity.Find(ctx.Sender) ?? throw new Exception("No player found for disconnected event");

        player.Online = false;
        ctx.Db.Player.Identity.Update(player);

        RemoveAllScheduledEventsForParticipant(ctx, ctx.Sender);
    }
}
