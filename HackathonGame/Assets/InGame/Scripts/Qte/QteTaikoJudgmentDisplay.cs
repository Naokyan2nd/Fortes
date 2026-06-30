using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 太鼓 QTE の判定ポップアップ表示（判定別プレハブ・プール・Duration 返却）。
/// </summary>
public sealed class QteTaikoJudgmentDisplay : MonoBehaviour
{
    private sealed class ActiveEntry
    {
        public QteTaikoJudgmentPopupView Popup;
        public QteJudgment Judgment;
        public CancellationTokenSource LinkedCts;
    }

    [SerializeField]
    private RectTransform _container;

    [SerializeField]
    private QteTaikoJudgmentPopupView _perfectPrefab;

    [SerializeField]
    private QteTaikoJudgmentPopupView _goodPrefab;

    [FormerlySerializedAs("_badPrefab")]
    [SerializeField]
    private QteTaikoJudgmentPopupView _missPrefab;

    [SerializeField]
    private float _displayDuration = 0.8f;

    [SerializeField]
    private int _poolSizePerType = 4;

    [SerializeField]
    private Vector2 _positionOffset;

    private readonly Stack<QteTaikoJudgmentPopupView> _perfectPool = new Stack<QteTaikoJudgmentPopupView>();
    private readonly Stack<QteTaikoJudgmentPopupView> _goodPool = new Stack<QteTaikoJudgmentPopupView>();
    private readonly Stack<QteTaikoJudgmentPopupView> _missPool = new Stack<QteTaikoJudgmentPopupView>();
    private readonly List<ActiveEntry> _active = new List<ActiveEntry>();

    private void Awake()
    {
        PrewarmPool(_perfectPrefab, _perfectPool);
        PrewarmPool(_goodPrefab, _goodPool);
        PrewarmPool(_missPrefab, _missPool);
    }

    /// <summary>
    /// 判定確定と同フレームでポップアップを表示する。
    /// </summary>
    /// <param name="judgment">Perfect / Good / Miss。</param>
    /// <param name="anchoredPosition">NoteParent（コンテナ）ローカル座標。</param>
    /// <param name="qteToken">QTE 全体のキャンセルトークン。</param>
    public void Show(QteJudgment judgment, Vector2 anchoredPosition, CancellationToken qteToken)
    {
        if (_container == null)
        {
            return;
        }

        QteTaikoJudgmentPopupView popup = Rent(judgment);
        if (popup == null)
        {
            return;
        }

        RectTransform rt = popup.RectTransform;
        rt.SetParent(_container, false);
        rt.anchoredPosition = anchoredPosition + _positionOffset;
        popup.PlayShow();

        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(qteToken);
        ActiveEntry entry = new ActiveEntry
        {
            Popup = popup,
            Judgment = judgment,
            LinkedCts = linkedCts,
        };
        _active.Add(entry);
        ReturnAfterDurationAsync(entry).Forget();
    }

    /// <summary>
    /// QTE 終了時に進行中の表示をすべて破棄してプールへ戻す。
    /// </summary>
    public void ClearAll()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            ActiveEntry entry = _active[i];
            entry.LinkedCts?.Cancel();
            entry.LinkedCts?.Dispose();
            entry.LinkedCts = null;
            ReturnToPool(entry.Popup, entry.Judgment);
        }

        _active.Clear();
    }

    private async UniTaskVoid ReturnAfterDurationAsync(ActiveEntry entry)
    {
        try
        {
            int delayMs = Mathf.Max(0, Mathf.RoundToInt(_displayDuration * 1000f));
            await UniTask.Delay(delayMs, cancellationToken: entry.LinkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!_active.Remove(entry))
        {
            return;
        }

        entry.LinkedCts?.Dispose();
        entry.LinkedCts = null;
        ReturnToPool(entry.Popup, entry.Judgment);
    }

    private void PrewarmPool(QteTaikoJudgmentPopupView prefab, Stack<QteTaikoJudgmentPopupView> pool)
    {
        if (prefab == null || _container == null || _poolSizePerType <= 0)
        {
            return;
        }

        for (int i = pool.Count; i < _poolSizePerType; i++)
        {
            QteTaikoJudgmentPopupView instance = Instantiate(prefab, _container);
            instance.ResetForPool();
            pool.Push(instance);
        }
    }

    private QteTaikoJudgmentPopupView Rent(QteJudgment judgment)
    {
        QteTaikoJudgmentPopupView prefab = GetPrefab(judgment);
        Stack<QteTaikoJudgmentPopupView> pool = GetPool(judgment);
        if (prefab == null || pool == null)
        {
            return null;
        }

        if (pool.Count > 0)
        {
            return pool.Pop();
        }

        QteTaikoJudgmentPopupView created = Instantiate(prefab, _container);
        created.ResetForPool();
        return created;
    }

    private void ReturnToPool(QteTaikoJudgmentPopupView popup, QteJudgment judgment)
    {
        if (popup == null)
        {
            return;
        }

        popup.ResetForPool();
        Stack<QteTaikoJudgmentPopupView> pool = GetPool(judgment);
        pool?.Push(popup);
    }

    private QteTaikoJudgmentPopupView GetPrefab(QteJudgment judgment)
    {
        switch (judgment)
        {
            case QteJudgment.Perfect:
                return _perfectPrefab;
            case QteJudgment.Good:
                return _goodPrefab;
            default:
                return _missPrefab;
        }
    }

    private Stack<QteTaikoJudgmentPopupView> GetPool(QteJudgment judgment)
    {
        switch (judgment)
        {
            case QteJudgment.Perfect:
                return _perfectPool;
            case QteJudgment.Good:
                return _goodPool;
            default:
                return _missPool;
        }
    }
}
