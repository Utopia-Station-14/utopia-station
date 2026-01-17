using Content.Shared._Utopia.ZLevels.Components;
using Content.Server.Chat.Managers;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using System.Numerics;

namespace Content.Server._Utopia.GridMotion.Systems;

public sealed class GridMotionDataSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<GridMotionMonitoringComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var motion, out var xform))
        {
            var gridUid = xform.GridUid ?? uid;
            var gridXform = Transform(gridUid);

            if (!TryComp<PhysicsComponent>(gridUid, out var physics))
                continue;

            // Синхронизация данных с грида
            motion.CurrentAngle = gridXform.LocalRotation;
            motion.Speed = physics.LinearVelocity.Length();
            motion.Direction = physics.LinearVelocity == Vector2.Zero
                ? Vector2.Zero
                : physics.LinearVelocity.Normalized();
            motion.AngularSpeed = physics.AngularVelocity;

            SendAdminAlert(uid, gridUid, motion);
        }
    }

    /// <summary>
    /// Устанавливает параметры движения грида через сущность
    /// </summary>
    public void SetGridMotion(
        EntityUid uid,
        Vector2 direction,
        float speed,
        float angularSpeed)
    {
        if (!TryComp(uid, out GridMotionMonitoringComponent? motion))
            return;

        var xform = Transform(uid);
        var gridUid = xform.GridUid ?? uid;

        motion.Direction = direction == Vector2.Zero
            ? Vector2.Zero
            : direction.Normalized();

        motion.Speed = speed;
        motion.AngularSpeed = angularSpeed;

        Dirty(uid, motion);

        SendAdminAlert(uid, gridUid, motion);
    }

    private void SendAdminAlert(
        EntityUid uid,
        EntityUid gridUid,
        GridMotionMonitoringComponent motion)
    {
        _chatManager.SendAdminAlert(
            $"{EntityManager.ToPrettyString(uid):uid} updated grid motion on " +
            $"{EntityManager.ToPrettyString(gridUid):target}\n" +
            $"• Direction: ({motion.Direction.X:0.00}, {motion.Direction.Y:0.00})\n" +
            $"• Speed: {motion.Speed:0.00}\n" +
            $"• AngularSpeed: {motion.AngularSpeed:0.00}\n" +
            $"• Angle: {motion.CurrentAngle.Degrees:0.00}°"
        );
    }
}
