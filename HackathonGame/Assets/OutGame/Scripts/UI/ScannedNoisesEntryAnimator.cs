using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reveals each direct child one by one in hierarchy order (fade + scale pop-in).
/// </summary>
[DisallowMultipleComponent]
public class ScannedNoisesEntryAnimator : MonoBehaviour
{
    [SerializeField] float elementStartInterval = 0.06f;
    [SerializeField] float elementRevealDuration = 0.2f;
    [SerializeField] [Range(0.5f, 1f)] float elementScaleFrom = 0.88f;
    [SerializeField] [Range(1f, 6f)] float easeOutPower = 3f;

    readonly List<ScannedNoiseElement> _elements = new();
    readonly Dictionary<int, Color> _graphicRestColors = new();
    int _activeElementCount = -1;

    public int ElementCount => _elements.Count;
    public int ActiveElementCount => GetActiveElementCount();

    void Awake()
    {
        EnsureElementsBuilt();
        HideAllElements();
    }

    public void ApplySettings(float startInterval, float revealDuration, float scaleFrom, float easeOut)
    {
        elementStartInterval = Mathf.Max(0f, startInterval);
        elementRevealDuration = Mathf.Max(0f, revealDuration);
        elementScaleFrom = Mathf.Clamp(scaleFrom, 0.5f, 1f);
        easeOutPower = Mathf.Clamp(easeOut, 1f, 6f);
    }

    /// <summary>
    /// Limits visible elements to the first N direct children in hierarchy order (top to bottom).
    /// </summary>
    public void ConfigureActiveElementCount(int count)
    {
        EnsureElementsBuilt();
        _activeElementCount = Mathf.Max(0, count);
        ApplyActiveElementVisibility();
    }

    public IEnumerator PlayEntry()
    {
        yield return PlayEntrySequence();
    }

    public string GetElementName(int index)
    {
        if (index < 0 || index >= _elements.Count)
        {
            return null;
        }

        return _elements[index].Rect.name;
    }

    public int FindElementIndexByName(string elementName)
    {
        if (string.IsNullOrEmpty(elementName))
        {
            return -1;
        }

        for (int i = 0; i < _elements.Count; i++)
        {
            if (_elements[i].Rect.name == elementName)
            {
                return i;
            }
        }

        return -1;
    }

    public IEnumerator RevealElementAt(int index)
    {
        if (!TryGetActiveElement(index, out ScannedNoiseElement element))
        {
            yield break;
        }

        yield return RevealElement(element);
    }

    public void HideElementAt(int index)
    {
        if (!TryGetActiveElement(index, out ScannedNoiseElement element))
        {
            return;
        }

        HideElement(element);
    }

    public void HideAllElements()
    {
        if (!EnsureElementsBuilt())
        {
            return;
        }

        int activeCount = GetActiveElementCount();
        for (int i = 0; i < activeCount; i++)
        {
            HideElement(_elements[i]);
        }
    }

    public void SetElementScaleX(int index, float scaleXMultiplier)
    {
        if (!TryGetActiveElement(index, out ScannedNoiseElement element))
        {
            return;
        }

        Vector3 scale = element.RestScale;
        scale.x = element.RestScale.x * scaleXMultiplier;
        element.Rect.localScale = scale;
    }

    public void SetElementUniformScale(int index, float scaleMultiplier)
    {
        if (!TryGetActiveElement(index, out ScannedNoiseElement element))
        {
            return;
        }

        element.Rect.localScale = element.RestScale * scaleMultiplier;
    }

    public void RestoreElementScale(int index)
    {
        if (!TryGetActiveElement(index, out ScannedNoiseElement element))
        {
            return;
        }

        element.Rect.localScale = element.RestScale;
    }

    public void SetElementAlpha(int index, float alpha)
    {
        if (!TryGetActiveElement(index, out ScannedNoiseElement element))
        {
            return;
        }

        SetGraphicsAlpha(element.Graphics, alpha);
    }

    public void SetElementPulseVisual(int index, float scaleMultiplier, float yStretchMultiplier, float alpha, float highlightBlend)
    {
        if (!TryGetActiveElement(index, out ScannedNoiseElement element))
        {
            return;
        }
        Vector3 scale = element.RestScale * scaleMultiplier;
        scale.y *= yStretchMultiplier;
        element.Rect.localScale = scale;
        ApplyElementHighlight(element, alpha, highlightBlend);
    }

    public void RestoreElementVisuals(int index)
    {
        if (!TryGetActiveElement(index, out ScannedNoiseElement element))
        {
            return;
        }

        RestoreElementScale(index);
        RestoreElementGraphics(element);
    }

    IEnumerator PlayEntrySequence()
    {
        if (!EnsureElementsBuilt())
        {
            yield break;
        }

        HideAllElements();

        int activeCount = GetActiveElementCount();
        for (int i = 0; i < activeCount; i++)
        {
            yield return RevealElement(_elements[i]);

            if (i < activeCount - 1 && elementStartInterval > 0f)
            {
                yield return new WaitForSeconds(elementStartInterval);
            }
        }
    }

    int GetActiveElementCount()
    {
        if (_activeElementCount < 0)
        {
            return _elements.Count;
        }

        return Mathf.Clamp(_activeElementCount, 0, _elements.Count);
    }

    bool TryGetActiveElement(int index, out ScannedNoiseElement element)
    {
        element = null;
        if (!EnsureElementsBuilt() || index < 0 || index >= GetActiveElementCount())
        {
            return false;
        }

        element = _elements[index];
        return true;
    }

