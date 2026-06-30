using UnityEngine;

/// <summary>
/// レベルごとの最大経験値（プロジェクト共通設定）。Resources または Home から PlayerLevelManager へ渡す。
/// </summary>
[CreateAssetMenu(fileName = "PlayerLevelConfig", menuName = "Progress/Player Level Config")]
public sealed class PlayerLevelConfig : ScriptableObject
{
    public const int MaxLevel = PlayerLevelExpTable.MaxLevel;

    [SerializeField] private PlayerLevelExpTable expPerLevel = new();
    [SerializeField] private PlayerLevelStatsTable statsPerLevel = new();

    public PlayerLevelExpTable ExpPerLevel => expPerLevel;
    public PlayerLevelStatsTable StatsPerLevel => statsPerLevel;

    public int GetExpRequiredForLevel(int level)
    {
        return expPerLevel != null ? expPerLevel.GetExpRequiredForLevel(level) : 0;
    }

    public int GetAttackForLevel(int level)
    {
        return statsPerLevel != null ? statsPerLevel.GetAttackForLevel(level) : 0;
    }

    public int GetMaxHpForLevel(int level)
    {
        return statsPerLevel != null ? statsPerLevel.GetMaxHpForLevel(level) : 1;
    }
}
