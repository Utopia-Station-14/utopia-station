using Content.Shared.Atmos;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Utopia.Atmos.UI
{
    [UsedImplicitly]
    public sealed class GasTankMixerBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private GasTankMixer? _window;

        public GasTankMixerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _window = this.CreateWindow<GasTankMixer>();

            _window.OnStartPressed += () => SendPredictedMessage(new GasTankMixerStartMessage());
            _window.OnSetTimePressed += (time) => SendPredictedMessage(new GasTankMixerSetTimeMessage(time));
            _window.OnEjectPressed += (slotId) => SendPredictedMessage(new GasTankMixerEjectMessage(slotId));
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not GasTankMixerBoundUserInterfaceState castState)
                return;

            _window?.UpdateState(castState);
        }
    }
}