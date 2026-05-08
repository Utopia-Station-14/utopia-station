using Content.Shared._Utopia.Sprite;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client._Utopia.Sprite;

public sealed class WindowSpriteSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WindowSpriteComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, WindowSpriteComponent component, ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        sprite.Color = sprite.Color.WithAlpha(component.Alpha);
    }
}