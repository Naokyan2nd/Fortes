using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Horizontal counterpart to <see cref="StageNoiseSlotFocus"/> for craft panels.
/// Scroll moves the panel on X; the nearest child to the center slot enlarges and spreads siblings.
/// </summary>
[DisallowMultipleComponent]
public class CraftPanelSlotFocus : MonoBehaviour
{
    [SerializeField] RectTransform itemsRoot;
    [SerializeField] string slotReferenceChildName;
    [SerializeField] float slotAdsorptionThreshold = 120f;
    [SerializeField] Vector2 slotPositionOffset = Vector2.zero;
    [SerializeField] float slotScale = 1.15f;
    [SerializeField] float siblingSpreadOffset = 30f;
    [SerializeField] float focusAnimDuration = 0.2f;
    [SerializeField] [Range(1f, 6f)] float focusEaseOutPower = 3f;
    [SerializeField] float snapDuration = 0.28f;
    [SerializeField] [Range(1f, 6f)] float snapEaseOutPower = 3f;
    [SerializeField] [Range(0.05f, 0.5f)] float scrollEdgeRubberBandStrength = 0.22f;
    [SerializeField] float scrollEdgeMaxOvershoot = 48f;
    [SerializeField] float scrollBoundSpringDuration = 0.2f;
    [SerializeField] [Range(1f, 6f)] float scrollBoundSpringEaseOutPower = 3f;
    [SerializeField] [Range(0.1f, 1f)] float unfocusedBrightness = 0.45f;

    float _slotReferenceContentX;
    RectTransform _focusedChild;
    CraftPanelHorizontalScroll _horizontalScroll;
    Coroutine _snapCoroutine;
    Coroutine _boundSpringCoroutine;
    Coroutine _focusAnimCoroutine;
    Coroutine _releaseScrollCoroutine;
    bool _interactionEnabled = true;
    bool _isSnapping;
    bool _isFocusAnimating;
    readonly Dictionary<RectTransform, SlotVisualState> _states = new();
    readonly Dictionary<RectTransform, List<CachedGraphic>> _graphicsByChild = new();
    readonly Dictionary<RectTransform, GameObject> _selectedIndicatorsByChild = new();

    public event Action<RectTransform> FocusedChildChanged;

    public RectTransform FocusedChild => _focusedChild;

    struct SlotVisualState
    {
        public Vector2 RestAnchoredPosition;
        public Vector3 RestLocalScale;
    }

    struct VisualAnimSnapshot
    {
        public RectTransform Rect;
        public Vector2 StartAnchoredPosition;
        public Vector3 StartLocalScale;
        public Vector2 TargetAnchoredPosition;
        public Vector3 TargetLocalScale;
    }

    struct CachedGraphic
    {
        public Graphic Graphic;
        public Color RestColor;
    }

    struct BrightnessAnimSnapshot
    {
        public Graphic Graphic;
        public Color StartColor;
        public Color TargetColor;
    }

