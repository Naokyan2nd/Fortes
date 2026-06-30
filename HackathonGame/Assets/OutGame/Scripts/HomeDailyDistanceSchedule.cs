using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Daily schedule: Day 1 DistanceTravelled = First Play Given Distance; GPS walks accumulate in background only.
/// Each rollover: DistanceTravelled clears, then receives yesterday's background accumulation; background resets.
/// </summary>
public static class HomeDailyDistanceSchedule
{
    const string PrefsLastRolloverDate = "OutGame_DailyRollover_LastDate";
    const string PrefsPreviousDayDistance = "OutGame_PreviousDayDistanceMeters";
    const string PrefsPreviousDayRecordedReal = "OutGame_PreviousDayRecordedRealMeters";
    const string PrefsFirstPlayDistanceGranted = "OutGame_FirstPlayDistanceGranted";
    const string PrefsFirstPlayGrantDate = "OutGame_FirstPlayGrantDate";
    const string PrefsPendingPlayDistancePopup = "OutGame_PlayDistancePopup_PendingAfterRolloverDate";
    const string PrefsPlayDistancePopupShownDate = "OutGame_PlayDistancePopup_ShownDate";
    const string PrefsScheduleEnabled = "OutGame_DailySchedule_Enabled";
    const string PrefsScheduleRolloverHour = "OutGame_DailySchedule_RolloverHour";
    const string PrefsScheduleRolloverMinute = "OutGame_DailySchedule_RolloverMinute";
    const string PrefsScheduleUseDebugTime = "OutGame_DailySchedule_UseDebugTime";
    const string PrefsScheduleDebugTimeTicks = "OutGame_DailySchedule_DebugTimeTicks";
    const string PrefsFirstPlayGivenDistanceMeters = "OutGame_DailySchedule_FirstPlayGivenMeters";

    static bool s_enabled;
    static bool s_persistedConfigLoaded;
    static int s_rolloverHour = 10;
    static int s_rolloverMinute = 30;
    static bool s_useDebugTime;
    static DateTime s_debugTime = DateTime.Now;
    static float s_firstPlayGivenDistanceMeters = 20000f;

    public static event Action DailyRolloverApplied;

    public static bool IsEnabled => s_enabled;

    /// <summary>Scan / battle entry is always allowed; daily rollover only affects distance sync and popup.</summary>
    public static bool IsScanAllowed => true;

    public static bool IsGpsDistanceAccumulationAllowed => true;

    public static bool HasGrantedFirstPlayDistance =>
        PlayerPrefs.GetInt(PrefsFirstPlayDistanceGranted, 0) == 1;

    /// <summary>Background GPS saved at last rollover (yesterday's walked meters).</summary>
    public static float StoredPreviousDayDistanceMeters =>
        Mathf.Max(0f, PlayerPrefs.GetFloat(PrefsPreviousDayDistance, 0f));

    public static float RecordedRealPreviousDayDistanceMeters =>
        Mathf.Max(0f, PlayerPrefs.GetFloat(PrefsPreviousDayRecordedReal, 0f));

    public static bool HasAppliedRolloverToday(DateTime? optionalNow = null)
    {
        if (!s_enabled)
        {
            return false;
        }

        DateTime now = optionalNow ?? GetNow();
        string todayKey = GetDateKey(now);
        return PlayerPrefs.GetString(PrefsLastRolloverDate, string.Empty) == todayKey;
    }

    public static bool HasEverAppliedRollover()
    {
        return PlayerPrefs.HasKey(PrefsLastRolloverDate);
    }

    /// <summary>True after a daily rollover until Home dismisses the play-distance popup.</summary>
    public static bool IsPlayDistancePopupPendingForToday(DateTime? optionalNow = null)
    {
        if (!s_enabled)
        {
            return false;
        }

        DateTime now = optionalNow ?? GetNow();
        return PlayerPrefs.GetString(PrefsPendingPlayDistancePopup, string.Empty) == GetDateKey(now);
    }

    public static void ClearPlayDistancePopupPendingForToday()
    {
        if (!PlayerPrefs.HasKey(PrefsPendingPlayDistancePopup))
        {
            return;
        }

        PlayerPrefs.DeleteKey(PrefsPendingPlayDistancePopup);
        PlayerPrefs.Save();
    }

