using Content.Shared.Teleportation.Components;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server.PortalView;

public sealed partial class PortalViewPvsSystem : EntitySystem
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ViewSubscriberSystem _viewSubscriber = default!;
    [Dependency] private TransformSystem _xform = default!;

    private const float Range = 20f;
    private const float Interval = 0.5f;

    private float _accum;
    private readonly Dictionary<ICommonSession, HashSet<EntityUid>> _subscribed = new();
    private readonly HashSet<Entity<PortalComponent>> _nearby = new();

    public override void Initialize()
    {
        base.Initialize();
        _players.PlayerStatusChanged += OnStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnStatusChanged;
    }

    private void OnStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.Disconnected)
            Clear(e.Session);
    }

    public override void Update(float frameTime)
    {
        _accum += frameTime;
        if (_accum < Interval)
            return;
        _accum = 0;

        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } player || session.Status != SessionStatus.InGame)
            {
                Clear(session);
                continue;
            }

            var needed = new HashSet<EntityUid>();
            _nearby.Clear();
            _lookup.GetEntitiesInRange(_xform.GetMapCoordinates(player), Range, _nearby);

            foreach (var portal in _nearby)
            {
                if (!TryComp(portal, out LinkedEntityComponent? link))
                    continue;

                foreach (var exit in link.LinkedEntities)
                    needed.Add(exit);
            }

            if (!_subscribed.TryGetValue(session, out var current))
            {
                current = new HashSet<EntityUid>();
                _subscribed[session] = current;
            }

            foreach (var exit in needed)
            {
                if (current.Add(exit))
                    _viewSubscriber.AddViewSubscriber(exit, session);
            }

            current.RemoveWhere(exit =>
            {
                if (needed.Contains(exit))
                    return false;

                if (!Deleted(exit))
                    _viewSubscriber.RemoveViewSubscriber(exit, session);
                return true;
            });
        }
    }

    private void Clear(ICommonSession session)
    {
        if (!_subscribed.Remove(session, out var set))
            return;

        foreach (var exit in set)
        {
            if (!Deleted(exit))
                _viewSubscriber.RemoveViewSubscriber(exit, session);
        }
    }
}
