using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// QTE 中のバトル HUD（プレイヤー HP・スキルボタン）の水平スライド退場・復帰。
/// </summary>
public sealed class BattleHudSlideView : MonoBehaviour
{
    private const string SlideTweenId = "BattleHudSlide";

    [SerializeField]
    private RectTransform _playerHpRoot;

    [SerializeField]
    private RectTransform[] _skillButtonRoots;

    [Header("Hide")]
    [SerializeField]
    private float _hideDuration = 0.25f;

    [SerializeField]
    private float _hpSlideOffsetX = -900f;

    [SerializeField]
    private float _hpHideExtraPaddingX = 48f;

    [SerializeField]
    private float _skillSlideOffsetX = 900f;

    [SerializeField]
    private Ease _hideEase = Ease.InQuad;

    [Header("Show")]
    [SerializeField]
    private float _showDuration = 0.3f;

    [SerializeField]
    private Ease _showEase = Ease.OutQuad;

    private Vector2 _hpRestPosition;
    private Vector2[] _skillRestPositions;
    private bool _hasCapturedRestPositions;
    private Sequence _activeSequence;

    /// <summary>HUD を画面外へ退場させる。</summary>
    public async UniTask PlayHideAsync(CancellationToken token)
    {
        if (!HasAnyTarget())
        {
            return;
        }

        CaptureRestPositions();
        PrepareRootsForSlide();

        if (_hideDuration <= 0f)
        {
            ApplyHiddenPositions();
            return;
        }

        await PlaySlideAsync(GetHiddenPositions(), _hideDuration, _hideEase, token);
    }

    /// <summary>HUD を通常位置へ復帰させる。</summary>
    public async UniTask PlayShowAsync(CancellationToken token)
    {
        if (!HasAnyTarget())
        {
            return;
        }

        if (!_hasCapturedRestPositions)
        {
            CaptureRestPositions();
        }

        if (_showDuration <= 0f)
        {
            ApplyRestPositions();
            return;
        }

        await PlaySlideAsync(GetRestPositions(), _showDuration, _showEase, token);
    }

    /// <summary>Tween 停止と rest 位置への即時復帰。</summary>
    public void KillAndReset()
    {
        KillTweens();

        if (!_hasCapturedRestPositions)
        {
            CaptureRestPositions();
        }

        ApplyRestPositions();
    }

