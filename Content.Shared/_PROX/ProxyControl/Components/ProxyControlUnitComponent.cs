using Robust.Shared.GameStates;

namespace Content.Shared._PROX.ProxyControl.Components;

/// <summary>
/// Attached to the proxy control unit item. This item replaces the MMI/positronic brain
/// in a borg chassis. It must be linked to a Neural Link Pod before installation.
/// The entity also has <see cref="Content.Shared.Silicons.Borgs.Components.BorgBrainComponent"/>
/// so it passes the borg chassis brain whitelist.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ProxyControlUnitComponent : Component
{
    /// <summary>
    /// The Neural Link Pod this unit is paired with.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? LinkedPod;

    /// <summary>
    /// Whether this unit has been linked to a pod.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsLinked;
}
