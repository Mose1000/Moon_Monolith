using Content.Shared._PROX.ProxyControl;
using Content.Shared._PROX.ProxyControl.Components;
using Content.Shared.Verbs;
using Content.Shared.ActionBlocker;
using Content.Shared.Mobs.Components;
using Robust.Shared.Utility;

namespace Content.Client._PROX.ProxyControl;

/// <summary>
/// Client-side stub for the proxy control system.
/// </summary>
public sealed class ProxyControlSystem : SharedProxyControlSystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;

    public override void Initialize()
    {
        base.Initialize();
        
        SubscribeLocalEvent<ProxyControlPodComponent, GetVerbsEvent<AlternativeVerb>>(AddAlternativeVerbs);
    }

    private void AddAlternativeVerbs(EntityUid uid, ProxyControlPodComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // Enter/Eject Body verb (Client Prediction Stub)
        if (component.BodyContainer.ContainedEntity != null)
        {
            if (args.User == component.BodyContainer.ContainedEntity || _blocker.CanInteract(args.User, uid))
            {
                args.Verbs.Add(new AlternativeVerb
                {
                    Text = Loc.GetString("proxy-control-pod-verb-eject"),
                    Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/eject.svg.192dpi.png")),
                    Priority = 2, // Must match Server explicit Priority exactly so the Client predictor picks this over ItemSlots generic eject
                    Act = () => { } // Leave empty. The client predictor does nothing, waiting for the server resolution
                });
            }
        }
        else if (HasComp<MobStateComponent>(args.User) && _blocker.CanMove(args.User))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("proxy-control-pod-verb-enter"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/in.svg.192dpi.png")),
                Priority = 2, // Must match Server explicit Priority exactly
                Act = () => { } // Leave empty. Client predicts nothing, awaiting server insertion
            });
        }
    }
}
