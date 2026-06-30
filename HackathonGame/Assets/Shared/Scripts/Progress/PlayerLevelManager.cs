using System;
using UnityEngine;

/// <summary>
/// プレイヤーレベル（1〜5）と現在レベル内の経験値を保持・永続化する。
/// </summary>
public sealed class PlayerLevelManager : MonoBehaviour
{
    public const int MaxLevel = PlayerLevelExpTable.MaxLevel;

    private const string PrefKeyLevel = "player_level";
    private const string PrefKeyLevelExp = "player_level_exp";

    public static PlayerLevelManager Instance { get; private set; }

    public event Action OnProgressChanged;

    [Tooltip("Optional. When set, overrides the inline Exp Per Level table below.")]
    [SerializeField] private PlayerLevelConfig config;

    [SerializeField] private PlayerLevelExpTable expPerLevel = new();
    [SerializeField] private PlayerLevelStatsTable statsPerLevel = new();

    [SerializeField] [Range(1, MaxLevel)] private int currentLevel = 1;
    [SerializeField] private int currentLevelExp;

    public PlayerLevelExpTable ExpPerLevel => expPerLevel;
    public PlayerLevelStatsTable StatsPerLevel => statsPerLevel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (Instance != null)
        {
            return;
        }

        var go = new GameObject("Runtime_PlayerLevelManager");
        Instance = go.AddComponent<PlayerLevelManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureConfig();
        LoadProgress();
    }

    void EnsureConfig()
    {
        if (config != null)
        {
            return;
        }

        config = Resources.Load<PlayerLevelConfig>("PlayerLevelConfig");
    }

    /// <summary>Home などから ScriptableObject 設定を注入する。</summary>
    public void ApplyConfig(PlayerLevelConfig levelConfig)
    {
        if (levelConfig == null)
        {
            return;
        }

        config = levelConfig;
        OnProgressChanged?.Invoke();
    }

    public int CurrentLevel => Mathf.Clamp(currentLevel, 1, MaxLevel);

    public int CurrentLevelExp => Mathf.Max(0, currentLevelExp);

    public bool IsMaxLevel => CurrentLevel >= MaxLevel;

    public int GetExpRequiredForCurrentLevel()
    {
        return GetExpRequiredForLevel(CurrentLevel);
    }

    public int GetExpRequiredForLevel(int level)
    {
        level = Mathf.Clamp(level, 1, MaxLevel);
        if (config != null)
        {
            return config.GetExpRequiredForLevel(level);
        }

        if (expPerLevel != null)
        {
            return expPerLevel.GetExpRequiredForLevel(level);
        }

        return GetDefaultExpRequiredForLevel(level);
    }

    public static int GetDefaultExpRequiredForLevel(int level)
    {
        var defaults = new PlayerLevelExpTable();
        return defaults.GetExpRequiredForLevel(level);
    }

    public int GetAttackForLevel(int level)
    {
        level = Mathf.Clamp(level, 1, MaxLevel);
        if (config != null)
        {
            return config.GetAttackForLevel(level);
        }

        if (statsPerLevel != null)
        {
            return statsPerLevel.GetAttackForLevel(level);
        }

        return GetDefaultAttackForLevel(level);
    }

    public int GetMaxHpForLevel(int level)
    {
        level = Mathf.Clamp(level, 1, MaxLevel);
        if (config != null)
        {
            return config.GetMaxHpForLevel(level);
        }

        if (statsPerLevel != null)
        {
            return statsPerLevel.GetMaxHpForLevel(level);
        }

        return GetDefaultMaxHpForLevel(level);
    }

    public static int GetDefaultAttackForLevel(int level)
    {
        var defaults = new PlayerLevelStatsTable();
        return defaults.GetAttackForLevel(level);
    }

    public static int GetDefaultMaxHpForLevel(int level)
    {
        var defaults = new PlayerLevelStatsTable();
        return defaults.GetMaxHpForLevel(level);
    }

    /// <summary>現在レベルの基礎攻撃力（装備ボーナス未加算）。</summary>
    public int BaseAttack => GetAttackForLevel(CurrentLevel);

    /// <summary>現在レベルの基礎最大 HP（装備ボーナス未加算）。</summary>
    public int BaseMaxHp => GetMaxHpForLevel(CurrentLevel);

    /// <summary>現在レベルの基礎ステータス。</summary>
    public PlayerCombatStats GetBaseCombatStats()
    {
        return new PlayerCombatStats(BaseAttack, BaseMaxHp);
    }

    /// <summary>基礎値に装備などのボーナスを加算したステータス。</summary>
    public PlayerCombatStats GetCombatStats(int bonusAttack = 0, int bonusMaxHp = 0)
    {
        return PlayerCombatStats.Combine(BaseAttack, BaseMaxHp, bonusAttack, bonusMaxHp);
    }

    public int GetDisplayRequiredExp()
    {
        int required = GetExpRequiredForCurrentLevel();
        if (required > 0)
        {
            return required;
        }

        return IsMaxLevel ? Mathf.Max(1, CurrentLevelExp) : 0;
    }

    public void AddExp(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        int remaining = amount;
        while (remaining > 0 && currentLevel < MaxLevel)
        {
            int required = GetExpRequiredForLevel(currentLevel);
            if (required <= 0)
            {
                break;
            }

            int space = required - currentLevelExp;
            if (space <= 0)
            {
                currentLevel++;
                currentLevelExp = 0;
                continue;
            }

            if (remaining >= space)
            {
                remaining -= space;
                currentLevel++;
                currentLevelExp = 0;
            }
            else
            {
                currentLevelExp += remaining;
                remaining = 0;
            }
        }

        if (remaining > 0 && currentLevel >= MaxLevel)
        {
            int cap = GetExpRequiredForLevel(MaxLevel);
            if (cap > 0)
            {
                currentLevelExp = Mathf.Min(currentLevelExp + remaining, cap);
            }
        }

        SaveProgress();
        OnProgressChanged?.Invoke();
    }

    public void SetProgress(int level, int levelExp)
    {
        currentLevel = Mathf.Clamp(level, 1, MaxLevel);
        currentLevelExp = Mathf.Max(0, levelExp);

        int required = GetExpRequiredForCurrentLevel();
        if (required > 0)
        {
            currentLevelExp = Mathf.Min(currentLevelExp, required);
        }

        SaveProgress();
        OnProgressChanged?.Invoke();
    }

    public void ResetProgress()
    {
        currentLevel = 1;
        currentLevelExp = 0;
        SaveProgress();
        OnProgressChanged?.Invoke();
    }

    private void LoadProgress()
    {
        currentLevel = PlayerPrefs.GetInt(PrefKeyLevel, 1);
        currentLevelExp = PlayerPrefs.GetInt(PrefKeyLevelExp, 0);
        currentLevel = Mathf.Clamp(currentLevel, 1, MaxLevel);
        currentLevelExp = Mathf.Max(0, currentLevelExp);

        int required = GetExpRequiredForCurrentLevel();
        if (required > 0)
        {
            currentLevelExp = Mathf.Min(currentLevelExp, required);
        }
    }

    private void SaveProgress()
    {
        PlayerPrefs.SetInt(PrefKeyLevel, CurrentLevel);
        PlayerPrefs.SetInt(PrefKeyLevelExp, CurrentLevelExp);
        PlayerPrefs.Save();
    }
}
