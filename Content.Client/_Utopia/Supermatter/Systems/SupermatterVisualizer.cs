using Content.Shared._Utopia.Supermatter.Components;
using Robust.Client.GameObjects;

namespace Content.Client._Utopia.Supermatter;

public sealed class SupermatterVisualizerSystem : VisualizerSystem<SupermatterComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, SupermatterComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<SupermatterVisualState>(uid, SupermatterVisuals.Status, out var state, args.Component))
            return;

        switch (state)
        {
            case SupermatterVisualState.Inactive:
                args.Sprite.LayerSetState(SupermatterVisualLayers.Crystal, "inactive");
                args.Sprite.LayerSetVisible(SupermatterVisualLayers.Glow, false);
                break;

            case SupermatterVisualState.Stable:
                args.Sprite.LayerSetState(SupermatterVisualLayers.Crystal, "stable");
                args.Sprite.LayerSetState(SupermatterVisualLayers.Glow, "glowing-stable");
                args.Sprite.LayerSetVisible(SupermatterVisualLayers.Glow, true);
                break;

            case SupermatterVisualState.Destabilization:
                args.Sprite.LayerSetState(SupermatterVisualLayers.Crystal, "destabilization");
                args.Sprite.LayerSetState(SupermatterVisualLayers.Glow, "glowing-destabilization");
                args.Sprite.LayerSetVisible(SupermatterVisualLayers.Glow, true);
                break;
        }
    }
}
