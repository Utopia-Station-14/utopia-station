using Robust.Shared.Serialization;

namespace Content.Shared._Utopia.Combat;

[Serializable, NetSerializable]
public enum CombatAction
{
    Disarm,
    Grab,
    Hit
}

[Serializable, NetSerializable]
public enum WeaponCombatAction
{
    ProtectiveHit,      // 0 (00)
    OffensiveHit,       // 1 (01)
    ProtectiveWideHit,  // 2 (10)
    OffensiveWideHit    // 3 (11)
}

[Serializable, NetSerializable]
public enum ComboWeaponStand : sbyte
{
    Protective,
    Offensive
}

[Serializable, NetSerializable]
public enum ComboWeaponState : sbyte
{
    State,
}

[ByRefEvent]
public record struct GetPerformedAttackTypesEvent(List<CombatAction>? AttackTypes = null, List<WeaponCombatAction>? WAttackTypes = null);
