namespace Content.Shared._RMC14.Stains;

[RegisterComponent]
[Access(typeof(SharedRMCStainSystem))]
public sealed partial class RMCStainOverlayComponent : Component
{
    [DataField]
    public string? WorldOverlayState;

    [DataField]
    public string? InhandOverlayState;

    [DataField]
    public string? EquippedOverlayType;
}
