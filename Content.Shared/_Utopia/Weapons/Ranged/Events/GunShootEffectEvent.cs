using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Ranged.Events;

[Serializable, NetSerializable]
public sealed class GunShootEffectEvent(NetEntity gun, NetEntity user, Angle angle) : EntityEventArgs
{
    public NetEntity Gun = gun;
    public NetEntity User = user;
    public Angle Angle = angle;
}
