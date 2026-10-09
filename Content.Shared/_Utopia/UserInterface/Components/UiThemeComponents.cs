using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._Utopia.UserInterface.Components;

[RegisterComponent]
public sealed partial class UiThemeComponent : Component
{
    [DataField]
    public Dictionary<Enum, UiThemeEntry> Themes = new();

    [DataField]
    public UiThemeEntry? Default;
}

[DataDefinition]
public sealed partial class UiThemeEntry
{
    [DataField]
    public Color? BackgroundColor;

    [DataField]
    public Color? HeaderColor;

    [DataField]
    public Color? BorderColor;

    [DataField]
    public int BorderThickness = 1;

    [DataField]
    public Color? TextColor;

    [DataField]
    public Color? ButtonTextColor;

    [DataField]
    public Color? HeaderTextColor;

    [DataField]
    public Color? ButtonColor;

    [DataField]
    public Color? ButtonHoverColor;

    [DataField]
    public Color? ButtonPressedColor;

    [DataField]
    public Color? ButtonDisabledColor;

    [DataField]
    public Color? ButtonSelectedColor;

    [DataField]
    public Color? ButtonBorderColor;

    [DataField]
    public bool PaintPanelsInsideButtons;

    [DataField]
    public Color? LineEditColor;

    [DataField]
    public Color? ProgressBarFgColor;

    [DataField]
    public Color? ProgressBarBgColor;

    [DataField]
    public Color? TabPanelColor;

    [DataField]
    public Color? TabActiveColor;

    [DataField]
    public Color? TabInactiveColor;

    [DataField]
    public Color? TabTextColor;

    [DataField]
    public Color? TabInactiveTextColor;

    [DataField]
    public Color? SeparatorColor;

    [DataField]
    public Color? StripeColor;

    [DataField]
    public List<string> IgnoreTypes = new();

    [DataField]
    public List<string> IgnoreNames = new();

    [DataField]
    public List<string> IgnoreStyleClasses = new();
}
