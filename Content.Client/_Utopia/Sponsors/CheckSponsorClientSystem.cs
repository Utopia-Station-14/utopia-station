using System.Linq;
using Content.Shared._Utopia.Sponsors;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Client.Corvax.Sponsors
{
    public sealed partial class CheckSponsorClientSystem : EntitySystem
    {
        public bool IsSponsor = false;
        public override void Initialize()
        {
            base.Initialize();
        }

        [SubscribeLocalEvent]

        private void SetSponsorStatus(CheckedUserSponsor ev)
        {
            IsSponsor = ev.IsSponsor;
        }

        public void TryCheckSponsor(string? player)
        {
            if (player != null)
            {
                var ev = new CheckUserSponsor(player);
                RaiseNetworkEvent(ev);
            }
        }

        public bool GetSponsorStatus()
        {
            return IsSponsor;
        }

    }
}
