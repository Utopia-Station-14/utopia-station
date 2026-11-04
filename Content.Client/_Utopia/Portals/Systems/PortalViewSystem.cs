using System.Linq;
using System.Numerics;
using Content.Shared.Teleportation.Components;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client.PortalView;

public sealed partial class PortalViewSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IEyeManager _eyeMan = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    public const int MaxPortals = 2;
    public const float MaxDistance = 20f;
    public const float ConeRange = 50f;
    public const float PortalHalfSize = 0.5f;
    public bool Rendering { get; private set; }
    public Vector2i MainViewportSize;
    public bool Debug;
    public string DebugInfo = "";
    public readonly List<PortalViewData> Views = new();
    private readonly List<IClydeViewport> _pool = new();
    private readonly Robust.Shared.Graphics.Eye _virtualEye = new();

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new PortalViewOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<PortalViewOverlay>();
        foreach (var vp in _pool)
            vp.Dispose();
        _pool.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        Views.Clear();

        if (Rendering)
            return;

        var mainEye = _eyeMan.CurrentEye;
        var eyePos = mainEye.Position;
        if (eyePos.MapId == MapId.Nullspace)
            return;

        var size = MainViewportSize == default ? _clyde.ScreenSize : MainViewportSize;
        var candidates = new List<(EntityUid Entry, EntityUid? Exit, Vector2 Pos, float Dist)>();
        var query = EntityQueryEnumerator<PortalComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != eyePos.MapId)
                continue;

            var pos = _xform.GetWorldPosition(xform);
            var dist = (pos - eyePos.Position).Length();
            if (dist > MaxDistance)
                continue;

            EntityUid? exit = null;
            if (TryComp(uid, out LinkedEntityComponent? link))
            {
                foreach (var linked in link.LinkedEntities)
                {
                    if (linked == uid ||
                        !HasComp<PortalComponent>(linked) ||
                        !TryComp(linked, out TransformComponent? linkedXform) ||
                        linkedXform.MapID == MapId.Nullspace)
                    {
                        continue;
                    }

                    exit = linked;
                    break;
                }
            }

            candidates.Add((uid, exit, pos, dist));
        }

        if (candidates.Count == 0)
            return;

        candidates.Sort((a, b) => a.Dist.CompareTo(b.Dist));
        var rendered = 0;
        foreach (var (entry, exit, entryPos, _) in candidates)
        {
            if (!TryBuildCone(eyePos.Position, entryPos, PortalHalfSize, out var cone))
                continue;

            if (exit is not { } exitUid
                || !TryComp(exitUid, out TransformComponent? exitXform)
                || exitXform.MapID == MapId.Nullspace)
            {
                Views.Add(new PortalViewData(null, cone));
                continue;
            }

            if (rendered >= MaxPortals)
                continue;

            var exitPos = _xform.GetWorldPosition(exitXform);
            var entryXform = Transform(entry);
            var entryGridRotation = GetGridRotation(entryXform);
            var exitGridRotation = GetGridRotation(exitXform);
            var delta = exitGridRotation - entryGridRotation;

            var eyeOffsetFromEntry = eyePos.Position - entryPos;
            var mappedEyeOffset = delta.RotateVec(eyeOffsetFromEntry);

            _virtualEye.Position = new MapCoordinates(
                exitPos + mappedEyeOffset,
                exitXform.MapID);
            _virtualEye.Rotation = mainEye.Rotation + delta;
            _virtualEye.Zoom = mainEye.Zoom;

            if (Debug)
            {
                DebugInfo = $"portals={candidates.Count} entry={entryPos} exit={exitPos} " +
                            $"shift={exitPos - entryPos} gridRotDelta={delta} eye={eyePos.Position}";
            }

            var vp = GetViewport(rendered, size);
            vp.Eye = _virtualEye;

            Rendering = true;
            try
            {
                vp.Render();
            }
            finally
            {
                Rendering = false;
            }

            rendered++;
            Views.Add(new PortalViewData(vp, cone));
        }
    }

    private Angle GetGridRotation(TransformComponent xform)
    {
        return xform.GridUid is { } grid
            ? _xform.GetWorldRotation(grid)
            : Angle.Zero;
    }

    private IClydeViewport GetViewport(int index, Vector2i size)
    {
        while (_pool.Count <= index)
            _pool.Add(_clyde.CreateViewport(size, $"PortalViewport{_pool.Count}"));

        if (_pool[index].Size != size)
        {
            _pool[index].Dispose();
            _pool[index] = _clyde.CreateViewport(size, $"PortalViewport{index}");
        }

        return _pool[index];
    }

    private static bool TryBuildCone(Vector2 eye, Vector2 center, float h, out Vector2[] verts)
    {
        verts = Array.Empty<Vector2>();

        if (Math.Abs(eye.X - center.X) < h && Math.Abs(eye.Y - center.Y) < h)
            return false;

        var dir = (center - eye).ToAngle();
        var min = double.MaxValue;
        var max = double.MinValue;
        var cMin = center;
        var cMax = center;

        for (var i = 0; i < 4; i++)
        {
            var c = center + new Vector2(i == 0 || i == 3 ? -h : h, i < 2 ? -h : h);
            var a = Angle.ShortestDistance(dir, (c - eye).ToAngle()).Theta;
            if (a < min) { min = a; cMin = c; }
            if (a > max) { max = a; cMax = c; }
        }

        verts = new[]
        {
            cMin,
            cMax,
            eye + Vector2.Normalize(cMax - eye) * ConeRange,
            eye + Vector2.Normalize(cMin - eye) * ConeRange,
        };
        return true;
    }
}

public readonly record struct PortalViewData(IClydeViewport? Viewport, Vector2[] Cone);
