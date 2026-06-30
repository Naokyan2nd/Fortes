using UnityEngine;

/// <summary>
/// 太鼓 QTE の判定ポップアップ 1 種（Perfect / Good / Miss ごとに別プレハブ）。
/// </summary>
public sealed class QteTaikoJudgmentPopupView : MonoBehaviour
{
    public const string ShowTrigger = "Show";

    [SerializeField]
    private RectTransform _rectTransform;

    [SerializeField]
    private Animator _animator;

    /// <summary>RectTransform（プール配置用）。</summary>
    public RectTransform RectTransform
    {
        get
        {
            if (_rectTransform == null)
            {
                _rectTransform = transform as RectTransform;
            }

            return _rectTransform;
        }
    }

    private void Awake()
    {
        if (_rectTransform == null)
        {
            _rectTransform = transform as RectTransform;
        }
    }

    /// <summary>プール返却前のリセット。</summary>
    public void ResetForPool()
    {
        if (_animator != null)
        {
            _animator.Rebind();
            _animator.Update(0f);
        }

        gameObject.SetActive(false);
    }

    /// <summary>表示開始（同フレームで呼ぶ）。</summary>
    public void PlayShow()
    {
        gameObject.SetActive(true);

        if (_animator != null)
        {
            _animator.ResetTrigger(ShowTrigger);
            _animator.SetTrigger(ShowTrigger);
        }
    }
}