    public void Configure(
        RectTransform panelRoot,
        CraftPanelHorizontalScroll horizontalScroll,
        string referenceChildName = null,
        float spreadOffset = 30f,
        float adsorptionThreshold = 120f,
        float enlargeAnimDuration = 0.2f,
        float enlargeAnimEaseOutPower = 3f,
        float scrollSnapDuration = 0.28f,
        float scrollSnapEaseOutPower = 3f,
        float edgeRubberBandStrength = 0.22f,
        float edgeMaxOvershoot = 48f,
        float boundSpringDuration = 0.2f,
        float boundSpringEaseOutPower = 3f,
        float dimmedSiblingBrightness = 0.45f)
    {
        RectTransform previousRoot = itemsRoot;

        UnsubscribeInteractionEvents();
        StopSnapAnimation();
        StopBoundSpringAnimation();
        StopReleaseScrollSequence();
        StopFocusAnimation(applyCurrentFocusTargets: false);

        if (previousRoot != null && previousRoot != panelRoot)
        {
            itemsRoot = previousRoot;
            RestoreAllInstant();
            SetFocusedChild(null);
        }

        itemsRoot = panelRoot;
        slotReferenceChildName = referenceChildName;
        siblingSpreadOffset = spreadOffset;
        slotAdsorptionThreshold = adsorptionThreshold;
        unfocusedBrightness = Mathf.Clamp(dimmedSiblingBrightness, 0.1f, 1f);
        snapDuration = scrollSnapDuration;
        snapEaseOutPower = scrollSnapEaseOutPower;
        focusAnimDuration = enlargeAnimDuration;
        focusEaseOutPower = enlargeAnimEaseOutPower;
        scrollEdgeRubberBandStrength = edgeRubberBandStrength;
        scrollEdgeMaxOvershoot = edgeMaxOvershoot;
        scrollBoundSpringDuration = boundSpringDuration;
        scrollBoundSpringEaseOutPower = boundSpringEaseOutPower;
        _horizontalScroll = horizontalScroll;

        CacheSlotReference();
        RebuildChildStates();
        SubscribeInteractionEvents();
    }

    public void RefreshRestLayoutFromScene()
    {
        if (itemsRoot == null)
        {
            return;
        }

        CacheSlotReference();

        for (int i = 0; i < itemsRoot.childCount; i++)
        {
            if (itemsRoot.GetChild(i) is not RectTransform childRect)
            {
                continue;
            }

            if (!_states.ContainsKey(childRect))
            {
                EnsureStateCached(childRect);
                continue;
            }

            if (_focusedChild == childRect)
            {
                continue;
            }

            SlotVisualState state = _states[childRect];
            state.RestAnchoredPosition = childRect.anchoredPosition;
            state.RestLocalScale = childRect.localScale;
            _states[childRect] = state;
        }
    }

    public void SnapToNearestChildAtCurrentScroll()
    {
        if (itemsRoot == null)
        {
            return;
        }

        RectTransform nearestChild = FindNearestChildForSlot(itemsRoot.anchoredPosition.x);
        if (nearestChild != null)
        {
            SnapToChild(nearestChild);
        }
    }

    public void SetInteractionEnabled(bool enabled)
    {
        _interactionEnabled = enabled;

        if (!enabled)
        {
            StopSnapAnimation();
            StopBoundSpringAnimation();
            StopReleaseScrollSequence();
        }
    }

    void OnDisable()
    {
        UnsubscribeInteractionEvents();
        StopSnapAnimation();
        StopBoundSpringAnimation();
        StopReleaseScrollSequence();
        StopFocusAnimation(applyCurrentFocusTargets: false);
        RestoreAllInstant();
        _states.Clear();
        _selectedIndicatorsByChild.Clear();
        SetFocusedChild(null);
    }

    void LateUpdate()
    {
        if (!_interactionEnabled || itemsRoot == null || _isSnapping || _isFocusAnimating)
        {
            return;
        }

        float scrollX = itemsRoot.anchoredPosition.x;
        RectTransform slotChild = FindBestChildForSlot(scrollX);

        if (slotChild == null
            && TryGetScrollXLimits(out float minScrollX, out float maxScrollX)
            && (scrollX <= minScrollX + 0.01f || scrollX >= maxScrollX - 0.01f))
        {
            slotChild = FindNearestChildForSlot(scrollX);
        }

        if (slotChild == _focusedChild)
        {
            if (_focusedChild != null && !IsVisuallyAtFocusPose(_focusedChild))
            {
                RequestFocus(_focusedChild);
            }

            return;
        }

        RequestFocus(slotChild);
    }

    void OnScrollInteractionEnded()
    {
        if (!_interactionEnabled)
        {
            return;
        }

        StopReleaseScrollSequence();
        _releaseScrollCoroutine = StartCoroutine(HandleScrollReleaseSequence());
    }

