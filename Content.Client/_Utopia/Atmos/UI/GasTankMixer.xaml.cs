using System;
using Content.Shared.Atmos;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._Utopia.Atmos.UI
{
    public sealed partial class GasTankMixer : DefaultWindow
    {
        public event Action? OnStartPressed;
        public event Action<float>? OnSetTimePressed;
        public event Action<string>? OnEjectPressed;
        private readonly Label _statusLabelA;
        private readonly Button _ejectAButton;
        private readonly Label _statusLabelB;
        private readonly Button _ejectBButton;
        private readonly Label _timerLabel;
        private readonly LineEdit _timeInput;
        private readonly Button _setTimeButton;
        private readonly Button _startButton;

        public GasTankMixer()
        {
            RobustXamlLoader.Load(this);

            _statusLabelA = FindControl<Label>("StatusLabelA");
            _ejectAButton = FindControl<Button>("EjectAButton");
            _statusLabelB = FindControl<Label>("StatusLabelB");
            _ejectBButton = FindControl<Button>("EjectBButton");
            _timerLabel = FindControl<Label>("TimerLabel");
            _timeInput = FindControl<LineEdit>("TimeInput");
            _setTimeButton = FindControl<Button>("SetTimeButton");
            _startButton = FindControl<Button>("StartButton");

            _ejectAButton.OnPressed += _ => OnEjectPressed?.Invoke("gas_tank_a");
            _ejectBButton.OnPressed += _ => OnEjectPressed?.Invoke("gas_tank_b");
            _startButton.OnPressed += _ => OnStartPressed?.Invoke();

            _setTimeButton.OnPressed += _ =>
            {
                if (float.TryParse(_timeInput.Text, out var val))
                    OnSetTimePressed?.Invoke(val);
            };
        }

        public void UpdateState(GasTankMixerBoundUserInterfaceState state)
        {
            _statusLabelA.Text = state.HasTankA 
                ? Loc.GetString("gas-tank-mixer-status-tank-a-inserted") 
                : Loc.GetString("gas-tank-mixer-status-tank-a-empty");
            _ejectAButton.Disabled = !state.HasTankA || state.IsActive;

            _statusLabelB.Text = state.HasTankB 
                ? Loc.GetString("gas-tank-mixer-status-tank-b-inserted") 
                : Loc.GetString("gas-tank-mixer-status-tank-b-empty");
            _ejectBButton.Disabled = !state.HasTankB || state.IsActive;

            _timerLabel.Text = state.IsActive 
                ? Loc.GetString("gas-tank-mixer-timer-active", ("timer", state.Timer.ToString("F1"))) 
                : Loc.GetString("gas-tank-mixer-timer-idle", ("timer", state.Timer.ToString("F1")));

            _timeInput.Editable = !state.IsActive;
            _startButton.Disabled = state.IsActive || !state.HasTankA || !state.HasTankB;
        }
    }
}