    private bool HasAnyTarget()
    {
        if (_playerHpRoot != null)
        {
            return true;
        }

        if (_skillButtonRoots == null)
        {
            return false;
        }

        for (int i = 0; i < _skillButtonRoots.Length; i++)
        {
            if (_skillButtonRoots[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void CaptureRestPositions()
    {
        if (_playerHpRoot != null)
        {
            _hpRestPosition = _playerHpRoot.anchoredPosition;
        }

        if (_skillButtonRoots != null && _skillButtonRoots.Length > 0)
        {
            _skillRestPositions = new Vector2[_skillButtonRoots.Length];
            for (int i = 0; i < _skillButtonRoots.Length; i++)
            {
                RectTransform root = _skillButtonRoots[i];
                _skillRestPositions[i] = root != null ? root.anchoredPosition : Vector2.zero;
            }
        }

        _hasCapturedRestPositions = true;
    }

    private void PrepareRootsForSlide()
    {
        if (_playerHpRoot != null)
        {
            DOTween.Kill(_playerHpRoot, SlideTweenId);
        }

        if (_skillButtonRoots == null)
        {
            return;
        }

        for (int i = 0; i < _skillButtonRoots.Length; i++)
        {
            RectTransform root = _skillButtonRoots[i];
            if (root == null)
            {
                continue;
            }

            DOTween.Kill(root, SlideTweenId);
        }
    }

    private Vector2[] GetRestPositions()
    {
        int count = GetTargetCount();
        Vector2[] positions = new Vector2[count];
        int index = 0;

        if (_playerHpRoot != null)
        {
            positions[index++] = _hpRestPosition;
        }

        if (_skillButtonRoots != null)
        {
            for (int i = 0; i < _skillButtonRoots.Length; i++)
            {
                if (_skillButtonRoots[i] == null)
                {
                    continue;
                }

                positions[index++] = _skillRestPositions != null && i < _skillRestPositions.Length
                    ? _skillRestPositions[i]
                    : _skillButtonRoots[i].anchoredPosition;
            }
        }

        return positions;
    }

    private Vector2[] GetHiddenPositions()
    {
        Vector2[] rest = GetRestPositions();
        Vector2[] hidden = new Vector2[rest.Length];
        int index = 0;

        if (_playerHpRoot != null)
        {
            hidden[index++] = rest[index - 1] + new Vector2(ResolveHpHideOffsetX(), 0f);
        }

        if (_skillButtonRoots != null)
        {
            for (int i = 0; i < _skillButtonRoots.Length; i++)
            {
                if (_skillButtonRoots[i] == null)
                {
                    continue;
                }

                hidden[index] = rest[index] + new Vector2(_skillSlideOffsetX, 0f);
                index++;
            }
        }

        return hidden;
    }

    private void ApplyRestPositions()
    {
        Vector2[] positions = GetRestPositions();
        ApplyPositions(positions);
    }

    private void ApplyHiddenPositions()
    {
        Vector2[] positions = GetHiddenPositions();
        ApplyPositions(positions);
    }

    private void ApplyPositions(Vector2[] positions)
    {
        int index = 0;

        if (_playerHpRoot != null)
        {
            _playerHpRoot.anchoredPosition = positions[index++];
        }

        if (_skillButtonRoots == null)
        {
            return;
        }

        for (int i = 0; i < _skillButtonRoots.Length; i++)
        {
            RectTransform root = _skillButtonRoots[i];
            if (root == null)
            {
                continue;
            }

            root.anchoredPosition = positions[index++];
        }
    }

    private int GetTargetCount()
    {
        int count = 0;

        if (_playerHpRoot != null)
        {
            count++;
        }

        if (_skillButtonRoots == null)
        {
            return count;
        }

        for (int i = 0; i < _skillButtonRoots.Length; i++)
        {
            if (_skillButtonRoots[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private RectTransform[] GetTargets()
    {
        int count = GetTargetCount();
        RectTransform[] targets = new RectTransform[count];
        int index = 0;

        if (_playerHpRoot != null)
        {
            targets[index++] = _playerHpRoot;
        }

        if (_skillButtonRoots != null)
        {
            for (int i = 0; i < _skillButtonRoots.Length; i++)
            {
                if (_skillButtonRoots[i] != null)
                {
                    targets[index++] = _skillButtonRoots[i];
                }
            }
        }

        return targets;
    }

    private async UniTask PlaySlideAsync(
        Vector2[] targetPositions,
        float duration,
        Ease ease,
        CancellationToken token)
    {
        RectTransform[] targets = GetTargets();
        if (targets.Length == 0)
        {
            return;
        }

        KillTweens();

        bool completed = false;
        _activeSequence = DOTween.Sequence();
        _activeSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        _activeSequence.SetUpdate(true);

        for (int i = 0; i < targets.Length; i++)
        {
            RectTransform target = targets[i];
            if (target == null)
            {
                continue;
            }

            _activeSequence.Join(
                target
                    .DOAnchorPos(targetPositions[i], duration)
                    .SetEase(ease)
                    .SetId(SlideTweenId));
        }

        _activeSequence.OnComplete(() => completed = true);
        _activeSequence.OnKill(() => completed = true);

        await UniTask.WaitUntil(() => completed, cancellationToken: token);
    }

    /// <summary>
    /// 子要素の右端が親の左端より外に出るよう、ローカル bounds から退場量を算出する。
    /// </summary>
    private float ResolveHpHideOffsetX()
    {
        if (_playerHpRoot == null)
        {
            return _hpSlideOffsetX;
        }

        RectTransform parent = _playerHpRoot.parent as RectTransform;
        if (parent == null)
        {
            return _hpSlideOffsetX;
        }

        Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            _playerHpRoot,
            _playerHpRoot);
        Vector2 rest = _hasCapturedRestPositions ? _hpRestPosition : _playerHpRoot.anchoredPosition;
        float parentLeftX = -parent.rect.width * parent.pivot.x;
        float rightEdgeX = rest.x + bounds.max.x;
        float computedOffset = parentLeftX - _hpHideExtraPaddingX - rightEdgeX;

        return Mathf.Min(_hpSlideOffsetX, computedOffset);
    }

    private void KillTweens()
    {
        if (_activeSequence != null && _activeSequence.IsActive())
        {
            _activeSequence.Kill(false);
        }

        _activeSequence = null;

        if (_playerHpRoot != null)
        {
            DOTween.Kill(_playerHpRoot, SlideTweenId);
        }

        if (_skillButtonRoots == null)
        {
            return;
        }

        for (int i = 0; i < _skillButtonRoots.Length; i++)
        {
            if (_skillButtonRoots[i] != null)
            {
                DOTween.Kill(_skillButtonRoots[i], SlideTweenId);
            }
        }
    }
}