    public static bool TryGetCountdownToNextRollover(out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;

        if (!s_enabled)
        {
            return false;
        }

        DateTime now = GetNow();
        TimeSpan rolloverTime = new TimeSpan(s_rolloverHour, s_rolloverMinute, 0);
        DateTime nextRollover = now.TimeOfDay < rolloverTime
            ? now.Date + rolloverTime
            : now.Date.AddDays(1) + rolloverTime;

        remaining = nextRollover - now;
        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        return true;
    }

    public static void Configure(
        bool enabled,
        int rolloverHour,
        int rolloverMinute,
        bool useDebugTime,
        DateTime debugTime,
        float firstPlayGivenDistanceMeters)
    {
        s_enabled = enabled;
        s_rolloverHour = Mathf.Clamp(rolloverHour, 0, 23);
        s_rolloverMinute = Mathf.Clamp(rolloverMinute, 0, 59);
        s_useDebugTime = useDebugTime;
        s_debugTime = debugTime;
        s_firstPlayGivenDistanceMeters = Mathf.Max(0f, firstPlayGivenDistanceMeters);
        SavePersistedConfig();
        s_persistedConfigLoaded = true;
        Tick();
    }

    /// <summary>Runs daily rollover while away from Home (uses last persisted Home inspector config).</summary>
    public static void TickWhenEnabled()
    {
        if (!s_enabled && !TryLoadPersistedConfig())
        {
            return;
        }

        if (!s_enabled)
        {
            return;
        }

        Tick();
    }

