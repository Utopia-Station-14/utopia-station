// SPDX-FileCopyrightText: 2026 ColonialMarinesUniverse contributors <https://github.com/AU-14/ColonialMarinesUniverse>
// SPDX-License-Identifier: AGPL-3.0-only
// Ported from ColonialMarinesUniverse Content.Client/_CMU14/ZLevels/Lighting/CEZLevelProjectedLightingSystem.cs.
// Client-only fake-light projection: lights on adjacent Z layers spawn PointLights at floor-opening
// centers on the viewer's map, attenuated by depth and distance. Purely cosmetic.

using System.Numerics;
using Content.Client._Utopia.ZLevels.Lighting;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._Utopia.CCVar;
using Content.Shared.Physics;
using Content.Shared.Maps;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Robust.Shared.GameObjects;
using DependencyAttribute = Robust.Shared.IoC.DependencyAttribute;

namespace Content.Client._Utopia.ZLevels.Lighting;

/// <summary>
/// Projects client-only point lights from adjacent Z-level maps onto the local receiving map.
/// </summary>
public sealed partial class CEZLevelProjectedLightingSystem : EntitySystem
{
    private const float OpeningConnectionDistance = 1.5f;
    private const int MinStripCandidateCount = 4;
    private const float MinStripLength = 3f;
    private const float StripLinearityRatio = 2.5f;
    private const float StripSampleSpacing = 1.5f;
    private const int MaxStripSamples = 8;
    private const float MaxProjectedCenterOffset = 0.5f;
    private const int PartialSelectionSortMultiplier = 4;
    private const float ViewBoundsLightPadding = 2f;

    private float _updateAccumulator = 0f;
    private const float UpdateRate = 1f;

    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private ITileDefinitionManager _tileDefinition = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private CEZLevelOpeningCache _zCache = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    private readonly Dictionary<ProjectedLightKey, EntityUid> _projectedLights = new();
    private readonly Dictionary<MergedProjectedLightKey, EntityUid> _mergedProjectedLights = new();

    private readonly HashSet<EntityUid> _activeThisFrame = new();
    private readonly List<ProjectedLightCandidate> _candidates = new();
    private readonly List<ProjectedLightCandidate> _sourceCandidates = new();
    private readonly List<ProjectedLightCandidate> _componentCandidates = new();
    private readonly List<int> _candidateStack = new();
    private readonly List<bool> _visitedSourceCandidates = new();
    private readonly List<Entity<MapGridComponent>> _openingGrids = new();
    private readonly List<ProjectedLightKey> _toRemove = new();
    private readonly List<MergedProjectedLightKey> _mergedToRemove = new();
    private readonly List<(Vector2 Center, float Distance)> _tempOpenings = new();
    private readonly List<(Vector2 Center, float Distance)> _tempIntermediateOpenings = new();
    private readonly Dictionary<MapId, List<SourceLight>> _sourceLightBuckets = new();
    private readonly Dictionary<OpeningCandidateBucketKey, List<int>> _openingCandidateBuckets = new();
    private readonly List<List<int>> _openingCandidateBucketPool = new();
    private readonly ProjectedLightAlongAxisComparer _alongAxisComparer = new();

