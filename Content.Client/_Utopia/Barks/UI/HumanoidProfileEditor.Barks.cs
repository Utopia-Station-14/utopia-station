using System.Linq;
using System.Numerics;
using Content.Client._Utopia.Barks;
using Content.Client._Utopia.SpeechBarks;
using Content.Client.UserInterface.Controls;
using Content.Shared._Utopia.CCVar;
using Content.Shared._Utopia.SpeechBarks;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private List<SpeechBarkPrototype> _barkList = new();
    private FancyWindow? _barkWindow;

    private void InitializeBarks()
    {
        _barkList = _prototypeManager
            .EnumeratePrototypes<SpeechBarkPrototype>()
            .Where(o => o.RoundStart)
            .OrderBy(o => o.LocName)
            .ToList();

        BarkProtoButton.OnPressed += _ => OpenBarkWindow();
        BarkPlayButton.OnPressed += _ => PlayPreviewBark();
    }

    private void OpenBarkWindow()
    {
        if (Profile is null)
            return;

        _barkWindow?.Close();
        _barkWindow = null;

        var barkTab = new BarkTab();
        barkTab.SetSelectedBark(
            Profile.Bark.Proto,
            Profile.Bark.Pitch,
            Profile.Bark.MinVar,
            Profile.Bark.MaxVar
        );

        barkTab.OnBarkSelected += OnBarkSelected;
        barkTab.OnPitchChanged += OnBarkPitchChanged;
        barkTab.OnMinVarChanged += OnBarkMinVarChanged;
        barkTab.OnMaxVarChanged += OnBarkMaxVarChanged;

        _barkWindow = new FancyWindow
        {
            Title = Loc.GetString("humanoid-profile-editor-bark-window-title"),
            MinSize = new Vector2(750, 600),
        };

        _barkWindow.ContentsContainer.AddChild(barkTab);
        _barkWindow.OnClose += () => _barkWindow = null;

        _barkWindow.OpenCentered();
    }

    private void SetBarkProto(string prototype)
    {
        Profile = Profile?.WithBarkProto(prototype);
        ReloadPreview();
        SetDirty();
    }

    private void SetBarkPitch(float pitch)
    {
        Profile = Profile?.WithBarkPitch(Math.Clamp(pitch, _cfgManager.GetCVar(UCCVars.BarksMinPitch), _cfgManager.GetCVar(UCCVars.BarksMaxPitch)));
        ReloadPreview();
        SetDirty();
    }

    private void SetBarkMinVariation(float variation)
    {
        Profile = Profile?.WithBarkMinVariation(Math.Clamp(variation, _cfgManager.GetCVar(UCCVars.BarksMinDelay), Profile.Bark.MaxVar));
        ReloadPreview();
        SetDirty();
    }

    private void SetBarkMaxVariation(float variation)
    {
        Profile = Profile?.WithBarkMaxVariation(Math.Clamp(variation, Profile.Bark.MinVar, _cfgManager.GetCVar(UCCVars.BarksMaxDelay)));
        ReloadPreview();
        SetDirty();
    }

    private void OnBarkSelected(string barkId)
    {
        SetBarkProto(barkId);
        UpdateBarkButtonText();
        UpdateSaveButton();
    }

    private void OnBarkPitchChanged(float pitch)
    {
        SetBarkPitch(pitch);
        UpdateSaveButton();
    }

    private void OnBarkMinVarChanged(float minVar)
    {
        SetBarkMinVariation(minVar);
        UpdateSaveButton();
    }

    private void OnBarkMaxVarChanged(float maxVar)
    {
        SetBarkMaxVariation(maxVar);
        UpdateSaveButton();
    }

    private void UpdateBarkVoicesControls()
    {
        if (Profile is null)
            return;

        UpdateBarkButtonText();
        if (_barkWindow != null && _barkWindow.ContentsContainer.ChildCount > 0)
        {
            if (_barkWindow.ContentsContainer.GetChild(0) is BarkTab barkTab)
            {
                barkTab.SetSelectedBark(
                    Profile.Bark.Proto,
                    Profile.Bark.Pitch,
                    Profile.Bark.MinVar,
                    Profile.Bark.MaxVar
                );
            }
        }
    }

    private void UpdateBarkButtonText()
    {
        if (Profile is null)
            return;

        var bark = _barkList.FirstOrDefault(b => b.ID == Profile.Bark.Proto);

        if (bark != null)
        {
            BarkProtoButton.Text = bark.LocName;
        }

        else
        {
            BarkProtoButton.Text = Loc.GetString("humanoid-profile-editor-bark-none");
        }
    }

    private void PlayPreviewBark()
    {
        if (Profile is null)
            return;

        _entManager.System<SpeechBarksSystem>().PlayDataPreview(
            Profile.Bark.Proto,
            Profile.Bark.Pitch,
            Profile.Bark.MinVar,
            Profile.Bark.MaxVar
        );
    }
}
