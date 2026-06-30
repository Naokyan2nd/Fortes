using UnityEngine;

/// <summary>
/// Home の NoisesAmount / DistanceTravelled / ゲージ fill を累計距離から一括算出する。
/// 1 ノイズ = metersPerCurrent（既定 5000m）。戦闘勝利時は距離を 1 段分減らす。
/// </summary>
public static class OutGameScanNoiseRevealCount
{
    const string LegacyPrefsKey = "OutGame_ScanMapNoiseRevealCount";
    const string PrefsMetersPerNoise = "OutGame_GaugeMetersPerNoise";

    public const float DefaultMetersPerCurrent = 5000f;
    public const float DefaultGaugeMax = 12f;

    static float s_homeMetersPerCurrent = DefaultMetersPerCurrent;
    static float s_homeGaugeMax = DefaultGaugeMax;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticStateOnPlaySessionStart()
    {
        s_homeMetersPerCurrent = DefaultMetersPerCurrent;
        s_homeGaugeMax = DefaultGaugeMax;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ClearLegacyNoiseCountPrefs()
    {
        if (!PlayerPrefs.HasKey(LegacyPrefsKey))
        {
            return;
        }

        PlayerPrefs.DeleteKey(LegacyPrefsKey);
        PlayerPrefs.DeleteKey("OutGame_GaugeManualDistanceActive");
        PlayerPrefs.DeleteKey("OutGame_GaugeManualDistanceMeters");
        PlayerPrefs.Save();
    }

    public static void SyncHomeGaugeBinding(float metersPerCurrent, float gaugeMax)
    {
        s_homeMetersPerCurrent = metersPerCurrent > 0f ? metersPerCurrent : DefaultMetersPerCurrent;
        s_homeGaugeMax = gaugeMax > 0f ? gaugeMax : DefaultGaugeMax;
        PlayerPrefs.SetFloat(PrefsMetersPerNoise, s_homeMetersPerCurrent);
        PlayerPrefs.Save();
    }

    static float ResolveMetersPerNoiseStep(float overrideMetersPerCurrent)
    {
        if (overrideMetersPerCurrent > 0f)
        {
            return overrideMetersPerCurrent;
        }

        if (s_homeMetersPerCurrent > 0f)
        {
            return s_homeMetersPerCurrent;
        }

        float pref = PlayerPrefs.GetFloat(PrefsMetersPerNoise, DefaultMetersPerCurrent);
        return pref > 0f ? pref : DefaultMetersPerCurrent;
    }

    public static float GetResolvedTotalDistanceMeters()
    {
        return LocationManager.GetTotalDistanceMeters();
    }

    public static int GetRevealCount(
        float metersPerCurrentOverride = 0f,
        float gaugeMaxOverride = 0f)
    {
        float meters = GetResolvedTotalDistanceMeters();
        float metersPerCurrent = ResolveMetersPerNoiseStep(metersPerCurrentOverride);
        float gaugeMax = gaugeMaxOverride > 0f
            ? gaugeMaxOverride
            : s_homeGaugeMax > 0f ? s_homeGaugeMax : DefaultGaugeMax;
        return ComputeNoiseCountFromDistance(meters, metersPerCurrent, gaugeMax);
    }

    public static int ComputeNoiseCountFromDistance(
        float totalDistanceMeters,
        float metersPerCurrent,
        float gaugeMax)
    {
        if (metersPerCurrent <= 0f)
        {
            return 0;
        }

        int maxUnits = Mathf.Max(0, Mathf.FloorToInt(gaugeMax));
        int currentUnits = Mathf.FloorToInt(Mathf.Max(0f, totalDistanceMeters) / metersPerCurrent);
        return Mathf.Clamp(currentUnits, 0, maxUnits);
    }

    public static float GetGaugeFillRatio(
        float totalDistanceMeters,
        float metersPerCurrent,
        float gaugeMax)
    {
        if (gaugeMax <= 0f || metersPerCurrent <= 0f)
        {
            return 0f;
        }

        float progressUnits = Mathf.Max(0f, totalDistanceMeters) / metersPerCurrent;
        return Mathf.Clamp01(progressUnits / gaugeMax);
    }

    /// <summary>MainScene 全ウェーブ勝利後: 距離を 1 ノイズ分減らす。</summary>
    public static void ConsumeOneAfterBattleVictory(float metersPerCurrentOverride = 0f)
    {
        float step = ResolveMetersPerNoiseStep(metersPerCurrentOverride);
        LocationManager.SubtractTotalDistanceMeters(step);
    }
}
