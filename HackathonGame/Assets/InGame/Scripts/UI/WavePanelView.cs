using TMPro;
using UnityEngine;

/// <summary>
/// 常時表示のウェーブ数（WAVE current/total）。
/// </summary>
public sealed class WavePanelView : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI _waveText;

    [SerializeField]
    private string _format = "WAVE    {0}/{1}";

    private void Awake()
    {
        if (_waveText == null)
        {
            Debug.LogError("[WavePanelView] _waveText が未設定です。", this);
        }
    }

    /// <summary>ウェーブパネルの表示／非表示を切り替える。</summary>
    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    /// <summary>
    /// ウェーブ表示を更新する。
    /// </summary>
    /// <param name="waveIndex">0始まりの現在ウェーブインデックス。</param>
    /// <param name="totalWaves">総ウェーブ数。</param>
    public void Refresh(int waveIndex, int totalWaves)
    {
        if (_waveText == null)
        {
            return;
        }

        if (totalWaves <= 0)
        {
            _waveText.text = string.Format(_format, 0, 0);
            return;
        }

        int current = Mathf.Clamp(waveIndex + 1, 1, totalWaves);
        _waveText.text = string.Format(_format, current, totalWaves);
    }
}
