using TMPro;
using UnityEngine;

/// <summary>
/// Binds SuperRareReward / RareReward / NormalReward "Amount" labels to <see cref="PlayerBattleRewardManager"/> inventory.
/// </summary>
[DisallowMultipleComponent]
public class BattleRewardInventoryAmountDisplay : MonoBehaviour
{
    const string AmountChildName = "Amount";

    [SerializeField] private TMP_Text superRareAmountText;
    [SerializeField] private TMP_Text rareAmountText;
    [SerializeField] private TMP_Text normalAmountText;

    [Header("Auto Find (scene object names)")]
    [SerializeField] private string superRareRewardObjectName = "SuperRareReward";
    [SerializeField] private string rareRewardObjectName = "RareReward";
    [SerializeField] private string normalRewardObjectName = "NormalReward";

    void Awake()
    {
        EnsureReferences();
    }

    void OnEnable()
    {
        EnsureReferences();
        Subscribe();
        Refresh();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    public void EnsureReferences()
    {
        if (superRareAmountText == null)
        {
            superRareAmountText = FindAmountText(superRareRewardObjectName);
        }

        if (rareAmountText == null)
        {
            rareAmountText = FindAmountText(rareRewardObjectName);
        }

        if (normalAmountText == null)
        {
            normalAmountText = FindAmountText(normalRewardObjectName);
        }
    }

    public void Refresh()
    {
        if (PlayerBattleRewardManager.Instance == null)
        {
            ApplyCounts(0, 0, 0);
            return;
        }

        PlayerBattleRewardManager manager = PlayerBattleRewardManager.Instance;
        ApplyCounts(manager.SuperRareCount, manager.RareCount, manager.NormalCount);
    }

    void ApplyCounts(int superRare, int rare, int normal)
    {
        SetAmountText(superRareAmountText, superRare);
        SetAmountText(rareAmountText, rare);
        SetAmountText(normalAmountText, normal);
    }

    static void SetAmountText(TMP_Text text, int count)
    {
        if (text != null)
        {
            text.text = Mathf.Max(0, count).ToString();
        }
    }

    static TMP_Text FindAmountText(string rewardRootObjectName)
    {
        if (string.IsNullOrEmpty(rewardRootObjectName))
        {
            return null;
        }

        GameObject rewardRoot = GameObject.Find(rewardRootObjectName);
        if (rewardRoot == null)
        {
            return null;
        }

        Transform amountTransform = rewardRoot.transform.Find(AmountChildName);
        return amountTransform != null ? amountTransform.GetComponent<TMP_Text>() : null;
    }

    void Subscribe()
    {
        if (PlayerBattleRewardManager.Instance != null)
        {
            PlayerBattleRewardManager.Instance.OnRewardsChanged -= Refresh;
            PlayerBattleRewardManager.Instance.OnRewardsChanged += Refresh;
        }
    }

    void Unsubscribe()
    {
        if (PlayerBattleRewardManager.Instance != null)
        {
            PlayerBattleRewardManager.Instance.OnRewardsChanged -= Refresh;
        }
    }
}
