using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._PROX.ProxyControl.Events;

/// <summary>
/// Raised as an InstantAction when the operator presses the disconnect button.
/// </summary>
public sealed partial class ProxyDisconnectActionEvent : Content.Shared.Actions.InstantActionEvent
{
}

/// <summary>
/// Do-after event for establishing the neural link.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ProxyLinkDoAfterEvent : SimpleDoAfterEvent
{
    public readonly NetEntity ProxyId;

    public ProxyLinkDoAfterEvent(NetEntity proxyId)
    {
        ProxyId = proxyId;
    }
}
