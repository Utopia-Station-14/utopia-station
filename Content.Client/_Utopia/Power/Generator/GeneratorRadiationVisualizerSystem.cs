using Content.Shared._Utopia.Power.Generator;
using Content.Shared.Power.Generator;
using Robust.Client.GameObjects;

namespace Content.Client._Utopia.Power.Generator;

public sealed class GeneratorRadiationVisualizerSystem : VisualizerSystem<GeneratorRadiationComponent>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GeneratorRadiationComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, GeneratorRadiationComponent component, ComponentStartup args)
    {
        if (TryComp(uid, out SpriteComponent? sprite) && TryComp(uid, out AppearanceComponent? appearance))
            UpdateSprite(uid, sprite, appearance);
    }

    protected override void OnAppearanceChange(EntityUid uid, GeneratorRadiationComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        UpdateSprite(uid, args.Sprite, args.Component);
    }

    private void UpdateSprite(EntityUid uid, SpriteComponent sprite, AppearanceComponent appearance)
    {
        if (!SpriteSystem.LayerMapTryGet((uid, sprite), GeneratorVisualLayers.Body, out var layer, false))
            return;

        AppearanceSystem.TryGetData(uid, GeneratorVisuals.Radiating, out bool radiating, appearance);
        AppearanceSystem.TryGetData(uid, GeneratorVisuals.Running, out bool running, appearance);

        var state = radiating
            ? GeneratorVisualState.Radiating
            : running
                ? GeneratorVisualState.Running
                : GeneratorVisualState.Idle;

        SpriteSystem.LayerSetRsiState((uid, sprite), layer,
        state switch
        {
            GeneratorVisualState.Idle => "portgen1",
            GeneratorVisualState.Running => "portgen1on",
            GeneratorVisualState.Radiating => "portgen1rad",
            _ => "portgen1"
        });
    }
}
