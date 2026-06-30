using System;
using System.Collections.Generic;
using R3;
using R3.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 太鼓風 QTE の UI ルート（タップ受付・音符プール）。
/// </summary>
[DefaultExecutionOrder(-200)]
public sealed class QteTaikoView : MonoBehaviour, IPointerDownHandler
{
    private const float OffScreenSpawnMargin = 48f;

    [SerializeField]
    private RectTransform _noteParent;

    [SerializeField]
    private QteTaikoNoteView _notePrefab;

    [SerializeField]
    private Image _raycastCatcher;

    [SerializeField]
    private int _notePoolPrewarmCount = 5;

    private readonly Stack<QteTaikoNoteView> _pool = new Stack<QteTaikoNoteView>();
    private readonly Subject<double> _tapDspSubject = new Subject<double>();
    private bool _inputEnabled = true;

    /// <summary>タップ時の dsp 時刻ストリーム。</summary>
    public Observable<double> OnTapDsp => _tapDspSubject;

    private void Awake()
    {
        if (_raycastCatcher != null)
        {
            _raycastCatcher.raycastTarget = true;
        }

        PrewarmNotePool();
    }

    private void OnDestroy()
    {
        _tapDspSubject.Dispose();
    }

    /// <summary>
    /// タップを無効化（判定確定後など）。
    /// </summary>
    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
    }

    private void Update()
    {
        if (!_inputEnabled)
        {
            return;
        }

        if (WasSpacePressedThisFrame())
        {
            NotifyTap();
        }
    }

    private static bool WasSpacePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.spaceKey.wasPressedThisFrame;
        }
#endif
        return Input.GetKeyDown(KeyCode.Space);
    }

    /// <inheritdoc />
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_inputEnabled)
        {
            return;
        }

        NotifyTap();
    }

    private void NotifyTap()
    {
        _tapDspSubject.OnNext(AudioSettings.dspTime);
    }

    /// <summary>
    /// 判定対象とする水平範囲（NoteParent ローカル・中心 X）。
    /// </summary>
    public bool TryGetPlayViewportXBounds(out float xMin, out float xMax)
    {
        if (_noteParent == null)
        {
            xMin = float.NegativeInfinity;
            xMax = float.PositiveInfinity;
            return false;
        }

        Rect rect = _noteParent.rect;
        xMin = rect.xMin;
        xMax = rect.xMax;
        return true;
    }

    /// <summary>ノート中心がプレイ表示範囲内か（画面外右／左は false）。</summary>
    public bool IsNoteCenterInPlayViewport(float centerAnchoredX)
    {
        if (!TryGetPlayViewportXBounds(out float xMin, out float xMax))
        {
            return true;
        }

        return centerAnchoredX >= xMin && centerAnchoredX <= xMax;
    }

    /// <summary>
    /// 親 Rect の右端より外側（画面外）のスポーン X を返す。
    /// </summary>
    public float GetOffScreenSpawnX()
    {
        if (_noteParent == null)
        {
            return OffScreenSpawnMargin;
        }

        float noteHalfWidth = _notePrefab != null ? _notePrefab.NoteWidth * 0.5f : 0f;
        float parentHalfWidth = _noteParent.rect.width * 0.5f;
        return parentHalfWidth + noteHalfWidth + OffScreenSpawnMargin;
    }

    private void PrewarmNotePool()
    {
        if (_notePoolPrewarmCount <= 0 || _notePrefab == null || _noteParent == null)
        {
            if (_notePoolPrewarmCount > 0 && (_notePrefab == null || _noteParent == null))
            {
                Debug.LogError("[QteTaikoView] 音符 Prewarm に _notePrefab / _noteParent が必要です。", this);
            }

            return;
        }

        float offScreenX = GetOffScreenSpawnX();
        for (int i = _pool.Count; i < _notePoolPrewarmCount; i++)
        {
            QteTaikoNoteView instance = Instantiate(_notePrefab, _noteParent);
            instance.ResetForPool(offScreenX, 0f);
            _pool.Push(instance);
        }
    }

    /// <summary>
    /// プールから音符を取得する（非表示のまま。ActivateAt まで表示しない）。
    /// </summary>
    public QteTaikoNoteView RentNote(float offScreenX, float laneY)
    {
        QteTaikoNoteView note;
        if (_pool.Count > 0)
        {
            note = _pool.Pop();
        }
        else
        {
            note = Instantiate(_notePrefab, _noteParent);
            note.gameObject.SetActive(false);
        }

        note.ResetForPool(offScreenX, laneY);
        return note;
    }

    /// <summary>音符をプールへ戻す。</summary>
    public void ReturnNote(QteTaikoNoteView note, float offScreenX, float laneY)
    {
        if (note == null)
        {
            return;
        }

        note.ResetForPool(offScreenX, laneY);
        _pool.Push(note);
    }
}
