using Content.Shared.Access;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._PROX.ProxyControl.Components;

/// <summary>
/// Marks an entity (typically a borg chassis) as remotely controllable via a Neural Link Pod.
/// Added dynamically when a <see cref="ProxyControlUnitComponent"/> is installed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ProxyControllableComponent : Component
{
    /// <summary>
    /// The pod currently controlling this proxy.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ControllingPod;

    /// <summary>
    /// The mind entity of the operator currently visiting this proxy.
    /// </summary>
    [DataField]
    public EntityUid? OperatorMind;

    /// <summary>
    /// The operator's original body entity.
    /// </summary>
    [DataField]
    public EntityUid? OperatorBody;

    /// <summary>
    /// Whether to disable/stun the proxy on link severance.
    /// </summary>
    [DataField]
    public bool DisableOnSeverance = true;

    /// <summary>
    /// The proxy's original name before it was overwritten by the operator's ID.
    /// </summary>
    [DataField]
    public string? OriginalName;

    /// <summary>
    /// The proxy's original access tags before they were overwritten.
    /// </summary>
    [DataField]
    public HashSet<ProtoId<AccessLevelPrototype>>? OriginalAccessTags;
}
