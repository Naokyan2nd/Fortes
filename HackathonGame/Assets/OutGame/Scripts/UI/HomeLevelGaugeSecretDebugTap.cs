using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Counts rapid clicks on LevelGauge (root + child graphics); opens debug scene when threshold is met.
/// </summary>
public class HomeLevelGaugeSecretDebugTap : MonoBehaviour, IPointerClickHandler
{
    Action _onThresholdReached;
    int _requiredTapCount = 10;
    float _tapWindowSeconds = 2f;
    int _tapCount;
    float _windowStartTime = -1f;
    bool _enabled;

    public void Configure(bool enabled, int requiredTapCount, float tapWindowSeconds, Action onThresholdReached)
    {
        _enabled = enabled;
        _requiredTapCount = Mathf.Max(1, requiredTapCount);
        _tapWindowSeconds = Mathf.Max(0.1f, tapWindowSeconds);
        _onThresholdReached = onThresholdReached;
        ResetTapWindow();
        BindChildClickForwarders(enabled);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        RegisterTap();
    }

    public void RegisterTap()
    {
        if (!_enabled || _onThresholdReached == null)
        {
            return;
        }

        float now = Time.unscaledTime;
        if (_windowStartTime < 0f || now - _windowStartTime > _tapWindowSeconds)
        {
            _windowStartTime = now;
            _tapCount = 0;
        }

        _tapCount++;
        if (_tapCount < _requiredTapCount)
        {
            return;
        }

        ResetTapWindow();
        _onThresholdReached.Invoke();
    }

    void BindChildClickForwarders(bool enabled)
    {
        HomeLevelGaugeSecretDebugTapForwarder[] forwarders =
            GetComponentsInChildren<HomeLevelGaugeSecretDebugTapForwarder>(true);
        for (int i = 0; i < forwarders.Length; i++)
        {
            if (forwarders[i] != null)
            {
                Destroy(forwarders[i]);
            }
        }

        if (!enabled)
        {
            return;
        }

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null)
            {
                continue;
            }

            graphic.raycastTarget = true;
            if (graphic.gameObject == gameObject)
            {
                continue;
            }

            HomeLevelGaugeSecretDebugTapForwarder forwarder =
                graphic.gameObject.AddComponent<HomeLevelGaugeSecretDebugTapForwarder>();
            forwarder.Bind(this);
        }
    }

    void ResetTapWindow()
    {
        _tapCount = 0;
        _windowStartTime = -1f;
    }
}

sealed class HomeLevelGaugeSecretDebugTapForwarder : MonoBehaviour, IPointerClickHandler
{
    HomeLevelGaugeSecretDebugTap _target;

    public void Bind(HomeLevelGaugeSecretDebugTap target)
    {
        _target = target;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _target?.RegisterTap();
    }
}
