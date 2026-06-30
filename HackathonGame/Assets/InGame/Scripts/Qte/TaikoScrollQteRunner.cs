using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

/// <summary>
/// 太鼓風・五線譜スクロール QTE。
/// 各音符は QtePointData.TimingInSeconds（中心到達）と NoteScrollDurationSeconds で配置し、
/// 速度は「画面外スポーン → 中心到達」の共通秒数から自動計算する。
/// タイムラインは再生 clip の AudioSource.time（＋バリアント別オフセット）を優先する。
/// 判定は Perfect / Good 帯とノート中心の位置関係（空間ゾーン）。
/// </summary>
public sealed class TaikoScrollQteRunner : MonoBehaviour
{
    private sealed class NoteSchedule
    {
        public int Index;
        public float SpawnSec;
        public float PerfectSec;
        public float ScrollSpeed;
        public bool Spawned;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool LoggedPerfectCrossing;
#endif
    }

    private sealed class ActiveNote
    {
        public int Index;
        public QteTaikoNoteView View;
        public float SpawnSec;
        public float PerfectSec;
        public float ScrollSpeed;
        public float CenterOffsetX;
        public bool Resolved;
        public bool FadingOut;
        public bool ReturnedToPool;
        public QteJudgment Judgment = QteJudgment.Miss;
    }

    [SerializeField]
    private QteTaikoView _view;

    [SerializeField]
    private QteTaikoSettingsSO _settings;

    [SerializeField]
    private InGameLogManager _log;

    [SerializeField]
    private QteTaikoJudgmentDisplay _judgmentDisplay;

    [SerializeField]
    private QteTaikoJudgmentZones _judgmentZones;

    [SerializeField]
    private QteTaikoHitEffectDisplay _hitEffectDisplay;

    [SerializeField]
    private QteTaikoLayerIntroView _layerIntro;

    [SerializeField]
    private QteLiveMultiplierView _liveMultiplier;

    private float _playbackEndSongTime;
    private float _playbackTimelineOffset;
    private double _playbackJingleStartDsp;
    private AudioSource _playbackJingleSource;
    private int _perfectStreak;

    private readonly List<QteTaikoSpatialJudgment.NoteHandle> _tapNoteHandles =
        new List<QteTaikoSpatialJudgment.NoteHandle>();

