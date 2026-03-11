using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.CCVar;
using Robust.Shared.Physics.Components;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    private void UpdateMovement(EntityUid uid,
                                CEZPhysicsComponent zPhys,
                                TransformComponent xform,
                                PhysicsComponent physics,
                                float frameTime)
    {
        var oldVelocity = zPhys.Velocity;
        var oldHeight = zPhys.LocalPosition;

        if (physics.BodyStatus == BodyStatus.OnGround)
        {
            //Velocity application
            var velocityEv = new CEGetZVelocityEvent((uid, zPhys));
            RaiseLocalEvent(uid, velocityEv);

            zPhys.Velocity += velocityEv.VelocityDelta * frameTime;
        }

        //Movement application
        zPhys.LocalPosition += zPhys.Velocity * frameTime;
        zPhys.Velocity = Math.Clamp(zPhys.Velocity, -ZVelocityLimit, ZVelocityLimit);

        UpdateGrounded(uid, zPhys, out var landed);
        HandleLevelChange(uid, zPhys);

        if (landed) //Just landed
            HandleFalling(uid, zPhys);

        if (Math.Abs(oldVelocity - zPhys.Velocity) > 0.01f)
            DirtyField(uid, zPhys, nameof(CEZPhysicsComponent.Velocity));

        if (Math.Abs(oldHeight - zPhys.LocalPosition) > 0.01f)
            DirtyField(uid, zPhys, nameof(CEZPhysicsComponent.LocalPosition));
    }

    private void UpdateGrounded(EntityUid uid, CEZPhysicsComponent zPhys, out bool landed)
    {
        landed = false;

        var distanceToGround = zPhys.LocalPosition - zPhys.CurrentGroundHeight;
        var currentlyGrounded = (distanceToGround <= 0.05f || zPhys.CurrentStickyGround) && distanceToGround <= MaxStepHeight;

        if (currentlyGrounded)
        {
            zPhys.LocalPosition -= distanceToGround; //Sticky move
        }

        if (currentlyGrounded == zPhys.IsGrounded)
            return;

        landed = !zPhys.IsGrounded && currentlyGrounded;

        zPhys.IsGrounded = currentlyGrounded;

        if (currentlyGrounded != zPhys.IsGrounded)
            DirtyField(uid, zPhys, nameof(CEZPhysicsComponent.IsGrounded));
    }

    private void HandleFalling(EntityUid uid, CEZPhysicsComponent zPhys)
    {
        if (MathF.Abs(zPhys.Velocity) >= Cfg.GetCVar(EchoCCVars.ZImpactVelocityLimit))
        {
            _queuedLandings.Add(uid, -zPhys.Velocity);
        }

        zPhys.Velocity = -zPhys.Velocity * zPhys.Bounciness;
    }

    private void HandleLevelChange(EntityUid uid, CEZPhysicsComponent zPhys)
    {
        if (zPhys.LocalPosition < 0) //Need teleport to ZLevel down
        {
            if (!TryMoveDownOrChasm(uid))
                return;

            zPhys.LocalPosition += 1;

            if (zPhys.CurrentStickyGround)
                return;

            var fallEv = new CEZLevelFallMapEvent();
            RaiseLocalEvent(uid, fallEv);
        }

        else if (zPhys.LocalPosition >= 1) //Need teleport to ZLevel up
        {
            if (HasTileAbove(uid)) //Hit roof
            {
                if (MathF.Abs(zPhys.Velocity) >= Cfg.GetCVar(EchoCCVars.ZImpactVelocityLimit)) // ECHO-Tweak: перенос констант в конфиг
                {
                    _queuedLandings.Add(uid, zPhys.Velocity);
                }

                zPhys.LocalPosition = 1;
                zPhys.Velocity = -zPhys.Velocity * zPhys.Bounciness;
            }
            else //Move up
            {
                if (TryMoveUp(uid))
                    zPhys.LocalPosition -= 1;
            }
        }
    }
}
