using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// When a direct child of Noises enters the slot adsorption range, applies a focus pose;
/// siblings above/below spread apart. On release, snaps the list so the focused item aligns to the slot.
/// </summary>
[DisallowMultipleComponent]
public class StageNoiseSlotFocus : MonoBehaviour
{
    [SerializeField] RectTransform noisesRoot;
    [SerializeField] string slotReferenceChildName = "SuperRare";
    [SerializeField] float slotAdsorptionThreshold = 120f;
    [SerializeField] Vector2 slotPositionOffset = new(-100f, 0f);
    [SerializeField] float slotScale = 1.3f;
    [SerializeField] float siblingSpreadOffset = 10f;
    [SerializeField] float focusAnimDuration = 0.2f;
    [SerializeField] [Range(1f, 6f)] float focusEaseOutPower = 3f;
    [SerializeField] float snapDuration = 0.28f;
    [SerializeField] [Range(1f, 6f)] float snapEaseOutPower = 3f;
    [SerializeField] [Range(0.05f, 0.5f)] float scrollEdgeRubberBandStrength = 0.22f;
    [SerializeField] float scrollEdgeMaxOvershoot = 48f;
    [SerializeField] float scrollBoundSpringDuration = 0.2f;
    [SerializeField] [Range(1f, 6f)] float scrollBoundSpringEaseOutPower = 3f;
    [SerializeField] [Range(0.1f, 1f)] float unfocusedBrightness = 0.45f;

    float _slotReferenceContentY;
    RectTransform _focusedChild;
    UITurntableDragRotator _turntable;
    UITurntableDrivenVerticalScroll _verticalScroll;
    Coroutine _snapCoroutine;
    Coroutine _boundSpringCoroutine;
    Coroutine _focusAnimCoroutine;
    Coroutine _releaseScrollCoroutine;
    bool _interactionEnabled = true;
    bool _isSnapping;
    bool _isFocusAnimating;
    bool _isScrollDragging;
    bool _hasLastScrollYForFocus;
    float _lastScrollYForFocus;
    readonly Dictionary<RectTransform, SlotVisualState> _states = new();
    readonly Dictionary<RectTransform, List<CachedGraphic>> _graphicsByChild = new();

    /// <summary>Fired when the focused Noises child changes (including cleared).</summary>
    public event Action<RectTransform> FocusedChildChanged;

    /// <summary>Fired when a Noises child begins the slot enlarge animation.</summary>
    public event Action<RectTransform> NoiseEnlarged;

    public RectTransform FocusedChild => _focusedChild;

    struct SlotVisualState
    {
        public bool InSlot;
        public bool IsSiblingSpreadAdjusted;
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
        public bool TargetInSlot;
        public bool TargetSpreadAdjusted;
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
        RectTransform noises,
        string referenceChildName = "SuperRare",
        float spreadOffset = 10f,
        float adsorptionThreshold = 120f,
        UITurntableDragRotator turntable = null,
        UITurntableDrivenVerticalScroll verticalScroll = null,
        float scrollSnapDuration = 0.28f,
        float scrollSnapEaseOutPower = 3f,
        float enlargeAnimDuration = 0.2f,
        float enlargeAnimEaseOutPower = 3f,
        float edgeRubberBandStrength = 0.22f,
        float edgeMaxOvershoot = 48f,
        float boundSpringDuration = 0.2f,
        float boundSpringEaseOutPower = 3f,
        float dimmedSiblingBrightness = 0.45f)
    {
        UnsubscribeInteractionEvents();
        StopSnapAnimation();
        StopBoundSpringAnimation();
        StopReleaseScrollSequence();
        StopFocusAnimation(applyCurrentFocusTargets: false);

        noisesRoot = noises;
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
        _turntable = turntable;
        _verticalScroll = verticalScroll;

        CacheSlotReference();
        RebuildChildStates();
        SubscribeInteractionEvents();
    }