    void ApplyActiveElementVisibility()
    {
        int activeCount = GetActiveElementCount();
        for (int i = 0; i < _elements.Count; i++)
        {
            bool isActive = i < activeCount;
            _elements[i].Rect.gameObject.SetActive(isActive);
        }
    }

    bool EnsureElementsBuilt()
    {
        if (_elements.Count > 0)
        {
            return true;
        }

        return BuildElements();
    }

    bool BuildElements()
    {
        _elements.Clear();

        if (transform.childCount == 0)
        {
            return false;
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            if (transform.GetChild(i) is not RectTransform elementRect)
            {
                continue;
            }

            List<GraphicAlphaState> graphics = CollectGraphics(elementRect, includeDescendants: true);
            if (graphics.Count == 0)
            {
                continue;
            }

            _elements.Add(new ScannedNoiseElement
            {
                Rect = elementRect,
                RestScale = elementRect.localScale,
                Graphics = graphics,
            });
        }

        return _elements.Count > 0;
    }

    void HideElement(ScannedNoiseElement element)
    {
        SetGraphicsAlpha(element.Graphics, 0f);
        element.Rect.localScale = element.RestScale * elementScaleFrom;
    }

    IEnumerator RevealElement(ScannedNoiseElement element)
    {
        float duration = Mathf.Max(0f, elementRevealDuration);
        Vector3 startScale = element.RestScale * elementScaleFrom;
        Vector3 endScale = element.RestScale;

        if (duration <= 0f)
        {
            element.Rect.localScale = endScale;
            SetGraphicsAlpha(element.Graphics, 1f);
            yield break;
        }

        SetGraphicsAlpha(element.Graphics, 0f);
        element.Rect.localScale = startScale;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EvaluateEaseOut(Mathf.Clamp01(elapsed / duration));
            element.Rect.localScale = Vector3.LerpUnclamped(startScale, endScale, t);
            SetGraphicsAlpha(element.Graphics, t);
            yield return null;
        }

        element.Rect.localScale = endScale;
        SetGraphicsAlpha(element.Graphics, 1f);
    }

    float EvaluateEaseOut(float normalizedTime)
    {
        normalizedTime = Mathf.Clamp01(normalizedTime);
        return 1f - Mathf.Pow(1f - normalizedTime, easeOutPower);
    }

    Color GetGraphicRestColor(Graphic graphic)
    {
        int id = graphic.GetInstanceID();
        if (_graphicRestColors.TryGetValue(id, out Color cached))
        {
            return cached;
        }

        Color rest = graphic.color;
        if (rest.a < 0.001f)
        {
            rest.a = 1f;
        }

        _graphicRestColors[id] = rest;
        return rest;
    }

    List<GraphicAlphaState> CollectGraphics(Transform root, bool includeDescendants)
    {
        var states = new List<GraphicAlphaState>();
        if (includeDescendants)
        {
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                states.Add(new GraphicAlphaState(graphic, GetGraphicRestColor(graphic)));
            }
        }
        else
        {
            foreach (Graphic graphic in root.GetComponents<Graphic>())
            {
                states.Add(new GraphicAlphaState(graphic, GetGraphicRestColor(graphic)));
            }
        }

        return states;
    }

    void SetGraphicsAlpha(IReadOnlyList<GraphicAlphaState> states, float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        for (int i = 0; i < states.Count; i++)
        {
            GraphicAlphaState state = states[i];
            if (state.Graphic == null)
            {
                continue;
            }

            Color color = state.RestColor;
            color.a = state.RestColor.a * alpha;
            state.Graphic.color = color;
        }
    }

    void ApplyElementHighlight(ScannedNoiseElement element, float alpha, float highlightBlend)
    {
        highlightBlend = Mathf.Clamp01(highlightBlend);
        alpha = Mathf.Clamp01(alpha);

        for (int i = 0; i < element.Graphics.Count; i++)
        {
            GraphicAlphaState state = element.Graphics[i];
            if (state.Graphic == null)
            {
                continue;
            }

            Color boosted = BoostColorForPulse(state.RestColor);
            Color color = Color.Lerp(state.RestColor, boosted, highlightBlend);
            color.a = state.RestColor.a * alpha;
            state.Graphic.color = color;
        }
    }

    void RestoreElementGraphics(ScannedNoiseElement element)
    {
        for (int i = 0; i < element.Graphics.Count; i++)
        {
            GraphicAlphaState state = element.Graphics[i];
            if (state.Graphic == null)
            {
                continue;
            }

            state.Graphic.color = state.RestColor;
        }
    }

    static Color BoostColorForPulse(Color rest)
    {
        const float brightnessBoost = 1.38f;
        Color scanTint = new Color(0.72f, 0.94f, 1f, rest.a);
        Color bright = new Color(
            Mathf.Clamp01(rest.r * brightnessBoost),
            Mathf.Clamp01(rest.g * brightnessBoost),
            Mathf.Clamp01(rest.b * brightnessBoost),
            rest.a);
        return Color.Lerp(bright, scanTint, 0.42f);
    }

    sealed class ScannedNoiseElement
    {
        public RectTransform Rect;
        public Vector3 RestScale;
        public List<GraphicAlphaState> Graphics;
    }

    sealed class GraphicAlphaState
    {
        public readonly Graphic Graphic;
        public readonly Color RestColor;

        public GraphicAlphaState(Graphic graphic, Color restColor)
        {
            Graphic = graphic;
            RestColor = restColor;
        }
    }
}
