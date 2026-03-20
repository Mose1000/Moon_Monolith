using Content.Shared._PROX.ProxyControl.Components;
using Robust.Shared.Containers;

namespace Content.Shared._PROX.ProxyControl;

/// <summary>
/// Shared base system for the proxy control feature.
/// </summary>
public abstract class SharedProxyControlSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ProxyControlPodComponent, ComponentInit>(OnPodInit);
    }

    private void OnPodInit(EntityUid uid, ProxyControlPodComponent component, ComponentInit args)
    {
        component.BodyContainer = _container.EnsureContainer<ContainerSlot>(uid, component.BodyContainerId);
        component.IdCardContainer = _container.EnsureContainer<ContainerSlot>(uid, component.IdCardSlotId);
    }
}