    /// <summary>撃破後の詰めレイアウト後に Rest 位置を更新する。</summary>
    public void RefreshRestLayoutFromScene()
    {
        EnsureReferences();
        CacheSlotReference();
        CaptureLayoutRestFromScene();
    }

    public void RestoreFocusedChildByName(string noiseChildName)
    {
        if (string.IsNullOrEmpty(noiseChildName))
        {
            return;
        }

        EnsureReferences();
        CacheSlotReference();
        RebuildChildStates();

        if (noisesRoot == null)
        {
            return;
        }

        RectTransform target = null;
        for (int i = 0; i < noisesRoot.childCount; i++)
        {
            if (noisesRoot.GetChild(i) is not RectTransform childRect)
            {
                continue;
            }

            if (childRect.name == noiseChildName)
            {
                target = childRect;
                break;
            }
        }

        if (target == null || !target.gameObject.activeInHierarchy)
        {
            target = FindFirstActiveNoiseChild();
        }

        if (target == null)
        {
            return;
        }

        StopFocusAnimation(applyCurrentFocusTargets: false);
        SetFocusedChild(target);
        ApplyFocusVisualsInstant(target);
    }

    public RectTransform FindFirstActiveNoiseChild()
    {
        if (noisesRoot == null)
        {
            return null;
        }

        for (int i = 0; i < noisesRoot.childCount; i++)
        {
            if (noisesRoot.GetChild(i) is RectTransform childRect
                && childRect.gameObject.activeInHierarchy)
            {
                return childRect;
            }
        }

        return null;
    }

    public void SetInteractionEnabled(bool enabled)
    {
        _interactionEnabled = enabled;

        if (enabled)
        {
            return;
        }

        _isScrollDragging = false;
        _hasLastScrollYForFocus = false;
        StopSnapAnimation();
        StopBoundSpringAnimation();
        StopReleaseScrollSequence();
    }

    void OnEnable()
    {
        EnsureReferences();
        CacheSlotReference();
        RebuildChildStates();
        SubscribeInteractionEvents();
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
        SetFocusedChild(null);
    }

