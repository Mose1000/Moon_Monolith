using Robust.Shared.GameStates;

namespace Content.Shared._PROX.ProxyControl.Components;

/// <summary>
/// Temporary component added to the operator's original body while they are
/// controlling a proxy. Used for tracking and enforcing immobility.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ProxyOperatorComponent : Component
{
    /// <summary>
    /// The pod the operator is buckled into.
    /// </summary>
    [DataField]
    public EntityUid Pod;

    /// <summary>
    /// The proxy entity being controlled.
    /// </summary>
    [DataField]
    public EntityUid Proxy;
}
