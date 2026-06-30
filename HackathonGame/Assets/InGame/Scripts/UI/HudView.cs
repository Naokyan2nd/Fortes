using TMPro;
using UnityEngine;

/// <summary>
/// プレイヤーHP（Slider）の表示。
/// </summary>
public sealed class HudView : MonoBehaviour
{
    [SerializeField]
    private HpReactiveSliderBinder _playerHpSlider;

    [SerializeField]
    private TextMeshProUGUI _hpText;

    private void Awake()
    {
        if (_playerHpSlider == null)
        {
            Debug.LogError("[HudView] _playerHpSlider が未設定です。", this);
        }

        if (_hpText == null)
        {
            Debug.LogError("[HudView] _hpText が未設定です。", this);
        }
    }

    private void OnDestroy()
    {
        ClearBindings();
    }

    /// <summary>
    /// プレイヤーモデルにバインドする。
    /// </summary>
    /// <param name="player">モデル。</param>
    public void Bind(PlayerModel player)
    {
        ClearBindings();
        if (player == null)
        {
            return;
        }

        if (_playerHpSlider != null)
        {
            _playerHpSlider.Bind(player.CurrentHp, player.MaxHp, _hpText);
        }
    }

    /// <summary>
    /// HP表示をモデル値へ補間する（即時スナップは行わない）。
    /// </summary>
    /// <param name="player">モデル。</param>
    public void Refresh(PlayerModel player)
    {
        if (player == null || _playerHpSlider == null)
        {
            return;
        }

        _playerHpSlider.TweenTo(player.CurrentHp.Value, player.MaxHp.Value);
    }

    private void ClearBindings()
    {
        if (_playerHpSlider != null)
        {
            _playerHpSlider.Unbind();
        }
    }
}
