using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Stains;
using Content.Shared.Armor;
using Content.Shared.Clothing.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Stains;

public sealed class RMCWashingMachineSystem : SharedRMCWashingMachineSystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCStainSystem _stain = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCWashingMachineComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<RMCWashingMachineComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<RMCWashingMachineComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<RMCWashingMachineComponent, OpenableClosedEvent>(OnClosed);
        SubscribeLocalEvent<RMCWashingMachineComponent, OpenableOpenAttemptEvent>(OnOpenAttempt);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<RMCWashingMachineComponent>();
        while (query.MoveNext(out var uid, out var machine))
        {
            if (!machine.Running || machine.FinishAt > now)
                continue;

            FinishCycle((uid, machine));
        }
    }

    private void OnComponentInit(Entity<RMCWashingMachineComponent> ent, ref ComponentInit args)
    {
        _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
    }

    private void OnInteractUsing(Entity<RMCWashingMachineComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Running)
        {
            _popup.PopupEntity(Loc.GetString("rmc-washing-machine-running"), ent, args.User, PopupType.MediumCaution);
            args.Handled = true;
            return;
        }

        if (!TryComp<OpenableComponent>(ent, out var openable) || !openable.Opened)
        {
            _popup.PopupEntity(Loc.GetString("rmc-washing-machine-closed"), ent, args.User, PopupType.MediumCaution);
            args.Handled = true;
            return;
        }

        var container = _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
        if (container.Count >= ent.Comp.MaxContents)
        {
            _popup.PopupEntity(Loc.GetString("rmc-washing-machine-full"), ent, args.User, PopupType.MediumCaution);
            args.Handled = true;
            return;
        }

        if (!IsWashable(args.Used))
        {
            _popup.PopupEntity(Loc.GetString("rmc-washing-machine-reject", ("item", args.Used)), ent, args.User, PopupType.MediumCaution);
            args.Handled = true;
            return;
        }

        if (!_container.Insert(args.Used, container))
            return;

        _popup.PopupEntity(Loc.GetString("rmc-washing-machine-insert", ("item", args.Used)), ent, args.User);
        args.Handled = true;
    }

    private void OnInteractHand(Entity<RMCWashingMachineComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Running)
        {
            _popup.PopupEntity(Loc.GetString("rmc-washing-machine-running"), ent, args.User, PopupType.MediumCaution);
            args.Handled = true;
            return;
        }

        if (!TryComp<OpenableComponent>(ent, out var openable) || !openable.Opened)
            return;

        var container = _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
        if (container.ContainedEntities.Count <= 0)
            return;

        var item = container.ContainedEntities[0];
        if (!_container.Remove(item, container))
            return;

        _hands.TryPickupAnyHand(args.User, item);
        _popup.PopupEntity(Loc.GetString("rmc-washing-machine-remove", ("item", item)), ent, args.User);
        args.Handled = true;
    }

    private void OnClosed(Entity<RMCWashingMachineComponent> ent, ref OpenableClosedEvent args)
    {
        if (ent.Comp.Running)
            return;

        var container = _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
        if (container.ContainedEntities.Count <= 0)
            return;

        SetRunning(ent, true, _timing.CurTime + ent.Comp.CycleTime);
        _popup.PopupEntity(Loc.GetString("rmc-washing-machine-start"), ent);
    }

    private void OnOpenAttempt(Entity<RMCWashingMachineComponent> ent, ref OpenableOpenAttemptEvent args)
    {
        if (!ent.Comp.Running)
            return;

        args.Cancelled = true;
        if (args.User != null)
            _popup.PopupEntity(Loc.GetString("rmc-washing-machine-running"), ent, args.User.Value, PopupType.MediumCaution);
    }

    private void FinishCycle(Entity<RMCWashingMachineComponent> ent)
    {
        var container = _container.EnsureContainer<Container>(ent, ent.Comp.ContainerId);
        var contained = new List<EntityUid>(container.ContainedEntities);
        foreach (var item in contained)
        {
            _stain.CleanEntityStainsAndForensics(item);
        }

        SetRunning(ent, false, TimeSpan.Zero);
        _popup.PopupEntity(Loc.GetString("rmc-washing-machine-finish"), ent);
    }

    private bool IsWashable(EntityUid item)
    {
        if (!TryComp<ClothingComponent>(item, out var clothing))
            return false;

        if ((clothing.Slots & (SlotFlags.HEAD | SlotFlags.MASK | SlotFlags.EYES | SlotFlags.EARS)) != 0)
            return false;

        if (HasComp<ArmorComponent>(item) || HasComp<CMArmorComponent>(item))
            return false;

        return (clothing.Slots & (SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING | SlotFlags.GLOVES | SlotFlags.FEET | SlotFlags.NECK)) != 0;
    }
}
