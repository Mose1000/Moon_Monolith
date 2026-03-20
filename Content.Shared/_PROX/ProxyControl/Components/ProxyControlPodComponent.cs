using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._PROX.ProxyControl.Components;

/// <summary>
/// Attached to a Neural Link Pod. Tracks the active link between an operator and a proxy entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ProxyControlPodComponent : Component
{
    /// <summary>
    /// The proxy entity currently being controlled through this pod.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? LinkedProxy;

    /// <summary>
    /// The mind entity of the operator currently using this pod.
    /// </summary>
    [DataField]
    public EntityUid? OperatorMind;

    /// <summary>
    /// The operator's original body entity.
    /// </summary>
    [DataField]
    public EntityUid? OperatorBody;

    /// <summary>
    /// Whether a neural link is currently active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IsLinked;

    /// <summary>
    /// Time in seconds to establish a neural link (do-after delay).
    /// </summary>
    [DataField]
    public float LinkEstablishTime = 3f;

    /// <summary>
    /// Maximum range in tiles for maintaining the link. 0 = unlimited.
    /// </summary>
    [DataField]
    public float MaxLinkRange;

    /// <summary>
    /// Minimum neural feedback damage applied to the operator when the proxy is destroyed.
    /// </summary>
    [DataField]
    public float FeedbackDamageMin;

    /// <summary>
    /// Maximum neural feedback damage applied to the operator when the proxy is destroyed.
    /// </summary>
    [DataField]
    public float FeedbackDamageMax = 25f;

    /// <summary>
    /// The item slot ID for the ID card slot on this pod.
    /// </summary>
    [DataField]
    public string IdCardSlotId = "pod_id_slot";

    /// <summary>
    /// The container ID for the occupant body.
    /// </summary>
    [DataField]
    public string BodyContainerId = "pod_body_container";

    /// <summary>
    /// The container slot for the occupant body.
    /// </summary>
    [ViewVariables]
    public ContainerSlot BodyContainer = default!;

    /// <summary>
    /// The container slot for the ID card.
    /// </summary>
    [ViewVariables]
    public ContainerSlot IdCardContainer = default!;

    /// <summary>
    /// The action prototype to grant the operator for disconnecting.
    /// </summary>
    [DataField]
    public EntProtoId DisconnectAction = "ActionProxyDisconnect";

    /// <summary>
    /// The instantiated disconnect action entity.
    /// </summary>
    [DataField]
    public EntityUid? DisconnectActionEntity;
}
