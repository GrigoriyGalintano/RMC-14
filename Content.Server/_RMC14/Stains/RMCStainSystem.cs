using System.Numerics;
using Content.Server.Chemistry.EntitySystems;
using Content.Server.Decals;
using Content.Server.Forensics;
using Content.Server.Forensics.Components;
using Content.Server.Popups;
using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._RMC14.Stains;
using Content.Shared._RMC14.Weather;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Decals;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Forensics;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Stains;

public sealed class RMCStainSystem : EntitySystem
{
    private static readonly EntProtoId FloorStainPrototype = "RMCFloorStain";
    private static readonly HashSet<string> CleaningReagents = ["SpaceCleaner", "SoapReagent", "Water"];
    private static readonly HashSet<string> BloodReagents = ["Blood", "InsectBlood", "Slime", "CopperBlood", "AmmoniaBlood", "ZombieBlood", "RMCSynthBlood"];

    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RMCReagentSystem _reagents = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedRMCStainSystem _stains = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RMCWeatherSystem _weather = default!;

    private readonly HashSet<EntityUid> _nearby = new();
    private readonly HashSet<EntityUid> _puddles = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<RMCBodyStainsComponent, MoveEvent>(OnBodyMove);
        SubscribeLocalEvent<RMCFloorStainComponent, ComponentShutdown>(OnFloorStainShutdown);
        SubscribeLocalEvent<RMCStainableComponent, VaporHitEvent>(OnStainableVaporHit);
        SubscribeLocalEvent<RMCBodyStainsComponent, VaporHitEvent>(OnBodyVaporHit);
        SubscribeLocalEvent<RMCStainableComponent, ReactionEntityEvent>(OnStainableReaction);
        SubscribeLocalEvent<RMCBodyStainsComponent, ReactionEntityEvent>(OnBodyReaction);
        SubscribeLocalEvent<CleanForensicsDoAfterEvent>(OnCleanForensicsDoAfter, after: [typeof(ForensicsSystem)]);
        SubscribeLocalEvent<RMCShowerComponent, InteractHandEvent>(OnShowerInteractHand);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var floorQuery = EntityQueryEnumerator<RMCFloorStainComponent>();
        while (floorQuery.MoveNext(out var uid, out var floor))
        {
            if (!floor.Dried && floor.DryAt <= now)
                _stains.MarkFloorDried((uid, floor));
        }

        var showerQuery = EntityQueryEnumerator<RMCShowerComponent, TransformComponent>();
        while (showerQuery.MoveNext(out var uid, out var shower, out var xform))
        {
            if (!shower.Enabled || shower.NextClean > now)
                continue;

            _stains.SetShowerNextClean((uid, shower), now + shower.CleanInterval);
            CleanShowerTile((uid, shower, xform));
        }

