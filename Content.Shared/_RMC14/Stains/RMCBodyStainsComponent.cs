using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Stains;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRMCStainSystem))]
public sealed partial class RMCBodyStainsComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool HasHandsStain;

    [DataField, AutoNetworkedField]
    public Color HandsColor = RMCStainColors.HumanBlood;

    [DataField, AutoNetworkedField]
    public int HandTransfersRemaining;

    [DataField, AutoNetworkedField]
    public bool HasFeetStain;

    [DataField, AutoNetworkedField]
    public Color FeetColor = RMCStainColors.HumanBlood;

    [DataField, AutoNetworkedField]
    public Color FootstepColor = RMCStainColors.HumanBlood;

    [DataField, AutoNetworkedField]
    public int FootstepsRemaining;
}
