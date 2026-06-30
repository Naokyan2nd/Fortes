using TMPro;
using UnityEngine;

/// <summary>
/// ReadyStatus 内の AttackPoints / HealthPoints を現在の戦闘ステータスで更新する。
/// </summary>
[DisallowMultipleComponent]
public class StageReadyStatusStatsDisplay : MonoBehaviour
{
    [SerializeField] RectTransform readyStatusRoot;
    [SerializeField] string attackStatObjectName = "AttackPoints";
    [SerializeField] string healthStatObjectName = "HealthPoints";
    [SerializeField] TMP_Text attackPointsText;
    [SerializeField] TMP_Text healthPointsText;

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

    public void Configure(RectTransform statusRoot)
    {
        Configure(statusRoot, null, null);
    }

    public void Configure(RectTransform statusRoot, string attackObjectName, string healthObjectName)
    {
        if (statusRoot != null)
        {
            readyStatusRoot = statusRoot;
        }

        if (!string.IsNullOrEmpty(attackObjectName))
        {
            attackStatObjectName = attackObjectName;
        }

        if (!string.IsNullOrEmpty(healthObjectName))
        {
            healthStatObjectName = healthObjectName;
        }

        attackPointsText = null;
        healthPointsText = null;
        EnsureReferences();
    }

    public void Refresh()
    {
        EnsureReferences();

        PlayerCombatStats stats = PlayerCombatStatsResolver.ResolveCurrent();
        if (attackPointsText != null)
        {
            attackPointsText.text = stats.Attack.ToString();
        }

        if (healthPointsText != null)
        {
            healthPointsText.text = stats.MaxHp.ToString();
        }
    }

    void EnsureReferences()
    {
        if (readyStatusRoot == null)
        {
            GameObject readyStatusObject = GameObject.Find("ReadyStatus");
            if (readyStatusObject != null)
            {
                readyStatusRoot = readyStatusObject.GetComponent<RectTransform>();
            }
        }

        if (attackPointsText == null && readyStatusRoot != null)
        {
            attackPointsText = FindStatText(readyStatusRoot, attackStatObjectName);
        }

        if (healthPointsText == null && readyStatusRoot != null)
        {
            healthPointsText = FindStatText(readyStatusRoot, healthStatObjectName);
        }
    }

    static TMP_Text FindStatText(Transform searchRoot, string objectName)
    {
        if (searchRoot == null || string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        Transform[] transforms = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == objectName)
            {
                return candidate.GetComponent<TMP_Text>();
            }
        }

        return null;
    }

    void Subscribe()
    {
        PlayerCombatStatsResolver.OnResolvedStatsChanged -= OnStatsSourceChanged;
        PlayerCombatStatsResolver.OnResolvedStatsChanged += OnStatsSourceChanged;

        if (PlayerLevelManager.Instance != null)
        {
            PlayerLevelManager.Instance.OnProgressChanged -= OnStatsSourceChanged;
            PlayerLevelManager.Instance.OnProgressChanged += OnStatsSourceChanged;
        }

        if (OutfitLoadoutManager.Instance != null)
        {
            OutfitLoadoutManager.Instance.OnLoadoutChanged -= OnOutfitLoadoutChanged;
            OutfitLoadoutManager.Instance.OnLoadoutChanged += OnOutfitLoadoutChanged;
        }
    }

    void Unsubscribe()
    {
        PlayerCombatStatsResolver.OnResolvedStatsChanged -= OnStatsSourceChanged;

        if (PlayerLevelManager.Instance != null)
        {
            PlayerLevelManager.Instance.OnProgressChanged -= OnStatsSourceChanged;
        }

        if (OutfitLoadoutManager.Instance != null)
        {
            OutfitLoadoutManager.Instance.OnLoadoutChanged -= OnOutfitLoadoutChanged;
        }
    }

    void OnStatsSourceChanged()
    {
        Refresh();
    }

    void OnOutfitLoadoutChanged(ItemType type)
    {
        if (type == ItemType.Top
            || type == ItemType.Bottom
            || type == ItemType.CD
            || type == ItemType.Weapon)
        {
            Refresh();
        }
    }
}