    void LateUpdate()
    {
        if (!_interactionEnabled || noisesRoot == null || _isSnapping)
        {
            return;
        }

        if (_isFocusAnimating && !_isScrollDragging)
        {
            return;
        }

        SyncChildLayoutStates();
        float scrollY = noisesRoot.anchoredPosition.y;

        if (_isScrollDragging)
        {
            UpdateFocusWhileScrolling(scrollY);
            return;
        }

        RectTransform slotChild = FindBestChildForSlot(scrollY);

        if (slotChild == null
            && TryGetScrollYLimits(out float minScrollY, out float maxScrollY)
            && (scrollY <= minScrollY + 0.01f || scrollY >= maxScrollY - 0.01f))
        {
            slotChild = FindNearestChildForSlot(scrollY);
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

    void UpdateFocusWhileScrolling(float scrollY)
    {
        if (_hasLastScrollYForFocus && !Mathf.Approximately(_lastScrollYForFocus, scrollY))
        {
            List<RectTransform> crossed = FindChildrenCrossedSlotCenter(_lastScrollYForFocus, scrollY);
            SortCrossedChildren(crossed, scrollY - _lastScrollYForFocus);

            for (int i = 0; i < crossed.Count; i++)
            {
                RectTransform child = crossed[i];
                if (i < crossed.Count - 1)
                {
                    ApplyScrollCrossingFocus(child);
                }
                else
                {
                    RequestFocusDuringScroll(child);
                }
            }

            if (crossed.Count == 0)
            {
                RectTransform nearestChild = FindNearestChildForSlot(scrollY);
                if (nearestChild != _focusedChild)
                {
                    RequestFocusDuringScroll(nearestChild);
                }
            }
        }
        else
        {
            RectTransform nearestChild = FindNearestChildForSlot(scrollY);
            if (nearestChild != _focusedChild)
            {
                RequestFocusDuringScroll(nearestChild);
            }
        }

        _lastScrollYForFocus = scrollY;
        _hasLastScrollYForFocus = true;
    }

    void ApplyScrollCrossingFocus(RectTransform child)
    {
        if (child == null)
        {
            return;
        }

        StopFocusAnimation(applyCurrentFocusTargets: false);

        bool playEnlargeCue = !IsVisuallyAtFocusPose(child);
        SetFocusedChild(child);

        if (playEnlargeCue)
        {
            NoiseEnlarged?.Invoke(child);
        }

        ApplyFocusVisualsInstant(child);
    }

    void RequestFocusDuringScroll(RectTransform newFocus)
    {
        if (newFocus == null)
        {
            if (_focusedChild == null)
            {
                return;
            }

            StopFocusAnimation(applyCurrentFocusTargets: false);
            SetFocusedChild(null);
            ApplyFocusVisualsInstant(null);
            return;
        }

        RequestFocus(newFocus);
    }

    void SortCrossedChildren(List<RectTransform> crossed, float scrollDelta)
    {
        if (crossed.Count <= 1)
        {
            return;
        }

        crossed.Sort((a, b) =>
        {
            float centerA = GetScrollYToCenterChildAtSlot(a);
            float centerB = GetScrollYToCenterChildAtSlot(b);
            return scrollDelta >= 0f ? centerA.CompareTo(centerB) : centerB.CompareTo(centerA);
        });
    }

    List<RectTransform> FindChildrenCrossedSlotCenter(float fromScrollY, float toScrollY)
    {
        var crossed = new List<RectTransform>();

        if (noisesRoot == null || Mathf.Approximately(fromScrollY, toScrollY))
        {
            return crossed;
        }

        float minScrollY = Mathf.Min(fromScrollY, toScrollY);
        float maxScrollY = Mathf.Max(fromScrollY, toScrollY);
        const float epsilon = 0.01f;

        for (int i = 0; i < noisesRoot.childCount; i++)
        {
            if (noisesRoot.GetChild(i) is not RectTransform childRect)
            {
                continue;
            }

            if (!childRect.gameObject.activeInHierarchy)
            {
                continue;
            }

            EnsureStateCached(childRect);
            float centerScrollY = GetScrollYToCenterChildAtSlot(childRect);
            if (centerScrollY + epsilon < minScrollY || centerScrollY - epsilon > maxScrollY)
            {
                continue;
            }

            crossed.Add(childRect);
        }

        return crossed;
    }

    void OnScrollInteractionEnded()
    {
        _isScrollDragging = false;
        _hasLastScrollYForFocus = false;

        if (!_interactionEnabled)
        {
            return;
        }

        StopReleaseScrollSequence();
        _releaseScrollCoroutine = StartCoroutine(HandleScrollReleaseSequence());
    }

    IEnumerator HandleScrollReleaseSequence()
    {
        if (noisesRoot != null
            && TryGetScrollYLimits(out _, out _)
            && IsScrollYOvershooting(noisesRoot.anchoredPosition.y, out float clampedScrollY))
        {
            StopBoundSpringAnimation();
            _boundSpringCoroutine = StartCoroutine(SpringScrollToY(noisesRoot.anchoredPosition.y, clampedScrollY));
            yield return _boundSpringCoroutine;
            _boundSpringCoroutine = null;
        }

        TrySnapToSlotOnRelease();
        _releaseScrollCoroutine = null;
    }

    public bool TryGetScrollYLimits(out float minScrollY, out float maxScrollY)
    {
        minScrollY = 0f;
        maxScrollY = 0f;

        if (noisesRoot == null)
        {
            return false;
        }

        float highestRestY = float.NegativeInfinity;
        float lowestRestY = float.PositiveInfinity;
        bool hasChild = false;

        for (int i = 0; i < noisesRoot.childCount; i++)
        {
            if (noisesRoot.GetChild(i) is not RectTransform childRect)
            {
                continue;
            }

            if (!childRect.gameObject.activeInHierarchy)
            {
                continue;
            }

            EnsureStateCached(childRect);
            float restY = _states[childRect].RestAnchoredPosition.y;
            highestRestY = Mathf.Max(highestRestY, restY);
            lowestRestY = Mathf.Min(lowestRestY, restY);
            hasChild = true;
        }

        if (!hasChild)
        {
            return false;
        }

        // Edge items should be able to center on the slot at the scroll limits.
        minScrollY = _slotReferenceContentY - highestRestY;
        maxScrollY = _slotReferenceContentY - lowestRestY;
        return minScrollY <= maxScrollY;
    }

    public float ApplyScrollDelta(float currentScrollY, float deltaY, bool allowRubberBand = true)
    {
        if (!TryGetScrollYLimits(out float minScrollY, out float maxScrollY))
        {
            return currentScrollY + deltaY;
        }

        float desiredScrollY = currentScrollY + deltaY;

        if (!allowRubberBand)
        {
            if (currentScrollY <= minScrollY && deltaY < 0f)
            {
                return currentScrollY;
            }

            if (currentScrollY >= maxScrollY && deltaY > 0f)
            {
                return currentScrollY;
            }

            return Mathf.Clamp(desiredScrollY, minScrollY, maxScrollY);
        }

        if (desiredScrollY >= minScrollY && desiredScrollY <= maxScrollY)
        {
            return desiredScrollY;
        }

        if (desiredScrollY > maxScrollY)
        {
            float overshoot = Mathf.Min(desiredScrollY - maxScrollY, scrollEdgeMaxOvershoot);
            return maxScrollY + overshoot * scrollEdgeRubberBandStrength;
        }

        float undershoot = Mathf.Min(minScrollY - desiredScrollY, scrollEdgeMaxOvershoot);
        return minScrollY - undershoot * scrollEdgeRubberBandStrength;
    }

    public float ClampScrollY(float scrollY)
    {
        if (!TryGetScrollYLimits(out float minScrollY, out float maxScrollY))
        {
            return scrollY;
        }

        return Mathf.Clamp(scrollY, minScrollY, maxScrollY);
    }

    public bool IsScrollYOvershooting(float scrollY, out float clampedScrollY)
    {
        clampedScrollY = ClampScrollY(scrollY);

        if (!TryGetScrollYLimits(out float minScrollY, out float maxScrollY))
        {
            return false;
        }

        return scrollY < minScrollY - 0.01f || scrollY > maxScrollY + 0.01f;
    }

    public void SnapToChild(RectTransform child)
    {
        if (noisesRoot == null || child == null || !child.gameObject.activeInHierarchy)
        {
            return;
        }

        if (child.parent != noisesRoot)
        {
            return;
        }

        StopReleaseScrollSequence();
        StopBoundSpringAnimation();
        StopSnapAnimation();

        EnsureStateCached(child);
        float startY = noisesRoot.anchoredPosition.y;
        float targetY = GetScrollYToCenterChildAtSlot(child);

        if (Mathf.Abs(targetY - startY) < 0.01f)
        {
            RequestFocus(child);
            return;
        }

        _snapCoroutine = StartCoroutine(SnapScrollCoroutine(child, startY, targetY));
    }

    void TrySnapToSlotOnRelease()
    {
        if (noisesRoot == null)
        {
            return;
        }

        StopSnapAnimation();

        float scrollY = noisesRoot.anchoredPosition.y;
        RectTransform snapChild = FindBestChildForSlot(scrollY);
        if (snapChild == null)
        {
            snapChild = FindNearestChildForSlot(scrollY);
        }

        if (snapChild == null)
        {
            return;
        }

        float startY = scrollY;
        float targetY = GetScrollYToCenterChildAtSlot(snapChild);

        if (Mathf.Abs(targetY - startY) < 0.01f)
        {
            RequestFocus(snapChild);
            return;
        }
        _snapCoroutine = StartCoroutine(SnapScrollCoroutine(snapChild, startY, targetY));
    }

    IEnumerator SnapScrollCoroutine(RectTransform snapChild, float startY, float targetY)
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
            float y = ClampScrollY(Mathf.Lerp(startY, targetY, eased));

            Vector2 noisesPosition = noisesRoot.anchoredPosition;
            noisesPosition.y = y;
            noisesRoot.anchoredPosition = noisesPosition;

            yield return null;
        }

        Vector2 finalPosition = noisesRoot.anchoredPosition;
        finalPosition.y = ClampScrollY(targetY);
        noisesRoot.anchoredPosition = finalPosition;

        _isSnapping = false;
        _snapCoroutine = null;
    }

