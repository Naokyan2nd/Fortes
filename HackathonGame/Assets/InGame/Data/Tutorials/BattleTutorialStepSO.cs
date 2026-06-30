using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// バトルチュートリアル1ステップ分の表示内容。
/// </summary>
[CreateAssetMenu(fileName = "BattleTutorialStep", menuName = "InGame/Battle Tutorial Step")]
public sealed class BattleTutorialStepSO : ScriptableObject
{
    [SerializeField]
    private string _stepId;

    [SerializeField]
    private WaveTutorialMoment _moment;

    [SerializeField]
    private string _title;

    [SerializeField]
    [TextArea(4, 12)]
    private string _body;

    [Header("Media")]
    [SerializeField]
    private BattleTutorialIllustrationKind _illustrationKind = BattleTutorialIllustrationKind.Illustration;

    [SerializeField]
    private VideoClip _loopVideo;

    public string StepId => _stepId;

    public WaveTutorialMoment Moment => _moment;

    public string Title => _title;

    public string Body => _body;

    public BattleTutorialIllustrationKind IllustrationKind => _illustrationKind;

    public VideoClip LoopVideo => _loopVideo;

    public bool UsesIllustration => _illustrationKind == BattleTutorialIllustrationKind.Illustration;

    public bool UsesVideo => _illustrationKind == BattleTutorialIllustrationKind.Video;
}
