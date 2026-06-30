using System;
using UnityEngine;

/// <summary>
/// 三種のバトル報酬（SuperRare / Rare / Normal）の所持数を永続化し、勝利時に付与する。
/// </summary>
public sealed class PlayerBattleRewardManager : MonoBehaviour
{
    private const string PrefKeySuperRare = "battle_reward_super_rare";
    private const string PrefKeyRare = "battle_reward_rare";
    private const string PrefKeyNormal = "battle_reward_normal";

    public static PlayerBattleRewardManager Instance { get; private set; }

    public event Action OnRewardsChanged;

    [SerializeField] private StageVictoryRewardSettings victoryRewardSettings = StageVictoryRewardSettings.CreateDefaults();

    [SerializeField] private int superRareCount;
    [SerializeField] private int rareCount;
    [SerializeField] private int normalCount;

    public int SuperRareCount => Mathf.Max(0, superRareCount);
    public int RareCount => Mathf.Max(0, rareCount);
    public int NormalCount => Mathf.Max(0, normalCount);
    public StageVictoryRewardSettings VictoryRewardSettings => victoryRewardSettings;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (Instance != null)
        {
            return;
        }

        var go = new GameObject("Runtime_PlayerBattleRewardManager");
        Instance = go.AddComponent<PlayerBattleRewardManager>();
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
        LoadProgress();
    }

    public void ApplyVictoryRewardSettings(StageVictoryRewardSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        victoryRewardSettings.CopyFrom(settings);
    }

    public BattleRewardCounts GrantVictoryRewardsForStageId(string stageId)
    {
        BattleRewardCounts grant = victoryRewardSettings.GetVictoryRewardsForStageId(stageId);
        if (grant.IsEmpty)
        {
            return grant;
        }

        AddRewards(grant);
        return grant;
    }

    public void AddRewards(BattleRewardCounts grant)
    {
        if (grant.IsEmpty)
        {
            return;
        }

        superRareCount = Mathf.Max(0, superRareCount + grant.superRare);
        rareCount = Mathf.Max(0, rareCount + grant.rare);
        normalCount = Mathf.Max(0, normalCount + grant.normal);
        SaveProgress();
        OnRewardsChanged?.Invoke();
    }

    public bool CanAfford(BattleRewardCounts cost)
    {
        return SuperRareCount >= cost.superRare
            && RareCount >= cost.rare
            && NormalCount >= cost.normal;
    }

    public bool TrySpendRewards(BattleRewardCounts cost)
    {
        if (!CanAfford(cost))
        {
            return false;
        }

        superRareCount = Mathf.Max(0, superRareCount - cost.superRare);
        rareCount = Mathf.Max(0, rareCount - cost.rare);
        normalCount = Mathf.Max(0, normalCount - cost.normal);
        SaveProgress();
        OnRewardsChanged?.Invoke();
        return true;
    }

    public void SetCounts(int superRare, int rare, int normal)
    {
        superRareCount = Mathf.Max(0, superRare);
        rareCount = Mathf.Max(0, rare);
        normalCount = Mathf.Max(0, normal);
        SaveProgress();
        OnRewardsChanged?.Invoke();
    }

    public void ResetProgress()
    {
        SetCounts(0, 0, 0);
    }

    private void LoadProgress()
    {
        superRareCount = Mathf.Max(0, PlayerPrefs.GetInt(PrefKeySuperRare, 0));
        rareCount = Mathf.Max(0, PlayerPrefs.GetInt(PrefKeyRare, 0));
        normalCount = Mathf.Max(0, PlayerPrefs.GetInt(PrefKeyNormal, 0));
    }

    private void SaveProgress()
    {
        PlayerPrefs.SetInt(PrefKeySuperRare, SuperRareCount);
        PlayerPrefs.SetInt(PrefKeyRare, RareCount);
        PlayerPrefs.SetInt(PrefKeyNormal, NormalCount);
        PlayerPrefs.Save();
    }
}
