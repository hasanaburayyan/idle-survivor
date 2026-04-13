using SpacetimeDB;

[SpacetimeDB.Type]
public enum LocationType : byte
{
    Waste,
    Shelter,
    GuildHall
}

public static partial class Module
{
    [SpacetimeDB.Table(Accessor = "StructureDefinition", Public = true)]
    public partial struct StructureDefinition
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Unique]
        public string Name;

        public List<ActivityCost> Cost;
    }

    [SpacetimeDB.Table(Accessor = "PlayerShelter", Public = true)]
    public partial struct PlayerShelter
    {
        [SpacetimeDB.PrimaryKey]
        public Identity Owner;

        public byte Level;
        public Timestamp BuiltAt;
    }

    [SpacetimeDB.Table(Accessor = "PlayerStructure", Public = true)]
    [SpacetimeDB.Index.BTree(Accessor = "by_owner_and_definition", Columns = new[] { "Owner", "DefinitionId" })]
    public partial struct PlayerStructure
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public Identity Owner;

        public ulong DefinitionId;
        public uint Level;
        public Timestamp BuiltAt;
    }

    [SpacetimeDB.Table(Accessor = "StructureModifier", Public = true)]
    public partial struct StructureModifier
    {
        [SpacetimeDB.PrimaryKey]
        [SpacetimeDB.AutoInc]
        public ulong Id;

        [SpacetimeDB.Index.BTree]
        public ulong PlayerStructureId;

        public string ModifierName;
        public int Value;
    }

    static bool IsLocationValid(LocationType? required, LocationType playerLoc) =>
        required is null ||
        required == playerLoc ||
        (required == LocationType.Shelter && playerLoc == LocationType.GuildHall);

    [SpacetimeDB.Reducer(ReducerKind.Init)]
    public static void Init(ReducerContext ctx)
    {
        Log.Info("Module initialized, seeding structure definitions");

        ctx.Db.StructureDefinition.Insert(new StructureDefinition
        {
            Id = 0,
            Name = "Smelter",
            Cost = new List<ActivityCost>
            {
                new ActivityCost { Type = ResourceType.Metal, Amount = 50 },
                new ActivityCost { Type = ResourceType.Parts, Amount = 30 }
            }
        });

    }

    [SpacetimeDB.Reducer]
    public static void Travel(ReducerContext ctx, LocationType destination)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
            throw new Exception("Player not found");

        if (player.Location == destination)
            throw new Exception("Already at that location");

        if (destination == LocationType.Shelter)
        {
            if (ctx.Db.PlayerShelter.Owner.Find(ctx.Sender) is null)
                throw new Exception("You have not built a shelter yet");
        }

        if (destination == LocationType.GuildHall)
        {
            if (ctx.Db.GuildMember.PlayerId.Find(ctx.Sender) is null)
                throw new Exception("You are not in a guild");
        }

        ctx.Db.Player.Identity.Update(player with { Location = destination });

        RemoveAllScheduledEventsForParticipant(ctx, ctx.Sender);

        if (destination == LocationType.Waste)
        {
            StartWasteSchedules(ctx, ctx.Sender);
        }
    }

    [SpacetimeDB.Reducer]
    public static void BuildStructure(ReducerContext ctx, ulong definitionId)
    {
        if (ctx.Db.Player.Identity.Find(ctx.Sender) is not Player player)
            throw new Exception("Player not found");

        if (!IsLocationValid(LocationType.Shelter, player.Location))
            throw new Exception("You must be in a shelter to build structures");

        if (ctx.Db.StructureDefinition.Id.Find(definitionId) is not StructureDefinition definition)
            throw new Exception("Structure definition not found");

        var existing = ctx.Db.PlayerStructure.by_owner_and_definition
            .Filter((Owner: ctx.Sender, DefinitionId: definitionId));
        if (existing.Any())
            throw new Exception($"You already have a {definition.Name}");

        foreach (var cost in definition.Cost)
        {
            var resource = ctx.Db.ResourceTracker.by_owner_and_type
                .Filter((Owner: ctx.Sender, Type: cost.Type));
            if (!resource.Any() || resource.First().Amount < cost.Amount)
                throw new Exception($"Insufficient {cost.Type}");
        }

        foreach (var cost in definition.Cost)
        {
            var resource = ctx.Db.ResourceTracker.by_owner_and_type
                .Filter((Owner: ctx.Sender, Type: cost.Type)).First();
            resource.Amount -= cost.Amount;
            ctx.Db.ResourceTracker.Id.Update(resource);
        }

        ctx.Db.PlayerStructure.Insert(new PlayerStructure
        {
            Id = 0,
            Owner = ctx.Sender,
            DefinitionId = definitionId,
            Level = 1,
            BuiltAt = ctx.Timestamp
        });

        ApplyPostBuildEffects(ctx, ctx.Sender, definition.Name);
    }

    private static void ApplyPostBuildEffects(ReducerContext ctx, Identity owner, string structureName)
    {
        switch (structureName)
        {
            case "Smelter":
                var hasSalvage = ctx.Db.Activity.by_activity_participant_type
                    .Filter((Participant: owner, Type: ActivityType.Salvage)).Any();
                if (!hasSalvage)
                {
                    ctx.Db.Activity.Insert(new Activity
                    {
                        Participant = owner,
                        Type = ActivityType.Salvage,
                        Cost = new List<ActivityCost>
                        {
                            new ActivityCost { Type = ResourceType.Parts, Amount = 10 }
                        },
                        DurationMs = 3000,
                        RequiredLocation = LocationType.Shelter,
                        UnlockCriteria = [],
                        Level = 1
                    });
                }
                break;
        }

    }

    private static readonly (ActivityType BuildType, ActivityType TrainType, List<ActivityCost> Cost)[] TrainingStructures =
    [
        (ActivityType.BuildDumbbells, ActivityType.TrainStrength, [new ActivityCost { Type = ResourceType.Metal, Amount = 40 }, new ActivityCost { Type = ResourceType.Parts, Amount = 20 }]),
        (ActivityType.BuildBookshelf, ActivityType.Study, [new ActivityCost { Type = ResourceType.Wood, Amount = 30 }, new ActivityCost { Type = ResourceType.Fabric, Amount = 30 }]),
        (ActivityType.BuildDartBoard, ActivityType.Focus, [new ActivityCost { Type = ResourceType.Wood, Amount = 25 }, new ActivityCost { Type = ResourceType.Metal, Amount = 25 }]),
        (ActivityType.BuildMeditationNook, ActivityType.TrainWit, [new ActivityCost { Type = ResourceType.Fabric, Amount = 35 }, new ActivityCost { Type = ResourceType.Wood, Amount = 25 }]),
        (ActivityType.BuildStairStepper, ActivityType.TrainEndurance, [new ActivityCost { Type = ResourceType.Metal, Amount = 40 }, new ActivityCost { Type = ResourceType.Wood, Amount = 20 }]),
        (ActivityType.BuildPingPongTable, ActivityType.TrainDexterity, [new ActivityCost { Type = ResourceType.Parts, Amount = 30 }, new ActivityCost { Type = ResourceType.Wood, Amount = 30 }]),
    ];

    public static void InsertBuildStructureActivities(ReducerContext ctx, Identity participant)
    {
        foreach (var (buildType, trainType, cost) in TrainingStructures)
        {
            if (ctx.Db.Activity.by_activity_participant_type
                .Filter((Participant: participant, Type: buildType)).Any())
                continue;

            if (ctx.Db.Activity.by_activity_participant_type
                .Filter((Participant: participant, Type: trainType)).Any())
                continue;

            ctx.Db.Activity.Insert(new Activity
            {
                Participant = participant,
                Type = buildType,
                Cost = cost,
                DurationMs = 10_000,
                RequiredLocation = LocationType.Shelter,
                UnlockCriteria = [],
                Level = 1
            });
        }
    }

    public static ActivityType? BuildTypeToTrainType(ActivityType buildType) => buildType switch
    {
        ActivityType.BuildDumbbells => ActivityType.TrainStrength,
        ActivityType.BuildBookshelf => ActivityType.Study,
        ActivityType.BuildDartBoard => ActivityType.Focus,
        ActivityType.BuildMeditationNook => ActivityType.TrainWit,
        ActivityType.BuildStairStepper => ActivityType.TrainEndurance,
        ActivityType.BuildPingPongTable => ActivityType.TrainDexterity,
        _ => null
    };
}
