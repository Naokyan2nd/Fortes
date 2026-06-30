using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage の Noises 子オブジェクト名ごとに撃破済みかを保持する（シーン遷移をまたぐ）。
/// </summary>
public static class StageDefeatedNoiseRegistry
{
    const string PrefsKey = "OutGame_StageDefeatedNoises";
    const char Separator = '|';

    public static void MarkDefeated(string noiseChildName)
    {
        if (string.IsNullOrEmpty(noiseChildName))
        {
            return;
        }

        HashSet<string> defeated = LoadDefeatedSet();
        if (!defeated.Add(noiseChildName))
        {
            return;
        }

        SaveDefeatedSet(defeated);
    }

    public static bool IsDefeated(string noiseChildName)
    {
        if (string.IsNullOrEmpty(noiseChildName))
        {
            return false;
        }

        return LoadDefeatedSet().Contains(noiseChildName);
    }

    public static int CountDefeated()
    {
        return LoadDefeatedSet().Count;
    }

    public static void ClearAll()
    {
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }

    static HashSet<string> LoadDefeatedSet()
    {
        var defeated = new HashSet<string>();
        string raw = PlayerPrefs.GetString(PrefsKey, string.Empty);
        if (string.IsNullOrEmpty(raw))
        {
            return defeated;
        }

        string[] parts = raw.Split(Separator);
        for (int i = 0; i < parts.Length; i++)
        {
            if (!string.IsNullOrEmpty(parts[i]))
            {
                defeated.Add(parts[i]);
            }
        }

        return defeated;
    }

    static void SaveDefeatedSet(HashSet<string> defeated)
    {
        if (defeated == null || defeated.Count == 0)
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
            return;
        }

        var names = new List<string>(defeated);
        names.Sort();
        PlayerPrefs.SetString(PrefsKey, string.Join(Separator, names));
        PlayerPrefs.Save();
    }
}
