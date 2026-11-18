using Content.Server.DeviceNetwork.Components;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._Utopia.ZLevels;
using Content.Shared.DeviceNetwork.Events;
using JetBrains.Annotations;

namespace Content.Server.DeviceNetwork.Systems
{
    [UsedImplicitly]
    public sealed partial class WirelessNetworkSystem : EntitySystem
    {
        [Dependency] private SharedTransformSystem _transformSystem = default!;
        [Dependency] private ZLevelsExtensions _zLevel = default!; // Utopia-Tweak : ZLevels

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<WirelessNetworkComponent, BeforePacketSentEvent>(OnBeforePacketSent);
        }

        /// <summary>
        /// Gets the position of both the sending and receiving entity and checks if the receiver is in range of the sender.
        /// </summary>
        private void OnBeforePacketSent(EntityUid uid, WirelessNetworkComponent component, BeforePacketSentEvent args)
        {
            var ownPosition = args.SenderPosition;
            var xform = Transform(uid);

            // not a wireless to wireless connection, just let it happen
            if (!TryComp<WirelessNetworkComponent>(args.Sender, out var sendingComponent))
                return;

            // Utopia-Tweak : ZLevels
            var sameMap = xform.MapID == args.SenderTransform.MapID;
            if (!sameMap && !_zLevel.IsSameZNetwork(xform.MapUid, args.SenderTransform.MapUid))
            {
                args.Cancel();
                return;
            }

            if ((ownPosition - _transformSystem.GetWorldPosition(xform)).Length() > sendingComponent.Range)
            {
                args.Cancel();
            }
            // Utopia-Tweak : ZLevels
        }
    }
}
