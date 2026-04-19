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

        if (player.Location == LocationType.Shelter)
        {
            StartShelterSchedules(ctx, player.Identity);
        }

        CleanupStaleActivities(ctx, player.Identity);
        EnsureActivityLevels(ctx, player.Identity);
        EnsurePlayerLevel(ctx, player.Identity);
        EnsureNewActivities(ctx, player.Identity);
    }

    [SpacetimeDB.Reducer(ReducerKind.ClientDisconnected)]
    public static void Disconnected(ReducerContext ctx)
    {
        var player = ctx.Db.Player.Identity.Find(ctx.Sender) ?? throw new Exception("No player found for disconnected event");

        player.Online = false;
        ctx.Db.Player.Identity.Update(player);

        HandleAdventureDisconnect(ctx, ctx.Sender);
        HandleRiskyBusinessDisconnect(ctx, ctx.Sender);
        RemoveAllScheduledEventsForParticipant(ctx, ctx.Sender);
    }

    /// <summary>Backfill PlayerLevel for players created before the level system existed.</summary>
    public static void EnsurePlayerLevel(ReducerContext ctx, Identity participant)
    {
        if (ctx.Db.PlayerLevel.Owner.Find(participant) is not null)
            return;

        ctx.Db.PlayerLevel.Insert(new PlayerLevel
        {
            Owner = participant,
            Level = 0,
            Xp = 0,
            AvailableSkillPoints = 0
        });
    }

    /// <summary>Delete Activity rows whose type byte exceeds the valid enum range
    /// (ghosts of the old enum: Study=3, Focus=4, BuildShelter=5, etc.).</summary>
    public static void CleanupStaleActivities(ReducerContext ctx, Identity participant)
    {
        foreach (var act in ctx.Db.Activity.Participant.Filter(participant))
        {
            if ((byte)act.Type > (byte)ActivityType.GatherFabric)
                ctx.Db.Activity.Id.Delete(act.Id);
        }
    }

    public static ulong? FindSkillIdByName(ReducerContext ctx, string name)
    {
        var skill = ctx.Db.SkillDefinition.Name.Find(name);
        return skill?.Id;
    }

    public static ulong? FindGearDefIdByName(ReducerContext ctx, string name)
    {
        var def = ctx.Db.GearDefinition.Name.Find(name);
        return def?.Id;
    }

    public static void EnsureNewActivities(ReducerContext ctx, Identity participant)
    {
        var unlockWoodNodeId = ctx.Db.SkillTreeNode.Name.Find("Unlock Wood")?.Id;
        var unlockScrapMetalNodeId = ctx.Db.SkillTreeNode.Name.Find("Unlock Scrap Metal")?.Id;
        var unlockFabricNodeId = ctx.Db.SkillTreeNode.Name.Find("Unlock Fabric")?.Id;

        UpsertActivity(ctx, participant, ActivityType.ChopWood, durationMs: 3000,
            requiredSkillTreeNodeId: unlockWoodNodeId);
        UpsertActivity(ctx, participant, ActivityType.Mine, durationMs: 3000,
            requiredSkillTreeNodeId: unlockScrapMetalNodeId);
        UpsertActivity(ctx, participant, ActivityType.GatherFabric, durationMs: 3000,
            requiredSkillTreeNodeId: unlockFabricNodeId);
    }

    private static void UpsertActivity(
        ReducerContext ctx, Identity participant,
        ActivityType type, ulong durationMs, ulong? requiredSkillTreeNodeId)
    {
        var existing = ctx.Db.Activity.by_activity_participant_type
            .Filter((Participant: participant, Type: type)).FirstOrDefault();

        if (existing.Id == 0 && existing.Participant == default)
        {
            ctx.Db.Activity.Insert(new Activity
            {
                Participant = participant,
                Type = type,
                Cost = [],
                DurationMs = durationMs,
                RequiredLocation = LocationType.Shelter,
                RequiredLevel = null,
                RequiredStructure = null,
                RequiredSkillId = null,
                RequiredSkillTreeNodeId = requiredSkillTreeNodeId,
                Level = 1
            });
            return;
        }

        if (existing.DurationMs != durationMs
            || existing.RequiredSkillTreeNodeId != requiredSkillTreeNodeId
            || existing.RequiredLevel != null
            || existing.RequiredSkillId != null)
        {
            ctx.Db.Activity.Id.Update(existing with
            {
                DurationMs = durationMs,
                RequiredLevel = null,
                RequiredSkillId = null,
                RequiredSkillTreeNodeId = requiredSkillTreeNodeId
            });
        }
    }
}
