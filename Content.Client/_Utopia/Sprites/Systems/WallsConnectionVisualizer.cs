using Content.Shared._Utopia.Walls;
using Robust.Client.GameObjects;
using Robust.Shared.Maths;
using System.Numerics;

namespace Content.Client._Utopia.Walls;

public sealed partial class WallConnectVisualizerSystem : VisualizerSystem<WallConnectComponent>
{
    [Dependency] private AppearanceSystem _appearance = default!;

    protected override void OnAppearanceChange(EntityUid uid, WallConnectComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<int>(uid, WallVisuals.ConnectMask, out var mask, args.Component))
            return;

        AppearanceSystem.TryGetData<bool>(uid, WallVisuals.FlipX, out var flip, args.Component);

        var state = component.StatePrefix + mask;

        args.Sprite.LayerSetState(0, state);
        args.Sprite.LayerSetScale(0, new Vector2(flip ? -1f : 1f, 1f));
    }
}
