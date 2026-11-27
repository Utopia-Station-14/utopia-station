using System.Numerics;
using Content.Shared._Utopia.Portals;
using Content.Shared.Teleportation.Components;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Content.Shared.Physics;
using Robust.Shared.Physics;

namespace Content.Client._Utopia.Portals;

public sealed partial class PortalViewSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IEyeManager _eyeMan = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    public const int MaxPortals = 2;
    public const float MaxDistance = 20f;
    public const float ConeRange = 50f;
    public const float DefaultHalfSize = 0.5f;

    public bool CheckVisibility = false;
    public bool Rendering { get; private set; }
    public Vector2i MainViewportSize;
    public bool BlackoutUnlinked = true;

    public IReadOnlyList<PortalViewData> Views => _views;

    private readonly List<PortalViewData> _views = new();
    private readonly List<Candidate> _candidates = new();
    private readonly List<IClydeViewport> _viewports = new();
    private readonly List<Robust.Shared.Graphics.Eye> _eyes = new();

    private readonly record struct Candidate(
        EntityUid Entry,
        Vector2 Pos,
        float Dist,
        float HalfSize,
        Angle GridRotation);

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(Robust.Client.GameObjects.EyeSystem));
        _overlays.AddOverlay(new PortalViewOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlays.RemoveOverlay<PortalViewOverlay>();
        foreach (var viewport in _viewports)
            viewport.Dispose();

        _viewports.Clear();
        _eyes.Clear();
        _views.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        _views.Clear();

        if (Rendering)
            return;

        var mainEye = _eyeMan.CurrentEye;
        var mapId = mainEye.Position.MapId;
        if (mapId == MapId.Nullspace)
            return;

        var eyeWorld = mainEye.Position.Position + mainEye.Offset;

        CollectCandidates(mapId, eyeWorld);
        if (_candidates.Count == 0)
            return;

        _candidates.Sort(static (a, b) => a.Dist.CompareTo(b.Dist));
        var size = MainViewportSize == default ? _clyde.ScreenSize : MainViewportSize;
        var rendered = 0;

        foreach (var candidate in _candidates)
        {
            if (!PortalViewCone.TryBuild(
                    eyeWorld,
                    candidate.Pos,
                    candidate.HalfSize,
                    candidate.GridRotation,
                    ConeRange,
                    out var cone))
            {
                continue;
            }

            var visible = IsPortalVisible(
                mapId,
                eyeWorld,
                candidate.Pos,
                candidate.HalfSize,
                candidate.GridRotation,
                candidate.Entry);

            if (!visible && CheckVisibility)
                continue;

            if (!TryGetExit(candidate.Entry, out var exitXform))
            {
                if (BlackoutUnlinked)
                    _views.Add(new PortalViewData(null, cone));
                continue;
            }

            if (rendered >= MaxPortals)
                continue;
            var exitPos = _xform.GetWorldPosition(exitXform);
            var delta = GetGridRotation(exitXform) - candidate.GridRotation;

            IClydeViewport viewport;
            try
            {
                viewport = RenderExitView(
                    rendered,
                    size,
                    mainEye,
                    eyeWorld,
                    candidate.Pos,
                    new MapCoordinates(exitPos, exitXform.MapID),
                    delta);
            }
            catch (Exception e)
            {
                _views.Add(new PortalViewData(null, cone));
                continue;
            }

            rendered++;
            _views.Add(new PortalViewData(viewport, cone));
        }
    }

    private void CollectCandidates(MapId mapId, Vector2 eyeWorld)
    {
        _candidates.Clear();

        var query = EntityQueryEnumerator<PortalComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var pos = _xform.GetWorldPosition(xform);
            var dist = (pos - eyeWorld).Length();
            if (dist > MaxDistance)
                continue;

            var halfSize = TryComp(uid, out PortalViewComponent? view)
                ? view.HalfSize
                : DefaultHalfSize;

            _candidates.Add(new Candidate(uid, pos, dist, halfSize, GetGridRotation(xform)));
        }
    }

    private bool TryGetExit(EntityUid entry, out TransformComponent exitXform)
    {
        exitXform = default!;

        if (!TryComp(entry, out LinkedEntityComponent? link))
            return false;

        foreach (var linked in link.LinkedEntities)
        {
            if (linked == entry ||
                !HasComp<PortalComponent>(linked) ||
                !TryComp(linked, out TransformComponent? xform) ||
                xform.MapID == MapId.Nullspace)
            {
                continue;
            }

            exitXform = xform;
            return true;
        }

        return false;
    }

    private IClydeViewport RenderExitView(int index, Vector2i size, IEye mainEye, Vector2 eyeWorld, Vector2 entryPos, MapCoordinates exit, Angle delta)
    {
        var mappedOffset = delta.RotateVec(eyeWorld - entryPos);

        var eye = GetEye(index);
        eye.Position = new MapCoordinates(exit.Position + mappedOffset, exit.MapId);
        eye.Rotation = mainEye.Rotation + delta;
        eye.Zoom = mainEye.Zoom;
        eye.DrawFov = mainEye.DrawFov;
        eye.DrawLight = mainEye.DrawLight;

        var viewport = GetViewport(index, size);
        viewport.Eye = eye;

        Rendering = true;
        try
        {
            viewport.Render();
        }
        finally
        {
            Rendering = false;
        }

        return viewport;
    }

    private Angle GetGridRotation(TransformComponent xform)
    {
        return xform.GridUid is { } grid
            ? _xform.GetWorldRotation(grid)
            : Angle.Zero;
    }

    private Robust.Shared.Graphics.Eye GetEye(int index)
    {
        while (_eyes.Count <= index)
            _eyes.Add(new Robust.Shared.Graphics.Eye());

        return _eyes[index];
    }

    private IClydeViewport GetViewport(int index, Vector2i size)
    {
        while (_viewports.Count <= index)
            _viewports.Add(_clyde.CreateViewport(size, $"PortalViewport{_viewports.Count}"));

        if (_viewports[index].Size != size)
        {
            _viewports[index].Dispose();
            _viewports[index] = _clyde.CreateViewport(size, $"PortalViewport{index}");
        }

        return _viewports[index];
    }

    private bool IsPortalVisible(MapId map, Vector2 eye, Vector2 portalPos, float halfSize, Angle rotation, EntityUid portal)
    {
        if (IsPointVisible(map, eye, portalPos, portalPos, halfSize, rotation, portal))
            return true;

        var offset = halfSize * 0.9f;
        for (var i = 0; i < 4; i++)
        {
            var local = new Vector2(
                i == 0 || i == 3 ? -offset : offset,
                i < 2 ? -offset : offset);

            var point = portalPos + rotation.RotateVec(local);

            if (IsPointVisible(map, eye, point, portalPos, halfSize, rotation, portal))
                return true;
        }

        return false;
    }

    private bool IsPointVisible(MapId map, Vector2 eye, Vector2 target, Vector2 portalPos, float halfSize, Angle rotation, EntityUid portal)
    {
        var delta = target - eye;
        var length = delta.Length();
        if (length <= 0.01f)
            return true;

        var ray = new CollisionRay(eye, delta / length, (int) CollisionGroup.Opaque);

        foreach (var hit in _physics.IntersectRay(map, ray, length, portal, returnOnFirstHit: false))
        {
            var local = (-rotation).RotateVec(hit.HitPos - portalPos);
            var limit = halfSize + 0.05f;

            if (Math.Abs(local.X) <= limit && Math.Abs(local.Y) <= limit)
                continue;

            return false;
        }

        return true;
    }
}
