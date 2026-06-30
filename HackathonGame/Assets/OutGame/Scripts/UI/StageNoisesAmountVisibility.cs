using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Noises の表示数（距離）と撃破済みを反映する。
/// 撃破した子は非表示にし、下の子をひとつ上のスロット位置へ詰める。
/// </summary>
[DisallowMultipleComponent]
public class StageNoisesAmountVisibility : MonoBehaviour
{
    [SerializeField] RectTransform noisesRoot;
    [SerializeField] bool overrideRevealCount;
    [SerializeField] int revealCountOverride;
    [SerializeField] StageNoiseSlotFocus noisesSlotFocus;

    readonly List<Vector2> _designAnchoredPositions = new();
    bool _designLayoutCached;

    public void Configure(
        RectTransform root,
        bool useOverride = false,
        int overrideCount = 0,
        StageNoiseSlotFocus slotFocus = null)
    {
        noisesRoot = root;
        overrideRevealCount = useOverride;
        revealCountOverride = Mathf.Max(0, overrideCount);
        if (slotFocus != null)
        {
            noisesSlotFocus = slotFocus;
        }

        InvalidateDesignLayoutCache();
        Apply();
    }

    void OnEnable()
    {
        if (!Application.isPlaying)
        {
            InvalidateDesignLayoutCache();
        }

        Apply();
    }

    public void Apply()
    {
        if (noisesRoot == null)
        {
            return;
        }

        EnsureDesignLayoutCache();
        int distanceRevealCount = ResolveRevealCount();
        int effectiveRevealCount = StageNoiseRoster.GetEffectiveRevealCount(distanceRevealCount);
        int slotWriteIndex = 0;

        for (int hierarchyIndex = 0; hierarchyIndex < noisesRoot.childCount; hierarchyIndex++)
        {
            if (noisesRoot.GetChild(hierarchyIndex) is not RectTransform childRect)
            {
                continue;
            }

            bool defeated = StageDefeatedNoiseRegistry.IsDefeated(childRect.name);
            bool show = StageNoiseRoster.ShouldShowNoise(
                childRect.name,
                hierarchyIndex,
                effectiveRevealCount,
                defeated);

            childRect.gameObject.SetActive(show);
            if (!show)
            {
                continue;
            }

            if (slotWriteIndex < _designAnchoredPositions.Count)
            {
                Vector2 layoutPosition = _designAnchoredPositions[slotWriteIndex];
                childRect.anchoredPosition = layoutPosition;
            }

            slotWriteIndex++;
        }

        RefreshSlotFocusLayout();
    }

    void EnsureDesignLayoutCache()
    {
        if (_designLayoutCached || noisesRoot == null)
        {
            return;
        }

        _designAnchoredPositions.Clear();
        for (int i = 0; i < noisesRoot.childCount; i++)
        {
            if (noisesRoot.GetChild(i) is RectTransform childRect)
            {
                _designAnchoredPositions.Add(childRect.anchoredPosition);
            }
            else
            {
                _designAnchoredPositions.Add(Vector2.zero);
            }
        }

        _designLayoutCached = true;
    }

    void InvalidateDesignLayoutCache()
    {
        _designLayoutCached = false;
        _designAnchoredPositions.Clear();
    }

    void RefreshSlotFocusLayout()
    {
        if (noisesSlotFocus == null)
        {
            noisesSlotFocus = FindAnyObjectByType<StageNoiseSlotFocus>();
        }

        noisesSlotFocus?.RefreshRestLayoutFromScene();
    }

    int ResolveRevealCount()
    {
        if (overrideRevealCount)
        {
            return Mathf.Max(0, revealCountOverride);
        }

        return OutGameScanNoiseRevealCount.GetRevealCount();
    }
}