    IEnumerator SpringScrollToY(float startY, float targetY)
    {
        float duration = Mathf.Max(0.01f, scrollBoundSpringDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, scrollBoundSpringEaseOutPower);
            float y = Mathf.Lerp(startY, targetY, eased);

            Vector2 noisesPosition = noisesRoot.anchoredPosition;
            noisesPosition.y = y;
            noisesRoot.anchoredPosition = noisesPosition;

            yield return null;
        }

        Vector2 finalPosition = noisesRoot.anchoredPosition;
        finalPosition.y = targetY;
        noisesRoot.anchoredPosition = finalPosition;
    }

    void RequestFocus(RectTransform newFocus)
    {
        if (newFocus == _focusedChild && !_isFocusAnimating && (newFocus == null || IsVisuallyAtFocusPose(newFocus)))
        {
            return;
        }

        StopFocusAnimation(applyCurrentFocusTargets: false);

        bool playEnlargeCue = newFocus != null && !IsVisuallyAtFocusPose(newFocus);
        SetFocusedChild(newFocus);

        if (playEnlargeCue)
        {
            NoiseEnlarged?.Invoke(newFocus);
        }

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

        if (snapshots.Count == 0 && brightnessSnapshots.Count == 0)
        {
            ApplyFocusFlags(newFocus);
            ApplyFocusBrightness(newFocus);
            _isFocusAnimating = false;
            _focusAnimCoroutine = null;
            yield break;
        }

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
                if (brightnessSnapshot.Graphic == null)
                {
                    continue;
                }

                brightnessSnapshot.Graphic.color = Color.LerpUnclamped(
                    brightnessSnapshot.StartColor,
                    brightnessSnapshot.TargetColor,
                    eased);
            }

            yield return null;
        }

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

        ApplyFocusFlags(newFocus);
        ApplyFocusBrightness(newFocus);
        _isFocusAnimating = false;
        _focusAnimCoroutine = null;
    }

    List<VisualAnimSnapshot> BuildFocusSnapshots(RectTransform newFocus)
    {
        var snapshots = new List<VisualAnimSnapshot>();
        var spreadTargets = new Dictionary<RectTransform, float>();

        if (newFocus != null)
        {
            List<RectTransform> sortedChildren = GetChildrenSortedByLayoutY();
            int focusIndex = sortedChildren.IndexOf(newFocus);

            if (focusIndex >= 0)
            {
                for (int i = 0; i < sortedChildren.Count; i++)
                {
                    if (i == focusIndex)
                    {
                        continue;
                    }

                    float offsetY = i < focusIndex ? siblingSpreadOffset : -siblingSpreadOffset;
                    spreadTargets[sortedChildren[i]] = offsetY;
                }
            }
        }

        for (int i = 0; i < noisesRoot.childCount; i++)
        {
            if (noisesRoot.GetChild(i) is not RectTransform childRect)
            {
                continue;
            }

            if (!childRect.gameObject.activeInHierarchy)
            {
                continue;
            }

            EnsureStateCached(childRect);
            SlotVisualState state = _states[childRect];

            Vector2 targetPosition = state.RestAnchoredPosition;
            Vector3 targetScale = state.RestLocalScale;
            bool targetInSlot = false;
            bool targetSpread = false;

            if (newFocus != null && childRect == newFocus)
            {
                targetPosition = state.RestAnchoredPosition + slotPositionOffset;
                targetScale = Vector3.one * slotScale;
                targetInSlot = true;
            }
            else if (spreadTargets.TryGetValue(childRect, out float spreadOffsetY))
            {
                targetPosition = state.RestAnchoredPosition;
                targetPosition.y += spreadOffsetY;
                targetSpread = true;
            }

            bool positionChanged = (childRect.anchoredPosition - targetPosition).sqrMagnitude > 0.01f;
            bool scaleChanged = (childRect.localScale - targetScale).sqrMagnitude > 0.0001f;

            if (!positionChanged && !scaleChanged)
            {
                state.InSlot = targetInSlot;
                state.IsSiblingSpreadAdjusted = targetSpread;
                _states[childRect] = state;
                continue;
            }

            snapshots.Add(new VisualAnimSnapshot
            {
                Rect = childRect,
                StartAnchoredPosition = childRect.anchoredPosition,
                StartLocalScale = childRect.localScale,
                TargetAnchoredPosition = targetPosition,
                TargetLocalScale = targetScale,
                TargetInSlot = targetInSlot,
                TargetSpreadAdjusted = targetSpread,
            });
        }

        return snapshots;
    }

    void ApplyFocusFlags(RectTransform newFocus)
    {
        for (int i = 0; i < noisesRoot.childCount; i++)
        {
            if (noisesRoot.GetChild(i) is not RectTransform childRect)
            {
                continue;
            }

            if (!_states.TryGetValue(childRect, out SlotVisualState state))
            {
                continue;
            }

            state.InSlot = false;
            state.IsSiblingSpreadAdjusted = false;
            _states[childRect] = state;
        }

        if (newFocus == null)
        {
            return;
        }

        if (_states.TryGetValue(newFocus, out SlotVisualState focusState))
        {
            focusState.InSlot = true;
            _states[newFocus] = focusState;
        }

        List<RectTransform> sortedChildren = GetChildrenSortedByLayoutY();
        int focusIndex = sortedChildren.IndexOf(newFocus);
        if (focusIndex < 0)
        {
            return;
        }

        for (int i = 0; i < sortedChildren.Count; i++)
        {
            if (i == focusIndex)
            {
                continue;
            }

            RectTransform sibling = sortedChildren[i];
            if (_states.TryGetValue(sibling, out SlotVisualState siblingState))
            {
                siblingState.IsSiblingSpreadAdjusted = true;
                _states[sibling] = siblingState;
            }
        }
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

    void OnScrollInteractionBegan()
    {
        if (!_interactionEnabled)
        {
            return;
        }

        _isScrollDragging = true;
        _hasLastScrollYForFocus = noisesRoot != null;
        _lastScrollYForFocus = noisesRoot != null ? noisesRoot.anchoredPosition.y : 0f;

        StopSnapAnimation();
        StopBoundSpringAnimation();
        StopReleaseScrollSequence();
        StopFocusAnimation();
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

        ApplyFocusFlags(focusChild);
        ApplyFocusBrightness(focusChild);
    }

    List<BrightnessAnimSnapshot> BuildBrightnessSnapshots(RectTransform newFocus)
    {
        var snapshots = new List<BrightnessAnimSnapshot>();

        if (noisesRoot == null)
        {
            return snapshots;
        }

        for (int i = 0; i < noisesRoot.childCount; i++)
        {
            if (noisesRoot.GetChild(i) is not RectTransform childRect)
            {
                continue;
            }

            if (!childRect.gameObject.activeInHierarchy)
            {
                continue;
            }

            EnsureGraphicsCached(childRect);
            if (!_graphicsByChild.TryGetValue(childRect, out List<CachedGraphic> graphics))
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

    void ApplyFocusBrightness(RectTransform focusChild)
    {
        if (noisesRoot == null)
        {
            return;
        }

        for (int i = 0; i < noisesRoot.childCount; i++)
        {
            if (noisesRoot.GetChild(i) is not RectTransform childRect)
            {
                continue;
            }

            if (!childRect.gameObject.activeInHierarchy)
            {
                continue;
            }

            bool isFocused = focusChild != null && childRect == focusChild;
            float brightness = isFocused || focusChild == null ? 1f : unfocusedBrightness;
            ApplyBrightnessToChild(childRect, brightness);
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
            if (graphic == null)
            {
                continue;
            }

            graphic.color = ScaleRgb(graphics[i].RestColor, brightness);
        }
    }

    void RestoreAllBrightness()
    {
        foreach (KeyValuePair<RectTransform, List<CachedGraphic>> entry in _graphicsByChild)
        {
            List<CachedGraphic> graphics = entry.Value;
            for (int i = 0; i < graphics.Count; i++)
            {
                Graphic graphic = graphics[i].Graphic;
                if (graphic == null)
                {
                    continue;
                }

                graphic.color = graphics[i].RestColor;
            }
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

    static Color ScaleRgb(Color color, float brightness)
    {
        brightness = Mathf.Clamp01(brightness);
        return new Color(color.r * brightness, color.g * brightness, color.b * brightness, color.a);
    }

    void SubscribeInteractionEvents()
    {
        if (_turntable != null)
        {
            _turntable.DragBegan += OnScrollInteractionBegan;
            _turntable.DragEnded += OnScrollInteractionEnded;
        }

        if (_verticalScroll != null)
        {
            _verticalScroll.ScrollDragBegan += OnScrollInteractionBegan;
            _verticalScroll.ScrollDragEnded += OnScrollInteractionEnded;
        }
    }

    void UnsubscribeInteractionEvents()
    {
        if (_turntable != null)
        {
            _turntable.DragBegan -= OnScrollInteractionBegan;
            _turntable.DragEnded -= OnScrollInteractionEnded;
        }

        if (_verticalScroll != null)
        {
            _verticalScroll.ScrollDragBegan -= OnScrollInteractionBegan;
            _verticalScroll.ScrollDragEnded -= OnScrollInteractionEnded;
        }
    }

    void SyncChildLayoutStates()
    {
        // Layout rest positions are fixed; do not read from transforms during focus/spread visuals.
    }

    void CaptureLayoutRestFromScene()
    {
        if (noisesRoot == null)
        {
            return;
        }

        for (int i = 0; i < noisesRoot.childCount; i++)
        {
            if (noisesRoot.GetChild(i) is not RectTransform childRect)
            {
                continue;
            }

            EnsureStateCached(childRect);
            SlotVisualState state = _states[childRect];
            state.RestAnchoredPosition = childRect.anchoredPosition;
            state.RestLocalScale = childRect.localScale;
            state.InSlot = false;
            state.IsSiblingSpreadAdjusted = false;
            _states[childRect] = state;
        }

        Vector2 noisesPosition = noisesRoot.anchoredPosition;
        noisesPosition.y = ClampScrollY(noisesPosition.y);
        noisesRoot.anchoredPosition = noisesPosition;
    }

    RectTransform FindBestChildForSlot(float scrollY)
    {
        RectTransform bestChild = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < noisesRoot.childCount; i++)
        {
            if (noisesRoot.GetChild(i) is not RectTransform childRect)
            {
                continue;
            }

            if (!childRect.gameObject.activeInHierarchy)
            {
                continue;
            }

            EnsureStateCached(childRect);
            SlotVisualState state = _states[childRect];
            float contentY = state.RestAnchoredPosition.y + scrollY;
            float distanceToSlotLine = Mathf.Abs(contentY - _slotReferenceContentY);

            if (distanceToSlotLine > slotAdsorptionThreshold)
            {
                continue;
            }

            float distanceToCenter = Mathf.Abs(scrollY - GetScrollYToCenterChildAtSlot(childRect));
            float score = distanceToSlotLine * 1000f + distanceToCenter;

            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestChild = childRect;
        }

        return bestChild;
    }

    RectTransform FindNearestChildForSlot(float scrollY)
    {
        RectTransform nearestChild = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < noisesRoot.childCount; i++)
        {
            if (noisesRoot.GetChild(i) is not RectTransform childRect)
            {
                continue;
            }

            if (!childRect.gameObject.activeInHierarchy)
            {
                continue;
            }

            EnsureStateCached(childRect);
            float contentY = _states[childRect].RestAnchoredPosition.y + scrollY;
            float distanceToSlotLine = Mathf.Abs(contentY - _slotReferenceContentY);

            if (distanceToSlotLine >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distanceToSlotLine;
            nearestChild = childRect;
        }

        return nearestChild;
    }

    float GetScrollYToCenterChildAtSlot(RectTransform childRect)
    {
        if (childRect == null || !_states.TryGetValue(childRect, out SlotVisualState state))
        {
            return noisesRoot != null ? noisesRoot.anchoredPosition.y : 0f;
        }

        return ClampScrollY(_slotReferenceContentY - state.RestAnchoredPosition.y);
    }

    List<RectTransform> GetChildrenSortedByLayoutY()
    {
        var children = new List<RectTransform>();

        for (int i = 0; i < noisesRoot.childCount; i++)
        {
            if (noisesRoot.GetChild(i) is not RectTransform childRect)
            {
                continue;
            }

            if (!childRect.gameObject.activeInHierarchy)
            {
                continue;
            }

            children.Add(childRect);
        }

        children.Sort((a, b) =>
        {
            float yA = _states[a].RestAnchoredPosition.y;
            float yB = _states[b].RestAnchoredPosition.y;
            return yB.CompareTo(yA);
        });

        return children;
    }

    void EnsureReferences()
    {
        if (noisesRoot != null)
        {
            return;
        }

        GameObject noisesObject = GameObject.Find("Noises");
        if (noisesObject != null)
        {
            noisesRoot = noisesObject.GetComponent<RectTransform>();
        }
    }

    void CacheSlotReference()
    {
        _slotReferenceContentY = 20f;

        if (noisesRoot == null)
        {
            return;
        }

        Transform reference = noisesRoot.Find(slotReferenceChildName);
        if (reference is RectTransform referenceRect)
        {
            _slotReferenceContentY = referenceRect.anchoredPosition.y;
        }
    }

    void RebuildChildStates()
    {
        RestoreAllInstant();
        _states.Clear();
        _graphicsByChild.Clear();

        if (noisesRoot == null)
        {
            return;
        }

        CaptureLayoutRestFromScene();
    }

    void EnsureStateCached(RectTransform childRect)
    {
        if (_states.ContainsKey(childRect))
        {
            return;
        }

        _states[childRect] = new SlotVisualState
        {
            InSlot = false,
            IsSiblingSpreadAdjusted = false,
            RestAnchoredPosition = childRect.anchoredPosition,
            RestLocalScale = childRect.localScale,
        };

        EnsureGraphicsCached(childRect);
    }

    void RestoreAllInstant()
    {
        StopSnapAnimation();
        StopFocusAnimation(applyCurrentFocusTargets: false);

        if (noisesRoot == null)
        {
            SetFocusedChild(null);
            return;
        }

        for (int i = 0; i < noisesRoot.childCount; i++)
        {
            if (noisesRoot.GetChild(i) is not RectTransform childRect)
            {
                continue;
            }

            if (!_states.TryGetValue(childRect, out SlotVisualState state))
            {
                continue;
            }

            childRect.anchoredPosition = state.RestAnchoredPosition;
            childRect.localScale = state.RestLocalScale;
            state.InSlot = false;
            state.IsSiblingSpreadAdjusted = false;
            _states[childRect] = state;
        }

        RestoreAllBrightness();
        SetFocusedChild(null);
    }
}
