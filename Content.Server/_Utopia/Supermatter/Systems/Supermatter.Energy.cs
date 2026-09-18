using Content.Shared._Utopia.Supermatter.Components;

namespace Content.Server._Utopia.Supermatter.Systems;

/// <summary>
/// Часть кода отвечающая за изменение энергии у Суперматерии.
/// Энергия Суперматерии разделена на две части.
/// Внутреняя - используется для баланса вселенной.
/// Внешняя - используется для большей части штук, которые требуют потребление энергии.
///
/// Любые сущности, которые сжигаются кристаллов передают свою энергию во "внешний контур".
/// Далее Суперматерия сама балансирует параметры.
/// </summary>
public sealed partial class SupermatterSystem
{
    /// <summary>
    /// Публичные функции для передачи внешней/внутренней энергии в СМ.
    /// </summary>
    public void ConsumeExternalEnergy(Entity<SupermatterComponent> sm, float baseAmount)
    {
        var actualLoss = baseAmount * sm.Comp.EnergyReductionModifier;
        ChangeExternalEnergy(sm.Comp, actualLoss);
    }

    public void ConsumeInternalEnergy(Entity<SupermatterComponent> sm, float baseAmount)
    {
        var actualLoss = baseAmount * sm.Comp.EnergyReductionModifier;
        ChangeInternalEnergy(sm.Comp, actualLoss);
    }


    /// <summary>
    /// Метод где происходит всякое.
    /// </summary>
    private void ProcessEnergy(Entity<SupermatterComponent> sm, float frameTime)
    {
        // Чем выше энергия - тем выше модификатор спада энергии.
        // Чем выше модификатор спада энергии - тем меньше энергии СМ будет получать.
        // Параллельно изменяется определенными реакциями/датой.
        sm.Comp.EnergyReductionModifier = 1f + (sm.Comp.TotalEnergy / DangerAmmountEnergy);
        sm.Comp.EnergyScaleModifier += GetIntegrityModifier(sm);

        var total = sm.Comp.ExternalEnergy + sm.Comp.InternalEnergy;
        var targetExternalShare = sm.Comp.TargetEnergyRatio / (1f + sm.Comp.TargetEnergyRatio);
        var targetExternal = total * targetExternalShare;
        var delta = targetExternal - sm.Comp.ExternalEnergy;

        // Логика отвечающая за распределение энергии по двум контурам.
        if (!MathHelper.CloseTo(delta, 0f, 0.01f))
        {
            var transferRate = Math.Clamp(sm.Comp.EnergyScaleModifier * frameTime, 0f, 1f);
            var transfer = delta * transferRate;

            transfer = transfer > 0f
                ? MathF.Min(transfer, sm.Comp.InternalEnergy)
                : MathF.Max(transfer, -sm.Comp.ExternalEnergy);

            ChangeExternalEnergy(sm.Comp, transfer);
            ChangeInternalEnergy(sm.Comp, -transfer);

            total = sm.Comp.ExternalEnergy + sm.Comp.InternalEnergy;
            if (total <= 0f)
                return;
        }

        var ratio = total / 500f;
        var powerReduction = ratio * ratio * ratio * sm.Comp.EnergyScaleModifier * frameTime;
        var maxLoss = total * 0.6f * frameTime;
        var loss = MathF.Min(powerReduction, maxLoss);

        if (loss <= 0f)
            return;

        var externalShare = sm.Comp.ExternalEnergy / total;
        var externalLoss = loss * externalShare;
        var internalLoss = loss - externalLoss;

        ChangeExternalEnergy(sm.Comp, -externalLoss);
        ChangeInternalEnergy(sm.Comp, -internalLoss);

        sm.Comp.TotalEnergy = sm.Comp.ExternalEnergy + sm.Comp.InternalEnergy;
    }

    /// <summary>
    /// Методы повышения/понижения энергии от различных действий Суперматерии.
    /// Сюда входят газы, радиация, молнии.
    /// </summary>
    private void ProcessGasWasteEnergy(Entity<SupermatterComponent> sm, float temperatureDelta)
    {
        var baseEnergy = temperatureDelta + (sm.Comp.ExternalEnergy * sm.Comp.TemperatureScaleModifier);

        var actualEnergy = baseEnergy / sm.Comp.EnergyReductionModifier;
        ChangeInternalEnergy(sm.Comp, -actualEnergy);
    }

    private void ProcessGasEnergy(Entity<SupermatterComponent> sm, float temperature, float frameTime)
    {
        var moles = sm.Comp.AtmosGas.TotalMoles;
        var baseEnergy = temperature + moles * sm.Comp.EnergyScaleModifier * frameTime;
        var actualEnergy = baseEnergy * sm.Comp.EnergyReductionModifier;

        var externalShare = sm.Comp.TargetEnergyRatio / (1f + sm.Comp.TargetEnergyRatio);
        ChangeExternalEnergy(sm.Comp, actualEnergy * externalShare);
        ChangeInternalEnergy(sm.Comp, actualEnergy * (1f - externalShare));
    }

    public void ProcessRadiationEnergy(Entity<SupermatterComponent> sm, float power)
    {
        var scale = MathF.Max(sm.Comp.EnergyScaleModifier, 0.01f);
        ConsumeInternalEnergy(sm, power * scale);
    }

    public void ProcessLightningEnergy(Entity<SupermatterComponent> sm, float power)
    {
        var scale = MathF.Max(sm.Comp.EnergyScaleModifier, 0.01f);
        ConsumeExternalEnergy(sm, power * scale);
    }


    private void ChangeInternalEnergy(SupermatterComponent sm, float energy)
        => sm.InternalEnergy = MathF.Max(0f, sm.InternalEnergy + energy);

    private void ChangeExternalEnergy(SupermatterComponent sm, float energy)
        => sm.ExternalEnergy = MathF.Max(0f, sm.ExternalEnergy + energy);
}
