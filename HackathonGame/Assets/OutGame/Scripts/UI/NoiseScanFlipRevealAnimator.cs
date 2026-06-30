using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase 1: reveal all unscanned noises. Phase 2: collective anticipation pulse. Phase 3: flip each to scanned.
/// </summary>
[DisallowMultipleComponent]
public class NoiseScanFlipRevealAnimator : MonoBehaviour
{
    [SerializeField] ScannedNoisesEntryAnimator unscannedNoisesEntryAnimator;
    [SerializeField] ScannedNoisesEntryAnimator scannedNoisesEntryAnimator;
    [Tooltip("Seconds after an unscanned reveal STARTS before the next one begins. Use less than reveal duration for overlapping pop-ins.")]
    [SerializeField] float revealStartInterval = 0.06f;
    [Tooltip("Seconds after a flip STARTS before the next flip begins.")]
    [SerializeField] float flipStartInterval = 0.2f;
    [SerializeField] float flipDuration = 0.35f;
    [SerializeField] [Range(1f, 6f)] float flipEaseOutPower = 3f;

    [Header("Pre-Flip Anticipation Pulse")]
    [SerializeField] bool preFlipPulseEnabled = true;
    [SerializeField] float preFlipPulseDuration = 0.7f;
    [SerializeField] [Range(1f, 1.35f)] float preFlipPulseScalePeak = 1.16f;
    [SerializeField] [Range(1f, 1.2f)] float preFlipPulseYStretchPeak = 1.12f;
    [SerializeField] [Range(0.7f, 1f)] float preFlipPulseAlphaMin = 0.78f;
    [SerializeField] [Range(0f, 1f)] float preFlipPulseHighlightPeak = 0.85f;
    [SerializeField] [Range(1, 3)] int preFlipPulseCount = 2;
    [SerializeField] float preFlipPulseHoldAtPeak = 0.08f;
    [SerializeField] [Range(1f, 6f)] float preFlipPulseEaseOutPower = 3f;
    [SerializeField] [Range(0f, 3f)] float preFlipPulseOvershoot = 2.1f;

    int _revealElementCount = -1;

    /// <summary>Fired when an individual card flip animation begins.</summary>
    public event Action CardFlipStarted;

    public void ApplySettings(
        ScannedNoisesEntryAnimator unscannedAnimator,
        ScannedNoisesEntryAnimator scannedAnimator,
        float revealInterval,
        float flipInterval,
        float flipAnimDuration,
        float flipEaseOut,
        bool anticipationPulseEnabled,
        float anticipationPulseDuration,
        float anticipationPulseScalePeak,
        float anticipationPulseYStretchPeak,
        float anticipationPulseAlphaMin,
        float anticipationPulseHighlightPeak,
        int anticipationPulseCount,
        float anticipationPulseHoldAtPeak,
        float anticipationPulseEaseOut,
        float anticipationPulseOvershoot)
    {
        unscannedNoisesEntryAnimator = unscannedAnimator;
        scannedNoisesEntryAnimator = scannedAnimator;
        revealStartInterval = Mathf.Max(0f, revealInterval);
        flipStartInterval = Mathf.Max(0f, flipInterval);
        flipDuration = Mathf.Max(0f, flipAnimDuration);
        flipEaseOutPower = Mathf.Clamp(flipEaseOut, 1f, 6f);
        preFlipPulseEnabled = anticipationPulseEnabled;
        preFlipPulseDuration = Mathf.Max(0f, anticipationPulseDuration);
        preFlipPulseScalePeak = Mathf.Clamp(anticipationPulseScalePeak, 1f, 1.35f);
        preFlipPulseYStretchPeak = Mathf.Clamp(anticipationPulseYStretchPeak, 1f, 1.2f);
        preFlipPulseAlphaMin = Mathf.Clamp(anticipationPulseAlphaMin, 0.7f, 1f);
        preFlipPulseHighlightPeak = Mathf.Clamp01(anticipationPulseHighlightPeak);
        preFlipPulseCount = Mathf.Clamp(anticipationPulseCount, 1, 3);
        preFlipPulseHoldAtPeak = Mathf.Max(0f, anticipationPulseHoldAtPeak);
        preFlipPulseEaseOutPower = Mathf.Clamp(anticipationPulseEaseOut, 1f, 6f);
        preFlipPulseOvershoot = Mathf.Clamp(anticipationPulseOvershoot, 0f, 3f);
    }

    public void ConfigureRevealCount(int revealCount)
    {
        _revealElementCount = Mathf.Max(0, revealCount);
    }

