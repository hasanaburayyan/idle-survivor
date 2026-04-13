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

        ctx.Db.StructureDefinition.Insert(new StructureDefinition
        {
            Id = 0, Name = "Dumbbells",
            Cost = [new ActivityCost { Type = ResourceType.Metal, Amount = 40 }, new ActivityCost { Type = ResourceType.Parts, Amount = 20 }]
        });

        ctx.Db.StructureDefinition.Insert(new StructureDefinition
        {
            Id = 0, Name = "Bookshelf",
            Cost = [new ActivityCost { Type = ResourceType.Wood, Amount = 30 }, new ActivityCost { Type = ResourceType.Fabric, Amount = 30 }]
        });

        ctx.Db.StructureDefinition.Insert(new StructureDefinition
        {
            Id = 0, Name = "Dart Board",
            Cost = [new ActivityCost { Type = ResourceType.Wood, Amount = 25 }, new ActivityCost { Type = ResourceType.Metal, Amount = 25 }]
        });

        ctx.Db.StructureDefinition.Insert(new StructureDefinition
        {
            Id = 0, Name = "Meditation Nook",
            Cost = [new ActivityCost { Type = ResourceType.Fabric, Amount = 35 }, new ActivityCost { Type = ResourceType.Wood, Amount = 25 }]
        });

        ctx.Db.StructureDefinition.Insert(new StructureDefinition
        {
            Id = 0, Name = "Stair Stepper",
            Cost = [new ActivityCost { Type = ResourceType.Metal, Amount = 40 }, new ActivityCost { Type = ResourceType.Wood, Amount = 20 }]
        });

        ctx.Db.StructureDefinition.Insert(new StructureDefinition
        {
            Id = 0, Name = "Ping Pong Table",
            Cost = [new ActivityCost { Type = ResourceType.Parts, Amount = 30 }, new ActivityCost { Type = ResourceType.Wood, Amount = 30 }]
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

        InsertTrainingActivityIfMissing(ctx, owner, structureName);
    }

    private static void InsertTrainingActivityIfMissing(ReducerContext ctx, Identity owner, string structureName)
    {
        var mapping = structureName switch
        {
            "Dumbbells" => (ActivityType?)ActivityType.TrainStrength,
            "Bookshelf" => ActivityType.Study,
            "Dart Board" => ActivityType.Focus,
            "Meditation Nook" => ActivityType.TrainWit,
            "Stair Stepper" => ActivityType.TrainEndurance,
            "Ping Pong Table" => ActivityType.TrainDexterity,
            _ => null
        };

        if (mapping is not ActivityType activityType)
            return;

        if (ctx.Db.Activity.by_activity_participant_type
            .Filter((Participant: owner, Type: activityType)).Any())
            return;

        ctx.Db.Activity.Insert(new Activity
        {
            Participant = owner,
            Type = activityType,
            Cost = [],
            DurationMs = 5000,
            RequiredLocation = LocationType.Shelter,
            UnlockCriteria = [],
            Level = 1
        });
    }
}