    IEnumerator HandleScrollReleaseSequence()
    {
        if (itemsRoot != null
            && TryGetScrollXLimits(out _, out _)
            && IsScrollXOvershooting(itemsRoot.anchoredPosition.x, out float clampedScrollX))
        {
            StopBoundSpringAnimation();
            _boundSpringCoroutine = StartCoroutine(SpringScrollToX(itemsRoot.anchoredPosition.x, clampedScrollX));
            yield return _boundSpringCoroutine;
            _boundSpringCoroutine = null;
        }

        TrySnapToSlotOnRelease();
        _releaseScrollCoroutine = null;
    }

    public bool TryGetScrollXLimits(out float minScrollX, out float maxScrollX)
    {
        minScrollX = 0f;
        maxScrollX = 0f;

        if (itemsRoot == null)
        {
            return false;
        }

        float leftmostRestX = float.PositiveInfinity;
        float rightmostRestX = float.NegativeInfinity;
        bool hasChild = false;

        for (int i = 0; i < itemsRoot.childCount; i++)
        {
            if (itemsRoot.GetChild(i) is not RectTransform childRect
                || !childRect.gameObject.activeInHierarchy
                || !_states.ContainsKey(childRect))
            {
                continue;
            }

            float restX = _states[childRect].RestAnchoredPosition.x;
            leftmostRestX = Mathf.Min(leftmostRestX, restX);
            rightmostRestX = Mathf.Max(rightmostRestX, restX);
            hasChild = true;
        }

        if (!hasChild)
        {
            return false;
        }

        minScrollX = _slotReferenceContentX - rightmostRestX;
        maxScrollX = _slotReferenceContentX - leftmostRestX;
        return minScrollX <= maxScrollX;
    }

    public float ApplyScrollDelta(float currentScrollX, float deltaX, bool allowRubberBand = true)
    {
        if (!TryGetScrollXLimits(out float minScrollX, out float maxScrollX))
        {
            return currentScrollX + deltaX;
        }

        float desiredScrollX = currentScrollX + deltaX;

        if (!allowRubberBand)
        {
            if (currentScrollX <= minScrollX && deltaX < 0f)
            {
                return currentScrollX;
            }

            if (currentScrollX >= maxScrollX && deltaX > 0f)
            {
                return currentScrollX;
            }

            return Mathf.Clamp(desiredScrollX, minScrollX, maxScrollX);
        }

        if (desiredScrollX >= minScrollX && desiredScrollX <= maxScrollX)
        {
            return desiredScrollX;
        }

        if (desiredScrollX > maxScrollX)
        {
            float overshoot = Mathf.Min(desiredScrollX - maxScrollX, scrollEdgeMaxOvershoot);
            return maxScrollX + overshoot * scrollEdgeRubberBandStrength;
        }

        float undershoot = Mathf.Min(minScrollX - desiredScrollX, scrollEdgeMaxOvershoot);
        return minScrollX - undershoot * scrollEdgeRubberBandStrength;
    }

    public float ClampScrollX(float scrollX)
    {
        if (!TryGetScrollXLimits(out float minScrollX, out float maxScrollX))
        {
            return scrollX;
        }

        return Mathf.Clamp(scrollX, minScrollX, maxScrollX);
    }

    public bool IsScrollXOvershooting(float scrollX, out float clampedScrollX)
    {
        clampedScrollX = ClampScrollX(scrollX);

        if (!TryGetScrollXLimits(out float minScrollX, out float maxScrollX))
        {
            return false;
        }

        return scrollX < minScrollX - 0.01f || scrollX > maxScrollX + 0.01f;
    }

    public void SnapToChild(RectTransform child)
    {
        if (itemsRoot == null || child == null || !child.gameObject.activeInHierarchy || child.parent != itemsRoot)
        {
            return;
        }

        StopReleaseScrollSequence();
        StopBoundSpringAnimation();
        StopSnapAnimation();

        EnsureStateCached(child);
        float startX = itemsRoot.anchoredPosition.x;
        float targetX = GetScrollXToCenterChildAtSlot(child);

        if (Mathf.Abs(targetX - startX) < 0.01f)
        {
            RequestFocus(child);
            return;
        }

        _snapCoroutine = StartCoroutine(SnapScrollCoroutine(child, startX, targetX));
    }

