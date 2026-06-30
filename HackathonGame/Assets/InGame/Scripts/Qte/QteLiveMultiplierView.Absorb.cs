using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// ノーツ吸収（並列フライ・倍率表示キュー）。
/// </summary>
public sealed partial class QteLiveMultiplierView
{
    private const float LastResortFlyStartOffsetX = 140f;

    private readonly SortedDictionary<int, (float product, bool playJuice)> _queuedMultiplierReveals =
        new SortedDictionary<int, (float product, bool playJuice)>();

    private int _nextMultiplierRevealIndex;

    /// <summary>進行中の吸収演出の完了を待つ（並列フライ含む）。</summary>
    public async UniTask WaitAllAbsorbVisualsAsync(CancellationToken token)
    {
        await WaitPendingAbsorptionsAsync(token);
        FlushRemainingMultiplierReveals();
    }

    /// <summary>QTE 開始時に吸収アイコンプールを noteCount 分確保する。</summary>
    public void PrewarmAbsorbIconPool(int noteCount)
    {
        if (noteCount <= 0 || _absorbNoteIconPrefab == null)
        {
            return;
        }

        EnsureAbsorbIconRoot();
        int target = Mathf.Clamp(noteCount, 1, QteTaikoSettingsSO.SimultaneousNoteMax);
        while (_absorbIconPool.Count < target)
        {
            QteAbsorbNoteIconView created = CreateAbsorbIconInstance();
            if (created == null)
            {
                break;
            }

            created.gameObject.SetActive(false);
            _absorbIconPool.Add(created);
        }
    }

    /// <summary>Miss 判定の同期処理（シェイク・倍率即時更新）。</summary>
    public void OnMissJudgmentResolved(
        IReadOnlyList<QteJudgment> judgments,
        IReadOnlyList<bool> isResolved,
        BattleSettingsSO settings)
    {
        if (settings == null || judgments == null || judgments.Count == 0)
        {
            return;
        }

        float resolvedProduct = QteOutcomeCalculator.ComputeProductMultiplier(judgments, settings, isResolved);
        ShowMainMultiplier(resolvedProduct, playJuice: false);
        PlayMissShake();
    }

    /// <summary>Perfect/Good 吸収を並列起動（呼び出し側は await しない）。</summary>
    public void LaunchAbsorbVisual(
        QteAbsorbFlightSnapshot snapshot,
        float productAfter,
        CancellationToken token)
    {
        if (!TryShouldAbsorbNote(snapshot.Judgment) || snapshot.NoteSprite == null)
        {
            return;
        }

        RunAbsorbFlightAsync(snapshot, productAfter, token).Forget();
    }

    private void ResetMultiplierRevealQueue()
    {
        _nextMultiplierRevealIndex = 0;
        _queuedMultiplierReveals.Clear();
    }

    private void QueueMultiplierReveal(int noteIndex, float product, bool playJuice)
    {
        _queuedMultiplierReveals[noteIndex] = (product, playJuice);
        FlushMultiplierRevealsInOrder();
    }

    private void FlushMultiplierRevealsInOrder()
    {
        while (_queuedMultiplierReveals.TryGetValue(_nextMultiplierRevealIndex, out (float product, bool playJuice) entry))
        {
            _queuedMultiplierReveals.Remove(_nextMultiplierRevealIndex);
            ShowMainMultiplier(entry.product, entry.playJuice);
            _nextMultiplierRevealIndex++;
        }
    }

    /// <summary>QTE 終了時に未表示の倍率更新を index 昇順ですべて適用する。</summary>
    private void FlushRemainingMultiplierReveals()
    {
        if (_queuedMultiplierReveals.Count == 0)
        {
            return;
        }

        List<int> keys = new List<int>(_queuedMultiplierReveals.Keys);
        keys.Sort();
        for (int i = 0; i < keys.Count; i++)
        {
            int key = keys[i];
            if (key < _nextMultiplierRevealIndex)
            {
                _queuedMultiplierReveals.Remove(key);
                continue;
            }

            (float product, bool playJuice) entry = _queuedMultiplierReveals[key];
            ShowMainMultiplier(entry.product, entry.playJuice);
            _nextMultiplierRevealIndex = key + 1;
        }

        _queuedMultiplierReveals.Clear();
    }

    private async UniTaskVoid RunAbsorbFlightAsync(
        QteAbsorbFlightSnapshot snapshot,
        float productAfter,
        CancellationToken token)
    {
        _pendingAbsorptionCount++;
        try
        {
            await FlyNoteIntoLabelAsync(snapshot, productAfter, token);
        }
        finally
        {
            _pendingAbsorptionCount = Mathf.Max(0, _pendingAbsorptionCount - 1);
        }
    }

    private async UniTask FlyNoteIntoLabelAsync(
        QteAbsorbFlightSnapshot snapshot,
        float productAfter,
        CancellationToken token)
    {
        QteJudgment judgment = snapshot.Judgment;
        Sprite noteSprite = snapshot.NoteSprite;

        if (_absorbNoteIconPrefab == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[QteLiveMultiplierView] absorb note icon prefab is null", this);
#endif
            SpawnMergeRadialSparkles(judgment);
            QueueMultiplierReveal(snapshot.NoteIndex, productAfter, playJuice: true);
            return;
        }

        EnsureAbsorbIconRoot();
        RectTransform flyParent = GetAbsorbFlyParent();
        if (flyParent == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[QteLiveMultiplierView] fly parent is null", this);
#endif
            SpawnMergeRadialSparkles(judgment);
            QueueMultiplierReveal(snapshot.NoteIndex, productAfter, playJuice: true);
            return;
        }

        (bool anchorsOk, Vector2 anchoredStart, Vector2 anchoredEnd) = await TryResolveAnchorsForFlightAsync(
            flyParent,
            snapshot,
            token);
        if (!anchorsOk)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogAbsorbSkip($"note {snapshot.NoteIndex}: using last-resort absorb anchors");
#endif
            TryGetLastResortAnchors(flyParent, snapshot.WorldSpawn, out anchoredStart, out anchoredEnd);
        }

