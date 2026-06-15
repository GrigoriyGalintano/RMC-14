using Content.Shared._RMC14.Stains;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Map;

namespace Content.Server._RMC14.Stains;

[DataDefinition]
public sealed partial class RMCBloodTileReaction : ITileReaction
{
    [DataField]
    public FixedPoint2 MinimumVolume = FixedPoint2.New(3);

    public FixedPoint2 TileReact(
        TileRef tile,
        ReagentPrototype reagent,
        FixedPoint2 reactVolume,
        IEntityManager entityManager,
        List<ReagentData>? data)
    {
        if (reactVolume < MinimumVolume)
            return FixedPoint2.Zero;

        var stains = entityManager.System<RMCStainSystem>();
        var kind = reagent.ID == "RMCSynthBlood" ? RMCStainKind.Oil : RMCStainKind.Blood;
        var color = reagent.ID switch
        {
            "Blood" => RMCStainColors.HumanBlood,
            "RMCSynthBlood" => RMCStainColors.SynthBlood,
            "Slime" => RMCStainColors.XenoBlood,
            "ZombieBlood" => RMCStainColors.ZombieBlood,
            _ => reagent.SubstanceColor,
        };

        stains.TryCreateFloorStain(tile, kind, color, Math.Clamp((int)MathF.Ceiling(reactVolume.Float() / 5f), 1, 7), reagent.ID);
        return FixedPoint2.Zero;
    }
}

[DataDefinition]
public sealed partial class RMCCleanStainsTileReaction : ITileReaction
{
    [DataField]
    public FixedPoint2 CleanCost = FixedPoint2.New(0.25f);

    public FixedPoint2 TileReact(
        TileRef tile,
        ReagentPrototype reagent,
        FixedPoint2 reactVolume,
        IEntityManager entityManager,
        List<ReagentData>? data)
    {
        if (reactVolume <= CleanCost)
            return FixedPoint2.Zero;

        return entityManager.System<RMCStainSystem>().CleanFloorStains(tile, CleanCost);
    }
}
