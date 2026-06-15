using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Stains;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRMCUVStorageCleanerSystem))]
public sealed partial class RMCUVStorageCleanerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Running;

    [DataField]
    public TimeSpan CycleTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan FinishAt;
}
