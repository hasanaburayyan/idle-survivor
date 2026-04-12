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
        StartActiveSchedules(ctx, player.Identity);
    }

    [SpacetimeDB.Reducer(ReducerKind.ClientDisconnected)]
    public static void Disconnected(ReducerContext ctx)
    {
        var player = ctx.Db.Player.Identity.Find(ctx.Sender) ?? throw new Exception("No player found for disconnected event");

        player.Online = false;
        ctx.Db.Player.Identity.Update(player);

        if (ctx.Db.GuildMember.PlayerId.Find(ctx.Sender) is GuildMember member && member.InSession)
        {
            ctx.Db.GuildMember.Id.Update(member with { InSession = false });
        }

        RemoveAllScheduledEventsForParticipant(ctx, ctx.Sender);
    }
}
