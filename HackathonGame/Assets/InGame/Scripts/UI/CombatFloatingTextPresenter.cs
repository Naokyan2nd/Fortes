using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 戦闘 FloatingText のプール管理とワールド座標からのスポーン。
/// </summary>
public sealed class CombatFloatingTextPresenter : MonoBehaviour
{
    private sealed class ActiveEntry
    {
        public CombatFloatingTextView View;
        public CancellationTokenSource LinkedCts;
    }

    [SerializeField]
    private RectTransform _container;

    [SerializeField]
    private CombatFloatingTextView _prefab;

    [SerializeField]
    private int _poolPrewarmCount = 12;

    [SerializeField]
    private float _returnAfterSeconds = 0.8f;

    [SerializeField]
    private Vector3 _worldOffset = new Vector3(0f, 1.2f, 0f);

    [SerializeField]
    [Tooltip("Screen Space 時の WorldToScreen 用。World Space では未使用。未設定時は Main Camera。")]
    private Camera _worldCamera;

    private readonly Stack<CombatFloatingTextView> _pool = new Stack<CombatFloatingTextView>();
    private bool _warnedWorldToLocalFailed;
    private readonly List<ActiveEntry> _active = new List<ActiveEntry>();
    private Canvas _rootCanvas;

    private void Awake()
    {
        _rootCanvas = _container != null ? _container.GetComponentInParent<Canvas>() : null;
        PrewarmPool();
    }

