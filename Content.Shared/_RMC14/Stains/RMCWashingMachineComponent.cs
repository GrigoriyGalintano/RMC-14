using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Stains;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRMCWashingMachineSystem))]
public sealed partial class RMCWashingMachineComponent : Component
{
    [DataField]
    public string ContainerId = "rmc_washing_machine";

    [DataField]
    public int MaxContents = 5;

    [DataField, AutoNetworkedField]
    public bool Running;

    [DataField]
    public TimeSpan CycleTime = TimeSpan.FromSeconds(20);

    [DataField]
    public TimeSpan FinishAt;
}
