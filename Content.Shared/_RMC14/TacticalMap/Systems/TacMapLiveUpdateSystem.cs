using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Timing;

namespace Content.Shared._RMC14.TacticalMap;

public sealed class TacMapLiveUpdateSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GrantTacMapLiveUpdateComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<GrantTacMapLiveUpdateComponent, GotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnGotEquipped(Entity<GrantTacMapLiveUpdateComponent> ent, ref GotEquippedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if ((ent.Comp.Slots & args.SlotFlags) == 0)
            return;

        EnsureComp<TacMapLiveUpdateComponent>(args.Equipee);
    }

    private void OnGotUnequipped(Entity<GrantTacMapLiveUpdateComponent> ent, ref GotUnequippedEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if ((ent.Comp.Slots & args.SlotFlags) == 0)
            return;

        var slots = _inventory.GetSlotEnumerator(args.Equipee);
        while (slots.NextItem(out var item, out var slot))
        {
            if (item != ent.Owner &&
                TryComp(item, out GrantTacMapLiveUpdateComponent? grant) &&
                (grant.Slots & slot.SlotFlags) != 0)
            {
                return;
            }
        }

        RemComp<TacMapLiveUpdateComponent>(args.Equipee);
    }
}
