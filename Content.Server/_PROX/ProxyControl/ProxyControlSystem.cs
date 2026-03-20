using System.Collections.Generic;
using Content.Server.Mind;
using System.Diagnostics.CodeAnalysis;
using Content.Shared._PROX.ProxyControl;
using Content.Shared._PROX.ProxyControl.Components;
using Content.Shared._PROX.ProxyControl.Events;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.ActionBlocker;
using Content.Shared.DragDrop;
using Content.Shared.Movement.Events;
using Content.Shared.Verbs;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._PROX.ProxyControl;

/// <summary>
/// Server-side system for the proxy control feature.
/// Handles linking proxy control units to pods, establishing/severing neural links,
/// ID card validation, and access/name copying.
/// </summary>
public sealed class ProxyControlSystem : SharedProxyControlSystem
{
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly SharedAccessSystem _access = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Pod events
        SubscribeLocalEvent<ProxyControlPodComponent, ComponentShutdown>(OnPodShutdown);
        SubscribeLocalEvent<ProxyControlPodComponent, EntInsertedIntoContainerMessage>(OnPodEntInserted);
        SubscribeLocalEvent<ProxyControlPodComponent, EntRemovedFromContainerMessage>(OnPodEntRemoved);
        SubscribeLocalEvent<ProxyControlPodComponent, AfterInteractUsingEvent>(OnPodInteractUsing);
        SubscribeLocalEvent<ProxyControlPodComponent, ProxyLinkDoAfterEvent>(OnLinkDoAfter);
        SubscribeLocalEvent<ProxyControlPodComponent, ContainerRelayMovementEntityEvent>(OnRelayMovement);
        SubscribeLocalEvent<ProxyControlPodComponent, GetVerbsEvent<AlternativeVerb>>(AddAlternativeVerbs);
        SubscribeLocalEvent<ProxyControlPodComponent, DragDropTargetEvent>(OnDragDropOn);
        SubscribeLocalEvent<ProxyControlPodComponent, CanDropTargetEvent>(OnCanDragDropOn);

        // UI events
        SubscribeLocalEvent<ProxyControlPodComponent, ProxySelectedMessage>(OnProxySelected);

        // Proxy control unit events — when installed/removed from a borg chassis
        SubscribeLocalEvent<ProxyControlUnitComponent, EntGotInsertedIntoContainerMessage>(OnUnitInsertedIntoContainer);
        SubscribeLocalEvent<ProxyControlUnitComponent, EntGotRemovedFromContainerMessage>(OnUnitRemovedFromContainer);

        // Proxy events
        SubscribeLocalEvent<ProxyControllableComponent, MobStateChangedEvent>(OnProxyMobStateChanged);
        SubscribeLocalEvent<ProxyControllableComponent, ComponentShutdown>(OnProxyShutdown);

        // Disconnect action
        SubscribeLocalEvent<ProxyDisconnectActionEvent>(OnDisconnectAction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Grid/Battery power and range checks for active links
        var query = EntityQueryEnumerator<ProxyControlPodComponent>();
        while (query.MoveNext(out var uid, out var pod))
        {
            if (!pod.IsLinked)
                continue;

            // ApcPowerReceiver.IsPowered covers both grid and battery (if ApcPowerReceiverBattery is present)
            if (!_powerReceiver.IsPowered(uid))
            {
                SeverLink((uid, pod), "proxy-control-pod-unpowered");
                continue; // Move to next pod after severing
            }

            var linkedProxy = pod.LinkedProxy;
            if (pod.MaxLinkRange <= 0 || linkedProxy == null)
                continue;

            if (!Exists(linkedProxy.Value) || Deleted(linkedProxy.Value))
            {
                SeverLink((uid, pod), "proxy-control-link-severed");
                continue;
            }

            var podPos = _transform.GetWorldPosition(uid);
            var proxyPos = _transform.GetWorldPosition(linkedProxy.Value);
            var distance = (podPos - proxyPos).Length();

            if (distance > pod.MaxLinkRange)
            {
                SeverLink((uid, pod), "proxy-control-link-severed");
            }
        }
    }

