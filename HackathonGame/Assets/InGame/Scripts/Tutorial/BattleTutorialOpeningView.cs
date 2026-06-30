using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// チュートリアル入場時の全画面黒パネル + 文言フェードイン。
/// </summary>
public sealed class BattleTutorialOpeningView : MonoBehaviour
{
    [SerializeField]
    private GameObject _root;

    [SerializeField]
    private CanvasGroup _rootGroup;

    [SerializeField]
    private Image _blackPanel;

    [SerializeField]
    private TMP_Text _bodyLabel;

    [Header("Copy")]
    [SerializeField]
    [TextArea(2, 4)]
    private string _bodyText =
        "あなたの現在地に、ノイズを感知しました。\n直ちに浄化を開始してください。";

    [Header("Timeline (seconds)")]
    [Tooltip("文言フェードイン前に黒画面のみを見せる時間。")]
    [SerializeField]
    private float _blackHoldDuration = 1f;

    [SerializeField]
    private float _bodyFadeInDuration = 0.4f;

    [Tooltip("文言表示後、オーバーレイを閉じるまでの待ち。")]
    [SerializeField]
    private float _holdDuration = 1.8f;

    [SerializeField]
    private Ease _fadeInEase = Ease.OutQuad;

    private Sequence _activeSequence;
    private bool _isAlive = true;

    private bool IsAlive => this != null && _isAlive;

    private void Awake()
    {
        ApplyCopy();
        PrepareForShow();
    }

    private void OnDestroy()
    {
        _isAlive = false;
        KillTweens();
    }

    public void Configure(
        GameObject root,
        CanvasGroup rootGroup,
        Image blackPanel,
        TMP_Text bodyLabel)
    {
        _root = root;
        _rootGroup = rootGroup;
        _blackPanel = blackPanel;
        _bodyLabel = bodyLabel;
        ApplyCopy();
        PrepareForShow();
    }

    /// <summary>入場直後の黒画面状態（文言は非表示）。</summary>
    public void PrepareForShow()
    {
        KillTweens();

        if (_root != null)
        {
            _root.SetActive(true);
        }

        if (_rootGroup != null)
        {
            _rootGroup.alpha = 1f;
            _rootGroup.blocksRaycasts = true;
            _rootGroup.interactable = false;
        }

        SetTextAlpha(_bodyLabel, 0f);
    }

    public async UniTask PlayAsync(CancellationToken token)
    {
        if (_root == null)
        {
            return;
        }

        ApplyCopy();
        PrepareForShow();

        KillTweens();

        bool completed = false;
        _activeSequence = DOTween.Sequence();
        _activeSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        _activeSequence.SetUpdate(true);

        if (_blackHoldDuration > 0f)
        {
            _activeSequence.AppendInterval(_blackHoldDuration);
        }

        if (_bodyLabel != null)
        {
            if (_bodyFadeInDuration > 0f)
            {
                _activeSequence.Append(
                    _bodyLabel.DOFade(1f, _bodyFadeInDuration).SetEase(_fadeInEase));
            }
            else
            {
                SetTextAlpha(_bodyLabel, 1f);
            }
        }

        if (_holdDuration > 0f)
        {
            _activeSequence.AppendInterval(_holdDuration);
        }

        _activeSequence.OnComplete(() => completed = true);
        _activeSequence.OnKill(() => completed = true);

        await UniTask.WaitUntil(() => completed, cancellationToken: token);

        if (token.IsCancellationRequested)
        {
            return;
        }

        HideAfterPlay();
    }

    public void AbortAndReset()
    {
        if (!IsAlive)
        {
            return;
        }

        KillTweens();
        PrepareForShow();
        if (_root != null)
        {
            _root.SetActive(false);
        }
    }

    private void HideAfterPlay()
    {
        if (_rootGroup != null)
        {
            _rootGroup.blocksRaycasts = false;
        }

        if (_root != null)
        {
            _root.SetActive(false);
        }
    }

    private void ApplyCopy()
    {
        if (_bodyLabel != null && !string.IsNullOrEmpty(_bodyText))
        {
            _bodyLabel.text = _bodyText;
        }
    }

    private void KillTweens()
    {
        if (_activeSequence != null && _activeSequence.IsActive())
        {
            _activeSequence.Kill();
        }

        _activeSequence = null;
        _bodyLabel?.DOKill();
    }

    private static void SetTextAlpha(TMP_Text text, float alpha)
    {
        if (text == null)
        {
            return;
        }

        Color c = text.color;
        c.a = alpha;
        text.color = c;
    }
}
