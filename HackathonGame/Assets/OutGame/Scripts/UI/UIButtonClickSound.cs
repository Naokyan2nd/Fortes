using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plays the shared out-game UI button click sound via <see cref="Button.onClick"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class UIButtonClickSound : MonoBehaviour
{
    Button _button;

    void Awake()
    {
        _button = GetComponent<Button>();
    }

    void OnEnable()
    {
        BindClickSound();
    }

    void OnDisable()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClicked);
        }
    }

    void OnButtonClicked()
    {
        OutGameUiButtonClickSound.Play();
    }

    void BindClickSound()
    {
        if (_button == null)
        {
            _button = GetComponent<Button>();
        }

        if (_button == null)
        {
            return;
        }

        _button.onClick.RemoveListener(OnButtonClicked);
        _button.onClick.AddListener(OnButtonClicked);
    }

    public static void EnsureOnButton(Button button)
    {
        if (button == null)
        {
            return;
        }

        UIButtonClickSound clickSound = button.GetComponent<UIButtonClickSound>();
        if (clickSound == null)
        {
            button.gameObject.AddComponent<UIButtonClickSound>();
            return;
        }

        clickSound.BindClickSound();
    }
}
