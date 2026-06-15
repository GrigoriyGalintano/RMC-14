using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Stains;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedRMCStainSystem))]
public sealed partial class RMCShowerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField, AutoNetworkedField]
    public TimeSpan CleanInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan NextClean;

    [DataField]
    public float Range = 0.55f;

    [DataField]
    public SoundSpecifier ToggleSound = new SoundPathSpecifier("/Audio/Ambience/Objects/drain.ogg");
}