        UpdateWeatherCleaning(frameTime);
    }

    private void OnMeleeHit(MeleeHitEvent args)
    {
        if (!args.IsHit)
            return;

        var physical = GetPhysicalDamage(args.BaseDamage);
        if (physical <= 0)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (!_stains.TryGetBloodStain(hit, out var kind, out var color, out var sourceReagent))
                continue;

            var weaponChance = Math.Clamp((physical * 2f + 25f) / 100f, 0f, 1f);
            if (_random.Prob(weaponChance))
                _stains.TryStain(args.Weapon, kind, color, hit, sourceReagent: sourceReagent);

            if (!InAdjacentRange(args.User, hit) || !_random.Prob(0.33f))
                continue;

            _stains.TryStainMob(args.User, RMCStainTargetFlags.Body | RMCStainTargetFlags.Hands, color, kind);
            _stains.TryStainMob(hit, RMCStainTargetFlags.Body, color, kind);

            if (_turf.TryGetTileRef(Transform(hit).Coordinates, out var tile))
                TryCreateFloorStain(tile.Value, kind, color, Math.Max(1, (int)MathF.Ceiling(physical / 5f)), sourceReagent);
        }
    }

    private void OnBodyMove(Entity<RMCBodyStainsComponent> ent, ref MoveEvent args)
    {
        if (args.OldPosition.EntityId == args.NewPosition.EntityId &&
            args.OldPosition.Position.Floored() == args.NewPosition.Position.Floored())
        {
            return;
        }

        if (args.Component.GridUid is not { } gridId ||
            !TryComp<MapGridComponent>(gridId, out var grid))
        {
            return;
        }

        var tile = _map.GetTileRef(gridId, grid, args.NewPosition);

        if (_stains.TryUseFootstep(ent, out var footstepColor))
        {
            TryCreateFloorStain(tile, RMCStainKind.Blood, footstepColor, 1, footprint: true, fresh: false);
        }

        CleanWaterTile(ent, tile);

        if (TryGetFloorStain(tile, out var floor) &&
            !floor.Comp.Dried &&
            floor.Comp.DryAt > _timing.CurTime)
        {
            _stains.TryStainMobFeet(ent, floor.Comp.Kind, floor.Comp.Color, Math.Max(2, floor.Comp.Amount * 2));
        }
    }

    public void TryCreateFloorStainFromPuddle(Entity<PuddleComponent> ent, Solution solution)
    {
        if (!TryGetSolutionStain(solution, out var kind, out var color, out var amount, out var sourceReagent))
        {
            return;
        }

        if (_turf.TryGetTileRef(Transform(ent).Coordinates, out var tile))
            TryCreateFloorStain(tile.Value, kind, color, amount, sourceReagent);
    }

    private void OnFloorStainShutdown(Entity<RMCFloorStainComponent> ent, ref ComponentShutdown args)
    {
        RemoveFloorDecals(ent);
    }

    private void OnStainableVaporHit(Entity<RMCStainableComponent> ent, ref VaporHitEvent args)
    {
        if (SolutionHasCleaner(args.Solution.Owner, args.Solution.Comp))
            _stains.TryCleanStain(ent);
    }

    private void OnBodyVaporHit(Entity<RMCBodyStainsComponent> ent, ref VaporHitEvent args)
    {
        if (SolutionHasCleaner(args.Solution.Owner, args.Solution.Comp))
            _stains.TryCleanMobStains(ent, RMCCleanStainFlags.All);
    }

    private void OnStainableReaction(Entity<RMCStainableComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method == ReactionMethod.Touch && CleaningReagents.Contains(args.Reagent.ID))
            _stains.TryCleanStain(ent);
    }

    private void OnBodyReaction(Entity<RMCBodyStainsComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method == ReactionMethod.Touch && CleaningReagents.Contains(args.Reagent.ID))
            _stains.TryCleanMobStains(ent, RMCCleanStainFlags.All);
    }

    private void OnCleanForensicsDoAfter(CleanForensicsDoAfterEvent args)
    {
        if (args.Cancelled || args.Target == null)
            return;

        _stains.TryCleanStain(args.Target.Value);
        _stains.TryCleanMobStains(args.Target.Value, RMCCleanStainFlags.All);
    }

    private void OnShowerInteractHand(Entity<RMCShowerComponent> ent, ref InteractHandEvent args)
    {
        var enabled = _stains.ToggleShower(ent);
        var message = enabled ? "rmc-shower-toggle-on" : "rmc-shower-toggle-off";
        _popup.PopupEntity(Loc.GetString(message), ent, args.User);
        args.Handled = true;
    }

    public bool TryCreateFloorStain(
        TileRef tile,
        RMCStainKind kind,
        Color color,
        int amount = 1,
        string? sourceReagent = null,
        bool footprint = false,
        bool fresh = true)
    {
        if (!TryComp<MapGridComponent>(tile.GridUid, out var grid) ||
            _turf.IsSpace(tile))
        {
            return false;
        }

        Entity<RMCFloorStainComponent> floor;
        if (!TryGetFloorStain(tile, out floor))
        {
            var center = _turf.GetTileCenter(tile);
            var uid = Spawn(FloorStainPrototype, center);
            floor = (uid, EnsureComp<RMCFloorStainComponent>(uid));
            _transform.AnchorEntity(uid, Transform(uid));
        }

        var dryAt = fresh
            ? _timing.CurTime + TimeSpan.FromSeconds(30 * (Math.Max(1, amount) + 1))
            : _timing.CurTime;

        _stains.UpdateFloorStain(floor, kind, color, amount, dryAt);
        if (!fresh)
            _stains.MarkFloorDried(floor);

        var decal = PickDecal(kind, color, footprint);
        var coords = _turf.GetTileCenter(tile).Offset(new Vector2(_random.NextFloat(-0.15f, 0.15f), _random.NextFloat(-0.15f, 0.15f)));
        if (_decals.TryAddDecal(decal, coords, out var decalId, color, Angle.FromDegrees(_random.NextFloat(0, 360)), cleanable: true))
            _stains.AddFloorDecal(floor, decalId);

        return true;
    }

    public FixedPoint2 CleanFloorStains(TileRef tile, FixedPoint2 cleanCost, bool cleanPuddles = true)
    {
        var cleaned = FixedPoint2.Zero;

        while (TryGetFloorStain(tile, out var floor))
        {
            RemoveFloorDecals(floor);
            QueueDel(floor);
            cleaned += cleanCost;
        }

        if (cleanPuddles)
            CleanBloodPuddles(tile);

        return cleaned;
    }

    public void CleanEntityStainsAndForensics(EntityUid target)
    {
        _stains.TryCleanStain(target);
        _stains.TryCleanMobStains(target, RMCCleanStainFlags.All);

        if (!TryComp<ForensicsComponent>(target, out var forensics))
            return;

        forensics.Fibers.Clear();
        forensics.Fingerprints.Clear();

        if (forensics.CanDnaBeCleaned)
            forensics.DNAs.Clear();
    }

    private void CleanShowerTile(Entity<RMCShowerComponent, TransformComponent> shower)
    {
        _nearby.Clear();
        _lookup.GetEntitiesInRange(shower.Comp2.Coordinates, shower.Comp1.Range, _nearby, LookupFlags.Uncontained);

        foreach (var target in _nearby)
        {
            if (target == shower.Owner)
                continue;

            _stains.TryCleanStain(target);
            _stains.TryCleanMobStains(target, RMCCleanStainFlags.All);
        }

        if (_turf.TryGetTileRef(shower.Comp2.Coordinates, out var tile))
            CleanFloorStains(tile.Value, FixedPoint2.New(0.25f));
    }

    private void CleanWaterTile(Entity<RMCBodyStainsComponent> body, TileRef tile)
    {
        var enumerator = _map.GetAnchoredEntitiesEnumerator(tile.GridUid, Comp<MapGridComponent>(tile.GridUid), tile.GridIndices);
        while (enumerator.MoveNext(out var anchored))
        {
            if (!TryComp<RMCWaterCleanStainsComponent>(anchored, out var water))
                continue;

            _stains.TryCleanMobStains(body, water.CleanInventory ? RMCCleanStainFlags.All : water.CleanFlags);
            return;
        }
    }

    private void UpdateWeatherCleaning(float frameTime)
    {
        var cycles = EntityQueryEnumerator<RMCWeatherCycleComponent, TransformComponent>();
        while (cycles.MoveNext(out var cycleUid, out var cycle, out var cycleXform))
        {
            if (cycle.CurrentEvent == null || !WeatherCleans(cycle.CurrentEvent))
                continue;

            if (!_weather.TryUseFloorStainCleanTick((cycleUid, cycle), TimeSpan.FromSeconds(frameTime)))
                continue;

            var stains = EntityQueryEnumerator<RMCFloorStainComponent, TransformComponent>();
            while (stains.MoveNext(out var stainUid, out _, out var stainXform))
            {
                if (stainXform.MapID != cycleXform.MapID ||
                    stainXform.GridUid is not { } gridId ||
                    !TryComp<MapGridComponent>(gridId, out var grid) ||
                    !_random.Prob(cycle.CurrentEvent.CleanChance))
                {
                    continue;
                }

                var tile = _map.GetTileRef(gridId, grid, stainXform.Coordinates);
                if (!_weather.CanWeatherAffectArea(gridId, grid, tile))
                    continue;

                CleanFloorStains(tile, FixedPoint2.New(0.25f));
            }
        }
    }

    private bool TryGetFloorStain(TileRef tile, out Entity<RMCFloorStainComponent> stain)
    {
        var grid = Comp<MapGridComponent>(tile.GridUid);
        var enumerator = _map.GetAnchoredEntitiesEnumerator(tile.GridUid, grid, tile.GridIndices);
        while (enumerator.MoveNext(out var anchored))
        {
            if (!TryComp<RMCFloorStainComponent>(anchored, out var component))
                continue;

            stain = (anchored.Value, component);
            return true;
        }

        stain = default;
        return false;
    }

    private void RemoveFloorDecals(Entity<RMCFloorStainComponent> floor)
    {
        var xform = Transform(floor);
        if (xform.GridUid is not { } grid)
            return;

        foreach (var decal in floor.Comp.DecalIds)
            _decals.RemoveDecal(grid, decal);

        _stains.ClearFloorDecals(floor);
    }

    private void CleanBloodPuddles(TileRef tile)
    {
        foreach (var uid in _lookup.GetLocalEntitiesIntersecting(tile, 0f))
        {
            if (!TryComp<PuddleComponent>(uid, out var puddle) ||
                !_solution.TryGetSolution(uid, puddle.SolutionName, out var solution, out var contents) ||
                !SolutionHasBlood(contents))
            {
                continue;
            }

            foreach (var reagent in contents.Contents.ToArray())
            {
                if (BloodReagents.Contains(reagent.Reagent.Prototype))
                    _solution.RemoveReagent(solution.Value, reagent.Reagent, reagent.Quantity);
            }
        }
    }

    private bool TryGetSolutionStain(Solution solution, out RMCStainKind kind, out Color color, out int amount, out string? sourceReagent)
    {
        kind = RMCStainKind.Blood;
        color = RMCStainColors.HumanBlood;
        amount = 0;
        sourceReagent = null;

        foreach (var reagent in solution.Contents)
        {
            if (!BloodReagents.Contains(reagent.Reagent.Prototype) ||
                reagent.Quantity < FixedPoint2.New(3))
            {
                continue;
            }

            GetStainForReagent(reagent.Reagent.Prototype, out kind, out color);
            amount = Math.Clamp((int)MathF.Ceiling(reagent.Quantity.Float() / 5f), 1, 7);
            sourceReagent = reagent.Reagent.Prototype;
            return true;
        }

        return false;
    }

    private bool SolutionHasBlood(Solution solution)
    {
        foreach (var reagent in solution.Contents)
        {
            if (BloodReagents.Contains(reagent.Reagent.Prototype))
                return true;
        }

        return false;
    }

    private bool SolutionHasCleaner(EntityUid uid, SolutionContainerManagerComponent? solution)
    {
        foreach (var (_, soln) in _solution.EnumerateSolutions((uid, solution)))
        {
            foreach (var reagent in soln.Comp.Solution.Contents)
            {
                if (CleaningReagents.Contains(reagent.Reagent.Prototype))
                    return true;
            }
        }

        return false;
    }

    private void GetStainForReagent(string reagent, out RMCStainKind kind, out Color color)
    {
        kind = reagent == "RMCSynthBlood" ? RMCStainKind.Oil : RMCStainKind.Blood;
        color = reagent switch
        {
            "Blood" => RMCStainColors.HumanBlood,
            "RMCSynthBlood" => RMCStainColors.SynthBlood,
            "Slime" => RMCStainColors.XenoBlood,
            "ZombieBlood" => RMCStainColors.ZombieBlood,
            _ when _reagents.TryIndex(reagent, out var proto) => proto.SubstanceColor,
            _ => RMCStainColors.HumanBlood,
        };
    }

    private string PickDecal(RMCStainKind kind, Color color, bool trail)
    {
        if (trail)
            return $"RMCDecalBloodTrail{_random.Next(1, 9)}";

        if (kind == RMCStainKind.Blood && color == RMCStainColors.XenoBlood)
            return $"RMCDecalBloodXenonidFloor{_random.Next(1, 8)}";

        return $"RMCDecalBloodFloor{_random.Next(1, 8)}";
    }

    private static bool WeatherCleans(RMCWeatherEvent weather)
    {
        if (weather.CleansFloorStains)
            return true;

        var id = weather.WeatherType.Id;
        if (id.Contains("Dust", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("Sand", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("Rock", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return id.Contains("Rain", StringComparison.OrdinalIgnoreCase) ||
               id.Contains("Storm", StringComparison.OrdinalIgnoreCase) ||
               id.Contains("Snow", StringComparison.OrdinalIgnoreCase) ||
               id.Contains("Blizzard", StringComparison.OrdinalIgnoreCase);
    }

    private bool InAdjacentRange(EntityUid first, EntityUid second)
    {
        return Transform(first).Coordinates.TryDistance(EntityManager, Transform(second).Coordinates, out var distance) &&
               distance <= 1.5f;
    }

    private static float GetPhysicalDamage(Content.Shared.Damage.DamageSpecifier damage)
    {
        var total = 0f;
        if (damage.DamageDict.TryGetValue("Blunt", out var blunt))
            total += blunt.Float();
        if (damage.DamageDict.TryGetValue("Slash", out var slash))
            total += slash.Float();
        if (damage.DamageDict.TryGetValue("Piercing", out var piercing))
            total += piercing.Float();
        if (damage.DamageDict.TryGetValue("Brute", out var brute))
            total += brute.Float();

        return total;
    }
}
