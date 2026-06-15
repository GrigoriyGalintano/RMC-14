using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Stains;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRMCStainSystem))]
public sealed partial class RMCStainableComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool IsStained;

    [DataField, AutoNetworkedField]
    public RMCStainKind Kind = RMCStainKind.Blood;

    [DataField, AutoNetworkedField]
    public Color Color = RMCStainColors.HumanBlood;

    [DataField, AutoNetworkedField]
    public string? SourceReagent;

    [DataField, AutoNetworkedField]
    public string WorldOverlayState = "itemblood";

    [DataField, AutoNetworkedField]
    public string InhandOverlayState = "itemblood";

    [DataField, AutoNetworkedField]
    public string EquippedOverlayType = "item";
}