    static void SavePersistedConfig()
    {
        PlayerPrefs.SetInt(PrefsScheduleEnabled, s_enabled ? 1 : 0);
        PlayerPrefs.SetInt(PrefsScheduleRolloverHour, s_rolloverHour);
        PlayerPrefs.SetInt(PrefsScheduleRolloverMinute, s_rolloverMinute);
        PlayerPrefs.SetInt(PrefsScheduleUseDebugTime, s_useDebugTime ? 1 : 0);
        PlayerPrefs.SetString(PrefsScheduleDebugTimeTicks, s_debugTime.Ticks.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.SetFloat(PrefsFirstPlayGivenDistanceMeters, s_firstPlayGivenDistanceMeters);
        PlayerPrefs.Save();
    }

    static bool TryLoadPersistedConfig()
    {
        if (s_persistedConfigLoaded)
        {
            return s_enabled;
        }

        if (!PlayerPrefs.HasKey(PrefsScheduleEnabled))
        {
            return false;
        }

        s_enabled = PlayerPrefs.GetInt(PrefsScheduleEnabled, 0) == 1;
        s_rolloverHour = Mathf.Clamp(PlayerPrefs.GetInt(PrefsScheduleRolloverHour, 10), 0, 23);
        s_rolloverMinute = Mathf.Clamp(PlayerPrefs.GetInt(PrefsScheduleRolloverMinute, 30), 0, 59);
        s_useDebugTime = PlayerPrefs.GetInt(PrefsScheduleUseDebugTime, 0) == 1;
        string debugTicksRaw = PlayerPrefs.GetString(PrefsScheduleDebugTimeTicks, string.Empty);
        if (long.TryParse(debugTicksRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long debugTicks))
        {
            s_debugTime = new DateTime(debugTicks);
        }

        s_firstPlayGivenDistanceMeters = Mathf.Max(
            0f,
            PlayerPrefs.GetFloat(PrefsFirstPlayGivenDistanceMeters, 20000f));
        s_persistedConfigLoaded = true;
        return s_enabled;
    }

    public static DateTime GetNow()
    {
        return s_useDebugTime ? s_debugTime : DateTime.Now;
    }

    public static void Tick()
    {
        if (s_enabled)
        {
            TryGrantFirstPlayDistanceIfNeeded();

            if (ShouldApplyRolloverToday())
            {
                ApplyDailyRollover();
            }
        }

        LocationManager.ApplyScheduleGpsAccumulationPolicy();
    }

    /// <summary>Day 1: DistanceTravelled = gift. Background GPS starts at 0.</summary>
    static void TryGrantFirstPlayDistanceIfNeeded()
    {
        if (HasGrantedFirstPlayDistance)
        {
            TryRepairLegacyFirstPlayGrantIfNeeded();
            return;
        }

        GrantFirstPlayDistance();
    }

    static void GrantFirstPlayDistance()
    {
        float grantMeters = Mathf.Max(0f, s_firstPlayGivenDistanceMeters);
        LocationManager.SetTotalDistanceMeters(grantMeters);
        LocationManager.ClearBackgroundAccumulatedMeters();
        LocationManager.ResetGpsAnchorsForNewTrackingPeriod();

        PlayerPrefs.SetInt(PrefsFirstPlayDistanceGranted, 1);
        PlayerPrefs.SetString(PrefsFirstPlayGrantDate, GetDateKey(GetNow()));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Older builds could mark first-play granted then immediately rollover to 0 on the same day.
    /// Re-grant once and anchor the first-play day to today so rollover waits until tomorrow.
    /// </summary>
    static void TryRepairLegacyFirstPlayGrantIfNeeded()
    {
        if (HasEverAppliedRollover() || PlayerPrefs.HasKey(PrefsFirstPlayGrantDate))
        {
            return;
        }

        if (LocationManager.GetTotalDistanceMeters() < 1f)
        {
            float grantMeters = Mathf.Max(0f, s_firstPlayGivenDistanceMeters);
            LocationManager.SetTotalDistanceMeters(grantMeters);
            LocationManager.ClearBackgroundAccumulatedMeters();
            LocationManager.ResetGpsAnchorsForNewTrackingPeriod();
        }

        PlayerPrefs.SetString(PrefsFirstPlayGrantDate, GetDateKey(GetNow()));
        PlayerPrefs.Save();
    }

    static bool ShouldApplyRolloverToday()
    {
        DateTime now = GetNow();
        if (HasAppliedRolloverToday(now))
        {
            return false;
        }

        if (IsOnFirstPlayGrantCalendarDay(now))
        {
            return false;
        }

        TimeSpan rolloverTime = new TimeSpan(s_rolloverHour, s_rolloverMinute, 0);
        return now.TimeOfDay >= rolloverTime;
    }

    /// <summary>True while the player is still on the calendar day they received the first-play gift.</summary>
    static bool IsOnFirstPlayGrantCalendarDay(DateTime now)
    {
        if (!HasGrantedFirstPlayDistance || HasEverAppliedRollover())
        {
            return false;
        }

        string grantDateKey = PlayerPrefs.GetString(PrefsFirstPlayGrantDate, string.Empty);
        if (string.IsNullOrEmpty(grantDateKey))
        {
            return true;
        }

        return grantDateKey == GetDateKey(now);
    }

    /// <summary>
    /// Day 2+ playtime: clear DistanceTravelled, apply yesterday's background walk total, reset background tracking.
    /// </summary>
    static void ApplyDailyRollover()
    {
        float yesterdayBackgroundMeters = LocationManager.GetBackgroundAccumulatedMeters();
        PlayerPrefs.SetFloat(PrefsPreviousDayRecordedReal, yesterdayBackgroundMeters);
        PlayerPrefs.SetFloat(PrefsPreviousDayDistance, yesterdayBackgroundMeters);

        LocationManager.SetTotalDistanceMeters(0f);
        LocationManager.SetTotalDistanceMeters(yesterdayBackgroundMeters);
        LocationManager.ClearBackgroundAccumulatedMeters();
        LocationManager.ForceAdoptStoredTotalDistance();
        LocationManager.ResetGpsAnchorsForNewTrackingPeriod();

        DateTime now = GetNow();
        PlayerPrefs.SetString(PrefsLastRolloverDate, GetDateKey(now));
        PlayerPrefs.SetString(PrefsPendingPlayDistancePopup, GetDateKey(now));
        if (PlayerPrefs.HasKey(PrefsPlayDistancePopupShownDate))
        {
            PlayerPrefs.DeleteKey(PrefsPlayDistancePopupShownDate);
        }

        PlayerPrefs.Save();

        DailyRolloverApplied?.Invoke();
    }

    static string GetDateKey(DateTime dateTime)
    {
        return dateTime.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
    }
}
