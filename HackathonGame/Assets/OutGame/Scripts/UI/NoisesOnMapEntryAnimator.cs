using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays a staged entry for each direct child under NoisesOnMap (left to right):
/// parent fades in first, then each child moves from the parent anchor while fading in.
/// </summary>
[DisallowMultipleComponent]
public class NoisesOnMapEntryAnimator : MonoBehaviour
{
    float _groupStartInterval = 0.15f;
    float _parentFadeDuration = 0.35f;
    float _childMoveDuration = 0.55f;
    float _easeOutPower = 3f;

    readonly List<NoiseMapGroup> _groups = new();
    int _revealGroupCount = -1;

    public void ApplySettings(
        float groupStartInterval,
        float parentFadeDuration,
        float childMoveDuration,
        float easeOutPower)
    {
        _groupStartInterval = Mathf.Max(0f, groupStartInterval);
        _parentFadeDuration = Mathf.Max(0f, parentFadeDuration);
        _childMoveDuration = Mathf.Max(0f, childMoveDuration);
        _easeOutPower = Mathf.Clamp(easeOutPower, 1f, 6f);
    }

    public void ConfigureRevealCount(int revealCount)
    {
        _revealGroupCount = Mathf.Max(0, revealCount);
    }

    public IEnumerator PlayEntry()
    {
        StopAllCoroutines();
        yield return PlayEntrySequence();
    }

    IEnumerator PlayEntrySequence()
    {
        if (!BuildGroups())
        {
            yield break;
        }

        ApplyRandomGroupSelection();

        var selectedGroups = new List<NoiseMapGroup>();
        for (int i = 0; i < _groups.Count; i++)
        {
            if (_groups[i].IsSelectedForReveal)
            {
                selectedGroups.Add(_groups[i]);
            }
        }

        if (selectedGroups.Count == 0)
        {
            yield break;
        }

        PrepareAllGroupsHidden();

        int groupsRemaining = selectedGroups.Count;
        for (int i = 0; i < selectedGroups.Count; i++)
        {
            NoiseMapGroup group = selectedGroups[i];
            StartCoroutine(PlaySingleGroupEntry(group, () => groupsRemaining--));

            if (i < selectedGroups.Count - 1 && _groupStartInterval > 0f)
            {
                yield return new WaitForSeconds(_groupStartInterval);
            }
        }

        while (groupsRemaining > 0)
        {
            yield return null;
        }
    }

    void ApplyRandomGroupSelection()
    {
        for (int i = 0; i < _groups.Count; i++)
        {
            _groups[i].Parent.gameObject.SetActive(true);
        }

        int revealCount = _revealGroupCount >= 0
            ? _revealGroupCount
            : OutGameScanNoiseRevealCount.GetRevealCount();

        revealCount = Mathf.Clamp(revealCount, 0, _groups.Count);

        var groupIndices = new List<int>(_groups.Count);
        for (int i = 0; i < _groups.Count; i++)
        {
            groupIndices.Add(i);
        }

        Shuffle(groupIndices);

        var selectedIndices = new HashSet<int>();
        for (int i = 0; i < revealCount; i++)
        {
            selectedIndices.Add(groupIndices[i]);
        }

        for (int i = 0; i < _groups.Count; i++)
        {
            NoiseMapGroup group = _groups[i];
            group.IsSelectedForReveal = selectedIndices.Contains(i);
            group.Parent.gameObject.SetActive(group.IsSelectedForReveal);
        }
    }

