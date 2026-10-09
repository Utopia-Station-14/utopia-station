using Content.Server.Cargo.Systems;
using Content.Shared.Stacks;

namespace Content.Server.Cargo.Components;

[RegisterComponent]
[Access(typeof(CargoSystem))]
public sealed partial class CargoPalletConsoleComponent : Component
// Utopia-Tweak : Economy
{
    [DataField]
    public string? CurrencyProto = null;
}
// Utopia-Tweak : Economy
