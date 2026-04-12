using SpacetimeDB;

[SpacetimeDB.Type]
public enum GuildRole : byte
{
    Member,
    Officer,
    Owner
}

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

        public GuildRole Role;
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

    [SpacetimeDB.Table(Accessor = "GuildJoinRequest", Public = true)]
    public partial struct GuildJoinRequest
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public ulong GuildId;

        [SpacetimeDB.Unique]
        public Identity RequesterId;

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

    private static bool IsOfficerOrAbove(GuildMember member) =>
        member.Role == GuildRole.Officer || member.Role == GuildRole.Owner;

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
            Role = GuildRole.Owner,
            InSession = false,
            JoinedAt = ctx.Timestamp
        });
    }

    [SpacetimeDB.Reducer]
    public static void InviteToGuild(ReducerContext ctx, Identity inviteeId)
    {
        if (ctx.Db.GuildMember.PlayerId.Find(ctx.Sender) is not GuildMember senderMember)
            throw new Exception("You are not in a guild");

        if (!IsOfficerOrAbove(senderMember))
            throw new Exception("Only officers and the owner can invite players");

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
            Role = GuildRole.Member,
            InSession = false,
            JoinedAt = ctx.Timestamp
        });

        foreach (var pending in ctx.Db.GuildInvite.InviteeId.Filter(ctx.Sender))
        {
            ctx.Db.GuildInvite.Id.Delete(pending.Id);
        }
    }

    [SpacetimeDB.Reducer]
    public static void RequestJoinGuild(ReducerContext ctx, ulong guildId)
    {
        if (ctx.Db.GuildMember.PlayerId.Find(ctx.Sender) is not null)
            throw new Exception("You are already in a guild");

        if (ctx.Db.Guild.Id.Find(guildId) is not Guild)
            throw new Exception("Guild does not exist");

        if (ctx.Db.GuildJoinRequest.RequesterId.Find(ctx.Sender) is not null)
            throw new Exception("You already have a pending join request");

        ctx.Db.GuildJoinRequest.Insert(new GuildJoinRequest
        {
            Id = 0,
            GuildId = guildId,
            RequesterId = ctx.Sender,
            CreatedAt = ctx.Timestamp
        });
    }

    [SpacetimeDB.Reducer]
    public static void CancelJoinRequest(ReducerContext ctx)
    {
        if (ctx.Db.GuildJoinRequest.RequesterId.Find(ctx.Sender) is not GuildJoinRequest request)
            throw new Exception("You have no pending join request");

        ctx.Db.GuildJoinRequest.Id.Delete(request.Id);
    }

    [SpacetimeDB.Reducer]
    public static void AcceptJoinRequest(ReducerContext ctx, ulong requestId)
    {
        if (ctx.Db.GuildMember.PlayerId.Find(ctx.Sender) is not GuildMember senderMember)
            throw new Exception("You are not in a guild");

        if (!IsOfficerOrAbove(senderMember))
            throw new Exception("Only officers and the owner can accept join requests");

        if (ctx.Db.GuildJoinRequest.Id.Find(requestId) is not GuildJoinRequest request)
            throw new Exception("Join request not found");

        if (request.GuildId != senderMember.GuildId)
            throw new Exception("That request is not for your guild");

        if (ctx.Db.GuildMember.PlayerId.Find(request.RequesterId) is not null)
            throw new Exception("That player is already in a guild");

        ctx.Db.GuildMember.Insert(new GuildMember
        {
            Id = 0,
            GuildId = senderMember.GuildId,
            PlayerId = request.RequesterId,
            Role = GuildRole.Member,
            InSession = false,
            JoinedAt = ctx.Timestamp
        });

        ctx.Db.GuildJoinRequest.Id.Delete(request.Id);
    }

    [SpacetimeDB.Reducer]
    public static void RejectJoinRequest(ReducerContext ctx, ulong requestId)
    {
        if (ctx.Db.GuildMember.PlayerId.Find(ctx.Sender) is not GuildMember senderMember)
            throw new Exception("You are not in a guild");

        if (!IsOfficerOrAbove(senderMember))
            throw new Exception("Only officers and the owner can reject join requests");

        if (ctx.Db.GuildJoinRequest.Id.Find(requestId) is not GuildJoinRequest request)
            throw new Exception("Join request not found");

        if (request.GuildId != senderMember.GuildId)
            throw new Exception("That request is not for your guild");

        ctx.Db.GuildJoinRequest.Id.Delete(request.Id);
    }

    [SpacetimeDB.Reducer]
    public static void PromoteMember(ReducerContext ctx, Identity playerId)
    {
        if (ctx.Db.GuildMember.PlayerId.Find(ctx.Sender) is not GuildMember senderMember)
            throw new Exception("You are not in a guild");

        if (!IsOfficerOrAbove(senderMember))
            throw new Exception("Only officers and the owner can promote members");

        if (ctx.Db.GuildMember.PlayerId.Find(playerId) is not GuildMember target)
            throw new Exception("Target player is not in a guild");

        if (target.GuildId != senderMember.GuildId)
            throw new Exception("That player is not in your guild");

        if (target.Role != GuildRole.Member)
            throw new Exception("Only members can be promoted to officer");

        ctx.Db.GuildMember.Id.Update(target with { Role = GuildRole.Officer });
    }

    [SpacetimeDB.Reducer]
    public static void DemoteMember(ReducerContext ctx, Identity playerId)
    {
        if (ctx.Db.GuildMember.PlayerId.Find(ctx.Sender) is not GuildMember senderMember)
            throw new Exception("You are not in a guild");

        if (!IsOfficerOrAbove(senderMember))
            throw new Exception("Only officers and the owner can demote members");

        if (ctx.Db.GuildMember.PlayerId.Find(playerId) is not GuildMember target)
            throw new Exception("Target player is not in a guild");

        if (target.GuildId != senderMember.GuildId)
            throw new Exception("That player is not in your guild");

        if (target.Role != GuildRole.Officer)
            throw new Exception("Only officers can be demoted");

        ctx.Db.GuildMember.Id.Update(target with { Role = GuildRole.Member });
    }

    [SpacetimeDB.Reducer]
    public static void KickMember(ReducerContext ctx, Identity playerId)
    {
        if (ctx.Db.GuildMember.PlayerId.Find(ctx.Sender) is not GuildMember senderMember)
            throw new Exception("You are not in a guild");

        if (!IsOfficerOrAbove(senderMember))
            throw new Exception("Only officers and the owner can kick members");

        if (ctx.Db.GuildMember.PlayerId.Find(playerId) is not GuildMember target)
            throw new Exception("Target player is not in a guild");

        if (target.GuildId != senderMember.GuildId)
            throw new Exception("That player is not in your guild");

        if (target.Role == GuildRole.Owner)
            throw new Exception("Cannot kick the guild owner");

        ctx.Db.GuildMember.Id.Delete(target.Id);
    }

    [SpacetimeDB.Reducer]
    public static void LeaveGuild(ReducerContext ctx)
    {
        if (ctx.Db.GuildMember.PlayerId.Find(ctx.Sender) is not GuildMember member)
            throw new Exception("You are not in a guild");

        if (member.Role == GuildRole.Owner)
            throw new Exception("The owner cannot leave the guild. Transfer ownership or disband instead.");

        var guildId = member.GuildId;
        ctx.Db.GuildMember.Id.Delete(member.Id);

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

        if (member.Role != GuildRole.Owner)
            throw new Exception("Only the guild owner can disband");

        var guildId = member.GuildId;

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

        foreach (var request in ctx.Db.GuildJoinRequest.GuildId.Filter(guildId))
        {
            ctx.Db.GuildJoinRequest.Id.Delete(request.Id);
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