    void OnCraftChildClicked(RectTransform child)
    {
        if (!_interactionEnabled)
        {
            return;
        }

        SnapToChild(child);
    }

    void TrySnapToSlotOnRelease()
    {
        if (itemsRoot == null)
        {
            return;
        }

        StopSnapAnimation();

        float scrollX = itemsRoot.anchoredPosition.x;
        RectTransform snapChild = FindBestChildForSlot(scrollX) ?? FindNearestChildForSlot(scrollX);
        if (snapChild == null)
        {
            return;
        }

        float startX = scrollX;
        float targetX = GetScrollXToCenterChildAtSlot(snapChild);

        if (Mathf.Abs(targetX - startX) < 0.01f)
        {
            RequestFocus(snapChild);
            return;
        }

        _snapCoroutine = StartCoroutine(SnapScrollCoroutine(snapChild, startX, targetX));
    }

    IEnumerator SnapScrollCoroutine(RectTransform snapChild, float startX, float targetX)
    {
        _isSnapping = true;
        RequestFocus(snapChild);

        float duration = Mathf.Max(0.01f, snapDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, snapEaseOutPower);
            float x = ClampScrollX(Mathf.Lerp(startX, targetX, eased));

            Vector2 panelPosition = itemsRoot.anchoredPosition;
            panelPosition.x = x;
            itemsRoot.anchoredPosition = panelPosition;

            yield return null;
        }

        Vector2 finalPosition = itemsRoot.anchoredPosition;
        finalPosition.x = ClampScrollX(targetX);
        itemsRoot.anchoredPosition = finalPosition;

