namespace Content.Shared._RMC14.Stains;

[RegisterComponent]
[Access(typeof(SharedRMCStainSystem))]
public sealed partial class RMCFloorStainComponent : Component
{
    [DataField]
    public RMCStainKind Kind = RMCStainKind.Blood;

    [DataField]
    public Color Color = RMCStainColors.HumanBlood;

    [DataField]
    public bool Dried;

    [DataField]
    public TimeSpan DryAt;

    [DataField]
    public int Amount = 1;

    [DataField]
    public List<uint> DecalIds = new();

    [DataField]
    public string FloorDecal = "RMCDecalBloodFloor1";

    [DataField]
    public string TrailDecal = "RMCDecalBloodTrail1";
}
