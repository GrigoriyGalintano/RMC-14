using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Stains;

[Serializable, NetSerializable]
public enum RMCStainKind : byte
{
    Blood,
    Oil,
}

[Flags]
[Serializable, NetSerializable]
public enum RMCStainTargetFlags : byte
{
    None = 0,
    Body = 1 << 0,
    Hands = 1 << 1,
    Feet = 1 << 2,
    Inventory = 1 << 3,
    HeldItems = 1 << 4,
    Floor = 1 << 5,
    All = Body | Hands | Feet | Inventory | HeldItems | Floor,
}

[Flags]
[Serializable, NetSerializable]
public enum RMCCleanStainFlags : byte
{
    None = 0,
    Body = 1 << 0,
    Hands = 1 << 1,
    Feet = 1 << 2,
    Inventory = 1 << 3,
    HeldItems = 1 << 4,
    Floor = 1 << 5,
    All = Body | Hands | Feet | Inventory | HeldItems | Floor,
}

public enum RMCStainApplyMode : byte
{
    Direct,
    Touch,
}

[Serializable, NetSerializable]
public enum RMCStainVisuals : byte
{
    Visible,
    Kind,
    Color,
    WorldState,
    InhandState,
    EquippedType,
}

[Serializable, NetSerializable]
public enum RMCStainVisualLayers : byte
{
    Stain,
    Hands,
    Feet,
}

public static class RMCStainColors
{
    public static readonly Color HumanBlood = Color.FromHex("#980002");
    public static readonly Color DryHumanBlood = Color.FromHex("#500000");
    public static readonly Color SynthBlood = Color.FromHex("#EEEEEE");
    public static readonly Color XenoBlood = Color.FromHex("#BED700");
    public static readonly Color RoyalXenoBlood = Color.FromHex("#CAC703");
    public static readonly Color YautjaBlood = Color.FromHex("#81D434");
    public static readonly Color ZombieBlood = Color.FromHex("#210000");
}
