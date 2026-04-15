using SpacetimeDB;

public static partial class Module
{
    // ── Friend Request ──────────────────────────────────────────────────
    [SpacetimeDB.Table(Accessor = "FriendRequest", Public = true)]
    public partial struct FriendRequest
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public Identity SenderId;

        [SpacetimeDB.Index.BTree]
        public Identity ReceiverId;

        public Timestamp CreatedAt;
    }

    // ── Friendship (one row per pair) ───────────────────────────────────
    [SpacetimeDB.Table(Accessor = "Friendship", Public = true)]
    public partial struct Friendship
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public Identity PlayerA;

        [SpacetimeDB.Index.BTree]
        public Identity PlayerB;

        public Timestamp CreatedAt;
    }

    // ── Party ───────────────────────────────────────────────────────────
    [SpacetimeDB.Table(Accessor = "Party", Public = true)]
    public partial struct Party
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        public Identity LeaderId;
        public Timestamp CreatedAt;
    }

    [SpacetimeDB.Table(Accessor = "PartyMember", Public = true)]
    public partial struct PartyMember
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public ulong PartyId;

        [SpacetimeDB.Unique]
        public Identity PlayerId;

        public Timestamp JoinedAt;
    }

    [SpacetimeDB.Table(Accessor = "PartyInvite", Public = true)]
    public partial struct PartyInvite
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        public ulong PartyId;
        public Identity InviterId;

        [SpacetimeDB.Index.BTree]
        public Identity InviteeId;

        public Timestamp CreatedAt;
    }

    // ── Helper: check if two identities are friends ─────────────────────
    private static bool AreFriends(ReducerContext ctx, Identity a, Identity b)
    {
        foreach (var f in ctx.Db.Friendship.PlayerA.Filter(a))
            if (f.PlayerB == b) return true;
        foreach (var f in ctx.Db.Friendship.PlayerA.Filter(b))
            if (f.PlayerB == a) return true;
        return false;
    }

    private static int CountPartyMembers(ReducerContext ctx, ulong partyId)
    {
        int count = 0;
        foreach (var _ in ctx.Db.PartyMember.PartyId.Filter(partyId))
            count++;
        return count;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  FRIEND REDUCERS
    // ═══════════════════════════════════════════════════════════════════

    [SpacetimeDB.Reducer]
    public static void SendFriendRequest(ReducerContext ctx, string targetPlayerName)
    {
        if (string.IsNullOrWhiteSpace(targetPlayerName))
            throw new Exception("Player name cannot be empty");

        Player? target = null;
        foreach (var p in ctx.Db.Player.Iter())
        {
            if (p.DisplayName == targetPlayerName)
            {
                target = p;
                break;
            }
        }
        if (target is not Player targetPlayer)
            throw new Exception("Player not found");

        if (targetPlayer.Identity == ctx.Sender)
            throw new Exception("Cannot send a friend request to yourself");

        if (AreFriends(ctx, ctx.Sender, targetPlayer.Identity))
            throw new Exception("You are already friends with that player");

        foreach (var req in ctx.Db.FriendRequest.SenderId.Filter(ctx.Sender))
            if (req.ReceiverId == targetPlayer.Identity)
                throw new Exception("You already have a pending request to that player");

        foreach (var req in ctx.Db.FriendRequest.ReceiverId.Filter(ctx.Sender))
            if (req.SenderId == targetPlayer.Identity)
                throw new Exception("That player already sent you a friend request");

        ctx.Db.FriendRequest.Insert(new FriendRequest
        {
            Id = 0,
            SenderId = ctx.Sender,
            ReceiverId = targetPlayer.Identity,
            CreatedAt = ctx.Timestamp
        });
    }

    [SpacetimeDB.Reducer]
    public static void AcceptFriendRequest(ReducerContext ctx, ulong requestId)
    {
        if (ctx.Db.FriendRequest.Id.Find(requestId) is not FriendRequest req)
            throw new Exception("Friend request not found");

        if (req.ReceiverId != ctx.Sender)
            throw new Exception("This request is not for you");

        ctx.Db.Friendship.Insert(new Friendship
        {
            Id = 0,
            PlayerA = req.SenderId,
            PlayerB = req.ReceiverId,
            CreatedAt = ctx.Timestamp
        });

        ctx.Db.FriendRequest.Id.Delete(req.Id);
    }

    [SpacetimeDB.Reducer]
    public static void DeclineFriendRequest(ReducerContext ctx, ulong requestId)
    {
        if (ctx.Db.FriendRequest.Id.Find(requestId) is not FriendRequest req)
            throw new Exception("Friend request not found");

        if (req.ReceiverId != ctx.Sender)
            throw new Exception("This request is not for you");

        ctx.Db.FriendRequest.Id.Delete(req.Id);
    }

    [SpacetimeDB.Reducer]
    public static void CancelFriendRequest(ReducerContext ctx, ulong requestId)
    {
        if (ctx.Db.FriendRequest.Id.Find(requestId) is not FriendRequest req)
            throw new Exception("Friend request not found");

        if (req.SenderId != ctx.Sender)
            throw new Exception("This is not your request");

        ctx.Db.FriendRequest.Id.Delete(req.Id);
    }

    [SpacetimeDB.Reducer]
    public static void RemoveFriend(ReducerContext ctx, ulong friendshipId)
    {
        if (ctx.Db.Friendship.Id.Find(friendshipId) is not Friendship friendship)
            throw new Exception("Friendship not found");

        if (friendship.PlayerA != ctx.Sender && friendship.PlayerB != ctx.Sender)
            throw new Exception("You are not part of this friendship");

        var other = friendship.PlayerA == ctx.Sender ? friendship.PlayerB : friendship.PlayerA;

        foreach (var invite in ctx.Db.PartyInvite.InviteeId.Filter(other))
            if (invite.InviterId == ctx.Sender)
                ctx.Db.PartyInvite.Id.Delete(invite.Id);
        foreach (var invite in ctx.Db.PartyInvite.InviteeId.Filter(ctx.Sender))
            if (invite.InviterId == other)
                ctx.Db.PartyInvite.Id.Delete(invite.Id);

        ctx.Db.Friendship.Id.Delete(friendshipId);
    }

    // ═══════════════════════════════════════════════════════════════════
    //  PARTY REDUCERS
    // ═══════════════════════════════════════════════════════════════════

    [SpacetimeDB.Reducer]
    public static void CreateParty(ReducerContext ctx)
    {
        if (ctx.Db.PartyMember.PlayerId.Find(ctx.Sender) is not null)
            throw new Exception("You are already in a party");

        var party = ctx.Db.Party.Insert(new Party
        {
            Id = 0,
            LeaderId = ctx.Sender,
            CreatedAt = ctx.Timestamp
        });

        ctx.Db.PartyMember.Insert(new PartyMember
        {
            Id = 0,
            PartyId = party.Id,
            PlayerId = ctx.Sender,
            JoinedAt = ctx.Timestamp
        });
    }

    [SpacetimeDB.Reducer]
    public static void InviteToParty(ReducerContext ctx, Identity targetIdentity)
    {
        if (ctx.Db.PartyMember.PlayerId.Find(ctx.Sender) is not PartyMember senderMember)
            throw new Exception("You are not in a party");

        if (!AreFriends(ctx, ctx.Sender, targetIdentity))
            throw new Exception("You can only invite friends to your party");

        if (ctx.Db.PartyMember.PlayerId.Find(targetIdentity) is not null)
            throw new Exception("That player is already in a party");

        if (CountPartyMembers(ctx, senderMember.PartyId) >= 4)
            throw new Exception("Party is full (max 4)");

        foreach (var invite in ctx.Db.PartyInvite.InviteeId.Filter(targetIdentity))
            if (invite.PartyId == senderMember.PartyId)
                throw new Exception("That player already has a pending invite to your party");

        ctx.Db.PartyInvite.Insert(new PartyInvite
        {
            Id = 0,
            PartyId = senderMember.PartyId,
            InviterId = ctx.Sender,
            InviteeId = targetIdentity,
            CreatedAt = ctx.Timestamp
        });
    }

    [SpacetimeDB.Reducer]
    public static void AcceptPartyInvite(ReducerContext ctx, ulong inviteId)
    {
        if (ctx.Db.PartyInvite.Id.Find(inviteId) is not PartyInvite invite)
            throw new Exception("Party invite not found");

        if (invite.InviteeId != ctx.Sender)
            throw new Exception("This invite is not for you");

        if (ctx.Db.PartyMember.PlayerId.Find(ctx.Sender) is not null)
            throw new Exception("You are already in a party");

        if (ctx.Db.Party.Id.Find(invite.PartyId) is not Party)
            throw new Exception("Party no longer exists");

        if (CountPartyMembers(ctx, invite.PartyId) >= 4)
            throw new Exception("Party is full (max 4)");

        ctx.Db.PartyMember.Insert(new PartyMember
        {
            Id = 0,
            PartyId = invite.PartyId,
            PlayerId = ctx.Sender,
            JoinedAt = ctx.Timestamp
        });

        foreach (var pending in ctx.Db.PartyInvite.InviteeId.Filter(ctx.Sender))
            ctx.Db.PartyInvite.Id.Delete(pending.Id);
    }

    [SpacetimeDB.Reducer]
    public static void DeclinePartyInvite(ReducerContext ctx, ulong inviteId)
    {
        if (ctx.Db.PartyInvite.Id.Find(inviteId) is not PartyInvite invite)
            throw new Exception("Party invite not found");

        if (invite.InviteeId != ctx.Sender)
            throw new Exception("This invite is not for you");

        ctx.Db.PartyInvite.Id.Delete(inviteId);
    }

    [SpacetimeDB.Reducer]
    public static void LeaveParty(ReducerContext ctx)
    {
        if (ctx.Db.PartyMember.PlayerId.Find(ctx.Sender) is not PartyMember member)
            throw new Exception("You are not in a party");

        var partyId = member.PartyId;

        if (ctx.Db.Party.Id.Find(partyId) is not Party party)
            throw new Exception("Party not found");

        ctx.Db.PartyMember.Id.Delete(member.Id);

        var remaining = new List<PartyMember>();
        foreach (var m in ctx.Db.PartyMember.PartyId.Filter(partyId))
            remaining.Add(m);

        if (remaining.Count == 0)
        {
            CleanupParty(ctx, partyId);
            return;
        }

        if (party.LeaderId == ctx.Sender)
        {
            var newLeader = remaining[0];
            ctx.Db.Party.Id.Update(party with { LeaderId = newLeader.PlayerId });
        }
    }

    [SpacetimeDB.Reducer]
    public static void KickFromParty(ReducerContext ctx, Identity targetIdentity)
    {
        if (ctx.Db.PartyMember.PlayerId.Find(ctx.Sender) is not PartyMember senderMember)
            throw new Exception("You are not in a party");

        if (ctx.Db.Party.Id.Find(senderMember.PartyId) is not Party party)
            throw new Exception("Party not found");

        if (party.LeaderId != ctx.Sender)
            throw new Exception("Only the party leader can kick members");

        if (targetIdentity == ctx.Sender)
            throw new Exception("Cannot kick yourself");

        if (ctx.Db.PartyMember.PlayerId.Find(targetIdentity) is not PartyMember target)
            throw new Exception("Target is not in a party");

        if (target.PartyId != senderMember.PartyId)
            throw new Exception("Target is not in your party");

        ctx.Db.PartyMember.Id.Delete(target.Id);
    }

    [SpacetimeDB.Reducer]
    public static void DisbandParty(ReducerContext ctx)
    {
        if (ctx.Db.PartyMember.PlayerId.Find(ctx.Sender) is not PartyMember member)
            throw new Exception("You are not in a party");

        if (ctx.Db.Party.Id.Find(member.PartyId) is not Party party)
            throw new Exception("Party not found");

        if (party.LeaderId != ctx.Sender)
            throw new Exception("Only the party leader can disband");

        var partyId = member.PartyId;
        foreach (var m in ctx.Db.PartyMember.PartyId.Filter(partyId))
            ctx.Db.PartyMember.Id.Delete(m.Id);

        CleanupParty(ctx, partyId);
    }

    private static void CleanupParty(ReducerContext ctx, ulong partyId)
    {
        foreach (var invite in ctx.Db.PartyInvite.Iter())
            if (invite.PartyId == partyId)
                ctx.Db.PartyInvite.Id.Delete(invite.Id);

        ctx.Db.Party.Id.Delete(partyId);
    }
}
