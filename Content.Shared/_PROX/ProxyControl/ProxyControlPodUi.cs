using Content.Shared._PROX.ProxyControl.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;

namespace Content.Shared._PROX.ProxyControl;

[Serializable, NetSerializable]
public enum ProxyControlPodUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class ProxyControlPodBoundUserInterfaceState : BoundUserInterfaceState
{
    public List<ProxyInfo> Proxies;

    public ProxyControlPodBoundUserInterfaceState(List<ProxyInfo> proxies)
    {
        Proxies = proxies;
    }
}

[Serializable, NetSerializable]
public record struct ProxyInfo(NetEntity Entity, string Name, bool IsAlive, bool IsOccupied);

[Serializable, NetSerializable]
public sealed class ProxySelectedMessage : BoundUserInterfaceMessage
{
    public NetEntity Proxy;

    public ProxySelectedMessage(NetEntity proxy)
    {
        Proxy = proxy;
    }
}
