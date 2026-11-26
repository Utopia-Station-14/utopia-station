using Content.Shared._Utopia.PDA.PdaPainter;
using Content.Shared.Access.Components;
using Content.Shared.PDA;
using Robust.Client.GameObjects;
using Robust.Shared.Map;

namespace Content.Client._Utopia.PDA.PdaPainter;

public sealed class PaintedIdCardVisualizerSystem : VisualizerSystem<IdCardComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, IdCardComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<string>(uid, PdaPainterVisuals.IdCardPrototype, out var protoId, args.Component))
            return;

        var template = Spawn(protoId, MapCoordinates.Nullspace);

        SpriteSystem.CopySprite(template, uid);
        Del(template);
    }
}

public sealed class PaintedPdaVisualizerSystem : VisualizerSystem<PdaComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, PdaComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<string>(uid, PdaPainterVisuals.PdaPrototype, out var protoId, args.Component))
            return;

        var template = Spawn(protoId, MapCoordinates.Nullspace);

        SpriteSystem.CopySprite(template, uid);
        Del(template);
    }
}