    public IEnumerator PlaySequence()
    {
        if (unscannedNoisesEntryAnimator == null || scannedNoisesEntryAnimator == null)
        {
            yield break;
        }

        int revealCount = ResolveRevealCount();
        unscannedNoisesEntryAnimator.ConfigureActiveElementCount(revealCount);
        scannedNoisesEntryAnimator.ConfigureActiveElementCount(revealCount);

        unscannedNoisesEntryAnimator.HideAllElements();
        scannedNoisesEntryAnimator.HideAllElements();

        var pairs = BuildHierarchyOrderPairs(revealCount);
        if (pairs.Count == 0)
        {
            yield break;
        }

        yield return PlayStaggeredReveals(pairs);

        if (preFlipPulseEnabled)
        {
            yield return PlayPreFlipAnticipationPulse(pairs);
        }

        yield return PlayStaggeredFlips(pairs);
    }

    IEnumerator PlayPreFlipAnticipationPulse(IReadOnlyList<NoiseScanPair> pairs)
    {
        float cycleDuration = Mathf.Max(0f, preFlipPulseDuration);
        if (cycleDuration <= 0f || pairs.Count == 0)
        {
            yield break;
        }

        int pulseCount = Mathf.Clamp(preFlipPulseCount, 1, 3);
        float holdDuration = preFlipPulseHoldAtPeak;
        float riseDuration = Mathf.Max(0.01f, (cycleDuration - holdDuration) * 0.5f);
        float fallDuration = Mathf.Max(0.01f, (cycleDuration - holdDuration) * 0.5f);

        for (int pulse = 0; pulse < pulseCount; pulse++)
        {
            float peakScale = pulse == pulseCount - 1
                ? preFlipPulseScalePeak
                : Mathf.Lerp(1f, preFlipPulseScalePeak, 0.82f);

            yield return AnimatePulseSegment(
                pairs,
                1f,
                peakScale,
                1f,
                preFlipPulseYStretchPeak,
                1f,
                preFlipPulseAlphaMin,
                0f,
                preFlipPulseHighlightPeak,
                riseDuration,
                useOvershoot: true);

            if (holdDuration > 0f)
            {
                ApplyPulseState(
                    pairs,
                    peakScale,
                    preFlipPulseYStretchPeak,
                    preFlipPulseAlphaMin,
                    preFlipPulseHighlightPeak);
                yield return new WaitForSeconds(holdDuration);
            }

            yield return AnimatePulseSegment(
                pairs,
                peakScale,
                1f,
                preFlipPulseYStretchPeak,
                1f,
                preFlipPulseAlphaMin,
                1f,
                preFlipPulseHighlightPeak,
                0f,
                fallDuration,
                useOvershoot: false);
        }

        RestorePulseElements(pairs);
    }

    IEnumerator AnimatePulseSegment(
        IReadOnlyList<NoiseScanPair> pairs,
        float scaleFrom,
        float scaleTo,
        float yStretchFrom,
        float yStretchTo,
        float alphaFrom,
        float alphaTo,
        float highlightFrom,
        float highlightTo,
        float duration,
        bool useOvershoot)
    {
        if (duration <= 0f)
        {
            ApplyPulseState(pairs, scaleTo, yStretchTo, alphaTo, highlightTo);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float t = useOvershoot
                ? EvaluateEaseOutBack(normalized, preFlipPulseOvershoot)
                : EvaluateEaseOut(normalized, preFlipPulseEaseOutPower);

            ApplyPulseState(
                pairs,
                Mathf.LerpUnclamped(scaleFrom, scaleTo, t),
                Mathf.LerpUnclamped(yStretchFrom, yStretchTo, t),
                Mathf.LerpUnclamped(alphaFrom, alphaTo, t),
                Mathf.LerpUnclamped(highlightFrom, highlightTo, t));
            yield return null;
        }

        ApplyPulseState(pairs, scaleTo, yStretchTo, alphaTo, highlightTo);
    }

