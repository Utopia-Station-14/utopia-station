namespace Content.Server._Utopia.Supermatter.Systems;

public sealed partial class SupermatterSystem
{
    private const float MaxIntegrity = 100f;
    private const float MinIntegrity = -100f;
    private const float SpeechCooldown = 5f;
    private const float MinRadiation = 3f;

    #region Energy
    private const float MinInternalEnergy = 350f;
    private const float MaxInternalEnergy = 100.000f;
    private const float EnergyPerCheck = 10f;

    private const float LowEnergy = 800f;
    private const float DangerAmmountEnergy = 5000f;
    private const float ToMuchEnergy = 8000f;
    #endregion


    #region Lightning
    private const float DelayedLightningChance = 0.3f;
    private const float MinDelaySeconds = 2f;
    private const float MaxDelaySeconds = 4f;
    private const float MinCooldownSeconds = 10;
    private const float MaxCooldownSeconds = 15f;
    private const float MaxLightningRange = 10f;
    private const float MinLightningRange = 3f;
    private const float MaxLightningPower = 50.000f;
    private const float MinLightningPower = 500f;
    #endregion


    #region Gases
    private const float ToMuchGas = 1800f;
    private const float MaxOxygenWaste = 0.8f;
    private const float MinOxygenWaste = 0.2f;
    private const float MaxPhoronWaste = 0.2f;
    #endregion


    #region Damage
    private const float BaseHealingPerTick = 0.1f;
    private const float IntegrityForWarningStatus = 95.5f;
    private const float IntegrityForDestabilizationStatus = 80f;
    private const float IntegrityForCatastropheStatus = 45f;
    private const float IntegrityForDelamination = 0f;
    #endregion Damage


    #region Delamination
    private TimeSpan DelaminationTimer = TimeSpan.FromSeconds(120f);
    #endregion


    #region Anomaly
    private const float AnomalyTimeBetweenSpawn = 10f;
    private const float MaxAnomalyTimeLife = 15f;
    private const float MinAnomalyTimeLife = 4f;
    private const float IntegrityForAnomalyLow = 75f;
    private const float IntegrityForAnomalyHight = 25f;
    private const float MinRangeAnomalySpawn = 8f;
    #endregion
}
