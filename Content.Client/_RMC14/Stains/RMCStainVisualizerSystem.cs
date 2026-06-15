using Content.Client.Clothing;
using Content.Client.Items.Systems;
using Content.Shared._RMC14.Stains;
using Content.Shared.Clothing;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.Stains;

public sealed class RMCStainVisualizerSystem : VisualizerSystem<RMCStainableComponent>
{
    private const string ItemStainRsi = "/Textures/_RMC14/Effects/stains.rsi";
    private const string EquipmentStainRsi = "_RMC14/Mobs/Equipment/stains.rsi";

    [Dependency] private readonly ItemSystem _item = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCStainableComponent, GetInhandVisualsEvent>(OnGetInhandVisuals, after: [typeof(ItemSystem)]);
        SubscribeLocalEvent<RMCStainableComponent, GetEquipmentVisualsEvent>(OnGetEquipmentVisuals, after: [typeof(ClientClothingSystem)]);
    }

    private void OnGetInhandVisuals(Entity<RMCStainableComponent> ent, ref GetInhandVisualsEvent args)
    {
        if (!ent.Comp.IsStained)
            return;

        var state = args.Location == HandLocation.Left ? "inhand-left" : "inhand-right";
        args.Layers.Add(($"rmc-stain-{state}", new PrototypeLayerData
        {
            RsiPath = "_RMC14/Effects/stains.rsi",
            State = state,
            MapKeys = [$"rmc-stain-{state}"],
            Color = ent.Comp.Color,
        }));
    }

    private void OnGetEquipmentVisuals(Entity<RMCStainableComponent> ent, ref GetEquipmentVisualsEvent args)
    {
        if (!ent.Comp.IsStained)
            return;

        var state = $"{ent.Comp.EquippedOverlayType}_blood";
        args.Layers.Add(($"rmc-stain-{state}", new PrototypeLayerData
        {
            RsiPath = EquipmentStainRsi,
            State = state,
            MapKeys = [$"rmc-stain-{state}"],
            Color = ent.Comp.Color,
        }));
    }

    protected override void OnAppearanceChange(EntityUid uid, RMCStainableComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (uid, args.Sprite);
        if (!_sprite.LayerMapTryGet(sprite, RMCStainVisualLayers.Stain, out var layer, false))
            layer = _sprite.LayerMapReserve(sprite, RMCStainVisualLayers.Stain);

        _sprite.LayerSetVisible(sprite, layer, component.IsStained);
        if (component.IsStained)
        {
            _sprite.LayerSetRsi(sprite, layer, new ResPath(ItemStainRsi));
            _sprite.LayerSetRsiState(sprite, layer, component.WorldOverlayState);
            _sprite.LayerSetColor(sprite, layer, component.Color);
        }

        _item.VisualsChanged(uid);
    }
}

public sealed class RMCBodyStainVisualizerSystem : VisualizerSystem<RMCBodyStainsComponent>
{
    private const string BodyStainRsi = "/Textures/_RMC14/Mobs/stains.rsi";

    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, RMCBodyStainsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var sprite = (uid, args.Sprite);
        if (!_sprite.LayerMapTryGet(sprite, RMCStainVisualLayers.Hands, out var handsLayer, false))
            handsLayer = _sprite.LayerMapReserve(sprite, RMCStainVisualLayers.Hands);

        _sprite.LayerSetVisible(sprite, handsLayer, component.HasHandsStain);
        if (component.HasHandsStain)
        {
            _sprite.LayerSetRsi(sprite, handsLayer, new ResPath(BodyStainRsi));
            _sprite.LayerSetRsiState(sprite, handsLayer, "hands_blood");
            _sprite.LayerSetColor(sprite, handsLayer, component.HandsColor);
        }

        if (!_sprite.LayerMapTryGet(sprite, RMCStainVisualLayers.Feet, out var feetLayer, false))
            feetLayer = _sprite.LayerMapReserve(sprite, RMCStainVisualLayers.Feet);

        _sprite.LayerSetVisible(sprite, feetLayer, component.HasFeetStain);
        if (component.HasFeetStain)
        {
            _sprite.LayerSetRsi(sprite, feetLayer, new ResPath(BodyStainRsi));
            _sprite.LayerSetRsiState(sprite, feetLayer, "feet_blood");
            _sprite.LayerSetColor(sprite, feetLayer, component.FeetColor);
        }
    }
}