    void ApplyPulseState(
        IReadOnlyList<NoiseScanPair> pairs,
        float scaleMultiplier,
        float yStretchMultiplier,
        float alpha,
        float highlightBlend)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            unscannedNoisesEntryAnimator.SetElementPulseVisual(
                pairs[i].UnscannedIndex,
                scaleMultiplier,
                yStretchMultiplier,
                alpha,
                highlightBlend);
        }
    }

    void RestorePulseElements(IReadOnlyList<NoiseScanPair> pairs)
    {
        for (int i = 0; i < pairs.Count; i++)
        {
            unscannedNoisesEntryAnimator.RestoreElementVisuals(pairs[i].UnscannedIndex);
        }
    }

    static float EvaluateEaseOutBack(float normalizedTime, float overshoot)
    {
        normalizedTime = Mathf.Clamp01(normalizedTime);
        overshoot = Mathf.Max(0f, overshoot);
        float c1 = overshoot;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(normalizedTime - 1f, 3f) + c1 * Mathf.Pow(normalizedTime - 1f, 2f);
    }

    IEnumerator PlayStaggeredReveals(IReadOnlyList<NoiseScanPair> pairs)
    {
        int revealsRemaining = pairs.Count;
        for (int i = 0; i < pairs.Count; i++)
        {
            int unscannedIndex = pairs[i].UnscannedIndex;
            StartCoroutine(RunRevealThenComplete(unscannedIndex, () => revealsRemaining--));

            if (i < pairs.Count - 1 && revealStartInterval > 0f)
            {
                yield return new WaitForSeconds(revealStartInterval);
            }
        }

        while (revealsRemaining > 0)
        {
            yield return null;
        }
    }

    IEnumerator PlayStaggeredFlips(IReadOnlyList<NoiseScanPair> pairs)
    {
        int flipsRemaining = pairs.Count;
        for (int i = 0; i < pairs.Count; i++)
        {
            NoiseScanPair pair = pairs[i];
            StartCoroutine(RunFlipThenComplete(pair.UnscannedIndex, pair.ScannedIndex, () => flipsRemaining--));

            if (i < pairs.Count - 1 && flipStartInterval > 0f)
            {
                yield return new WaitForSeconds(flipStartInterval);
            }
        }

        while (flipsRemaining > 0)
        {
            yield return null;
        }
    }

    IEnumerator RunRevealThenComplete(int unscannedIndex, System.Action onComplete)
    {
        yield return unscannedNoisesEntryAnimator.RevealElementAt(unscannedIndex);
        onComplete?.Invoke();
    }

    IEnumerator RunFlipThenComplete(int unscannedIndex, int scannedIndex, System.Action onComplete)
    {
        yield return FlipAndRevealScanned(unscannedIndex, scannedIndex);
        onComplete?.Invoke();
    }

    int ResolveRevealCount()
    {
        if (_revealElementCount >= 0)
        {
            return _revealElementCount;
        }

        return OutGameScanNoiseRevealCount.GetRevealCount();
    }

    static List<NoiseScanPair> BuildHierarchyOrderPairs(
        int revealCount,
        ScannedNoisesEntryAnimator unscannedAnimator,
        ScannedNoisesEntryAnimator scannedAnimator)
    {
        var pairs = new List<NoiseScanPair>();
        int pairCount = Mathf.Min(
            revealCount,
            unscannedAnimator.ElementCount,
            scannedAnimator.ElementCount);

        for (int i = 0; i < pairCount; i++)
        {
            pairs.Add(new NoiseScanPair(i, i));
        }

        return pairs;
    }

    List<NoiseScanPair> BuildHierarchyOrderPairs(int revealCount)
    {
        return BuildHierarchyOrderPairs(revealCount, unscannedNoisesEntryAnimator, scannedNoisesEntryAnimator);
    }

    IEnumerator FlipAndRevealScanned(int unscannedIndex, int scannedIndex)
    {
        CardFlipStarted?.Invoke();

        float duration = Mathf.Max(0f, flipDuration);
        if (duration <= 0f)
        {
            unscannedNoisesEntryAnimator.HideElementAt(unscannedIndex);
            unscannedNoisesEntryAnimator.RestoreElementScale(unscannedIndex);
            yield return scannedNoisesEntryAnimator.RevealElementAt(scannedIndex);
            yield break;
        }

        float halfDuration = duration * 0.5f;
        float elapsed = 0f;

        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = EvaluateEaseOut(Mathf.Clamp01(elapsed / halfDuration), flipEaseOutPower);
            unscannedNoisesEntryAnimator.SetElementScaleX(unscannedIndex, 1f - t);
            yield return null;
        }

        unscannedNoisesEntryAnimator.HideElementAt(unscannedIndex);
        unscannedNoisesEntryAnimator.RestoreElementScale(unscannedIndex);

        scannedNoisesEntryAnimator.HideElementAt(scannedIndex);
        scannedNoisesEntryAnimator.SetElementScaleX(scannedIndex, 0f);
        scannedNoisesEntryAnimator.SetElementAlpha(scannedIndex, 0f);

        elapsed = 0f;
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            float t = EvaluateEaseOut(Mathf.Clamp01(elapsed / halfDuration), flipEaseOutPower);
            scannedNoisesEntryAnimator.SetElementScaleX(scannedIndex, t);
            scannedNoisesEntryAnimator.SetElementAlpha(scannedIndex, t);
            yield return null;
        }

        scannedNoisesEntryAnimator.RestoreElementScale(scannedIndex);
        scannedNoisesEntryAnimator.SetElementAlpha(scannedIndex, 1f);
    }

    static float EvaluateEaseOut(float normalizedTime, float easeOutPower)
    {
        normalizedTime = Mathf.Clamp01(normalizedTime);
        easeOutPower = Mathf.Clamp(easeOutPower, 1f, 6f);
        return 1f - Mathf.Pow(1f - normalizedTime, easeOutPower);
    }

    readonly struct NoiseScanPair
    {
        public readonly int UnscannedIndex;
        public readonly int ScannedIndex;

        public NoiseScanPair(int unscannedIndex, int scannedIndex)
        {
            UnscannedIndex = unscannedIndex;
            ScannedIndex = scannedIndex;
        }
    }
}
