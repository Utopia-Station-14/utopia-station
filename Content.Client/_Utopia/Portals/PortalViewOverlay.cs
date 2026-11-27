using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Utopia.Portals;

public sealed partial class PortalViewOverlay : Overlay
{
    [Dependency] private IEntitySystemManager _sysMan = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly List<ShaderInstance> _shaders = new();
    private PortalViewSystem? _system;

    public PortalViewOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _system ??= _sysMan.GetEntitySystem<PortalViewSystem>();
        if (_system.Rendering)
            return;

        _system.MainViewportSize = args.Viewport.Size;

        var views = _system.Views;
        if (views.Count == 0)
            return;

        var handle = args.WorldHandle;

        for (var i = 0; i < views.Count; i++)
        {
            var view = views[i];

            if (view.Viewport is not { } viewport)
            {
                handle.UseShader(null);
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, view.Cone, Color.Black);
                continue;
            }

            var shader = GetShader(i);
            shader.SetParameter("portalTex", viewport.RenderTarget.Texture);
            handle.UseShader(shader);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, view.Cone, Color.White);
        }

        handle.UseShader(null);
    }

    private ShaderInstance GetShader(int index)
    {
        while (_shaders.Count <= index)
            _shaders.Add(_proto.Index<ShaderPrototype>("PortalView").InstanceUnique());

        return _shaders[index];
    }
}
