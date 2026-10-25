using Content.Shared.Corvax.JoinQueue;
using Robust.Client.Console;
using Robust.Client.GameObjects;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client.Corvax.JoinQueue;

public sealed partial class QueueState : State
{
    [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
    [Dependency] private IClientConsoleHost _consoleHost = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    private const string JoinSoundPath = "/Audio/Effects/voteding.ogg";

    private QueueGui? _gui;

    protected override void Startup()
    {
        _gui = new QueueGui();
        _userInterfaceManager.StateRoot.AddChild(_gui);

        _gui.QuitPressed += OnQuitPressed;
    }

    public override void FrameUpdate(FrameEventArgs e)
    {
        base.FrameUpdate(e);
    }

    protected override void Shutdown()
    {
        _gui!.QuitPressed -= OnQuitPressed;
        _gui.Dispose();

        Ding();
    }

    private void Ding()
    {
        _audio.PlayGlobal(JoinSoundPath, Filter.Local(), false);
    }

    public void OnQueueUpdate(MsgQueueUpdate msg)
    {
        _gui?.UpdateInfo(msg.Total, msg.Position);
    }

    private void OnQuitPressed()
    {
        _consoleHost.ExecuteCommand("quit");
    }
}
