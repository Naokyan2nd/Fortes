using UnityEngine;

[CreateAssetMenu(fileName = "OutgameBgmConfig", menuName = "Audio/Outgame BGM Config")]
public class OutgameBgmConfig : ScriptableObject
{
    [Tooltip("StageScene / ScanScene で再生する BGM")]
    public AudioClip stageAndScanMusic;
}
