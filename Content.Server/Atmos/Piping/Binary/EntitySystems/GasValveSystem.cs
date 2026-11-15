using System.Diagnostics.CodeAnalysis;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos.Piping.Binary.Components;
using Content.Shared.Atmos.Piping.Binary.Systems;
using Content.Shared.Audio;

namespace Content.Server.Atmos.Piping.Binary.EntitySystems;

public sealed partial class GasValveSystem : SharedGasValveSystem
{
    [Dependency] private SharedAmbientSoundSystem _ambientSoundSystem = default!;
    [Dependency] private NodeContainerSystem _nodeContainer = default!;

    public override void Set(EntityUid uid, GasValveComponent component, bool value)
    {
        base.Set(uid, component, value);

        if (!TryPipe(uid, component.InletName, out var inlet) ||
            !TryPipe(uid, component.OutletName, out var outlet))
        {
            return;
        }

        // Utopia-Tweak : AtmosUpdt
        switch (component.Kind)
        {
            case GasValveType.Straight:
                Link(inlet, outlet, component.Open);
                break;

            case GasValveType.ThreeWay:
            {
                if (!TryPipe(uid, component.SideName, out var side))
                    return;

                if (component.Open)
                {
                    Link(inlet, side, false);
                    Link(inlet, outlet, true);
                }
                else
                {
                    Link(inlet, outlet, false);
                    Link(inlet, side, true);
                }

                break;
            }

            case GasValveType.FourWay:
            {
                if (!TryPipe(uid, component.InletBName, out var inletB) ||
                    !TryPipe(uid, component.OutletBName, out var outletB))
                {
                    return;
                }

                if (component.Open)
                {
                    Link(inletB, outletB, false);
                    Link(inlet, outlet, true);
                }
                else
                {
                    Link(inlet, outlet, false);
                    Link(inletB, outletB, true);
                }

                break;
            }
        }

        _ambientSoundSystem.SetAmbience(uid, component.Open);
    }

    private bool TryPipe(EntityUid uid, string? name, [NotNullWhen(true)] out PipeNode? node)
    {
        node = null;
        return !string.IsNullOrEmpty(name) && _nodeContainer.TryGetNode(uid, name, out node);
    }

    private static void Link(PipeNode a, PipeNode b, bool connected)
    {
        if (connected)
        {
            a.AddAlwaysReachable(b);
            b.AddAlwaysReachable(a);
        }
        else
        {
            a.RemoveAlwaysReachable(b);
            b.RemoveAlwaysReachable(a);
        }
    }
    // Utopia-Tweak : AtmosUpdt
}
