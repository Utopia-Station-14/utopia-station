using Content.Shared._Utopia.Supermatter.Components;

namespace Content.Server._Utopia.Supermatter.Systems;

/// <summary>
/// Часть кода, отвечающая за изменение энергии у Суперматерии.
/// Внутренняя - используется для баланса вселенной.
/// Внешняя - используется для большинства систем, потребляющих энергию.
/// </summary>
public sealed partial class SupermatterSystem
{
    /// <summary>
    /// Публичные функции для передачи внешней/внутренней энергии в СМ.
    /// Чем выше модификатор срезки (EnergyReductionModifier), тем меньше энергии усваивает СМ.
    /// </summary>
    public void ConsumeExternalEnergy(Entity<SupermatterComponent> sm, float baseAmount)
    {
        var reduction = MathF.Max(1f, sm.Comp.EnergyReductionModifier);
        var actualGain = baseAmount / reduction;
        ChangeExternalEnergy(sm.Comp, actualGain);
    }

    public void ConsumeInternalEnergy(Entity<SupermatterComponent> sm, float baseAmount)
    {
        var reduction = MathF.Max(1f, sm.Comp.EnergyReductionModifier);
        var actualGain = baseAmount / reduction;
        ChangeInternalEnergy(sm.Comp, actualGain);
    }

    /// <summary>
    /// Основная логика балансировки и рассеивания энергии за кадр.
    /// </summary>
    private void ProcessEnergy(Entity<SupermatterComponent> sm, float frameTime)
    {
        // Пересчет текущей суммарной энергии
        sm.Comp.TotalEnergy = sm.Comp.ExternalEnergy + sm.Comp.InternalEnergy;

        // Чем выше энергия — тем выше модификатор срезки (минимум 1.0).
        var dangerRatio = DangerAmmountEnergy > 0f ? sm.Comp.TotalEnergy / DangerAmmountEnergy : 0f;
        sm.Comp.EnergyReductionModifier = MathF.Max(1f, 1f + dangerRatio);

        // Умножаем на frameTime, чтобы изменение модификатора было плавной дельтой в секунду
        sm.Comp.EnergyScaleModifier += GetIntegrityModifier(sm) * frameTime;

        var total = sm.Comp.TotalEnergy;
        if (total <= 0f)
            return;

        var targetExternalShare = sm.Comp.TargetEnergyRatio / (1f + sm.Comp.TargetEnergyRatio);
        var targetExternal = total * targetExternalShare;
        var delta = targetExternal - sm.Comp.ExternalEnergy;

        // Распределение энергии между контурами
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

        // Рассеивание/спад энергии
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

    private void ProcessGasWasteEnergy(Entity<SupermatterComponent> sm, float temperatureDelta)
    {
        var baseEnergy = temperatureDelta + (sm.Comp.ExternalEnergy * sm.Comp.TemperatureScaleModifier);
        var reduction = MathF.Max(1f, sm.Comp.EnergyReductionModifier);
        var actualEnergy = baseEnergy / reduction;
        ChangeInternalEnergy(sm.Comp, -actualEnergy);
    }

    private void ProcessGasEnergy(Entity<SupermatterComponent> sm, float temperature, float frameTime)
    {
        var moles = sm.Comp.AtmosGas.TotalMoles;
        var baseEnergy = temperature + moles * sm.Comp.EnergyScaleModifier * frameTime;

        var reduction = MathF.Max(1f, sm.Comp.EnergyReductionModifier);
        var actualEnergy = baseEnergy / reduction;

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
