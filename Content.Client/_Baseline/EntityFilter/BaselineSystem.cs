using Content.Shared.CCVar;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Client._Baseline.EntityFilter;

public sealed partial class BaselineSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        _cfg.OnValueChanged(CCVars.ServerLanguage, OnLanguageChange, true);
        _cfg.SetCVar(CVars.LocCultureName, _cfg.GetCVar(CCVars.ServerLanguage));
    }

    private void OnLanguageChange(string obj)
    {
        _cfg.SetCVar(CVars.LocCultureName, obj);
    }
}
