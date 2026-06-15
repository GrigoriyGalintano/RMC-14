using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Stains;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCWaterCleanStainsComponent : Component
{
    [DataField, AutoNetworkedField]
    public RMCCleanStainFlags CleanFlags = RMCCleanStainFlags.Feet;

    [DataField, AutoNetworkedField]
    public bool CleanInventory;
}
