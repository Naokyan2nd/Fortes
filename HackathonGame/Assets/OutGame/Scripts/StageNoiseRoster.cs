using UnityEngine;

/// <summary>
/// Stage の Noises リストは常に 1×SuperRare + 1×Rare + 10×Normal（最大 12）。
/// 距離による解放は上から順（SR→Rare→Normal…）で、撃破した個体だけ非表示にする。
/// </summary>
public static class StageNoiseRoster
{
    public const int MaxSlots = 12;
    public const int SuperRareRequiredReveal = 1;
    public const int RareRequiredReveal = 2;
    public const int FirstNormalRequiredReveal = 3;

    /// <summary>距離解放数 + 撃破数。勝利で距離が減っても、撃破分だけ表示枠を維持する。</summary>
    public static int GetEffectiveRevealCount(int distanceRevealCount)
    {
        return Mathf.Clamp(distanceRevealCount + StageDefeatedNoiseRegistry.CountDefeated(), 0, MaxSlots);
    }

    public static int GetRequiredRevealCount(string childName, int hierarchyIndex)
    {
        string statusKey = StageStatusFocusSync.ResolveStatusKeyFromNoiseName(childName);
        if (statusKey == "SuperRare")
        {
            return SuperRareRequiredReveal;
        }

        if (statusKey == "Rare")
        {
            return RareRequiredReveal;
        }

        if (statusKey == "Normal")
        {
            if (childName == "Normal")
            {
                return FirstNormalRequiredReveal;
            }

            if (TryParseNormalSuffixIndex(childName, out int suffixIndex))
            {
                return FirstNormalRequiredReveal + suffixIndex;
            }
        }

        return Mathf.Clamp(hierarchyIndex + 1, 1, MaxSlots);
    }

    public static bool ShouldShowNoise(string childName, int hierarchyIndex, int effectiveRevealCount, bool defeated)
    {
        if (defeated)
        {
            return false;
        }

        return effectiveRevealCount >= GetRequiredRevealCount(childName, hierarchyIndex);
    }

    static bool TryParseNormalSuffixIndex(string childName, out int suffixIndex)
    {
        suffixIndex = 0;
        const string prefix = "Normal_";
        if (!childName.StartsWith(prefix, System.StringComparison.Ordinal)
            || childName.Length <= prefix.Length)
        {
            return false;
        }

        string digits = childName.Substring(prefix.Length);
        if (!int.TryParse(digits, out int parsed) || parsed < 1)
        {
            return false;
        }

        suffixIndex = parsed;
        return true;
    }
}
