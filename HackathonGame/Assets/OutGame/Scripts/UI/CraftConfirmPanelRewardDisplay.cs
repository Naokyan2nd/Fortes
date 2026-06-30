using TMPro;
using UnityEngine;

/// <summary>
/// CraftConfirmPanel の *Reward/Amount を「所持数/必要数」表示に更新する。
/// 必要数は craft スロット内の SuperRareAmount / RareAmount / NormalAmount から読む。
/// </summary>
public static class CraftConfirmPanelRewardDisplay
{
    static readonly string[] ConfirmAmountChildNames =
    {
        "Amount",
        "CurrentAmount/RequiredAmount",
    };

    struct RewardPair
    {
        public string RewardRootName;
        public string SlotAmountName;

        public RewardPair(string rewardRootName, string slotAmountName)
        {
            RewardRootName = rewardRootName;
            SlotAmountName = slotAmountName;
        }
    }

    static readonly RewardPair[] RewardPairs =
    {
        new("SuperRareReward", "SuperRareAmount"),
        new("RareReward", "RareAmount"),
        new("NormalReward", "NormalAmount"),
    };

    public static BattleRewardCounts GetRequiredCosts(Transform craftSlotRoot)
    {
        if (craftSlotRoot == null)
        {
            return default;
        }

        return new BattleRewardCounts(
            ParseRequiredAmount(craftSlotRoot, "SuperRareAmount", "SuperRareReward"),
            ParseRequiredAmount(craftSlotRoot, "RareAmount", "RareReward"),
            ParseRequiredAmount(craftSlotRoot, "NormalAmount", "NormalReward"));
    }

    public static bool CanAffordCraft(Transform craftSlotRoot)
    {
        if (craftSlotRoot == null || PlayerBattleRewardManager.Instance == null)
        {
            return false;
        }

        BattleRewardCounts cost = GetRequiredCosts(craftSlotRoot);
        return PlayerBattleRewardManager.Instance.CanAfford(cost);
    }

    public static void Refresh(Transform confirmFrameRoot, Transform craftSlotRoot)
    {
        if (confirmFrameRoot == null || craftSlotRoot == null)
        {
            return;
        }

        int ownedSuperRare = 0;
        int ownedRare = 0;
        int ownedNormal = 0;
        if (PlayerBattleRewardManager.Instance != null)
        {
            ownedSuperRare = PlayerBattleRewardManager.Instance.SuperRareCount;
            ownedRare = PlayerBattleRewardManager.Instance.RareCount;
            ownedNormal = PlayerBattleRewardManager.Instance.NormalCount;
        }

        for (int i = 0; i < RewardPairs.Length; i++)
        {
            RewardPair pair = RewardPairs[i];
            int owned = pair.RewardRootName switch
            {
                "SuperRareReward" => ownedSuperRare,
                "RareReward" => ownedRare,
                "NormalReward" => ownedNormal,
                _ => 0,
            };

            int required = ParseRequiredAmount(craftSlotRoot, pair.SlotAmountName, pair.RewardRootName);
            SetRewardRatioText(confirmFrameRoot, pair.RewardRootName, owned, required);
        }
    }

    static void SetRewardRatioText(Transform frameRoot, string rewardRootName, int owned, int required)
    {
        Transform rewardRoot = FindChildByName(frameRoot, rewardRootName);
        if (rewardRoot == null)
        {
            return;
        }

        TMP_Text amountText = FindAmountTextUnderReward(rewardRoot);
        if (amountText == null)
        {
            return;
        }

        owned = Mathf.Max(0, owned);
        required = Mathf.Max(0, required);
        amountText.text = $"{owned}/{required}";
    }

    static TMP_Text FindAmountTextUnderReward(Transform rewardRoot)
    {
        for (int i = 0; i < ConfirmAmountChildNames.Length; i++)
        {
            string childName = ConfirmAmountChildNames[i];
            Transform amountTransform = rewardRoot.Find(childName);
            if (amountTransform == null)
            {
                amountTransform = FindChildByName(rewardRoot, childName);
            }

            if (amountTransform != null
                && amountTransform.TryGetComponent<TMP_Text>(out TMP_Text amountText))
            {
                return amountText;
            }
        }

        return null;
    }

    static int ParseRequiredAmount(Transform craftSlotRoot, string amountObjectName, string rewardRootName)
    {
        int best = 0;

        TMP_Text[] allTexts = craftSlotRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < allTexts.Length; i++)
        {
            TMP_Text amountText = allTexts[i];
            if (!string.Equals(amountText.gameObject.name, amountObjectName, System.StringComparison.Ordinal))
            {
                continue;
            }

            best = Mathf.Max(best, ParseRequiredFromDisplayText(amountText.text));
        }

        if (best > 0)
        {
            return best;
        }

        Transform rewardRoot = FindChildByName(craftSlotRoot, rewardRootName);
        if (rewardRoot == null)
        {
            return 0;
        }

        for (int i = 0; i < ConfirmAmountChildNames.Length; i++)
        {
            string childName = ConfirmAmountChildNames[i];
            Transform amountTransform = rewardRoot.Find(childName);
            if (amountTransform == null)
            {
                amountTransform = FindChildByName(rewardRoot, childName);
            }

            if (amountTransform != null
                && amountTransform.TryGetComponent<TMP_Text>(out TMP_Text amountText))
            {
                best = Mathf.Max(best, ParseRequiredFromDisplayText(amountText.text));
            }
        }

        return best;
    }

    /// <summary>
    /// スロットの Amount 表示（"10" / "+30" / "0/10"）から必要数だけ取り出す。
    /// </summary>
    static int ParseRequiredFromDisplayText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        int slashIndex = text.IndexOf('/');
        if (slashIndex >= 0)
        {
            return ParsePositiveIntFromDisplayText(text.Substring(slashIndex + 1));
        }

        return ParsePositiveIntFromDisplayText(text);
    }

    static int ParsePositiveIntFromDisplayText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        int value = 0;
        bool hasDigit = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (!char.IsDigit(c))
            {
                continue;
            }

            hasDigit = true;
            value = (value * 10) + (c - '0');
        }

        return hasDigit ? value : 0;
    }

    static Transform FindChildByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        if (string.Equals(root.name, objectName, System.StringComparison.Ordinal))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
