using System.Linq;
using Content.Shared._RMC14.Marines.Roles.Ranks;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Robust.Shared.Containers;

namespace Content.Server._RMC14.Marines.Roles.Ranks;

public sealed class RankPinSystem : EntitySystem
{
    private const string RankCategory = "Rank";

    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedRankSystem _rank = default!;
    [Dependency] private readonly SharedUniformAccessorySystem _uniformAccessory = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete, after: [typeof(RankSystem)]);
        SubscribeLocalEvent<RankComponent, RankChangedEvent>(OnRankChanged);
        SubscribeLocalEvent<RankComponent, RMCAutomatedVendedUserEvent>(OnAutomatedVendedUser);
        SubscribeLocalEvent<UniformAccessoryHolderComponent, ClothingGotEquippedEvent>(OnClothingGotEquipped);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        RefreshEquippedRankPin(ev.Mob);
    }

    private void OnRankChanged(Entity<RankComponent> ent, ref RankChangedEvent args)
    {
        RefreshEquippedRankPin(ent.Owner);
    }

    private void OnAutomatedVendedUser(Entity<RankComponent> ent, ref RMCAutomatedVendedUserEvent args)
    {
        if (!TryComp(args.Item, out ClothingComponent? clothing) ||
            (clothing.Slots & SlotFlags.INNERCLOTHING) == SlotFlags.NONE)
        {
            return;
        }

        if (!HasComp<UniformAccessoryHolderComponent>(args.Item) ||
            !_uniformAccessory.HolderAllowsCategory(args.Item, RankCategory))
        {
            return;
        }

        if (!TrySpawnRankPin(ent.Owner, out var pin))
            return;

        if (!_uniformAccessory.TryInsertUniformAccessory(pin, args.Item, ent.Owner))
            QueueDel(pin);
    }

    private void OnClothingGotEquipped(Entity<UniformAccessoryHolderComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if ((args.Clothing.Slots & (SlotFlags.INNERCLOTHING | SlotFlags.OUTERCLOTHING)) == SlotFlags.NONE ||
            !_uniformAccessory.HolderAllowsCategory(ent.Owner, RankCategory, ent.Comp))
        {
            return;
        }

        RefreshEquippedRankPin(args.Wearer);
    }

    private void RefreshEquippedRankPin(EntityUid user)
    {
        RemoveOwnedRankPinsFromEquipped(user);

        if (!TrySpawnRankPin(user, out var pin))
            return;

        if (!TryInsertToPreferredSlot(pin, user))
            QueueDel(pin);
    }

    private bool TrySpawnRankPin(EntityUid user, out EntityUid pin)
    {
        pin = default;

        if (_rank.GetRank(user)?.RankPin is not { } pinProto)
            return false;

        pin = Spawn(pinProto, Transform(user).Coordinates);

        if (TryComp<UniformAccessoryComponent>(pin, out var accessory))
        {
            accessory.User = GetNetEntity(user);
            Dirty(pin, accessory);
        }

        return true;
    }

    private bool TryInsertToPreferredSlot(EntityUid accessory, EntityUid user)
    {
        if (TryGetAccessoryHolder(user, "outerClothing", out var outerClothing) &&
            _uniformAccessory.TryInsertUniformAccessory(accessory, outerClothing, user))
        {
            return true;
        }

        if (TryGetAccessoryHolder(user, "jumpsuit", out var jumpsuit) &&
            _uniformAccessory.TryInsertUniformAccessory(accessory, jumpsuit, user))
        {
            return true;
        }

        return false;
    }

    private bool TryGetAccessoryHolder(EntityUid user, string slotId, out EntityUid holder)
    {
        holder = default;

        if (!_inventory.TryGetSlotEntity(user, slotId, out var slotEntity) ||
            slotEntity is not { } resolvedSlotEntity ||
            !HasComp<UniformAccessoryHolderComponent>(resolvedSlotEntity))
        {
            return false;
        }

        holder = resolvedSlotEntity;
        return true;
    }

    private void RemoveOwnedRankPinsFromEquipped(EntityUid user)
    {
        if (TryGetAccessoryHolder(user, "outerClothing", out var outerClothing))
            RemoveOwnedRankPinsFromHolder(outerClothing, user);

        if (TryGetAccessoryHolder(user, "jumpsuit", out var jumpsuit))
            RemoveOwnedRankPinsFromHolder(jumpsuit, user);
    }

    private void RemoveOwnedRankPinsFromHolder(EntityUid holder, EntityUid user)
    {
        if (!_uniformAccessory.TryGetHolderContainer(holder, out var container))
        {
            return;
        }

        foreach (var accessory in container.ContainedEntities.ToList())
        {
            if (!TryComp<UniformAccessoryComponent>(accessory, out var accessoryComp) ||
                accessoryComp.Category != RankCategory ||
                accessoryComp.User is not { } accessoryUser ||
                !_uniformAccessory.BelongsToUser(accessoryUser, user))
            {
                continue;
            }

            _container.Remove(accessory, container);
            QueueDel(accessory);
        }
    }
}
