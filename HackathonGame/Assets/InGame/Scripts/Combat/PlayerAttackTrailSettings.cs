using DG.Tweening;
using UnityEngine;

/// <summary>
/// プレイヤー単体攻撃の Trail 1 本（直進）の演出パラメータ（SkillDataSO から参照）。
/// </summary>
[System.Serializable]
public sealed class PlayerAttackTrailSettings
{
    [SerializeField]
    [Tooltip("飛ばす Trail VFX プレハブ（Play On Awake 推奨）。")]
    private GameObject _trailPrefab;

    [SerializeField]
    [Min(0.1f)]
    [Tooltip("飛行時間の算出用（発射点→着弾点の直線距離 ÷ Speed）。")]
    private float _speed = 8f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("飛行時間の下限（秒）。")]
    private float _durationMin = 0.15f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("飛行時間の上限（秒）。")]
    private float _durationMax = 1.2f;

    [SerializeField]
    [Tooltip("進行度（0→1）に応じた直進移動のイージング。")]
    private Ease _pathEase = Ease.Linear;

    [SerializeField]
    [Tooltip("Trail のスケール乱数の最小値。")]
    private float _scaleMin = 0.8f;

    [SerializeField]
    [Tooltip("Trail のスケール乱数の最大値。")]
    private float _scaleMax = 1.2f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("着弾後、パーティクルを Stop して Destroy するまでの待ち（秒）。")]
    private float _destroyDelay = 0.15f;

    [SerializeField]
    [Tooltip("敵着弾時に CombatHitPoint の子へ生成。再生はプレハブの Play On Awake に任せる。")]
    private GameObject _hitVfxPrefab;

    [SerializeField]
    [Min(0f)]
    [Tooltip("着弾 VFX を Stop して Destroy するまでの待ち（秒）。Delay の後に Stop する。")]
    private float _hitVfxDestroyDelay = 1.2f;

    public GameObject TrailPrefab => _trailPrefab;

    public float Speed => _speed;

    public float DurationMin => _durationMin;

    public float DurationMax => _durationMax;

    public float ComputeFlightDuration(Vector3 emitWorld, Vector3 targetWorld)
    {
        float distance = Vector3.Distance(emitWorld, targetWorld);
        float duration = distance > 0.001f ? distance / _speed : _durationMin;
        if (_durationMax > 0f)
        {
            duration = Mathf.Min(duration, _durationMax);
        }

        if (_durationMin > 0f)
        {
            duration = Mathf.Max(duration, _durationMin);
        }

        return Mathf.Max(0.05f, duration);
    }

    public Ease PathEase => _pathEase;

    public float ScaleMin => _scaleMin;

    public float ScaleMax => _scaleMax;

    public float DestroyDelay => _destroyDelay;

    public GameObject HitVfxPrefab => _hitVfxPrefab;

    public float HitVfxDestroyDelay => _hitVfxDestroyDelay;

    public bool IsConfigured => _trailPrefab != null;
}
