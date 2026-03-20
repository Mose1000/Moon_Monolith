using Content.Shared._PROX.ProxyControl;
using Robust.Client.GameObjects;

namespace Content.Client._PROX.ProxyControl.UI;

public sealed class ProxyControlPodBoundUserInterface : BoundUserInterface
{
    private ProxyControlPodWindow? _window;

    public ProxyControlPodBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new ProxyControlPodWindow();
        _window.OnClose += Close;
        _window.OnProxySelected += entity =>
        {
            SendMessage(new ProxySelectedMessage(entity));
            _window?.Close();
        };

        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not ProxyControlPodBoundUserInterfaceState podState)
            return;

        _window?.UpdateState(podState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _window?.Dispose();
        }
    }
}