    /// <summary>
    /// バトル終了時などに進行中の表示をすべてプールへ戻す。
    /// </summary>
    public void ClearAll()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            ActiveEntry entry = _active[i];
            entry.LinkedCts?.Cancel();
            entry.LinkedCts?.Dispose();
            entry.LinkedCts = null;
            ReturnViewToPool(entry.View);
        }

        _active.Clear();
    }

    /// <summary>
    /// ワールド座標に FloatingText を表示する。
    /// </summary>
    public void Show(CombatFloatingTextKind kind, int amount, Vector3 worldPosition)
    {
        if (!TryFormatText(kind, amount, out string text))
        {
            return;
        }

        ShowText(text, worldPosition);
    }

    /// <summary>QTE 確定倍率をプレイヤー上に表示する（×1.25 形式）。</summary>
    public void ShowQteMultiplier(float productMultiplier, Vector3 worldPosition)
    {
        ShowText(QteOutcomeCalculator.FormatProductMultiplierDisplay(productMultiplier), worldPosition);
    }

    /// <summary>任意テキストをワールド座標に FloatingText 表示する。</summary>
    public void ShowText(string text, Vector3 worldPosition)
    {
        if (_container == null || _prefab == null || string.IsNullOrEmpty(text))
        {
            return;
        }

        Vector3 spawnWorld = worldPosition + _worldOffset;
        CombatFloatingTextView view = Rent();
        if (view == null)
        {
            return;
        }

        RectTransform rt = view.RectTransform;
        rt.SetParent(_container, false);
        rt.SetAsLastSibling();

        if (UsesWorldSpaceCanvas())
        {
            view.PlayShowWorld(text, spawnWorld, OnViewRequestReturn);
        }
        else
        {
            if (!TryWorldToContainerLocal(spawnWorld, out Vector2 localPos))
            {
                WarnWorldToLocalFailedOnce(spawnWorld);
                ReturnViewToPool(view);
                return;
            }

            view.PlayShow(text, localPos, OnViewRequestReturn);
        }

        CancellationTokenSource cts = new CancellationTokenSource();
        ActiveEntry entry = new ActiveEntry
        {
            View = view,
            LinkedCts = cts,
        };
        _active.Add(entry);
        ReturnAfterDurationAsync(entry).Forget();
    }

    private bool UsesWorldSpaceCanvas()
    {
        if (_rootCanvas == null && _container != null)
        {
            _rootCanvas = _container.GetComponentInParent<Canvas>();
        }

        return _rootCanvas != null && _rootCanvas.renderMode == RenderMode.WorldSpace;
    }

    private void OnViewRequestReturn(CombatFloatingTextView view)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            ActiveEntry entry = _active[i];
            if (entry.View != view)
            {
                continue;
            }

            entry.LinkedCts?.Cancel();
            entry.LinkedCts?.Dispose();
            entry.LinkedCts = null;
            _active.RemoveAt(i);
            ReturnViewToPool(view);
            return;
        }
    }

    private async UniTaskVoid ReturnAfterDurationAsync(ActiveEntry entry)
    {
        try
        {
            int delayMs = Mathf.Max(0, Mathf.RoundToInt(_returnAfterSeconds * 1000f));
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
        ReturnViewToPool(entry.View);
    }

    private bool TryFormatText(CombatFloatingTextKind kind, int amount, out string text)
    {
        text = null;
        int magnitude = Mathf.Abs(amount);
        if (magnitude <= 0)
        {
            return false;
        }

        switch (kind)
        {
            case CombatFloatingTextKind.DamageToPlayer:
            case CombatFloatingTextKind.DamageToEnemy:
            case CombatFloatingTextKind.HealHp:
                text = magnitude.ToString();
                return true;
            default:
                return false;
        }
    }

    private bool TryWorldToContainerLocal(Vector3 worldPosition, out Vector2 localPoint)
    {
        localPoint = default;
        if (_container == null)
        {
            return false;
        }

        Camera worldCamera = ResolveWorldCamera();
        if (worldCamera == null)
        {
            return false;
        }

        Vector3 screen3 = worldCamera.WorldToScreenPoint(worldPosition);
        if (screen3.z < 0f)
        {
            return false;
        }

        Canvas canvas = _rootCanvas != null ? _rootCanvas : _container.GetComponentInParent<Canvas>();
        Camera eventCamera = null;
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            eventCamera = canvas.worldCamera != null ? canvas.worldCamera : worldCamera;
        }

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _container,
            screen3,
            eventCamera,
            out localPoint);
    }

    private Camera ResolveWorldCamera()
    {
        if (_worldCamera != null)
        {
            return _worldCamera;
        }

        Camera main = FindMainSceneCamera();
        if (main != null)
        {
            return main;
        }

        if (Camera.main != null)
        {
            return Camera.main;
        }

        Camera[] buffer = new Camera[8];
        int count = Camera.GetAllCameras(buffer);
        return count > 0 ? buffer[0] : null;
    }

    private static Camera FindMainSceneCamera()
    {
        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera camera in cameras)
        {
            if (camera == null || !camera.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (camera.gameObject.name == "Main Camera")
            {
                return camera;
            }
        }

        return null;
    }

    private void WarnWorldToLocalFailedOnce(Vector3 worldPosition)
    {
        if (_warnedWorldToLocalFailed)
        {
            return;
        }

        _warnedWorldToLocalFailed = true;
        Debug.LogWarning(
            $"[CombatFloatingText] ワールド座標を UI に変換できませんでした。world={worldPosition} " +
            $"worldCamera={ResolveWorldCamera()?.name ?? "null"} container={_container?.name}",
            this);
    }

    private void PrewarmPool()
    {
        if (_prefab == null || _container == null || _poolPrewarmCount <= 0)
        {
            return;
        }

        for (int i = _pool.Count; i < _poolPrewarmCount; i++)
        {
            CombatFloatingTextView instance = Instantiate(_prefab, _container);
            instance.ResetForPool();
            _pool.Push(instance);
        }
    }

    private CombatFloatingTextView Rent()
    {
        if (_prefab == null || _container == null)
        {
            return null;
        }

        if (_pool.Count > 0)
        {
            return _pool.Pop();
        }

        CombatFloatingTextView created = Instantiate(_prefab, _container);
        created.ResetForPool();
        return created;
    }

    private void ReturnViewToPool(CombatFloatingTextView view)
    {
        if (view == null)
        {
            return;
        }

        view.ResetForPool();
        _pool.Push(view);
    }
}
