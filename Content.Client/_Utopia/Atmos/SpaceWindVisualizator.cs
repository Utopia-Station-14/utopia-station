using Content.Shared._Utopia.Atmos;
using Robust.Client.GameObjects;

namespace Content.Client._Utopia.Atmos.EntitySystems;

public sealed class SpaceWindVisualizerSystem : VisualizerSystem<AppearanceComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, AppearanceComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<float>(uid, SpaceWindVisuals.Alpha, out var alpha, component))
            return;

        SpriteSystem.SetColor((uid, args.Sprite), args.Sprite.Color.WithAlpha(alpha));
    }
}
