namespace Content.Shared._Utopia.Combat;

[RegisterComponent]
public sealed partial class ComboCounterComponent : Component
{
    [DataField]
    public int ComboCounter;

    [DataField]
    public int MaxCombo;

    [DataField]
    public TimeSpan LastCombo;

    [DataField]
    public float Duration = 3f;
}