        QteAbsorbNoteIconView icon = RentAbsorbIcon();
        if (icon == null)
        {
            PrewarmAbsorbIconPool(1);
            icon = RentAbsorbIcon();
        }

        if (icon == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[QteLiveMultiplierView] could not rent absorb icon", this);
#endif
            SpawnMergeRadialSparkles(judgment);
            QueueMultiplierReveal(snapshot.NoteIndex, productAfter, playJuice: true);
            return;
        }

        int trailSparkleCount = 0;
        if (TryGetAbsorbSparkleSettings(judgment, out QteAbsorbSparkleJudgmentSettings sparkleSettings))
        {
            trailSparkleCount = sparkleSettings.TrailSparkleCount;
        }

        icon.PrepareForAbsorbFlight();
        BringAbsorbIconToFront(icon);
        icon.ApplySprite(noteSprite);
        if (icon.IconRect != null)
        {
            icon.IconRect.anchoredPosition = anchoredStart;
            icon.IconRect.SetAsLastSibling();
        }

        bool flyFinished = false;
        void FinishAbsorbVisual()
        {
            if (flyFinished)
            {
                return;
            }

            flyFinished = true;
            SpawnMergeRadialSparkles(judgment);
            QueueMultiplierReveal(snapshot.NoteIndex, productAfter, playJuice: true);
            icon.ResetForPool();
        }

        float flyTimeoutSeconds = Mathf.Max(_chipFlyDuration * 3f, 2f);
        try
        {
            await icon
                .PlayAbsorbToAnchoredAsync(
                    anchoredStart,
                    anchoredEnd,
                    _chipFlyDuration,
                    _noteFlyStartScale,
                    _noteFlyEndScale,
                    _noteFlyScaleEase,
                    _noteFlyScaleDelayRatio,
                    _noteFlyScaleDurationRatio,
                    _noteFlySpinRotations,
                    trailSparkleCount,
                    _trailSpawnInterval,
                    anchored => SpawnTrailSparkleAt(anchored, judgment),
                    token)
                .Timeout(TimeSpan.FromSeconds(flyTimeoutSeconds));

            FinishAbsorbVisual();
        }
        catch (OperationCanceledException)
        {
            if (token.IsCancellationRequested)
            {
                throw;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogAbsorbSkip($"note {snapshot.NoteIndex}: absorb fly timed out after {flyTimeoutSeconds:0.##}s");
#endif
            icon.CancelAbsorbSilently();
            FinishAbsorbVisual();
        }
        finally
        {
            if (!flyFinished)
            {
                icon.CancelAbsorbSilently();
                FinishAbsorbVisual();
            }
        }
    }

    private async UniTask<(bool ok, Vector2 anchoredStart, Vector2 anchoredEnd)> TryResolveAnchorsForFlightAsync(
        RectTransform flyParent,
        QteAbsorbFlightSnapshot snapshot,
        CancellationToken token)
    {
        if (snapshot.HasValidAnchors)
        {
            return (true, snapshot.AnchoredStart, snapshot.AnchoredEnd);
        }

        EnsureReadyForInQteAbsorb();
        if (TryResolveAbsorbAnchors(
                flyParent,
                snapshot.WorldSpawn,
                usePrecomputedStart: false,
                Vector2.zero,
                usePrecomputedEnd: false,
                Vector2.zero,
                out Vector2 anchoredStart,
                out Vector2 anchoredEnd))
        {
            return (true, anchoredStart, anchoredEnd);
        }

        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
        EnsureReadyForInQteAbsorb();
        bool ok = TryResolveAbsorbAnchors(
            flyParent,
            snapshot.WorldSpawn,
            usePrecomputedStart: false,
            Vector2.zero,
            usePrecomputedEnd: false,
            Vector2.zero,
            out anchoredStart,
            out anchoredEnd);
        return (ok, anchoredStart, anchoredEnd);
    }

    private bool TryGetLastResortAnchors(
        RectTransform flyParent,
        Vector3 worldSpawn,
        out Vector2 anchoredStart,
        out Vector2 anchoredEnd)
    {
        anchoredStart = Vector2.zero;
        anchoredEnd = Vector2.zero;

        QtePresenter.EnsureQteCanvasScale(flyParent);
        Canvas.ForceUpdateCanvases();

        if (!TryWorldToAnchoredInRect(flyParent, GetAbsorbTargetWorldPosition(), out anchoredEnd))
        {
            anchoredEnd = Vector2.zero;
        }

        if (!TryWorldToAnchoredInRect(flyParent, worldSpawn, out anchoredStart))
        {
            anchoredStart = anchoredEnd + new Vector2(-LastResortFlyStartOffsetX, 0f);
        }

        if ((anchoredEnd - anchoredStart).sqrMagnitude < 4f)
        {
            anchoredStart = anchoredEnd + new Vector2(-LastResortFlyStartOffsetX, 0f);
        }

        return true;
    }

    private QteAbsorbNoteIconView CreateAbsorbIconInstance()
    {
        if (_absorbNoteIconPrefab == null)
        {
            return null;
        }

        EnsureAbsorbIconRoot();
        Transform parent = _absorbIconRoot != null ? _absorbIconRoot : transform;
        QteAbsorbNoteIconView created = Instantiate(_absorbNoteIconPrefab, parent);
        created.gameObject.SetActive(false);
        ApplyAbsorbIconStyle(created);
        return created;
    }
}
