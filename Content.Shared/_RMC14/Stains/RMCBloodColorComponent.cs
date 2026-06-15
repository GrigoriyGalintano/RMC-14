using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Stains;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRMCStainSystem))]
public sealed partial class RMCBloodColorComponent : Component
{
    [DataField, AutoNetworkedField]
    public RMCStainKind Kind = RMCStainKind.Blood;

    [DataField, AutoNetworkedField]
    public Color Color = RMCStainColors.HumanBlood;
}