        _isSnapping = false;
        _snapCoroutine = null;
    }

    IEnumerator SpringScrollToX(float startX, float targetX)
    {
        float duration = Mathf.Max(0.01f, scrollBoundSpringDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, scrollBoundSpringEaseOutPower);
            float x = Mathf.Lerp(startX, targetX, eased);

            Vector2 panelPosition = itemsRoot.anchoredPosition;
            panelPosition.x = x;
            itemsRoot.anchoredPosition = panelPosition;

            yield return null;
        }

        Vector2 finalPosition = itemsRoot.anchoredPosition;
        finalPosition.x = targetX;
        itemsRoot.anchoredPosition = finalPosition;
    }

    void RequestFocus(RectTransform newFocus)
    {
        if (newFocus == _focusedChild && !_isFocusAnimating && (newFocus == null || IsVisuallyAtFocusPose(newFocus)))
        {
            return;
        }

        StopFocusAnimation(applyCurrentFocusTargets: false);
        SetFocusedChild(newFocus);
        _focusAnimCoroutine = StartCoroutine(RunFocusAnimation(newFocus));
    }

    void SetFocusedChild(RectTransform newFocus)
    {
        if (_focusedChild == newFocus)
        {
            return;
        }

        _focusedChild = newFocus;
        FocusedChildChanged?.Invoke(_focusedChild);
    }

    IEnumerator RunFocusAnimation(RectTransform newFocus)
    {
        _isFocusAnimating = true;

        List<VisualAnimSnapshot> snapshots = BuildFocusSnapshots(newFocus);
        List<BrightnessAnimSnapshot> brightnessSnapshots = BuildBrightnessSnapshots(newFocus);

        float duration = Mathf.Max(0.01f, focusAnimDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, focusEaseOutPower);

            for (int i = 0; i < snapshots.Count; i++)
            {
                VisualAnimSnapshot snapshot = snapshots[i];
                if (snapshot.Rect == null)
                {
                    continue;
                }

                snapshot.Rect.anchoredPosition = Vector2.LerpUnclamped(
                    snapshot.StartAnchoredPosition,
                    snapshot.TargetAnchoredPosition,
                    eased);
                snapshot.Rect.localScale = Vector3.LerpUnclamped(
                    snapshot.StartLocalScale,
                    snapshot.TargetLocalScale,
                    eased);
            }

            for (int i = 0; i < brightnessSnapshots.Count; i++)
            {
                BrightnessAnimSnapshot brightnessSnapshot = brightnessSnapshots[i];
                if (brightnessSnapshot.Graphic != null)
                {
                    brightnessSnapshot.Graphic.color = Color.LerpUnclamped(
                        brightnessSnapshot.StartColor,
                        brightnessSnapshot.TargetColor,
                        eased);
                }
            }

            yield return null;
        }

        ApplyFocusVisualsInstant(newFocus);
        _isFocusAnimating = false;
        _focusAnimCoroutine = null;
    }

    List<VisualAnimSnapshot> BuildFocusSnapshots(RectTransform newFocus)
    {
        var snapshots = new List<VisualAnimSnapshot>();
        var spreadTargets = new Dictionary<RectTransform, float>();

        if (newFocus != null)
        {
            List<RectTransform> sortedChildren = GetChildrenSortedByLayoutX();
            int focusIndex = sortedChildren.IndexOf(newFocus);

            if (focusIndex >= 0)
            {
                for (int i = 0; i < sortedChildren.Count; i++)
                {
                    if (i == focusIndex)
                    {
                        continue;
                    }

                    float offsetX = i < focusIndex ? -siblingSpreadOffset : siblingSpreadOffset;
                    spreadTargets[sortedChildren[i]] = offsetX;
                }
            }
        }

        for (int i = 0; i < itemsRoot.childCount; i++)
        {
            if (itemsRoot.GetChild(i) is not RectTransform childRect
                || !childRect.gameObject.activeInHierarchy
                || !_states.TryGetValue(childRect, out SlotVisualState state))
            {
                continue;
            }

            Vector2 targetPosition = state.RestAnchoredPosition;
            Vector3 targetScale = state.RestLocalScale;

            if (newFocus != null && childRect == newFocus)
            {
                targetPosition = state.RestAnchoredPosition + slotPositionOffset;
                targetScale = Vector3.one * slotScale;
            }
            else if (spreadTargets.TryGetValue(childRect, out float spreadOffsetX))
            {
                targetPosition = state.RestAnchoredPosition;
                targetPosition.x += spreadOffsetX;
            }

            bool positionChanged = (childRect.anchoredPosition - targetPosition).sqrMagnitude > 0.01f;
            bool scaleChanged = (childRect.localScale - targetScale).sqrMagnitude > 0.0001f;

            if (!positionChanged && !scaleChanged)
            {
                continue;
            }

            snapshots.Add(new VisualAnimSnapshot
            {
                Rect = childRect,
                StartAnchoredPosition = childRect.anchoredPosition,
                StartLocalScale = childRect.localScale,
                TargetAnchoredPosition = targetPosition,
                TargetLocalScale = targetScale,
            });
        }

        return snapshots;
    }

    List<BrightnessAnimSnapshot> BuildBrightnessSnapshots(RectTransform newFocus)
    {
        var snapshots = new List<BrightnessAnimSnapshot>();

        if (itemsRoot == null)
        {
            return snapshots;
        }

        for (int i = 0; i < itemsRoot.childCount; i++)
        {
            if (itemsRoot.GetChild(i) is not RectTransform childRect
                || !childRect.gameObject.activeInHierarchy
                || !_graphicsByChild.TryGetValue(childRect, out List<CachedGraphic> graphics))
            {
                continue;
            }

            bool isFocused = newFocus != null && childRect == newFocus;
            float brightness = isFocused || newFocus == null ? 1f : unfocusedBrightness;

            for (int g = 0; g < graphics.Count; g++)
            {
                CachedGraphic cached = graphics[g];
                if (cached.Graphic == null)
                {
                    continue;
                }

                snapshots.Add(new BrightnessAnimSnapshot
                {
                    Graphic = cached.Graphic,
                    StartColor = cached.Graphic.color,
                    TargetColor = ScaleRgb(cached.RestColor, brightness),
                });
            }
        }

        return snapshots;
    }

    void ApplyFocusVisualsInstant(RectTransform focusChild)
    {
        List<VisualAnimSnapshot> snapshots = BuildFocusSnapshots(focusChild);

        for (int i = 0; i < snapshots.Count; i++)
        {
            VisualAnimSnapshot snapshot = snapshots[i];
            if (snapshot.Rect == null)
            {
                continue;
            }

            snapshot.Rect.anchoredPosition = snapshot.TargetAnchoredPosition;
            snapshot.Rect.localScale = snapshot.TargetLocalScale;
        }

        ApplyFocusBrightness(focusChild);
        ApplySelectedIndicators(focusChild);

        if (focusChild != null)
        {
            focusChild.SetAsLastSibling();
        }
    }

    void ApplyFocusBrightness(RectTransform focusChild)
    {
        if (itemsRoot == null)
        {
            return;
        }

        for (int i = 0; i < itemsRoot.childCount; i++)
        {
            if (itemsRoot.GetChild(i) is not RectTransform childRect
                || !childRect.gameObject.activeInHierarchy)
            {
                continue;
            }

            bool isFocused = focusChild != null && childRect == focusChild;
            float brightness = isFocused || focusChild == null ? 1f : unfocusedBrightness;
            ApplyBrightnessToChild(childRect, brightness);
        }
    }

    void ApplySelectedIndicators(RectTransform focusChild)
    {
        foreach (KeyValuePair<RectTransform, GameObject> entry in _selectedIndicatorsByChild)
        {
            if (entry.Value != null)
            {
                entry.Value.SetActive(focusChild != null && entry.Key == focusChild);
            }
        }
    }

    void ApplyBrightnessToChild(RectTransform childRect, float brightness)
    {
        if (!_graphicsByChild.TryGetValue(childRect, out List<CachedGraphic> graphics))
        {
            return;
        }

        for (int i = 0; i < graphics.Count; i++)
        {
            Graphic graphic = graphics[i].Graphic;
            if (graphic != null)
            {
                graphic.color = ScaleRgb(graphics[i].RestColor, brightness);
            }
        }
    }

    void RestoreAllBrightness()
    {
        foreach (KeyValuePair<RectTransform, List<CachedGraphic>> entry in _graphicsByChild)
        {
            for (int i = 0; i < entry.Value.Count; i++)
            {
                Graphic graphic = entry.Value[i].Graphic;
                if (graphic != null)
                {
                    graphic.color = entry.Value[i].RestColor;
                }
            }
        }
    }

    bool IsVisuallyAtFocusPose(RectTransform childRect)
    {
        if (childRect == null || !_states.TryGetValue(childRect, out SlotVisualState state))
        {
            return false;
        }

        Vector2 focusedPosition = state.RestAnchoredPosition + slotPositionOffset;
        Vector3 focusedScale = Vector3.one * slotScale;
        bool positionMatches = (childRect.anchoredPosition - focusedPosition).sqrMagnitude <= 0.25f;
        bool scaleMatches = (childRect.localScale - focusedScale).sqrMagnitude <= 0.0001f;
        return positionMatches && scaleMatches;
    }

    RectTransform FindBestChildForSlot(float scrollX)
    {
        RectTransform bestChild = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < itemsRoot.childCount; i++)
        {
            if (itemsRoot.GetChild(i) is not RectTransform childRect
                || !childRect.gameObject.activeInHierarchy
                || !_states.TryGetValue(childRect, out SlotVisualState state))
            {
                continue;
            }

            float contentX = state.RestAnchoredPosition.x + scrollX;
            float distanceToSlotLine = Mathf.Abs(contentX - _slotReferenceContentX);

            if (distanceToSlotLine > slotAdsorptionThreshold)
            {
                continue;
            }

            float distanceToCenter = Mathf.Abs(scrollX - GetScrollXToCenterChildAtSlot(childRect));
            float score = distanceToSlotLine * 1000f + distanceToCenter;

            if (score < bestScore)
            {
                bestScore = score;
                bestChild = childRect;
            }
        }

        return bestChild;
    }

    RectTransform FindNearestChildForSlot(float scrollX)
    {
        RectTransform nearestChild = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < itemsRoot.childCount; i++)
        {
            if (itemsRoot.GetChild(i) is not RectTransform childRect
                || !childRect.gameObject.activeInHierarchy
                || !_states.TryGetValue(childRect, out SlotVisualState state))
            {
                continue;
            }

            float contentX = state.RestAnchoredPosition.x + scrollX;
            float distanceToSlotLine = Mathf.Abs(contentX - _slotReferenceContentX);

            if (distanceToSlotLine >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distanceToSlotLine;
            nearestChild = childRect;
        }

        return nearestChild;
    }

    float GetScrollXToCenterChildAtSlot(RectTransform childRect)
    {
        if (childRect == null || !_states.TryGetValue(childRect, out SlotVisualState state))
        {
            return itemsRoot != null ? itemsRoot.anchoredPosition.x : 0f;
        }

        return ClampScrollX(_slotReferenceContentX - state.RestAnchoredPosition.x);
    }

    List<RectTransform> GetChildrenSortedByLayoutX()
    {
        var children = new List<RectTransform>();

        for (int i = 0; i < itemsRoot.childCount; i++)
        {
            if (itemsRoot.GetChild(i) is RectTransform childRect
                && childRect.gameObject.activeInHierarchy
                && _states.ContainsKey(childRect))
            {
                children.Add(childRect);
            }
        }

        children.Sort((a, b) =>
            _states[a].RestAnchoredPosition.x.CompareTo(_states[b].RestAnchoredPosition.x));
        return children;
    }

    void CacheSlotReference()
    {
        _slotReferenceContentX = 0f;

        if (itemsRoot == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(slotReferenceChildName))
        {
            Transform reference = itemsRoot.Find(slotReferenceChildName);
            if (reference is RectTransform referenceRect)
            {
                _slotReferenceContentX = referenceRect.anchoredPosition.x;
                return;
            }
        }

        // Midpoint of slot rest X positions (screen-center line). Do not pick the nearest child:
        // after-craft panels have no slot at x=0, and "nearest" would stay focused at scroll 0.
        float leftmostRestX = float.PositiveInfinity;
        float rightmostRestX = float.NegativeInfinity;
        bool hasChild = false;

        for (int i = 0; i < itemsRoot.childCount; i++)
        {
            if (itemsRoot.GetChild(i) is not RectTransform childRect
                || !childRect.gameObject.activeInHierarchy)
            {
                continue;
            }

            float restX = childRect.anchoredPosition.x;
            leftmostRestX = Mathf.Min(leftmostRestX, restX);
            rightmostRestX = Mathf.Max(rightmostRestX, restX);
            hasChild = true;
        }

        if (hasChild)
        {
            _slotReferenceContentX = (leftmostRestX + rightmostRestX) * 0.5f;
        }
    }

    void RebuildChildStates()
    {
        RestoreAllInstant();
        _states.Clear();
        _graphicsByChild.Clear();
        _selectedIndicatorsByChild.Clear();

        if (itemsRoot == null)
        {
            return;
        }

        CaptureLayoutRestFromScene();
    }

    void CaptureLayoutRestFromScene()
    {
        if (itemsRoot == null)
        {
            return;
        }

        for (int i = 0; i < itemsRoot.childCount; i++)
        {
            if (itemsRoot.GetChild(i) is not RectTransform childRect)
            {
                continue;
            }

            // All direct panel children are scroll slots; Selected is optional (indicator only).
            EnsureStateCached(childRect);
        }

        Vector2 panelPosition = itemsRoot.anchoredPosition;
        panelPosition.x = ClampScrollX(panelPosition.x);
        itemsRoot.anchoredPosition = panelPosition;
    }

    void EnsureStateCached(RectTransform childRect)
    {
        if (_states.ContainsKey(childRect))
        {
            return;
        }

        _states[childRect] = new SlotVisualState
        {
            RestAnchoredPosition = childRect.anchoredPosition,
            RestLocalScale = childRect.localScale,
        };

        EnsureGraphicsCached(childRect);

        Transform selectedTransform = childRect.Find("Selected");
        if (selectedTransform != null)
        {
            _selectedIndicatorsByChild[childRect] = selectedTransform.gameObject;
            selectedTransform.gameObject.SetActive(false);
        }
    }

    void EnsureGraphicsCached(RectTransform childRect)
    {
        if (_graphicsByChild.ContainsKey(childRect))
        {
            return;
        }

        var graphics = new List<CachedGraphic>();
        Graphic[] graphicComponents = childRect.GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphicComponents.Length; i++)
        {
            Graphic graphic = graphicComponents[i];
            if (graphic == null)
            {
                continue;
            }

            graphics.Add(new CachedGraphic
            {
                Graphic = graphic,
                RestColor = graphic.color,
            });
        }

        _graphicsByChild[childRect] = graphics;
    }

    void RestoreAllInstant()
    {
        StopSnapAnimation();
        StopFocusAnimation(applyCurrentFocusTargets: false);

        if (itemsRoot == null)
        {
            SetFocusedChild(null);
            return;
        }

        for (int i = 0; i < itemsRoot.childCount; i++)
        {
            if (itemsRoot.GetChild(i) is not RectTransform childRect
                || !_states.TryGetValue(childRect, out SlotVisualState state))
            {
                continue;
            }

            childRect.anchoredPosition = state.RestAnchoredPosition;
            childRect.localScale = state.RestLocalScale;
        }

        RestoreAllBrightness();
        ApplySelectedIndicators(null);
        SetFocusedChild(null);
    }

    void SubscribeInteractionEvents()
    {
        if (_horizontalScroll != null)
        {
            _horizontalScroll.ScrollDragBegan += OnScrollInteractionBegan;
            _horizontalScroll.ScrollDragEnded += OnScrollInteractionEnded;
            _horizontalScroll.ScrollChildClicked += OnCraftChildClicked;
        }
    }

    void UnsubscribeInteractionEvents()
    {
        if (_horizontalScroll != null)
        {
            _horizontalScroll.ScrollDragBegan -= OnScrollInteractionBegan;
            _horizontalScroll.ScrollDragEnded -= OnScrollInteractionEnded;
            _horizontalScroll.ScrollChildClicked -= OnCraftChildClicked;
        }
    }

    void OnScrollInteractionBegan()
    {
        if (!_interactionEnabled)
        {
            return;
        }

        StopSnapAnimation();
        StopBoundSpringAnimation();
        StopReleaseScrollSequence();
        StopFocusAnimation();
    }

    void StopSnapAnimation()
    {
        if (_snapCoroutine != null)
        {
            StopCoroutine(_snapCoroutine);
            _snapCoroutine = null;
        }

        _isSnapping = false;
    }

    void StopFocusAnimation(bool applyCurrentFocusTargets = true)
    {
        if (_focusAnimCoroutine != null)
        {
            StopCoroutine(_focusAnimCoroutine);
            _focusAnimCoroutine = null;
        }

        _isFocusAnimating = false;

        if (applyCurrentFocusTargets)
        {
            ApplyFocusVisualsInstant(_focusedChild);
        }
    }

    void StopBoundSpringAnimation()
    {
        if (_boundSpringCoroutine != null)
        {
            StopCoroutine(_boundSpringCoroutine);
            _boundSpringCoroutine = null;
        }
    }

    void StopReleaseScrollSequence()
    {
        if (_releaseScrollCoroutine != null)
        {
            StopCoroutine(_releaseScrollCoroutine);
            _releaseScrollCoroutine = null;
        }
    }

    static Color ScaleRgb(Color color, float brightness)
    {
        brightness = Mathf.Clamp01(brightness);
        return new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
    }
}
