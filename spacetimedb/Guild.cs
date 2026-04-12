using SpacetimeDB;

public static partial class Module
{
    [SpacetimeDB.Table(Accessor = "Guild", Public = true)]
    public partial struct Guild
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        public Identity FounderId;
        public string Name;
        public Timestamp CreatedAt;
    }

    [SpacetimeDB.Table(Accessor = "GuildMember", Public = true)]
    public partial struct GuildMember
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public ulong GuildId;

        [SpacetimeDB.Unique]
        public Identity PlayerId;

        public bool InSession;
        public Timestamp JoinedAt;
    }

    [SpacetimeDB.Table(Accessor = "GuildInvite", Public = true)]
    public partial struct GuildInvite
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        public ulong GuildId;
        public Identity InviterId;

        [SpacetimeDB.Index.BTree]
        public Identity InviteeId;

        public Timestamp CreatedAt;
    }

    [SpacetimeDB.Table(Accessor = "GuildResourceTracker", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "by_guild_and_resource_type", Columns = new[] { "GuildId", "Type" })]
    public partial struct GuildResourceTracker
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public ulong GuildId;

        public ResourceType Type;
        public ulong Amount;
    }

    [SpacetimeDB.Reducer]
    public static void CreateGuild(ReducerContext ctx, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new Exception("Guild name cannot be empty");

        var existingMembership = ctx.Db.GuildMember.PlayerId.Find(ctx.Sender);
        if (existingMembership is not null)
            throw new Exception("You are already in a guild");

        var guild = ctx.Db.Guild.Insert(new Guild
        {
            Id = 0,
            FounderId = ctx.Sender,
            Name = name,
            CreatedAt = ctx.Timestamp
        });

        ctx.Db.GuildMember.Insert(new GuildMember
        {
            Id = 0,
            GuildId = guild.Id,
            PlayerId = ctx.Sender,
            InSession = false,
            JoinedAt = ctx.Timestamp
        });
    }

    [SpacetimeDB.Reducer]
    public static void InviteToGuild(ReducerContext ctx, Identity inviteeId)
    {
        if (ctx.Db.GuildMember.PlayerId.Find(ctx.Sender) is not GuildMember senderMember)
            throw new Exception("You are not in a guild");

        if (ctx.Db.Player.Identity.Find(inviteeId) is not Player)
            throw new Exception("Invited player does not exist");

        if (ctx.Db.GuildMember.PlayerId.Find(inviteeId) is not null)
            throw new Exception("That player is already in a guild");

        foreach (var invite in ctx.Db.GuildInvite.InviteeId.Filter(inviteeId))
        {
            if (invite.GuildId == senderMember.GuildId)
                throw new Exception("That player already has a pending invite to your guild");
        }

        ctx.Db.GuildInvite.Insert(new GuildInvite
        {
            Id = 0,
            GuildId = senderMember.GuildId,
            InviterId = ctx.Sender,
            InviteeId = inviteeId,
            CreatedAt = ctx.Timestamp
        });
    }

    [SpacetimeDB.Reducer]
    public static void JoinGuild(ReducerContext ctx, ulong inviteId)
    {
        if (ctx.Db.GuildInvite.Id.Find(inviteId) is not GuildInvite invite)
            throw new Exception("Invite not found");

        if (invite.InviteeId != ctx.Sender)
            throw new Exception("This invite is not for you");

        if (ctx.Db.GuildMember.PlayerId.Find(ctx.Sender) is not null)
            throw new Exception("You are already in a guild");

        if (ctx.Db.Guild.Id.Find(invite.GuildId) is not Guild)
            throw new Exception("Guild no longer exists");

        ctx.Db.GuildMember.Insert(new GuildMember
        {
            Id = 0,
            GuildId = invite.GuildId,
            PlayerId = ctx.Sender,
            InSession = false,
            JoinedAt = ctx.Timestamp
        });

        // Clean up all pending invites for this player
        foreach (var pending in ctx.Db.GuildInvite.InviteeId.Filter(ctx.Sender))
        {
            ctx.Db.GuildInvite.Id.Delete(pending.Id);
        }
    }

    [SpacetimeDB.Reducer]
    public static void LeaveGuild(ReducerContext ctx)
    {
        if (ctx.Db.GuildMember.PlayerId.Find(ctx.Sender) is not GuildMember member)
            throw new Exception("You are not in a guild");

        var guildId = member.GuildId;
        ctx.Db.GuildMember.Id.Delete(member.Id);

        // If last member, auto-disband
        var remainingMembers = ctx.Db.GuildMember.GuildId.Filter(guildId);
        if (!remainingMembers.Any())
        {
            CleanupGuild(ctx, guildId);
        }
    }

    [SpacetimeDB.Reducer]
    public static void DisbandGuild(ReducerContext ctx)
    {
        if (ctx.Db.GuildMember.PlayerId.Find(ctx.Sender) is not GuildMember member)
            throw new Exception("You are not in a guild");

        if (ctx.Db.Guild.Id.Find(member.GuildId) is not Guild guild)
            throw new Exception("Guild not found");

        if (guild.FounderId != ctx.Sender)
            throw new Exception("Only the guild founder can disband");

        var guildId = guild.Id;

        foreach (var m in ctx.Db.GuildMember.GuildId.Filter(guildId))
        {
            ctx.Db.GuildMember.Id.Delete(m.Id);
        }

        CleanupGuild(ctx, guildId);
    }

    [SpacetimeDB.Reducer]
    public static void EnterGuildSession(ReducerContext ctx)
    {
        if (ctx.Db.GuildMember.PlayerId.Find(ctx.Sender) is not GuildMember member)
            throw new Exception("You are not in a guild");

        if (member.InSession)
            throw new Exception("You are already in a guild session");

        ctx.Db.GuildMember.Id.Update(member with { InSession = true });
    }

    [SpacetimeDB.Reducer]
    public static void LeaveGuildSession(ReducerContext ctx)
    {
        if (ctx.Db.GuildMember.PlayerId.Find(ctx.Sender) is not GuildMember member)
            throw new Exception("You are not in a guild");

        if (!member.InSession)
            throw new Exception("You are not in a guild session");

        ctx.Db.GuildMember.Id.Update(member with { InSession = false });
    }

    private static void CleanupGuild(ReducerContext ctx, ulong guildId)
    {
        foreach (var invite in ctx.Db.GuildInvite.Iter())
        {
            if (invite.GuildId == guildId)
                ctx.Db.GuildInvite.Id.Delete(invite.Id);
        }

        foreach (var resource in ctx.Db.GuildResourceTracker.GuildId.Filter(guildId))
        {
            ctx.Db.GuildResourceTracker.Id.Delete(resource.Id);
        }

        ctx.Db.Guild.Id.Delete(guildId);
    }

    public static void AddResourceToGuild(ReducerContext ctx, ulong guildId, ResourceType type, ulong amount)
    {
        var existing = ctx.Db.GuildResourceTracker.by_guild_and_resource_type.Filter((GuildId: guildId, Type: type));

        if (!existing.Any())
        {
            ctx.Db.GuildResourceTracker.Insert(new GuildResourceTracker
            {
                Id = 0,
                GuildId = guildId,
                Type = type,
                Amount = amount
            });
            return;
        }

        var row = existing.First();
        ctx.Db.GuildResourceTracker.Id.Update(row with { Amount = row.Amount + amount });
    }
}