    static void Shuffle(IList<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
        }
    }

    IEnumerator PlaySingleGroupEntry(NoiseMapGroup group, System.Action onComplete)
    {
        yield return FadeInGraphics(group.ParentGraphics, _parentFadeDuration);

        for (int i = 0; i < group.Children.Count; i++)
        {
            yield return AnimateChildFromParent(group.Children[i]);
        }

        onComplete?.Invoke();
    }

    bool BuildGroups()
    {
        _groups.Clear();

        var parents = new List<RectTransform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            if (transform.GetChild(i) is RectTransform rect)
            {
                parents.Add(rect);
            }
        }

        if (parents.Count == 0)
        {
            return false;
        }

        parents = parents.OrderBy(p => p.anchoredPosition.x).ToList();

        foreach (RectTransform parent in parents)
        {
            var group = new NoiseMapGroup
            {
                Parent = parent,
                ParentGraphics = CollectGraphics(parent, includeDescendants: false),
                Children = new List<ChildEntry>(),
            };

            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i) is not RectTransform childRect)
                {
                    continue;
                }

                group.Children.Add(new ChildEntry
                {
                    Rect = childRect,
                    RestAnchoredPosition = childRect.anchoredPosition,
                    Graphics = CollectGraphics(childRect, includeDescendants: true),
                });
            }

            _groups.Add(group);
        }

        return _groups.Count > 0;
    }

    void PrepareAllGroupsHidden()
    {
        foreach (NoiseMapGroup group in _groups)
        {
            if (!group.IsSelectedForReveal)
            {
                continue;
            }

            SetGraphicsAlpha(group.ParentGraphics, 0f);

            foreach (ChildEntry child in group.Children)
            {
                SetGraphicsAlpha(child.Graphics, 0f);
                child.Rect.anchoredPosition = Vector2.zero;
            }
        }
    }

    IEnumerator FadeInGraphics(IReadOnlyList<GraphicAlphaState> graphics, float duration)
    {
        if (graphics.Count == 0)
        {
            yield break;
        }

        duration = Mathf.Max(0f, duration);
        if (duration <= 0f)
        {
            SetGraphicsAlpha(graphics, 1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EvaluateEaseOut(Mathf.Clamp01(elapsed / duration));
            SetGraphicsAlpha(graphics, t);
            yield return null;
        }

        SetGraphicsAlpha(graphics, 1f);
    }

    IEnumerator AnimateChildFromParent(ChildEntry child)
    {
        float duration = Mathf.Max(0f, _childMoveDuration);
        if (duration <= 0f)
        {
            child.Rect.anchoredPosition = child.RestAnchoredPosition;
            SetGraphicsAlpha(child.Graphics, 1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EvaluateEaseOut(Mathf.Clamp01(elapsed / duration));
            child.Rect.anchoredPosition = Vector2.LerpUnclamped(Vector2.zero, child.RestAnchoredPosition, t);
            SetGraphicsAlpha(child.Graphics, t);
            yield return null;
        }

        child.Rect.anchoredPosition = child.RestAnchoredPosition;
        SetGraphicsAlpha(child.Graphics, 1f);
    }

    float EvaluateEaseOut(float normalizedTime)
    {
        normalizedTime = Mathf.Clamp01(normalizedTime);
        return 1f - Mathf.Pow(1f - normalizedTime, _easeOutPower);
    }

    static List<GraphicAlphaState> CollectGraphics(Transform root, bool includeDescendants)
    {
        var states = new List<GraphicAlphaState>();
        if (includeDescendants)
        {
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                states.Add(new GraphicAlphaState(graphic));
            }
        }
        else
        {
            foreach (Graphic graphic in root.GetComponents<Graphic>())
            {
                states.Add(new GraphicAlphaState(graphic));
            }
        }

        return states;
    }

    static void SetGraphicsAlpha(IReadOnlyList<GraphicAlphaState> states, float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        for (int i = 0; i < states.Count; i++)
        {
            GraphicAlphaState state = states[i];
            if (state.Graphic == null)
            {
                continue;
            }

            Color color = state.RestColor;
            color.a = state.RestColor.a * alpha;
            state.Graphic.color = color;
        }
    }

    sealed class NoiseMapGroup
    {
        public RectTransform Parent;
        public List<GraphicAlphaState> ParentGraphics;
        public List<ChildEntry> Children;
        public bool IsSelectedForReveal;
    }

    sealed class ChildEntry
    {
        public RectTransform Rect;
        public Vector2 RestAnchoredPosition;
        public List<GraphicAlphaState> Graphics;
    }

    sealed class GraphicAlphaState
    {
        public readonly Graphic Graphic;
        public readonly Color RestColor;

        public GraphicAlphaState(Graphic graphic)
        {
            Graphic = graphic;
            RestColor = graphic.color;
        }
    }
}