    /// <summary>
    /// Taiko QTE を実行する。
    /// </summary>
    public async UniTask<IReadOnlyList<QteJudgment>> RunAsync(
        SkillQtePlaybackContext playbackContext,
        BattleSettingsSO battleSettings,
        AudioSource jingleSource,
        double jingleStartDsp,
        CancellationToken token)
    {
        _perfectStreak = 0;

        QtePresenter.EnsureQteCanvasScale(transform);
        Canvas.ForceUpdateCanvases();

        if (_view == null || _settings == null || playbackContext?.Skill == null || playbackContext.Variant == null)
        {
            Debug.LogError("[TaikoScrollQteRunner] 参照または QTE 再生コンテキストが null です。", this);
            return FillMiss(playbackContext);
        }

        if (_judgmentZones == null)
        {
            Debug.LogError("[TaikoScrollQteRunner] QteTaikoJudgmentZones が未設定です。", this);
            return FillMiss(playbackContext);
        }

        float spawnX = _view.GetOffScreenSpawnX();
        float judgmentX = _settings.JudgmentLineX;
        float travelDistance = spawnX - judgmentX;
        if (travelDistance <= 0f)
        {
            Debug.LogError(
                "[TaikoScrollQteRunner] 画面外スポーン X が JudgmentLineX 以下です。NoteParent の Rect を確認してください。",
                this);
            return FillMiss(playbackContext);
        }

        QtePointData[] points = playbackContext.QteTimings;
        if (points == null || points.Length == 0)
        {
            Debug.LogError("[TaikoScrollQteRunner] QteTimings が空です。", this);
            return FillMiss(playbackContext);
        }

        int noteCount = points.Length;

        float scrollDuration = playbackContext.NoteScrollDurationSeconds;
        if (!QteTaikoTimingValidator.TryValidate(
                points,
                scrollDuration,
                _judgmentZones,
                judgmentX,
                out string validationError))
        {
            Debug.LogError(validationError, this);
            return FillMiss(playbackContext);
        }

        float timelineOffset = playbackContext.JingleTimelineOffsetSeconds;
        var schedules = new NoteSchedule[noteCount];
        float maxPerfectSec = 0f;
        for (int i = 0; i < noteCount; i++)
        {
            QtePointData point = points[i];
            float perfectSec = point.TimingInSeconds;
            float spawnSec = perfectSec - scrollDuration;
            schedules[i] = new NoteSchedule
            {
                Index = i,
                SpawnSec = spawnSec,
                PerfectSec = perfectSec,
                ScrollSpeed = travelDistance / scrollDuration,
            };

            if (perfectSec > maxPerfectSec)
            {
                maxPerfectSec = perfectSec;
            }
        }

        float laneY = _settings.NoteLaneY;
        Sprite noteSprite = _settings.GetNoteSprite(playbackContext.Category);
        Sprite hitEffectSprite = _settings.GetHitEffectSprite(playbackContext.Category);
        _view.TryGetPlayViewportXBounds(out float playViewportXMin, out float playViewportXMax);
        var activeNotes = new List<ActiveNote>(noteCount);
        var results = new QteJudgment[noteCount];
        for (int i = 0; i < noteCount; i++)
        {
            results[i] = QteJudgment.Miss;
        }

        if (_layerIntro != null)
        {
            await _layerIntro.WaitUntilIntroCompleteAsync(token);
            _layerIntro.EnsureStaffScaleForPlay();
        }

        QtePresenter.EnsureQteCanvasScale(transform);
        Canvas.ForceUpdateCanvases();

        if (jingleSource != null && jingleSource.clip != null && jingleStartDsp > 0d)
        {
            double playbackWaitDeadlineDsp = jingleStartDsp + 0.25;
            await UniTask.WaitUntil(
                () => jingleSource.isPlaying || AudioSettings.dspTime >= playbackWaitDeadlineDsp,
                cancellationToken: token,
                timing: PlayerLoopTiming.Update);
        }

        IDisposable tapSub = null;
        Queue<double> tapQueue = new Queue<double>();
        var noteResolved = new bool[noteCount];

        try
        {
            if (_liveMultiplier != null)
            {
                _liveMultiplier.PrewarmAbsorbIconPool(noteCount);
            }

            tapSub = _view.OnTapDsp.Subscribe(_ => tapQueue.Enqueue(0d));

            int resolvedCount = 0;
            while (resolvedCount < noteCount)
            {
                token.ThrowIfCancellationRequested();
                float songTime = GetSongTime(jingleSource, jingleStartDsp, timelineOffset);

                for (int i = 0; i < noteCount; i++)
                {
                    NoteSchedule schedule = schedules[i];
                    if (schedule.Spawned || songTime < schedule.SpawnSec)
                    {
                        continue;
                    }

                    schedule.Spawned = true;
                    QteTaikoNoteView noteView = _view.RentNote(spawnX, laneY);
                    noteView.ApplySprite(noteSprite);
                    noteView.ActivateAt(spawnX, laneY);
                    float centerOffsetX = noteView.GetCenterAnchoredOffsetX();
                    activeNotes.Add(new ActiveNote
                    {
                        Index = schedule.Index,
                        View = noteView,
                        SpawnSec = schedule.SpawnSec,
                        PerfectSec = schedule.PerfectSec,
                        ScrollSpeed = schedule.ScrollSpeed,
                        CenterOffsetX = centerOffsetX,
                    });
                }

                for (int i = 0; i < activeNotes.Count; i++)
                {
                    ActiveNote note = activeNotes[i];
                    if (note.ReturnedToPool)
                    {
                        continue;
                    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    LogPerfectCrossingIfNeeded(
                        playbackContext,
                        schedules[note.Index],
                        songTime,
                        jingleSource,
                        judgmentX,
                        note.View,
                        note.CenterOffsetX);
#endif

                    float elapsed = songTime - note.SpawnSec;
                    float centerX = spawnX - elapsed * note.ScrollSpeed;
                    float anchorX = centerX - note.CenterOffsetX;
                    note.View.SetAnchoredPosition(anchorX, laneY);

                    if (note.FadingOut)
                    {
                        continue;
                    }

                    if (note.Resolved)
                    {
                        continue;
                    }

                    if (TryHandlePassMiss(
                            note,
                            anchorX,
                            laneY,
                            spawnX,
                            noteSprite,
                            playbackContext,
                            battleSettings,
                            results,
                            noteResolved,
                            token,
                            ref resolvedCount))
                    {
                        continue;
                    }

                    if (TryHandleLeftViewportMiss(
                            note,
                            anchorX,
                            laneY,
                            spawnX,
                            playViewportXMin,
                            noteSprite,
                            playbackContext,
                            battleSettings,
                            results,
                            noteResolved,
                            token,
                            ref resolvedCount))
                    {
                        continue;
                    }
                }

                while (tapQueue.Count > 0)
                {
                    tapQueue.Dequeue();
                    if (!TrySelectNoteForTap(activeNotes, playViewportXMin, playViewportXMax, out ActiveNote matched))
                    {
                        continue;
                    }

                    QteJudgment judgment = _judgmentZones.JudgeByCenter(matched.View.RectTransform);
                    ResolveNote(matched, judgment);
                    results[matched.Index] = matched.Judgment;
                    noteResolved[matched.Index] = true;
                    NotifyLiveMultiplier(matched, noteSprite, results, noteResolved, battleSettings, token);
                    float matchedX = matched.View.GetCenterAnchoredPosition().x;
                    _hitEffectDisplay?.Show(new Vector2(matchedX, laneY), hitEffectSprite);
                    ShowJudgmentAtNote(matched, matchedX, laneY, matched.Judgment, token, includeTapSound: true);
                    ReturnActiveNote(matched, spawnX, laneY);
                    resolvedCount++;
                    LogHit(playbackContext, matched.Index, matched.Judgment, matchedX);
                }

                if (resolvedCount < noteCount)
                {
                    await UniTask.Yield(token);
                }
            }

            _view.SetInputEnabled(false);

            if (_liveMultiplier != null)
            {
                await _liveMultiplier.WaitAllAbsorbVisualsAsync(token);
            }

            _playbackEndSongTime = maxPerfectSec + _settings.PostRollSeconds;
            _playbackTimelineOffset = timelineOffset;
            _playbackJingleStartDsp = jingleStartDsp;
            _playbackJingleSource = jingleSource;

            return results;
        }
        finally
        {
            tapSub?.Dispose();
            _view.SetInputEnabled(false);
            for (int i = 0; i < activeNotes.Count; i++)
            {
                ReturnActiveNote(activeNotes[i], spawnX, laneY);
            }

            _judgmentDisplay?.ClearAll();
            _hitEffectDisplay?.ClearAll();
        }
    }

    private static float GetSongTime(AudioSource jingleSource, double jingleStartDsp, float timelineOffset)
    {
        if (jingleSource != null && jingleSource.clip != null && jingleSource.isPlaying)
        {
            return jingleSource.time + timelineOffset;
        }

        if (jingleStartDsp > 0d)
        {
            return (float)(AudioSettings.dspTime - jingleStartDsp) + timelineOffset;
        }

        return timelineOffset;
    }

    private static bool HasPlaybackClipEnded(AudioSource source)
    {
        if (source == null || source.clip == null)
        {
            return false;
        }

        return !source.isPlaying && source.time >= source.clip.length - 0.02f;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void LogPerfectCrossingIfNeeded(
        SkillQtePlaybackContext playbackContext,
        NoteSchedule schedule,
        float songTime,
        AudioSource jingleSource,
        float judgmentX,
        QteTaikoNoteView noteView,
        float centerOffsetX)
    {
        if (schedule.LoggedPerfectCrossing || songTime < schedule.PerfectSec)
        {
            return;
        }

        schedule.LoggedPerfectCrossing = true;
        float audioTime = jingleSource != null ? jingleSource.time : -1f;
        float centerX = noteView.GetCenterAnchoredPosition().x;
        string skillId = playbackContext != null ? playbackContext.SkillId : "?";
        Debug.Log(
            $"[QteSync] skill={skillId} note={schedule.Index} " +
            $"songTime={songTime:F3} audioTime={audioTime:F3} perfectSec={schedule.PerfectSec:F3} " +
            $"centerX={centerX:F2} judgmentX={judgmentX:F2} centerOffsetX={centerOffsetX:F2}",
            this);
    }
#endif

    /// <summary>案A: 全ノート判定後に clip 終端まで待つ（UI 退場と並行）。</summary>
    public async UniTask WaitForPlaybackEndAsync(CancellationToken token)
    {
        AudioSource jingleSource = _playbackJingleSource;
        if (jingleSource == null)
        {
            return;
        }

        await UniTask.WaitUntil(
            () => GetSongTime(jingleSource, _playbackJingleStartDsp, _playbackTimelineOffset) >= _playbackEndSongTime
                || HasPlaybackClipEnded(jingleSource),
            cancellationToken: token,
            timing: PlayerLoopTiming.Update);
    }

    private bool TryHandlePassMiss(
        ActiveNote note,
        float x,
        float laneY,
        float spawnX,
        Sprite noteSprite,
        SkillQtePlaybackContext playbackContext,
        BattleSettingsSO battleSettings,
        QteJudgment[] results,
        bool[] noteResolved,
        CancellationToken token,
        ref int resolvedCount)
    {
        if (!_judgmentZones.HasPassedGoodZone(note.View.RectTransform))
        {
            return false;
        }

        ResolveNote(note, QteJudgment.Miss);
        results[note.Index] = note.Judgment;
        noteResolved[note.Index] = true;
        NotifyLiveMultiplier(note, noteSprite, results, noteResolved, battleSettings, token);
        ShowJudgmentAtNote(note, x, laneY, note.Judgment, token);
        resolvedCount++;
        LogHit(playbackContext, note.Index, note.Judgment, note.View.GetCenterAnchoredPosition().x);

        note.FadingOut = true;
        note.View.PlayPassMissFadeOut(_settings.MissFadeOutSeconds, () =>
        {
            note.FadingOut = false;
            ReturnActiveNote(note, spawnX, laneY);
        });
        return true;
    }

    private bool TryHandleLeftViewportMiss(
        ActiveNote note,
        float x,
        float laneY,
        float spawnX,
        float playViewportXMin,
        Sprite noteSprite,
        SkillQtePlaybackContext playbackContext,
        BattleSettingsSO battleSettings,
        QteJudgment[] results,
        bool[] noteResolved,
        CancellationToken token,
        ref int resolvedCount)
    {
        if (x >= playViewportXMin)
        {
            return false;
        }

        ResolveNote(note, QteJudgment.Miss);
        results[note.Index] = note.Judgment;
        noteResolved[note.Index] = true;
        NotifyLiveMultiplier(note, noteSprite, results, noteResolved, battleSettings, token);
        ShowJudgmentAtNote(note, x, laneY, note.Judgment, token);
        ReturnActiveNote(note, spawnX, laneY);
        resolvedCount++;
        LogHit(playbackContext, note.Index, note.Judgment, note.View.GetCenterAnchoredPosition().x);
        return true;
    }

    private void NotifyLiveMultiplier(
        ActiveNote note,
        Sprite noteSprite,
        QteJudgment[] results,
        bool[] noteResolved,
        BattleSettingsSO battleSettings,
        CancellationToken token)
    {
        if (_liveMultiplier == null || note?.View == null || battleSettings == null)
        {
            return;
        }

        if (note.Judgment == QteJudgment.Miss)
        {
            _liveMultiplier.OnMissJudgmentResolved(results, noteResolved, battleSettings);
            return;
        }

        if (noteSprite == null)
        {
            return;
        }

        RectTransform rt = note.View.RectTransform;
        QtePresenter.EnsureQteCanvasScale(rt);
        Vector3 world = rt.TransformPoint(rt.rect.center);
        bool hasAnchors = _liveMultiplier.TryComputeAbsorbAnchors(
            rt,
            world,
            out Vector2 anchoredStart,
            out Vector2 anchoredEnd);

        float product = QteOutcomeCalculator.ComputeProductMultiplier(results, battleSettings, noteResolved);
        var snapshot = new QteAbsorbFlightSnapshot(
            note.Index,
            note.Judgment,
            noteSprite,
            world,
            hasAnchors,
            anchoredStart,
            anchoredEnd);

        _liveMultiplier.LaunchAbsorbVisual(snapshot, product, token);
    }

    private void ReturnActiveNote(ActiveNote note, float spawnX, float laneY)
    {
        if (note == null || note.ReturnedToPool || note.View == null)
        {
            return;
        }

        note.View.KillFade();
        _view.ReturnNote(note.View, spawnX, laneY);
        note.ReturnedToPool = true;
    }

    private bool TrySelectNoteForTap(
        List<ActiveNote> activeNotes,
        float playViewportXMin,
        float playViewportXMax,
        out ActiveNote selected)
    {
        selected = null;
        BuildTapHandles(activeNotes);
        if (!QteTaikoSpatialJudgment.TrySelectNoteForTap(
                _tapNoteHandles,
                _judgmentZones,
                playViewportXMin,
                playViewportXMax,
                out QteTaikoSpatialJudgment.NoteHandle handle))
        {
            return false;
        }

        for (int i = 0; i < activeNotes.Count; i++)
        {
            ActiveNote note = activeNotes[i];
            if (note.Index == handle.Index && note.View == handle.View)
            {
                selected = note;
                return true;
            }
        }

        return false;
    }

    private void BuildTapHandles(List<ActiveNote> activeNotes)
    {
        _tapNoteHandles.Clear();
        for (int i = 0; i < activeNotes.Count; i++)
        {
            ActiveNote note = activeNotes[i];
            _tapNoteHandles.Add(new QteTaikoSpatialJudgment.NoteHandle(
                note.Index,
                note.SpawnSec,
                note.View,
                note.Resolved));
        }
    }

    private void ShowJudgmentAtNote(
        ActiveNote note,
        float anchoredX,
        float laneY,
        QteJudgment judgment,
        CancellationToken token,
        bool includeTapSound = false)
    {
        PlayQteInputSe(judgment, includeTapSound);

        if (_judgmentDisplay == null || note == null)
        {
            return;
        }

        _judgmentDisplay.Show(judgment, new Vector2(anchoredX, laneY), token);
    }

    private void PlayQteInputSe(QteJudgment judgment, bool includeTapSound)
    {
        if (judgment == QteJudgment.Perfect)
        {
            _perfectStreak++;
            if (includeTapSound)
            {
                if (!InGameSe.TryGetPlaybackPitch(InGameSeKey.QteTap, out float catalogBasePitch))
                {
                    catalogBasePitch = 1f;
                }

                float pitch = _settings.GetPerfectJudgmentPitch(_perfectStreak, catalogBasePitch);
                InGameSe.Play(InGameSeKey.QteTap, 1f, pitch);
            }

            return;
        }

        _perfectStreak = 0;
        if (includeTapSound)
        {
            if (!InGameSe.TryGetPlaybackPitch(InGameSeKey.QteTap, out float tapBasePitch))
            {
                tapBasePitch = 1f;
            }

            InGameSe.Play(InGameSeKey.QteTap, 1f, tapBasePitch);
        }

        InGameSe.Play(InGameSeKey.FromQteJudgment(judgment));
    }

    private static void ResolveNote(ActiveNote note, QteJudgment judgment)
    {
        note.Resolved = true;
        note.Judgment = judgment;
    }

    /// <summary>
    /// 検証失敗時などに全 Miss のリストを作る。
    /// </summary>
    public static List<QteJudgment> CreateAllMiss(SkillQtePlaybackContext playbackContext)
    {
        return FillMiss(playbackContext);
    }

    private static List<QteJudgment> FillMiss(SkillQtePlaybackContext playbackContext)
    {
        List<QteJudgment> list = new List<QteJudgment>();
        QtePointData[] timings = playbackContext?.QteTimings;
        if (timings == null)
        {
            return list;
        }

        for (int i = 0; i < timings.Length; i++)
        {
            list.Add(QteJudgment.Miss);
        }

        return list;
    }

    private void LogHit(SkillQtePlaybackContext playbackContext, int noteIndex, QteJudgment j, float noteCenterX)
    {
        if (_log == null || !_log.IsEnabled(InGameLogCategory.Qte))
        {
            return;
        }

        string skillId = playbackContext != null ? playbackContext.SkillId : "?";
        _log.Log(
            InGameLogCategory.Qte,
            $"system=TaikoScroll skill={skillId} note={noteIndex} judgment={j} centerX={noteCenterX:F2}");
    }
}
