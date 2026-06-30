using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 太鼓 QTE タップヒットエフェクトのプール表示。
/// </summary>
public sealed class QteTaikoHitEffectDisplay : MonoBehaviour
{
    [SerializeField]
    private RectTransform _container;

    [SerializeField]
    private QteTaikoHitEffectView _prefab;

    [SerializeField]
    private int _poolPrewarmCount = 4;

    private readonly Stack<QteTaikoHitEffectView> _pool = new Stack<QteTaikoHitEffectView>();
    private readonly List<QteTaikoHitEffectView> _active = new List<QteTaikoHitEffectView>();

    private void Awake()
    {
        PrewarmPool();
    }

    /// <summary>
    /// タップ位置（コンテナローカル）でヒットエフェクトを再生する。
    /// </summary>
    public void Show(Vector2 anchoredPosition, Sprite sprite)
    {
        if (_container == null)
        {
            return;
        }

        QteTaikoHitEffectView view = Rent();
        if (view == null)
        {
            return;
        }

        RectTransform rt = view.transform as RectTransform;
        if (rt != null)
        {
            rt.SetParent(_container, false);
            rt.anchoredPosition = anchoredPosition;
            rt.localScale = Vector3.one;
        }

        view.ApplySprite(sprite);
        _active.Add(view);
        view.PlayHitEffect(() => ReturnActive(view));
    }

    /// <summary>
    /// QTE 終了時に進行中のエフェクトをすべてプールへ戻す。
    /// </summary>
    public void ClearAll()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            ReturnActive(_active[i]);
        }

        _active.Clear();
    }

    private void ReturnActive(QteTaikoHitEffectView view)
    {
        if (view == null)
        {
            return;
        }

        _active.Remove(view);
        ReturnToPool(view);
    }

    private void PrewarmPool()
    {
        if (_prefab == null || _container == null || _poolPrewarmCount <= 0)
        {
            return;
        }

        for (int i = _pool.Count; i < _poolPrewarmCount; i++)
        {
            QteTaikoHitEffectView instance = Instantiate(_prefab, _container);
            instance.ResetForPool();
            _pool.Push(instance);
        }
    }

    private QteTaikoHitEffectView Rent()
    {
        if (_prefab == null || _container == null)
        {
            return null;
        }

        if (_pool.Count > 0)
        {
            return _pool.Pop();
        }

        QteTaikoHitEffectView created = Instantiate(_prefab, _container);
        created.ResetForPool();
        return created;
    }

    private void ReturnToPool(QteTaikoHitEffectView view)
    {
        if (view == null)
        {
            return;
        }

        view.ResetForPool();
        _pool.Push(view);
    }
}
