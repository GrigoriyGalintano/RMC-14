using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Body.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Robust.Shared.Random;

namespace Content.Shared._RMC14.Stains;

public sealed class SharedRMCStainSystem : EntitySystem
{
    private const string DefaultEquippedOverlayType = "item";

    private static readonly string[] BodySlots = ["outerClothing", "jumpsuit"];
    private static readonly string[] HeadSlots = ["mask", "head", "eyes"];

    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly RMCReagentSystem _reagents = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCStainableComponent, ExaminedEvent>(OnStainableExamined);
        SubscribeLocalEvent<RMCBodyStainsComponent, ExaminedEvent>(OnBodyStainsExamined);
    }

    private void OnStainableExamined(Entity<RMCStainableComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.IsStained)
            return;

        var loc = ent.Comp.Kind == RMCStainKind.Oil
            ? "rmc-stain-examine-oil"
            : "rmc-stain-examine-blood";

        args.PushMarkup(Loc.GetString(loc), priority: -1);
    }

    private void OnBodyStainsExamined(Entity<RMCBodyStainsComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.HasHandsStain)
            args.PushMarkup(Loc.GetString("rmc-stain-examine-hands"), priority: -1);

        if (ent.Comp.HasFeetStain)
            args.PushMarkup(Loc.GetString("rmc-stain-examine-feet"), priority: -1);
    }

    public bool HasCleanableStain(EntityUid target)
    {
        if (TryComp<RMCStainableComponent>(target, out var stainable) && stainable.IsStained)
            return true;

        if (TryComp<RMCBodyStainsComponent>(target, out var body) &&
            (body.HasHandsStain || body.HasFeetStain))
        {
            return true;
        }

        return false;
    }

    public bool TryStain(
        EntityUid target,
        RMCStainKind kind,
        Color color,
        EntityUid? source = null,
        RMCStainApplyMode mode = RMCStainApplyMode.Direct,
        string? sourceReagent = null)
    {
        if (Deleted(target) || HasComp<RMCNoStainComponent>(target))
            return false;

        var component = EnsureComp<RMCStainableComponent>(target);
        var wasClean = !component.IsStained;

        if (mode == RMCStainApplyMode.Touch && !wasClean)
            return false;

        component.IsStained = true;
        component.Kind = kind;
        component.Color = color;
        component.SourceReagent = sourceReagent;

        ApplyOverlaySettings(target, component);
        if (component.EquippedOverlayType == DefaultEquippedOverlayType &&
            TryComp<ClothingComponent>(target, out var clothing))
        {
            component.EquippedOverlayType = GetEquippedOverlayType(clothing.Slots);
        }

        Dirty(target, component);
        UpdateStainAppearance((target, component));
        _item.VisualsChanged(target);
        return wasClean;
    }

    public bool TryCleanStain(EntityUid target, RMCCleanStainFlags flags = RMCCleanStainFlags.All)
    {
        if (!TryComp<RMCStainableComponent>(target, out var component) || !component.IsStained)
            return false;

        component.IsStained = false;
        Dirty(target, component);
        UpdateStainAppearance((target, component));
        _item.VisualsChanged(target);
        return true;
    }

    public bool TryStainMob(
        EntityUid mob,
        RMCStainTargetFlags flags,
        Color color,
        RMCStainKind kind = RMCStainKind.Blood)
    {
        var any = false;

        if ((flags & RMCStainTargetFlags.Body) != 0)
            any |= TryStainMobSlots(mob, BodySlots, kind, color);

        if ((flags & RMCStainTargetFlags.Hands) != 0)
            any |= TryStainMobHands(mob, kind, color);

        if ((flags & RMCStainTargetFlags.Feet) != 0)
            any |= TryStainMobFeet(mob, kind, color, 8);

        return any;
    }

    public bool TryStainMobHead(EntityUid mob, Color color, RMCStainKind kind = RMCStainKind.Blood)
    {
        return TryStainMobSlots(mob, HeadSlots, kind, color) ||
               TryStainMob(mob, RMCStainTargetFlags.Body, color, kind);
    }

    public bool TryStainMobFeet(EntityUid mob, RMCStainKind kind, Color color, int footsteps)
    {
        if (_inventory.TryGetSlotEntity(mob, "shoes", out var shoes))
            return TryStain(shoes.Value, kind, color);

        var body = EnsureComp<RMCBodyStainsComponent>(mob);
        var wasClean = !body.HasFeetStain;
        body.HasFeetStain = true;
        body.FeetColor = color;
        body.FootstepColor = color;
        body.FootstepsRemaining = Math.Max(body.FootstepsRemaining, footsteps);
        Dirty(mob, body);
        UpdateBodyAppearance((mob, body));
        return wasClean;
    }

    public bool TryCleanMobStains(EntityUid mob, RMCCleanStainFlags flags = RMCCleanStainFlags.All)
    {
        var any = false;

        if ((flags & RMCCleanStainFlags.Hands) != 0)
        {
            if (_inventory.TryGetSlotEntity(mob, "gloves", out var gloves))
                any |= TryCleanStain(gloves.Value);

            if (TryComp<RMCBodyStainsComponent>(mob, out var body) && body.HasHandsStain)
            {
                body.HasHandsStain = false;
                body.HandTransfersRemaining = 0;
                Dirty(mob, body);
                UpdateBodyAppearance((mob, body));
                any = true;
            }
        }

        if ((flags & RMCCleanStainFlags.Feet) != 0)
        {
            if (_inventory.TryGetSlotEntity(mob, "shoes", out var shoes))
                any |= TryCleanStain(shoes.Value);

            if (TryComp<RMCBodyStainsComponent>(mob, out var body) && body.HasFeetStain)
            {
                body.HasFeetStain = false;
                body.FootstepsRemaining = 0;
                Dirty(mob, body);
                UpdateBodyAppearance((mob, body));
                any = true;
            }
        }

        if ((flags & RMCCleanStainFlags.Body) != 0)
            any |= CleanSlots(mob, BodySlots) | CleanSlots(mob, HeadSlots);

        if ((flags & RMCCleanStainFlags.Inventory) != 0)
            any |= CleanInventory(mob);

        if ((flags & RMCCleanStainFlags.HeldItems) != 0)
            any |= CleanHeldItems(mob);

        return any;
    }

    public bool TryGetBloodStain(EntityUid source, out RMCStainKind kind, out Color color, out string? sourceReagent)
    {
        if (TryComp<RMCBloodColorComponent>(source, out var colorComponent))
        {
            kind = colorComponent.Kind;
            color = colorComponent.Color;
            sourceReagent = null;
            return true;
        }

        if (TryComp<BloodstreamComponent>(source, out var bloodstream))
        {
            var bloodReagent = bloodstream.BloodReagent.Id;
            sourceReagent = bloodReagent;

            if (bloodReagent == "RMCSynthBlood")
            {
                kind = RMCStainKind.Oil;
                color = RMCStainColors.SynthBlood;
                return true;
            }

            if (HasComp<XenoComponent>(source))
            {
                kind = RMCStainKind.Blood;
                color = RMCStainColors.XenoBlood;
                return true;
            }

            kind = RMCStainKind.Blood;
            color = bloodReagent switch
            {
                "Blood" => RMCStainColors.HumanBlood,
                "ZombieBlood" => RMCStainColors.ZombieBlood,
                _ when _reagents.TryIndex(bloodstream.BloodReagent, out var reagent) => reagent.SubstanceColor,
                _ => RMCStainColors.HumanBlood,
            };
            return true;
        }

        kind = RMCStainKind.Blood;
        color = Color.White;
        sourceReagent = null;
        return false;
    }

    public bool TryGetBloodStain(EntityUid source, out RMCStainKind kind, out Color color)
    {
        return TryGetBloodStain(source, out kind, out color, out _);
    }

    public bool TryTransferHandStain(EntityUid user, EntityUid target)
    {
        if (HasComp<RMCNoStainComponent>(target))
            return false;

        if (_inventory.TryGetSlotEntity(user, "gloves", out var gloves) &&
            TryComp<RMCStainableComponent>(gloves, out var gloveStain) &&
            gloveStain.IsStained)
        {
            return TryStain(target, gloveStain.Kind, gloveStain.Color, user, RMCStainApplyMode.Touch, gloveStain.SourceReagent);
        }

        if (!TryComp<RMCBodyStainsComponent>(user, out var body) ||
            !body.HasHandsStain ||
            body.HandTransfersRemaining <= 0)
        {
            return false;
        }

        if (!TryStain(target, RMCStainKind.Blood, body.HandsColor, user, RMCStainApplyMode.Touch))
            return false;

        body.HandTransfersRemaining--;
        if (body.HandTransfersRemaining <= 0)
            body.HasHandsStain = false;

        Dirty(user, body);
        UpdateBodyAppearance((user, body));
        return true;
    }

    public bool TryUseFootstep(EntityUid mob, out Color color)
    {
        color = RMCStainColors.HumanBlood;
        if (!TryComp<RMCBodyStainsComponent>(mob, out var body) ||
            !body.HasFeetStain ||
            body.FootstepsRemaining <= 0)
        {
            return false;
        }

        color = body.FootstepColor;
        body.FootstepsRemaining--;
        if (body.FootstepsRemaining <= 0 && !_inventory.TryGetSlotEntity(mob, "shoes", out _))
            body.HasFeetStain = false;

        Dirty(mob, body);
        UpdateBodyAppearance((mob, body));
        return true;
    }

    public bool ToggleShower(Entity<RMCShowerComponent> shower)
    {
        shower.Comp.Enabled = !shower.Comp.Enabled;
        shower.Comp.NextClean = TimeSpan.Zero;
        Dirty(shower);
        return shower.Comp.Enabled;
    }

    public void SetShowerNextClean(Entity<RMCShowerComponent> shower, TimeSpan nextClean)
    {
        shower.Comp.NextClean = nextClean;
        Dirty(shower);
    }

    public void UpdateFloorStain(Entity<RMCFloorStainComponent> floor, RMCStainKind kind, Color color, int amount, TimeSpan dryAt)
    {
        floor.Comp.Kind = kind;
        floor.Comp.Color = color;
        floor.Comp.Amount = Math.Max(floor.Comp.Amount, amount);
        floor.Comp.DryAt = dryAt;
        floor.Comp.Dried = false;
        Dirty(floor);
    }

    public void AddFloorDecal(Entity<RMCFloorStainComponent> floor, uint decal)
    {
        floor.Comp.DecalIds.Add(decal);
        Dirty(floor);
    }

    public void MarkFloorDried(Entity<RMCFloorStainComponent> floor)
    {
        if (floor.Comp.Dried)
            return;

        floor.Comp.Dried = true;
        Dirty(floor);
    }

    public void ClearFloorDecals(Entity<RMCFloorStainComponent> floor)
    {
        floor.Comp.DecalIds.Clear();
        Dirty(floor);
    }

    private bool TryStainMobSlots(EntityUid mob, IReadOnlyList<string> slots, RMCStainKind kind, Color color)
    {
        foreach (var slot in slots)
        {
            if (_inventory.TryGetSlotEntity(mob, slot, out var entity))
                return TryStain(entity.Value, kind, color);
        }

        return false;
    }

    private bool TryStainMobHands(EntityUid mob, RMCStainKind kind, Color color)
    {
        if (_inventory.TryGetSlotEntity(mob, "gloves", out var gloves))
            return TryStain(gloves.Value, kind, color);

        var body = EnsureComp<RMCBodyStainsComponent>(mob);
        var wasClean = !body.HasHandsStain;
        body.HasHandsStain = true;
        body.HandsColor = color;
        body.HandTransfersRemaining = _random.Next(2, 5);
        Dirty(mob, body);
        UpdateBodyAppearance((mob, body));
        return wasClean;
    }

    private bool CleanSlots(EntityUid mob, IReadOnlyList<string> slots)
    {
        var any = false;
        foreach (var slot in slots)
        {
            if (_inventory.TryGetSlotEntity(mob, slot, out var item))
                any |= TryCleanStain(item.Value);
        }

        return any;
    }

    private bool CleanInventory(EntityUid mob)
    {
        var any = false;
        var enumerator = _inventory.GetSlotEnumerator(mob, SlotFlags.All);
        while (enumerator.NextItem(out var item, out _))
        {
            any |= TryCleanStain(item);
        }

        return any;
    }

    private bool CleanHeldItems(EntityUid mob)
    {
        var any = false;
        foreach (var held in _hands.EnumerateHeld(mob))
        {
            any |= TryCleanStain(held);
        }

        return any;
    }

    private void UpdateStainAppearance(Entity<RMCStainableComponent> ent)
    {
        var appearance = EnsureComp<AppearanceComponent>(ent);
        _appearance.SetData(ent, RMCStainVisuals.Visible, ent.Comp.IsStained, appearance);
        _appearance.SetData(ent, RMCStainVisuals.Kind, ent.Comp.Kind, appearance);
        _appearance.SetData(ent, RMCStainVisuals.Color, ent.Comp.Color, appearance);
        _appearance.SetData(ent, RMCStainVisuals.WorldState, ent.Comp.WorldOverlayState, appearance);
        _appearance.SetData(ent, RMCStainVisuals.InhandState, ent.Comp.InhandOverlayState, appearance);
        _appearance.SetData(ent, RMCStainVisuals.EquippedType, ent.Comp.EquippedOverlayType, appearance);
    }

    private void UpdateBodyAppearance(Entity<RMCBodyStainsComponent> ent)
    {
        var appearance = EnsureComp<AppearanceComponent>(ent);
        _appearance.SetData(ent, RMCStainVisuals.Visible, ent.Comp.HasHandsStain || ent.Comp.HasFeetStain, appearance);
        _appearance.SetData(ent, RMCStainVisuals.Color, ent.Comp.HandsColor, appearance);
    }

    private void ApplyOverlaySettings(EntityUid target, RMCStainableComponent component)
    {
        if (!TryComp<RMCStainOverlayComponent>(target, out var overlay))
            return;

        if (overlay.WorldOverlayState != null)
            component.WorldOverlayState = overlay.WorldOverlayState;

        if (overlay.InhandOverlayState != null)
            component.InhandOverlayState = overlay.InhandOverlayState;

        if (overlay.EquippedOverlayType != null)
            component.EquippedOverlayType = overlay.EquippedOverlayType;
    }

    private static string GetEquippedOverlayType(SlotFlags flags)
    {
        if ((flags & SlotFlags.OUTERCLOTHING) != 0)
            return "suit";
        if ((flags & SlotFlags.INNERCLOTHING) != 0)
            return "uniform";
        if ((flags & SlotFlags.HEAD) != 0)
            return "helmet";
        if ((flags & SlotFlags.MASK) != 0)
            return "mask";
        if ((flags & SlotFlags.GLOVES) != 0)
            return "hands";
        if ((flags & SlotFlags.FEET) != 0)
            return "feet";
        if ((flags & SlotFlags.EYES) != 0)
            return "mask";

        return DefaultEquippedOverlayType;
    }
}
