using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// チュートリアルステップ ID と、シーン上の Illustration 用 <see cref="Image"/> の対応。
/// スプライトは Image に事前設定し、表示時は <see cref="Image.enabled"/> のみ切り替える。
/// </summary>
[Serializable]
public sealed class BattleTutorialStepIllustrationBinding
{
    [SerializeField]
    private string _stepId;

    [SerializeField]
    private Image _image;

    public string StepId => _stepId;

    public Image Image => _image;
}