    [Dependency] private EntityQuery<CEZProjectedLightComponent> _projectedQuery = default!;
    [Dependency] private EntityQuery<PointLightComponent> _pointLightQuery = default!;
    [Dependency] private EntityQuery<MapComponent> _mapQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private EntityQuery<CEZLevelMapComponent> _zMapQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_config.GetCVar(UCCVars.CEZProjectedLightingEnabled))
        {
            CleanupAllProjectedLights();
            return;
        }

        _updateAccumulator += frameTime;
        if (_updateAccumulator < UpdateRate)
            return;

        _updateAccumulator = 0f;

        if (_player.LocalEntity is not { } playerUid ||
            !TryComp<CEZLevelViewerComponent>(playerUid, out _) ||
            !_xformQuery.TryComp(playerUid, out var playerXform) ||
            playerXform.MapUid is not { } playerMapUid ||
            !_mapQuery.TryComp(playerMapUid, out var playerMapComp) ||
            !_zMapQuery.TryComp(playerMapUid, out var playerZMap))
        {
            CleanupAllProjectedLights();
            return;
        }

        var maxPerLevel = Math.Max(0, _config.GetCVar(UCCVars.CEZMaxProjectedLightsPerLevel));
        if (maxPerLevel == 0)
        {
            CleanupAllProjectedLights();
            return;
        }

        var attenuationPerDepth = Math.Max(0f, _config.GetCVar(UCCVars.CEZProjectedLightAttenuationPerDepth));
        var attenuationPerTile = Math.Max(0f, _config.GetCVar(UCCVars.CEZProjectedLightAttenuationPerTile));
        var maxRadius = Math.Max(0f, _config.GetCVar(UCCVars.CEZProjectedLightMaxRadius));
        var radiusScale = Math.Max(0f, _config.GetCVar(UCCVars.CEZProjectedLightRadiusScale));
        var minEnergy = Math.Max(0f, _config.GetCVar(UCCVars.CEZProjectedLightMinEnergy));
        var maxDepth = CESharedZLevelsSystem.MaxZLevelsBelowRendering;

        var currentFrame = _timing.CurFrame;
        _activeThisFrame.Clear();

        var viewBounds = _eyeManager.GetWorldViewbounds();
        Entity<CEZLevelMapComponent?> playerZLevelMap = (playerMapUid, playerZMap);

        var activeMaps = new HashSet<MapId> { playerMapComp.MapId };

        for (var depthOffset = -maxDepth; depthOffset <= maxDepth; depthOffset++)
        {
            if (depthOffset == 0) continue;

            if (_zLevels.TryMapOffset(playerZLevelMap, depthOffset, out var adjacentMap) &&
                adjacentMap is { } adj &&
                _mapQuery.TryComp(adj.Owner, out var adjacentMapComp) &&
                adjacentMapComp.MapId != MapId.Nullspace)
            {
                activeMaps.Add(adjacentMapComp.MapId);
            }
        }

        BuildSourceLightBuckets(viewBounds, minEnergy, activeMaps);

        _candidates.Clear();
        for (var depthOffset = -maxDepth; depthOffset <= maxDepth; depthOffset++)
        {
            if (depthOffset == 0)
                continue;

            if (!_zLevels.TryMapOffset(playerZLevelMap, depthOffset, out var adjacentMap) ||
                adjacentMap is not { } adj ||
                !_mapQuery.TryComp(adj.Owner, out var adjacentMapComp) ||
                adjacentMapComp.MapId == MapId.Nullspace)
            {
                continue;
            }

            if (!_sourceLightBuckets.TryGetValue(adjacentMapComp.MapId, out var sourceLights) ||
                sourceLights.Count == 0)
            {
                continue;
            }

            CollectCandidates(
                sourceLights,
                adj,
                adjacentMapComp.MapId,
                playerMapUid,
                playerMapComp.MapId,
                depthOffset,
                attenuationPerDepth,
                attenuationPerTile,
                radiusScale,
                maxRadius,
                minEnergy,
                playerZLevelMap,
                0,
                depthOffset);
        }

        ApplyLevelCap(maxPerLevel, currentFrame);

        // Pass 2: Каскад для слоев под игроком — проецирует свет со ВСЕХ верхних слоев на нижние.
        for (var receivingDepth = -1; receivingDepth >= -maxDepth; receivingDepth--)
        {
            if (!_zLevels.TryMapOffset(playerZLevelMap, receivingDepth, out var receivingMap) ||
                receivingMap is not { } receiving ||
                !_mapQuery.TryComp(receiving.Owner, out var receivingMapComp) ||
                receivingMapComp.MapId == MapId.Nullspace)
            {
                continue;
            }

            for (var sourceDepth = receivingDepth + 1; sourceDepth <= maxDepth; sourceDepth++)
            {
                if (sourceDepth == receivingDepth) continue;

                Entity<CEZLevelMapComponent> sourceMap;
                MapComponent sourceMapComp;
                if (sourceDepth == 0)
                {
                    sourceMap = (playerMapUid, playerZMap);
                    sourceMapComp = playerMapComp;
                }
                else if (!_zLevels.TryMapOffset(playerZLevelMap, sourceDepth, out var offsetSourceMap) ||
                         offsetSourceMap is not { } offsetSource ||
                         !_mapQuery.TryComp(offsetSource.Owner, out var offsetSourceMapComp))
                {
                    continue;
                }
                else
                {
                    sourceMap = offsetSource;
                    sourceMapComp = offsetSourceMapComp;
                }

                if (sourceMapComp.MapId == MapId.Nullspace)
                    continue;

                _candidates.Clear();
                if (!_sourceLightBuckets.TryGetValue(sourceMapComp.MapId, out var sourceLights) ||
                    sourceLights.Count == 0)
                {
                    continue;
                }

                var relativeDepthOffset = sourceDepth - receivingDepth;

                CollectCandidates(
                    sourceLights,
                    sourceMap,
                    sourceMapComp.MapId,
                    receiving.Owner,
                    receivingMapComp.MapId,
                    relativeDepthOffset,
                    attenuationPerDepth,
                    attenuationPerTile,
                    radiusScale,
                    maxRadius,
                    minEnergy,
                    playerZLevelMap,
                    receivingDepth,
                    sourceDepth);

                ApplyLevelCap(maxPerLevel, currentFrame);
            }
        }

        CleanupStaleProjectedLights();
    }

    private void BuildSourceLightBuckets(Box2Rotated viewBounds, float minEnergy, HashSet<MapId> activeMaps)
    {
        ClearSourceLightBuckets();

        var aabb = viewBounds.CalcBoundingBox();
        var maxRadius = Math.Max(0f, _config.GetCVar(UCCVars.CEZProjectedLightMaxRadius));
        var lookupBounds = aabb.Enlarged(maxRadius + ViewBoundsLightPadding);

        foreach (var mapId in activeMaps)
        {
            if (mapId == MapId.Nullspace)
                continue;

            var bucket = GetSourceLightBucket(mapId);

            foreach (var ent in _lookup.GetEntitiesIntersecting(mapId, lookupBounds))
            {
                if (!_pointLightQuery.TryComp(ent, out var light) ||
                    _projectedQuery.HasComp(ent) ||
                    !light.Enabled ||
                    light.Radius <= 0f ||
                    light.Energy <= 0f ||
                    light.Energy < minEnergy)
                {
                    continue;
                }

                var lightXform = _xformQuery.GetComponent(ent);
                var lightWorldPos = _transform.GetWorldPosition(lightXform);

                var expandedBounds = viewBounds.Enlarged(light.Radius + ViewBoundsLightPadding);
                if (!expandedBounds.Contains(lightWorldPos))
                    continue;

                bucket.Add(new SourceLight(
                    ent,
                    lightWorldPos,
                    light.Radius,
                    light.Energy,
                    light.Color,
                    light.Softness));
            }
        }
    }

    private List<SourceLight> GetSourceLightBucket(MapId mapId)
    {
        if (_sourceLightBuckets.TryGetValue(mapId, out var bucket))
            return bucket;

        bucket = new List<SourceLight>();
        _sourceLightBuckets[mapId] = bucket;
        return bucket;
    }

    private void ClearSourceLightBuckets()
    {
        foreach (var bucket in _sourceLightBuckets.Values)
        {
            bucket.Clear();
        }
    }

    private void ApplyLevelCap(int maxPerLevel, uint currentFrame)
    {
        if (_candidates.Count == 0)
            return;

        if (_candidates.Count <= maxPerLevel)
        {
            _candidates.Sort(CompareProjectedEnergyDescending);
            foreach (var candidate in _candidates)
            {
                UpdateProjectedLight(candidate, currentFrame);
            }

            return;
        }

        var directCount = Math.Max(0, maxPerLevel - 1);
        if (directCount > 0 && ShouldPartiallySelectCandidates(_candidates.Count, directCount))
        {
            SelectTopCandidates(directCount);
        }
        else if (directCount > 0)
        {
            _candidates.Sort(CompareProjectedEnergyDescending);
        }

        for (var i = 0; i < directCount; i++)
        {
            UpdateProjectedLight(_candidates[i], currentFrame);
        }

        UpdateProjectedLight(MergeOverflowCandidates(directCount), currentFrame);
    }

    private static bool ShouldPartiallySelectCandidates(int candidateCount, int directCount)
    {
        return directCount > 0 && candidateCount > directCount * PartialSelectionSortMultiplier;
    }

    private void SelectTopCandidates(int directCount)
    {
        for (var i = 0; i < directCount; i++)
        {
            var bestIndex = i;
            for (var j = i + 1; j < _candidates.Count; j++)
            {
                if (_candidates[j].ProjectedEnergy > _candidates[bestIndex].ProjectedEnergy)
                    bestIndex = j;
            }

            if (bestIndex == i)
                continue;

            (_candidates[i], _candidates[bestIndex]) = (_candidates[bestIndex], _candidates[i]);
        }
    }

    private static int CompareProjectedEnergyDescending(ProjectedLightCandidate left, ProjectedLightCandidate right)
    {
        return right.ProjectedEnergy.CompareTo(left.ProjectedEnergy);
    }

    private ProjectedLightCandidate MergeOverflowCandidates(int startIndex)
    {
        var first = _candidates[startIndex];
        var weightedOpening = Vector2.Zero;
        var weightedProjection = Vector2.Zero;
        var weightedColor = Vector4.Zero;
        var weightedSoftness = 0f;
        var totalWeight = 0f;
        var maxEnergy = 0f;
        var maxRadius = 0f;

        for (var i = startIndex; i < _candidates.Count; i++)
        {
            var candidate = _candidates[i];
            var weight = Math.Max(candidate.ProjectedEnergy, 0.001f);
            weightedOpening += candidate.OpeningCenter * weight;
            weightedProjection += candidate.ProjectedCenter * weight;
            weightedColor += candidate.Color.RGBA * weight;
            weightedSoftness += candidate.Softness * weight;
            totalWeight += weight;
            maxEnergy = Math.Max(maxEnergy, candidate.ProjectedEnergy);
            maxRadius = Math.Max(maxRadius, candidate.ProjectedRadius);
        }

        return new ProjectedLightCandidate(
            EntityUid.Invalid,
            first.SourceMapId,
            first.ReceivingMapId,
            first.DepthOffset,
            weightedOpening / totalWeight,
            weightedProjection / totalWeight,
            maxRadius,
            maxEnergy,
            new Color(weightedColor / totalWeight),
            weightedSoftness / totalWeight,
            true);
    }

    private void CollectCandidates(
        List<SourceLight> sourceLights,
        Entity<CEZLevelMapComponent> adjacentMap,
        MapId adjacentMapId,
        EntityUid playerMapUid,
        MapId playerMapId,
        int depthOffset,
        float attenuationPerDepth,
        float attenuationPerTile,
        float radiusScale,
        float maxRadius,
        float minEnergy,
        Entity<CEZLevelMapComponent?> playerZLevelMap,
        int startDepth,
        int endDepth)
    {
        var openingMap = GetOpeningMapForProjection(adjacentMap, playerMapUid, depthOffset);
        if (!_mapQuery.TryComp(openingMap, out var openingMapComp) ||
            openingMapComp.MapId == MapId.Nullspace)
        {
            return;
        }

        foreach (var sourceLight in sourceLights)
        {
            _tempOpenings.Clear();

            _zCache.FindOpeningCentersNear(
                openingMapComp.MapId,
                sourceLight.WorldPosition,
                sourceLight.Radius,
                _tempOpenings,
                _openingGrids,
                _map,
                _transform,
                _tileDefinition,
                true);

            if (_tempOpenings.Count == 0)
                continue;

            _sourceCandidates.Clear();

            foreach (var (openingCenter, sourceToOpeningDistance) in _tempOpenings)
            {
                if (Math.Abs(endDepth - startDepth) > 1 &&
                    !AreIntermediateLevelsOpen(playerZLevelMap, startDepth, endDepth, openingCenter))
                {
                    continue;
                }

                var rayDirection = openingCenter - sourceLight.WorldPosition;
                var rayLength = rayDirection.Length();
                if (rayLength > 0.01f)
                {
                    var ray = new CollisionRay(sourceLight.WorldPosition, rayDirection.Normalized(), (int) CollisionGroup.Opaque);
                    var blocked = false;
                    foreach (var _ in _physics.IntersectRay(adjacentMapId, ray, rayLength, ignoredEnt: sourceLight.Entity, returnOnFirstHit: true))
                    {
                        blocked = true;
                        break;
                    }

                    if (blocked)
                        continue;
                }

                var depth = Math.Abs(depthOffset);
                var s = Math.Clamp(sourceToOpeningDistance / sourceLight.Radius, 0f, 1f);
                var s2 = s * s;
                var numerator = (1f - s2) * (1f - s2);
                var denominator = 1f + attenuationPerDepth * depth + attenuationPerTile * sourceToOpeningDistance;
                var factor = numerator / denominator;
                var projectedEnergy = sourceLight.Energy * factor;

                if (projectedEnergy < minEnergy)
                    continue;

                var remainingDistance = sourceLight.Radius - sourceToOpeningDistance;
                if (remainingDistance <= 0f)
                    continue;

                var projectedRadius = Math.Min(remainingDistance * radiusScale, maxRadius);
                if (projectedRadius <= 0f)
                    continue;

                var projectedCenter = openingCenter;
                if (rayLength > 0.01f)
                    projectedCenter += rayDirection / rayLength * Math.Min(projectedRadius, MaxProjectedCenterOffset);

                var candidate = new ProjectedLightCandidate(
                    sourceLight.Entity,
                    adjacentMapId,
                    playerMapId,
                    depthOffset,
                    openingCenter,
                    projectedCenter,
                    projectedRadius,
                    projectedEnergy,
                    sourceLight.Color,
                    sourceLight.Softness);

                _sourceCandidates.Add(candidate);
            }

            if (_sourceCandidates.Count > 0)
                AddSourceCandidates();
        }
    }

    public bool IsTileTransparent(MapId mapId, Vector2 worldPos)
    {
        if (!_map.TryFindGridAt(mapId, worldPos, out var gridUid, out var gridComp))
            return false;

        if (!_map.TryGetTileRef(gridUid, gridComp, worldPos, out var tileRef))
            return false;

        var tileDef = (ContentTileDefinition) _tileDefinition[tileRef.Tile.TypeId];
        return tileDef.Transparent;
    }

    private bool AreIntermediateLevelsOpen(
        Entity<CEZLevelMapComponent?> playerZLevelMap,
        int startDepth,
        int endDepth,
        Vector2 openingCenter)
    {
        if (Math.Abs(endDepth - startDepth) <= 1)
            return true;

        var step = endDepth > startDepth ? 1 : -1;

        for (var d = startDepth + step; d != endDepth; d += step)
        {
            EntityUid interOwner;
            CEZLevelMapComponent? interComp;

            if (d == 0)
            {
                interOwner = playerZLevelMap.Owner;
                interComp = playerZLevelMap.Comp;
            }
            else if (!_zLevels.TryMapOffset(playerZLevelMap, d, out var offsetInterMap) || offsetInterMap is not { } offsetInter)
            {
                return false;
            }
            else
            {
                interOwner = offsetInter.Owner;
                interComp = offsetInter.Comp;
            }

            if (interComp == null ||
                !_mapQuery.TryComp(interOwner, out var interMapComp) ||
                interMapComp.MapId == MapId.Nullspace)
            {
                return false;
            }

            _tempIntermediateOpenings.Clear();
            _zCache.FindOpeningCentersNear(
                interMapComp.MapId,
                openingCenter,
                0.8f,
                _tempIntermediateOpenings,
                _openingGrids,
                _map,
                _transform,
                _tileDefinition,
                true);

            if (_tempIntermediateOpenings.Count == 0)
                return false;
        }

        return true;
    }

    private static EntityUid GetOpeningMapForProjection(
        Entity<CEZLevelMapComponent> sourceMap,
        EntityUid receivingMap,
        int depthOffset)
    {
        return depthOffset > 0 ? sourceMap.Owner : receivingMap;
    }

    private void RebuildOpeningCandidateBuckets()
    {
        ClearOpeningCandidateBuckets();

        for (var i = 0; i < _sourceCandidates.Count; i++)
        {
            var bucketKey = GetOpeningCandidateBucketKey(_sourceCandidates[i].OpeningCenter);
            if (!_openingCandidateBuckets.TryGetValue(bucketKey, out var bucket))
            {
                bucket = RentOpeningCandidateBucket();
                _openingCandidateBuckets[bucketKey] = bucket;
            }

            bucket.Add(i);
        }
    }

    private List<int> RentOpeningCandidateBucket()
    {
        if (_openingCandidateBucketPool.Count == 0)
            return new List<int>();

        var bucket = _openingCandidateBucketPool[^1];
        _openingCandidateBucketPool.RemoveAt(_openingCandidateBucketPool.Count - 1);
        return bucket;
    }

    private void ClearOpeningCandidateBuckets()
    {
        foreach (var bucket in _openingCandidateBuckets.Values)
        {
            bucket.Clear();
            _openingCandidateBucketPool.Add(bucket);
        }

        _openingCandidateBuckets.Clear();
    }

    private void AddSourceCandidates()
    {
        RebuildOpeningCandidateBuckets();

        _visitedSourceCandidates.Clear();
        for (var i = 0; i < _sourceCandidates.Count; i++)
        {
            _visitedSourceCandidates.Add(false);
        }

        for (var i = 0; i < _sourceCandidates.Count; i++)
        {
            if (_visitedSourceCandidates[i])
                continue;

            _componentCandidates.Clear();
            _candidateStack.Clear();
            _candidateStack.Add(i);
            _visitedSourceCandidates[i] = true;

            while (_candidateStack.Count > 0)
            {
                var index = _candidateStack[^1];
                _candidateStack.RemoveAt(_candidateStack.Count - 1);

                var candidate = _sourceCandidates[index];
                _componentCandidates.Add(candidate);

                QueueConnectedOpeningCandidates(candidate);
            }

            AddOpeningComponentCandidates(_componentCandidates);
        }

        ClearOpeningCandidateBuckets();
    }

    private void QueueConnectedOpeningCandidates(ProjectedLightCandidate candidate)
    {
        var bucketKey = GetOpeningCandidateBucketKey(candidate.OpeningCenter);
        for (var x = -1; x <= 1; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                var neighborKey = new OpeningCandidateBucketKey(bucketKey.X + x, bucketKey.Y + y);
                if (!_openingCandidateBuckets.TryGetValue(neighborKey, out var indexes))
                    continue;

                foreach (var index in indexes)
                {
                    if (_visitedSourceCandidates[index] ||
                        !AreConnectedOpenings(candidate, _sourceCandidates[index]))
                    {
                        continue;
                    }

                    _visitedSourceCandidates[index] = true;
                    _candidateStack.Add(index);
                }
            }
        }
    }

    private static OpeningCandidateBucketKey GetOpeningCandidateBucketKey(Vector2 openingCenter)
    {
        return new OpeningCandidateBucketKey(
            (int) MathF.Floor(openingCenter.X / OpeningConnectionDistance),
            (int) MathF.Floor(openingCenter.Y / OpeningConnectionDistance));
    }

    private static bool AreConnectedOpenings(ProjectedLightCandidate left, ProjectedLightCandidate right)
    {
        return Vector2.DistanceSquared(left.OpeningCenter, right.OpeningCenter) <=
               OpeningConnectionDistance * OpeningConnectionDistance;
    }

    private void AddOpeningComponentCandidates(List<ProjectedLightCandidate> component)
    {
        if (component.Count < MinStripCandidateCount ||
            !TryAddStripCandidates(component))
        {
            AddSeparatedCandidates(component, 1f);
        }
    }

    private bool TryAddStripCandidates(List<ProjectedLightCandidate> component)
    {
        if (!TryGetStripAxis(component, out var axis, out var minAlong, out var maxAlong))
            return false;

        _alongAxisComparer.Axis = axis;
        component.Sort(_alongAxisComparer);

        var length = maxAlong - minAlong;
        var sampleCount = Math.Clamp(
            (int) MathF.Ceiling(length / StripSampleSpacing) + 1,
            2,
            Math.Min(component.Count, MaxStripSamples));
        var energyScale = 1f / MathF.Sqrt(sampleCount);

        for (var i = 0; i < sampleCount; i++)
        {
            var index = sampleCount == 1
                ? 0
                : (int) MathF.Round(i * (component.Count - 1) / (sampleCount - 1f));
            var baseCandidate = component[Math.Clamp(index, 0, component.Count - 1)];
            var candidate = baseCandidate with
            {
                ProjectedEnergy = baseCandidate.ProjectedEnergy * energyScale,
            };

            if (OverlapsAcceptedCandidate(candidate))
                continue;

            _candidates.Add(candidate);
        }

        return true;
    }

    private static bool TryGetStripAxis(
        List<ProjectedLightCandidate> component,
        out Vector2 axis,
        out float minAlong,
        out float maxAlong)
    {
        axis = Vector2.UnitX;
        minAlong = 0f;
        maxAlong = 0f;

        var mean = Vector2.Zero;
        foreach (var candidate in component)
        {
            mean += candidate.OpeningCenter;
        }

        mean /= component.Count;

        var xx = 0f;
        var xy = 0f;
        var yy = 0f;
        foreach (var candidate in component)
        {
            var delta = candidate.OpeningCenter - mean;
            xx += delta.X * delta.X;
            xy += delta.X * delta.Y;
            yy += delta.Y * delta.Y;
        }

        var angle = 0.5f * MathF.Atan2(2f * xy, xx - yy);
        axis = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var perpendicular = new Vector2(-axis.Y, axis.X);

        minAlong = float.MaxValue;
        maxAlong = float.MinValue;
        var minAcross = float.MaxValue;
        var maxAcross = float.MinValue;

        foreach (var candidate in component)
        {
            var relative = candidate.OpeningCenter - mean;
            var along = Vector2.Dot(relative, axis);
            var across = Vector2.Dot(relative, perpendicular);
            minAlong = Math.Min(minAlong, along);
            maxAlong = Math.Max(maxAlong, along);
            minAcross = Math.Min(minAcross, across);
            maxAcross = Math.Max(maxAcross, across);
        }

        var length = maxAlong - minAlong;
        var width = Math.Max(maxAcross - minAcross, 0.001f);
        return length >= MinStripLength && length / width >= StripLinearityRatio;
    }

    private void AddSeparatedCandidates(List<ProjectedLightCandidate> candidates, float energyScale)
    {
        candidates.Sort(CompareProjectedEnergyDescending);

        foreach (var candidate in candidates)
        {
            var scaledCandidate = candidate with
            {
                ProjectedEnergy = candidate.ProjectedEnergy * energyScale,
            };

            if (OverlapsAcceptedCandidate(scaledCandidate))
                continue;

            _candidates.Add(scaledCandidate);
        }
    }

    private bool OverlapsAcceptedCandidate(ProjectedLightCandidate candidate)
    {
        foreach (var accepted in _candidates)
        {
            if (accepted.SourceLight != candidate.SourceLight ||
                accepted.DepthOffset != candidate.DepthOffset)
            {
                continue;
            }

            var minSeparation = Math.Max(0.75f, Math.Min(candidate.ProjectedRadius, accepted.ProjectedRadius) * 0.5f);
            if (Vector2.DistanceSquared(candidate.ProjectedCenter, accepted.ProjectedCenter) < minSeparation * minSeparation)
                return true;
        }

        return false;
    }

    private void UpdateProjectedLight(ProjectedLightCandidate candidate, uint currentFrame)
    {
        var projectedUid = GetOrCreateProjectedLight(candidate);

        if (_pointLightQuery.TryComp(projectedUid, out var light))
        {
            if (!MathHelper.CloseTo(light.Radius, candidate.ProjectedRadius))
                _lights.SetRadius(projectedUid, candidate.ProjectedRadius, light);

            if (!MathHelper.CloseTo(light.Energy, candidate.ProjectedEnergy))
                _lights.SetEnergy(projectedUid, candidate.ProjectedEnergy, light);

            if (light.Color != candidate.Color)
                _lights.SetColor(projectedUid, candidate.Color, light);

            if (!MathHelper.CloseTo(light.Softness, candidate.Softness))
                _lights.SetSoftness(projectedUid, candidate.Softness, light);

            if (light.CastShadows)
                _lights.SetCastShadows(projectedUid, false, light);

            if (!light.Enabled)
                _lights.SetEnabled(projectedUid, true, light);
        }
        else
        {
            _lights.SetRadius(projectedUid, candidate.ProjectedRadius);
            _lights.SetEnergy(projectedUid, candidate.ProjectedEnergy);
            _lights.SetColor(projectedUid, candidate.Color);
            _lights.SetSoftness(projectedUid, candidate.Softness);
            _lights.SetCastShadows(projectedUid, false);
            _lights.SetEnabled(projectedUid, true);
        }

        if (_projectedQuery.TryComp(projectedUid, out var projected))
        {
            if (projected.LastAppliedMapId != candidate.ReceivingMapId ||
                !projected.LastAppliedCenter.EqualsApprox(candidate.ProjectedCenter))
            {
                _transform.SetMapCoordinates(projectedUid, new MapCoordinates(candidate.ProjectedCenter, candidate.ReceivingMapId));
                projected.LastAppliedMapId = candidate.ReceivingMapId;
                projected.LastAppliedCenter = candidate.ProjectedCenter;
            }

            projected.OpeningCenter = candidate.OpeningCenter;
            projected.LastActiveFrame = currentFrame;
            projected.SourceMapId = candidate.SourceMapId;
            projected.DepthOffset = candidate.DepthOffset;
        }
        else
        {
            _transform.SetMapCoordinates(projectedUid, new MapCoordinates(candidate.ProjectedCenter, candidate.ReceivingMapId));
        }

        _activeThisFrame.Add(projectedUid);
    }

    private EntityUid GetOrCreateProjectedLight(ProjectedLightCandidate candidate)
    {
        EntityUid projectedUid;
        var key = new ProjectedLightKey(candidate.SourceLight, candidate.ReceivingMapId, candidate.OpeningCenter);
        var mergedKey = new MergedProjectedLightKey(candidate.ReceivingMapId, candidate.DepthOffset);
        var hasProjectedLight = candidate.IsMerged
            ? _mergedProjectedLights.TryGetValue(mergedKey, out projectedUid)
            : _projectedLights.TryGetValue(key, out projectedUid);

        if (!hasProjectedLight || !Exists(projectedUid))
        {
            projectedUid = Spawn(null, new MapCoordinates(candidate.ProjectedCenter, candidate.ReceivingMapId));
            var projectedComp = AddComp<CEZProjectedLightComponent>(projectedUid);
            projectedComp.SourceLight = candidate.SourceLight;
            projectedComp.SourceMapId = candidate.SourceMapId;
            projectedComp.DepthOffset = candidate.DepthOffset;
            projectedComp.OpeningCenter = candidate.OpeningCenter;
            projectedComp.LastAppliedMapId = candidate.ReceivingMapId;
            projectedComp.LastAppliedCenter = candidate.ProjectedCenter;

            AddComp<PointLightComponent>(projectedUid);

            if (candidate.IsMerged)
                _mergedProjectedLights[mergedKey] = projectedUid;
            else
                _projectedLights[key] = projectedUid;
        }

        return projectedUid;
    }

    private void CleanupStaleProjectedLights()
    {
        _toRemove.Clear();
        foreach (var (key, projectedUid) in _projectedLights)
        {
            if (_activeThisFrame.Contains(projectedUid))
                continue;

            _toRemove.Add(key);
            if (Exists(projectedUid))
                Del(projectedUid);
        }

        foreach (var key in _toRemove)
        {
            _projectedLights.Remove(key);
        }

        _mergedToRemove.Clear();
        foreach (var (key, projectedUid) in _mergedProjectedLights)
        {
            if (_activeThisFrame.Contains(projectedUid))
                continue;

            _mergedToRemove.Add(key);
            if (Exists(projectedUid))
                Del(projectedUid);
        }

        foreach (var key in _mergedToRemove)
        {
            _mergedProjectedLights.Remove(key);
        }
    }

    private void CleanupAllProjectedLights()
    {
        foreach (var (_, projectedUid) in _projectedLights)
        {
            if (Exists(projectedUid))
                Del(projectedUid);
        }

        foreach (var (_, projectedUid) in _mergedProjectedLights)
        {
            if (Exists(projectedUid))
                Del(projectedUid);
        }

        _projectedLights.Clear();
        _mergedProjectedLights.Clear();
        _activeThisFrame.Clear();
        ClearSourceLightBuckets();
        ClearOpeningCandidateBuckets();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CleanupAllProjectedLights();
    }

    private readonly record struct ProjectedLightKey(
        EntityUid SourceLight,
        MapId ReceivingMapId,
        Vector2 OpeningCenter);

    private readonly record struct MergedProjectedLightKey(
        MapId ReceivingMapId,
        int DepthOffset);

    private readonly record struct OpeningCandidateBucketKey(
        int X,
        int Y);

    private readonly record struct SourceLight(
        EntityUid Entity,
        Vector2 WorldPosition,
        float Radius,
        float Energy,
        Color Color,
        float Softness);

    private readonly record struct ProjectedLightCandidate(
        EntityUid SourceLight,
        MapId SourceMapId,
        MapId ReceivingMapId,
        int DepthOffset,
        Vector2 OpeningCenter,
        Vector2 ProjectedCenter,
        float ProjectedRadius,
        float ProjectedEnergy,
        Color Color,
        float Softness,
        bool IsMerged = false);

    private sealed class ProjectedLightAlongAxisComparer : IComparer<ProjectedLightCandidate>
    {
        public Vector2 Axis;

        public int Compare(ProjectedLightCandidate left, ProjectedLightCandidate right)
        {
            return Vector2.Dot(left.OpeningCenter, Axis).CompareTo(Vector2.Dot(right.OpeningCenter, Axis));
        }
    }
}
