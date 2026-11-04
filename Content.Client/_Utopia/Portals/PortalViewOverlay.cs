using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client.PortalView;

public sealed partial class PortalViewOverlay : Overlay
{
    [Dependency] private IEntitySystemManager _sysMan = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    private readonly ShaderInstance _shader;
    private PortalViewSystem? _system;

    public PortalViewOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _proto.Index<ShaderPrototype>("PortalView").InstanceUnique();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _system ??= _sysMan.GetEntitySystem<PortalViewSystem>();
        if (_system.Rendering)
            return;

        _system.MainViewportSize = args.Viewport.Size;

        if (_system.Views.Count == 0)
            return;

        var handle = args.WorldHandle;

        foreach (var view in _system.Views)
        {
            if (view.Viewport is not { } vp)
            {
                handle.UseShader(null);
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, view.Cone, Color.Black);
                continue;
            }

            handle.UseShader(_shader);
            _shader.SetParameter("portalTex", vp.RenderTarget.Texture);
            _shader.SetParameter("debug", _system.Debug ? 1f : 0f);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, view.Cone, Color.White);
        }

        handle.UseShader(null);
    }
}
