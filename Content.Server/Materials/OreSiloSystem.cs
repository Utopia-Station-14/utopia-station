using Content.Server._CE.ZLevels.Core;
using Content.Server.Pinpointer;
using Content.Shared._Utopia.ZLevels;
using Content.Shared.IdentityManagement;
using Content.Shared.Materials.OreSilo;
using Robust.Server.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.Materials;

/// <inheritdoc/>
public sealed partial class OreSiloSystem : SharedOreSiloSystem
{
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private SharedUserInterfaceSystem _userInterface = default!;
    // Utopia-Tweak : ZLevels
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private CEZLevelsSystem _zLevel = default!;
    [Dependency] private ZLevelsExtensions _zExtention = default!; // Utopia-Tweak : ZLevels
    // Utopia-Tweak : ZLevels

    private const float OreSiloPreloadRangeSquared = 225f; // ~1 screen

    private readonly HashSet<Entity<OreSiloClientComponent>> _clientLookup = new();
    private readonly HashSet<(NetEntity, string, string)> _clientInformation = new();
    private readonly HashSet<EntityUid> _silosToAdd = new();
    private readonly HashSet<EntityUid> _silosToRemove = new();

    protected override void UpdateOreSiloUi(Entity<OreSiloComponent> ent)
    {
        if (!_userInterface.IsUiOpen(ent.Owner, OreSiloUiKey.Key))
            return;
        _clientLookup.Clear();
        _clientInformation.Clear();

        var xform = Transform(ent);

        // Utopia-Tweak : ZLevels
        var siloCoord = _xform.GetMapCoordinates(xform);
        var maps = new HashSet<MapId> { xform.MapID };
        if (xform.MapUid is { } siloMapUid && _zLevel.TryGetMapNetwork(siloMapUid, out var siloNet))
        {
            foreach (var zMapUid in siloNet.Comp.SortedZLevels)
            {
                if (!zMapUid.IsValid())
                    continue;

                var zMapId = _xform.GetMapId(zMapUid);
                if (zMapId != MapId.Nullspace)
                {
                    maps.Add(zMapId);
                }
            }
        }

        foreach (var mapId in maps)
        {
            var searchCoord = new MapCoordinates(siloCoord.Position, mapId);
            _entityLookup.GetEntitiesInRange(searchCoord, ent.Comp.Range, _clientLookup);
        }
        // Utopia-Tweak : ZLevels

        foreach (var client in _clientLookup)
        {
            // don't show already-linked clients.
            if (client.Comp.Silo is not null)
                continue;

            // Don't show clients on the screen if we can't link them.
            if (!CanTransmitMaterials((ent, ent, xform), client))
                continue;

            var netEnt = GetNetEntity(client);
            var name = Identity.Name(client, EntityManager);
            var beacon = _navMap.GetNearestBeaconString(client.Owner, onlyName: true);

            var txt = Loc.GetString("ore-silo-ui-itemlist-entry",
                ("name", name),
                ("beacon", beacon),
                ("linked", ent.Comp.Clients.Contains(client)),
                ("inRange", true));

            _clientInformation.Add((netEnt, txt, beacon));
        }

        // Get all clients of this silo, including those out of range.
        foreach (var client in ent.Comp.Clients)
        {
            var netEnt = GetNetEntity(client);
            var name = Identity.Name(client, EntityManager);
            var beacon = _navMap.GetNearestBeaconString(client, onlyName: true);
            var inRange = CanTransmitMaterials((ent, ent, xform), client);

            var txt = Loc.GetString("ore-silo-ui-itemlist-entry",
                ("name", name),
                ("beacon", beacon),
                ("linked", ent.Comp.Clients.Contains(client)),
                ("inRange", inRange));

            _clientInformation.Add((netEnt, txt, beacon));
        }

        _userInterface.SetUiState(ent.Owner, OreSiloUiKey.Key, new OreSiloBuiState(_clientInformation));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Solving an annoying problem: we need to send the silo to people who are near the silo so that
        // Things don't start wildly mispredicting. We do this as cheaply as possible via grid-based local-pos checks.
        // Sloth okay-ed this in the interim until a better solution comes around.

        var actorQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actorQuery.MoveNext(out var ent1, out var actorComp, out var actorXform)) // Utopia-Tweak : ZLevels
        {
            _silosToAdd.Clear();
            _silosToRemove.Clear();

            var actorCoord = _xform.GetMapCoordinates(actorXform); // Utopia-Tweak : ZLevels
            var clientQuery = EntityQueryEnumerator<OreSiloClientComponent, TransformComponent>();
            while (clientQuery.MoveNext(out var ent2, out var clientComp, out var clientXform)) // Utopia-Tweak : ZLevels
            {
                if (clientComp.Silo == null)
                    continue;

                if (!_zExtention.IsSameZLevel((ent1, actorXform), (ent2, clientXform))) // Utopia-Tweak : ZLevels
                    continue;

                // Utopia-Tweak : ZLevels
                var clientCoord = _xform.GetMapCoordinates(clientXform);
                var within = (actorCoord.Position - clientCoord.Position).LengthSquared() <= OreSiloPreloadRangeSquared;
                // Utopia-Tweak : ZLevels

                if (within) // Utopia-Tweak : ZLevels
                {
                    _silosToAdd.Add(clientComp.Silo.Value);
                }
                else
                {
                    _silosToRemove.Add(clientComp.Silo.Value);
                }
            }

            foreach (var toRemove in _silosToRemove)
            {
                _pvsOverride.RemoveSessionOverride(toRemove, actorComp.PlayerSession);
            }
            foreach (var toAdd in _silosToAdd)
            {
                _pvsOverride.AddSessionOverride(toAdd, actorComp.PlayerSession);
            }
        }
    }
}
