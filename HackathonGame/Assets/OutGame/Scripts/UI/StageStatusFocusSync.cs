using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows the left StageStatus panel that matches the currently focused Noises child
/// (e.g. SuperRare -> StageStatusSuperRare, Normal_003 -> StageStatusNormal).
/// </summary>
[DisallowMultipleComponent]
public class StageStatusFocusSync : MonoBehaviour
{
    [SerializeField] StageNoiseSlotFocus slotFocus;
    [SerializeField] string statusPanelPrefix = "StageStatus";
    [SerializeField] Transform statusPanelSearchRoot;

    readonly Dictionary<string, GameObject> _statusPanelsByKey = new(StringComparer.Ordinal);
    string _activeStatusKey;

    public void Configure(StageNoiseSlotFocus focus, Transform searchRoot = null)
    {
        Unsubscribe();

        slotFocus = focus;
        if (searchRoot != null)
        {
            statusPanelSearchRoot = searchRoot;
        }

        RebuildPanelMap();
        HideAllPanels();
        Subscribe();
        ApplyFocusedNoise(slotFocus != null ? slotFocus.FocusedChild : null);
    }

    void OnEnable()
    {
        if (slotFocus == null)
        {
            slotFocus = FindAnyObjectByType<StageNoiseSlotFocus>();
        }

        if (_statusPanelsByKey.Count == 0)
        {
            RebuildPanelMap();
            HideAllPanels();
        }

        Subscribe();

        if (slotFocus != null)
        {
            ApplyFocusedNoise(slotFocus.FocusedChild);
        }
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void Subscribe()
    {
        if (slotFocus != null)
        {
            slotFocus.FocusedChildChanged += OnFocusedChildChanged;
        }
    }

    void Unsubscribe()
    {
        if (slotFocus != null)
        {
            slotFocus.FocusedChildChanged -= OnFocusedChildChanged;
        }
    }

    void OnFocusedChildChanged(RectTransform focusedChild)
    {
        ApplyFocusedNoise(focusedChild);
    }

    void RebuildPanelMap()
    {
        _statusPanelsByKey.Clear();

        Transform root = statusPanelSearchRoot;
        if (root == null)
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            root = canvas != null ? canvas.transform : null;
        }

        if (root == null)
        {
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (!TryParseStatusPanelKey(child.name, out string statusKey))
            {
                continue;
            }

            _statusPanelsByKey[statusKey] = child.gameObject;
        }
    }

    bool TryParseStatusPanelKey(string objectName, out string statusKey)
    {
        statusKey = null;

        if (string.IsNullOrEmpty(objectName)
            || !objectName.StartsWith(statusPanelPrefix, StringComparison.Ordinal)
            || objectName.Length <= statusPanelPrefix.Length)
        {
            return false;
        }

        statusKey = objectName.Substring(statusPanelPrefix.Length);
        return !string.IsNullOrEmpty(statusKey);
    }

    public static string ResolveStatusKeyFromNoiseName(string noiseObjectName)
    {
        if (string.IsNullOrEmpty(noiseObjectName))
        {
            return null;
        }

        if (noiseObjectName == "SuperRare" || noiseObjectName == "Rare")
        {
            return noiseObjectName;
        }

        if (noiseObjectName == "Normal" || noiseObjectName.StartsWith("Normal", StringComparison.Ordinal))
        {
            return "Normal";
        }

        return noiseObjectName;
    }

    void ApplyFocusedNoise(RectTransform focusedChild)
    {
        string statusKey = focusedChild != null
            ? ResolveStatusKeyFromNoiseName(focusedChild.name)
            : null;

        ApplyStatusKey(statusKey);
    }

    public void ApplyStatusKey(string statusKey)
    {
        if (statusKey == _activeStatusKey)
        {
            return;
        }

        _activeStatusKey = statusKey;
        HideAllPanels();

        if (statusKey != null
            && _statusPanelsByKey.TryGetValue(statusKey, out GameObject panel)
            && panel != null)
        {
            panel.SetActive(true);
        }
    }

    void HideAllPanels()
    {
        foreach (KeyValuePair<string, GameObject> entry in _statusPanelsByKey)
        {
            if (entry.Value != null)
            {
                entry.Value.SetActive(false);
            }
        }
    }
}
