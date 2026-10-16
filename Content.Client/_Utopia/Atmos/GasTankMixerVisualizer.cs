using Content.Shared.Atmos;
using Robust.Client.GameObjects;

namespace Content.Client._Utopia.Atmos;

public sealed partial class GasTankMixerClientSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GasTankMixerVisualsComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnHandleState(EntityUid uid, GasTankMixerVisualsComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.LayerSetVisible((uid, sprite), GasTankMixerVisualLayers.TankA, comp.HasTankA);
        _sprite.LayerSetVisible((uid, sprite), GasTankMixerVisualLayers.TankB, comp.HasTankB);
    }
}
