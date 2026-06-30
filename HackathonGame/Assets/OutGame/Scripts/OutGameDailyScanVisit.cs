using System.Globalization;
using System;
using UnityEngine;

/// <summary>
/// ScanScene can be entered at most once per schedule day; later Home → To Scan goes straight to StageScene.
/// </summary>
public static class OutGameDailyScanVisit
{
    const string PrefsScanVisitedDate = "OutGame_ScanScene_VisitedDate";

    public static bool HasVisitedScanToday(DateTime? optionalNow = null)
    {
        string visitedDate = PlayerPrefs.GetString(PrefsScanVisitedDate, string.Empty);
        return visitedDate == GetDateKey(optionalNow);
    }

    public static void MarkVisitedToday(DateTime? optionalNow = null)
    {
        PlayerPrefs.SetString(PrefsScanVisitedDate, GetDateKey(optionalNow));
        PlayerPrefs.Save();
    }

    static string GetDateKey(DateTime? optionalNow)
    {
        DateTime now = optionalNow ?? HomeDailyDistanceSchedule.GetNow();
        return now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
    }
}