    #region Pod Lifecycle

    private void OnPodShutdown(EntityUid uid, ProxyControlPodComponent component, ComponentShutdown args)
    {
        if (component.IsLinked)
            SeverLink((uid, component), "proxy-control-link-severed");
    }

    #endregion

    #region Interaction Handlers

    private void OnCanDragDropOn(EntityUid uid, ProxyControlPodComponent component, ref CanDropTargetEvent args)
    {
        args.Handled = true;
        args.CanDrop |= CanPodInsert(uid, args.Dragged, component);
    }

    private void OnDragDropOn(EntityUid uid, ProxyControlPodComponent component, ref DragDropTargetEvent args)
    {
        InsertBody(uid, args.Dragged, component);
    }

    private void OnRelayMovement(EntityUid uid, ProxyControlPodComponent component, ref ContainerRelayMovementEntityEvent args)
    {
        if (!_blocker.CanInteract(args.Entity, uid))
            return;

        EjectBody(uid, component);
    }
    private void AddAlternativeVerbs(EntityUid uid, ProxyControlPodComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // Enter/Eject Body verb
        if (IsOccupied(component))
        {
            if (args.User == component.BodyContainer.ContainedEntity || _blocker.CanInteract(args.User, uid))
            {
                args.Verbs.Add(new AlternativeVerb
                {
                    Text = Loc.GetString("proxy-control-pod-verb-eject"),
                    Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
                    Priority = 2, // Prioritize over generic ID card eject
                    Act = () => EjectBody(uid, component)
                });
            }
        }
        else if (CanPodInsert(uid, args.User, component) && _blocker.CanMove(args.User))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("proxy-control-pod-verb-enter"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/in.svg.192dpi.png")),
                Priority = 2, // Prioritize over generic ID card eject
                Act = () => InsertBody(uid, args.User, component)
            });
        }
    }

    public bool CanPodInsert(EntityUid uid, EntityUid target, ProxyControlPodComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        // Must be a player/humanoid (entity with mob state)
        return HasComp<MobStateComponent>(target);
    }

    public static bool IsOccupied(ProxyControlPodComponent component)
    {
        return component.BodyContainer.ContainedEntity != null;
    }

    public void InsertBody(EntityUid uid, EntityUid toInsert, ProxyControlPodComponent? component)
    {
        if (!Resolve(uid, ref component))
            return;

        if (IsOccupied(component))
            return;

        if (!CanPodInsert(uid, toInsert, component))
            return;

        // Ensure ID card is present before insertion to avoid crashes from removal during event
        if (!TryGetInsertedIdCard(uid, component, out _))
        {
            _popup.PopupEntity(Loc.GetString("proxy-control-no-id"), uid, toInsert);
            return;
        }

        _container.Insert(toInsert, component.BodyContainer);
    }

    public void EjectBody(EntityUid uid, ProxyControlPodComponent? component)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.BodyContainer.ContainedEntity is not { Valid: true } contained)
            return;

        _container.Remove(contained, component.BodyContainer);
    }

    #endregion

    #region Container & UI Events

    /// <summary>
    /// Handles player entry into the pod. Validates ID and opens selection UI.
    /// </summary>
    private void OnPodEntInserted(EntityUid uid, ProxyControlPodComponent component, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != component.BodyContainerId)
            return;

        // Open the proxy selection UI for this occupant
        UpdateUserInterface(uid, component);
        _ui.OpenUi(uid, ProxyControlPodUiKey.Key, args.Entity);
    }

    /// <summary>
    /// Severs the link when the occupant or ID card is removed from the pod.
    /// </summary>
    private void OnPodEntRemoved(EntityUid uid, ProxyControlPodComponent component, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == component.BodyContainerId || args.Container.ID == component.IdCardSlotId)
        {
            if (component.IsLinked)
                SeverLink((uid, component), "proxy-control-link-severed");
        }
    }

    /// <summary>
    /// Handles proxy selection from the UI.
    /// </summary>
    /// <summary>
    /// Handles proxy selection from the UI.
    /// </summary>
    private void OnProxySelected(EntityUid uid, ProxyControlPodComponent component, ProxySelectedMessage args)
    {
        Log.Debug($"ProxyControlSystem: ProxySelectedMessage received for pod {uid}. Selected proxy: {args.Proxy}");

        var proxy = GetEntity(args.Proxy);
        if (!Exists(proxy) || !HasComp<ProxyControllableComponent>(proxy))
        {
            Log.Debug($"ProxyControlSystem: Selected proxy {args.Proxy} does not exist or lacks ProxyControllableComponent.");
            return;
        }

        if (args.Actor is not { Valid: true } user)
        {
            Log.Debug("ProxyControlSystem: No valid actor for ProxySelectedMessage.");
            return;
        }

        if (component.BodyContainer.ContainedEntity != user)
        {
            Log.Debug($"ProxyControlSystem: User {user} is not the occupant of pod {uid}.");
            return;
        }

        TryStartLinkEstablishment((uid, component), proxy, user);
    }

    private void UpdateUserInterface(EntityUid uid, ProxyControlPodComponent component)
    {
        var proxies = new List<ProxyInfo>();

        // Find all proxies linked to this pod
        var query = EntityQueryEnumerator<ProxyControllableComponent, BorgChassisComponent>();
        while (query.MoveNext(out var proxyUid, out var controllable, out var borgComp))
        {
            if (borgComp.BrainEntity == null)
                continue;

            if (!TryComp<ProxyControlUnitComponent>(borgComp.BrainEntity, out var unitComp))
                continue;

            if (!unitComp.IsLinked || unitComp.LinkedPod != uid)
                continue;

            proxies.Add(new ProxyInfo(
                GetNetEntity(proxyUid),
                Name(proxyUid),
                !_mobState.IsDead(proxyUid),
                controllable.ControllingPod != null && controllable.ControllingPod != uid
            ));
        }

        _ui.SetUiState(uid, ProxyControlPodUiKey.Key, new ProxyControlPodBoundUserInterfaceState(proxies));
    }

    private void TryStartLinkEstablishment(Entity<ProxyControlPodComponent> pod, EntityUid proxyUid, EntityUid operator_)
    {
        Log.Debug($"ProxyControlSystem: TryStartLinkEstablishment for pod {pod.Owner}, proxy {proxyUid}, operator {operator_}.");

        if (pod.Comp.IsLinked)
        {
            Log.Debug($"ProxyControlSystem: Pod {pod.Owner} is already linked.");
            return;
        }

        if (!TryComp<ProxyControllableComponent>(proxyUid, out var controllable))
        {
            Log.Debug($"ProxyControlSystem: Proxy {proxyUid} lacks ProxyControllableComponent.");
            return;
        }

        if (controllable.ControllingPod != null)
        {
            Log.Debug($"ProxyControlSystem: Proxy {proxyUid} is already controlled by pod {controllable.ControllingPod}.");
            _popup.PopupEntity(Loc.GetString("proxy-control-proxy-occupied"), pod, operator_);
            return;
        }

        if (_mobState.IsDead(proxyUid))
        {
            Log.Debug($"ProxyControlSystem: Proxy {proxyUid} is dead.");
            _popup.PopupEntity(Loc.GetString("proxy-control-proxy-dead"), pod, operator_);
            return;
        }

        Log.Debug($"ProxyControlSystem: Starting DoAfter for {pod.Comp.LinkEstablishTime} seconds.");

        // Start the do-after
        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            operator_,
            TimeSpan.FromSeconds(pod.Comp.LinkEstablishTime),
            new ProxyLinkDoAfterEvent(GetNetEntity(proxyUid)),
            pod,
            target: null) // explicitly null to bypass SharedDoAfterSystem range checks
        {
            BreakOnDamage = true,
            BreakOnMove = false, // We're in a container, shouldn't move
            NeedHand = false,
            RequireCanInteract = false, // explicitly false to not require unobstructed interaction
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
        {
            _popup.PopupEntity(Loc.GetString("proxy-control-link-started"), pod, operator_);
        }
        else
        {
            Log.Debug("ProxyControlSystem: DoAfter failed to start.");
        }
    }

    #endregion


    #region Pod Interaction — Linking a Control Unit

    /// <summary>
    /// When a player uses a ProxyControlUnit on the pod, link them.
    /// </summary>
    private void OnPodInteractUsing(EntityUid uid, ProxyControlPodComponent component, AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (!TryComp<ProxyControlUnitComponent>(args.Used, out var unitComp))
            return;

        if (unitComp.IsLinked)
        {
            _popup.PopupEntity(Loc.GetString("proxy-control-unit-already-linked"), uid, args.User);
            args.Handled = true;
            return;
        }

        // Link the control unit to this pod
        unitComp.LinkedPod = uid;
        unitComp.IsLinked = true;
        Dirty(args.Used, unitComp);

        _popup.PopupEntity(
            Loc.GetString("proxy-control-unit-linked", ("pod", uid)),
            uid,
            args.User);

        args.Handled = true;
    }

    #endregion

    #region Control Unit Container Events

    /// <summary>
    /// When a control unit is inserted into a borg chassis brain slot,
    /// add ProxyControllableComponent to the chassis.
    /// </summary>
    private void OnUnitInsertedIntoContainer(EntityUid uid, ProxyControlUnitComponent component, EntGotInsertedIntoContainerMessage args)
    {
        var chassis = args.Container.Owner;

        // Only activate if inserted into a borg brain container
        if (!TryComp<BorgChassisComponent>(chassis, out var borgComp))
            return;

        if (args.Container.ID != borgComp.BrainContainerId)
            return;

        // Add the controllable component to the chassis
        var controllable = EnsureComp<ProxyControllableComponent>(chassis);
        Dirty(chassis, controllable);
    }

    /// <summary>
    /// When a control unit is removed from a borg chassis, clean up.
    /// </summary>
    private void OnUnitRemovedFromContainer(EntityUid uid, ProxyControlUnitComponent component, EntGotRemovedFromContainerMessage args)
    {
        if (Terminating(uid))
            return;

        var chassis = args.Container.Owner;

        // If the chassis has a controllable component, check if we need to sever the link
        if (TryComp<ProxyControllableComponent>(chassis, out var controllable))
        {
            if (controllable.ControllingPod != null)
            {
                if (TryComp<ProxyControlPodComponent>(controllable.ControllingPod.Value, out var podComp))
                {
                    Log.Debug($"ProxyControlSystem: Severing link for pod {controllable.ControllingPod.Value} due to module {uid} removal from {chassis}.");
                    SeverLink((controllable.ControllingPod.Value, podComp), "proxy-control-link-severed");
                }
            }

            // Remove the controllable state from the chassis if the module was in the brain slot
            if (TryComp<BorgChassisComponent>(chassis, out var borgComp) && args.Container.ID == borgComp.BrainContainerId)
            {
                RemCompDeferred<ProxyControllableComponent>(chassis);
            }
        }
    }

    #endregion

    #region Proxy Events

    private void OnProxyMobStateChanged(EntityUid uid, ProxyControllableComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (component.ControllingPod == null)
            return;

        if (!TryComp<ProxyControlPodComponent>(component.ControllingPod, out var podComp))
            return;

        SeverLink((component.ControllingPod.Value, podComp), "proxy-control-link-severed");
    }

    private void OnProxyShutdown(EntityUid uid, ProxyControllableComponent component, ComponentShutdown args)
    {
        if (component.ControllingPod == null)
            return;

        if (TryComp<ProxyControlPodComponent>(component.ControllingPod, out var podComp) && podComp.IsLinked)
            SeverLink((component.ControllingPod.Value, podComp), "proxy-control-link-severed");
    }

    #endregion

    #region Disconnect Action

    private void OnDisconnectAction(ProxyDisconnectActionEvent args)
    {
        if (args.Handled)
            return;

        var performer = args.Performer;

        // The performer is the proxy entity, find the pod through ProxyControllableComponent
        if (!TryComp<ProxyControllableComponent>(performer, out var controllable) || controllable.ControllingPod == null)
            return;

        if (!TryComp<ProxyControlPodComponent>(controllable.ControllingPod, out var podComp))
            return;

        SeverLink((controllable.ControllingPod.Value, podComp), "proxy-control-link-severed");
        args.Handled = true;
    }

    #endregion

    #region Link Establishment


    private void OnLinkDoAfter(EntityUid uid, ProxyControlPodComponent component, ProxyLinkDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        var target = GetEntity(args.ProxyId);

        if (!Exists(target) || !TryComp<ProxyControllableComponent>(target, out var controllable))
            return;

        EstablishLink((uid, component), (target, controllable), args.User);
        args.Handled = true;
    }

    /// <summary>
    /// Establishes the neural link between pod and proxy.
    /// </summary>
    private void EstablishLink(
        Entity<ProxyControlPodComponent> pod,
        Entity<ProxyControllableComponent> proxy,
        EntityUid operator_)
    {
        Log.Debug($"ProxyControlSystem: Completing link establishment for pod {pod.Owner}, proxy {proxy.Owner}.");

        if (!_mind.TryGetMind(operator_, out var mindId, out var mind))
        {
            Log.Debug($"ProxyControlSystem: Failed to get mind for operator {operator_}. Cannot establish link.");
            return;
        }

        // Store state
        pod.Comp.LinkedProxy = proxy.Owner;
        pod.Comp.OperatorMind = mindId;
        pod.Comp.OperatorBody = operator_;
        pod.Comp.IsLinked = true;
        Dirty(pod);

        proxy.Comp.ControllingPod = pod.Owner;
        proxy.Comp.OperatorMind = mindId;
        proxy.Comp.OperatorBody = operator_;

        // Save original proxy name and access for later restoration
        proxy.Comp.OriginalName = Name(proxy.Owner);
        if (TryComp<AccessComponent>(proxy.Owner, out var proxyAccess))
            proxy.Comp.OriginalAccessTags = new HashSet<ProtoId<AccessLevelPrototype>>(proxyAccess.Tags);
        Dirty(proxy);

        // Copy ID card name and access to proxy
        if (TryGetInsertedIdCard(pod.Owner, pod.Comp, out var idCardEntity))
        {
            Log.Debug($"ProxyControlSystem: Copying ID credentials from {idCardEntity.Value} to proxy.");
            CopyIdToProxy(idCardEntity.Value, proxy.Owner);
        }

        // Add operator component to body
        var operatorComp = EnsureComp<ProxyOperatorComponent>(operator_);
        operatorComp.Pod = pod.Owner;
        operatorComp.Proxy = proxy.Owner;

        // Grant disconnect action to the proxy (operator will control it)
        EntityUid? actionEnt = null;
        _actions.AddAction(proxy.Owner, ref actionEnt, pod.Comp.DisconnectAction);
        if (actionEnt != null)
            pod.Comp.DisconnectActionEntity = actionEnt.Value;

        Log.Debug($"ProxyControlSystem: Calling Mind.Visit to transfer viewport to {proxy.Owner}.");

        // Visit: move the player's viewpoint to the proxy
        _mind.Visit(mindId, proxy.Owner, mind);

        _popup.PopupEntity(
            Loc.GetString("proxy-control-link-established", ("name", Name(proxy.Owner))),
            proxy.Owner,
            operator_);
            
        Log.Debug("ProxyControlSystem: Link successfully established.");
    }

    #endregion

    #region Link Severance

    /// <summary>
    /// Severs the neural link and returns the operator to their body.
    /// </summary>
    public void SeverLink(Entity<ProxyControlPodComponent> pod, string? popupMessage = null)
    {
        if (!pod.Comp.IsLinked)
            return;

        var mindId = pod.Comp.OperatorMind;
        var operator_ = pod.Comp.OperatorBody;
        var proxyUid = pod.Comp.LinkedProxy;

        // Remove disconnect action
        if (pod.Comp.DisconnectActionEntity != null && proxyUid != null && Exists(proxyUid.Value))
        {
            _actions.RemoveAction(proxyUid.Value, pod.Comp.DisconnectActionEntity.Value);
            pod.Comp.DisconnectActionEntity = null;
        }

        // UnVisit: return the player to their body
        if (mindId != null && Exists(mindId.Value))
        {
            _mind.UnVisit(mindId.Value);
        }

        // Restore proxy name and access
        if (proxyUid != null && Exists(proxyUid.Value) && TryComp<ProxyControllableComponent>(proxyUid.Value, out var controllable))
        {
            RestoreProxy(proxyUid.Value, controllable);

            controllable.ControllingPod = null;
            controllable.OperatorMind = null;
            controllable.OperatorBody = null;
            Dirty(proxyUid.Value, controllable);
        }

        // Remove operator component from body
        if (operator_ != null && Exists(operator_.Value))
        {
            RemCompDeferred<ProxyOperatorComponent>(operator_.Value);

            // Apply feedback damage if proxy was destroyed
            if (proxyUid != null && (!Exists(proxyUid.Value) || (TryComp<MobStateComponent>(proxyUid.Value, out var mobState) && _mobState.IsDead(proxyUid.Value, mobState))))
            {
                ApplyFeedbackDamage(operator_.Value, pod.Comp);
            }

            // Show popup to operator
            if (popupMessage != null)
                _popup.PopupEntity(Loc.GetString(popupMessage), operator_.Value, operator_.Value);
        }

        // Clear pod state
        pod.Comp.LinkedProxy = null;
        pod.Comp.OperatorMind = null;
        pod.Comp.OperatorBody = null;
        pod.Comp.IsLinked = false;
        Dirty(pod);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Tries to get the ID card entity inserted into the pod's ID slot.
    /// </summary>
    private bool TryGetInsertedIdCard(EntityUid podUid, ProxyControlPodComponent podComp, [NotNullWhen(true)] out EntityUid? idCard)
    {
        idCard = null;

        if (!_itemSlots.TryGetSlot(podUid, podComp.IdCardSlotId, out var slot) || slot.Item == null)
            return false;

        idCard = slot.Item;
        return true;
    }

    /// <summary>
    /// Copies the name and access from an ID card to the proxy entity.
    /// </summary>
    private void CopyIdToProxy(EntityUid idCard, EntityUid proxy)
    {
        // Copy name
        if (TryComp<IdCardComponent>(idCard, out var idComp) && idComp.FullName != null)
        {
            _metadata.SetEntityName(proxy, idComp.FullName);
        }

        // Copy and strict-replace access
        if (TryComp<AccessComponent>(idCard, out var idAccess))
        {
            _access.TrySetTags(proxy, idAccess.Tags);
        }
    }

    /// <summary>
    /// Restores the proxy's original name and access after disconnection.
    /// </summary>
    private void RestoreProxy(EntityUid proxy, ProxyControllableComponent controllable)
    {
        // Restore name
        if (controllable.OriginalName != null)
        {
            _metadata.SetEntityName(proxy, controllable.OriginalName);
            controllable.OriginalName = null;
        }

        // Restore access
        if (controllable.OriginalAccessTags != null)
        {
            _access.TrySetTags(proxy, controllable.OriginalAccessTags);
            controllable.OriginalAccessTags = null;
        }
    }

    /// <summary>
    /// Applies neural feedback damage to the operator when the proxy is destroyed.
    /// </summary>
    private void ApplyFeedbackDamage(EntityUid operator_, ProxyControlPodComponent podComp)
    {
        if (podComp.FeedbackDamageMax <= 0)
            return;

        var damage = _random.NextFloat(podComp.FeedbackDamageMin, podComp.FeedbackDamageMax);

        if (damage <= 0)
            return;

        // Apply as shock/stun damage via the damage system
        if (TryComp<DamageableComponent>(operator_, out _))
        {
            var damageSpec = new DamageSpecifier();
            damageSpec.DamageDict.Add("Shock", FixedPoint2.New(damage));
            _damageable.TryChangeDamage(operator_, damageSpec);
        }
    }

    #endregion
}
