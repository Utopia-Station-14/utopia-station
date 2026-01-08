using System.Collections.Generic;
using Content.Shared.ZLevels.Components;
using Content.Shared.ZLevels;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Map.Components;
using System.Numerics;

namespace Content.Server.ZLevels.Systems
{
    /// <summary>
    /// Выполняет синхронизацию координат связанных гридов.
    /// </summary>
    public sealed class ZLinkedGridSystem : EntitySystem
    {
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly IMapManager _mapManager = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<ZLinkedGridComponent, MapInitEvent>(OnInit);
            SubscribeLocalEvent<ZLinkedGridComponent, ComponentShutdown>(OnShutdown);
        }

        private void OnInit(EntityUid uid, ZLinkedGridComponent comp, MapInitEvent args)
        {
            RebuildGroup(comp.LinkGroupId);
        }

        private void OnShutdown(EntityUid uid, ZLinkedGridComponent comp, ComponentShutdown args)
        {
            RebuildGroup(comp.LinkGroupId);
        }

        private void RebuildGroup(string groupId)
        {
            EntityUid? anchor = null;
            var grids = new List<(EntityUid uid, ZLinkedGridComponent comp)>();

            foreach (var comp in EntityQuery<ZLinkedGridComponent>())
            {
                var uid = comp.Owner;

                if (comp.LinkGroupId != groupId)
                    continue;

                grids.Add((uid, comp));
                if (comp.IsAnchor)
                    anchor = uid;
            }

            if (anchor is null)
                return;

            if (!TryComp<TransformComponent>(anchor.Value, out var anchorXform))
                return;

            if (!anchorXform.GridUid.HasValue)
                return;

            if (!TryComp<MapGridComponent>(anchorXform.GridUid.Value, out var mapGrid))
                return;

            var tileSize = mapGrid.TileSizeVector;
            foreach ((EntityUid uid, ZLinkedGridComponent comp) in grids)
            {
                if (uid == anchor)
                    continue;

                var offset = (Vector2)comp.TileOffset * tileSize;

                _transform.SetWorldPosition(
                    uid,
                    anchorXform.WorldPosition + offset
                );

                _transform.SetWorldRotation(
                    uid,
                    anchorXform.WorldRotation
                );
            }
        }

        /// <summary>
        /// Вызывает событие ZLinkedGridEvent на всех связанных гридах.
        /// </summary>
        public void RaiseZLinkedEvent(EntityUid source, ZLinkedGridEvent ev)
        {
            if (!TryComp<ZLinkedGridComponent>(source, out var comp))
                return;

            foreach (var target in comp.LinkedGrids)
            {
                RaiseLocalEvent(target, ev);
            }
        }
    }
}