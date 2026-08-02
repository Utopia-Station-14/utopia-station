namespace Content.Shared._Utopia.Combat;

[RegisterComponent]
public sealed partial class ComboCounterComponent : Component
{
    [ViewVariables]
    public int ComboCounter;

    [DataField]
    public int MaxCombo;

    [ViewVariables]
    public TimeSpan LastCombo;

    [DataField]
    public float Duration = 3f;
}
