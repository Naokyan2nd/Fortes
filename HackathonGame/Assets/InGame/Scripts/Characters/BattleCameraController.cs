using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// バトル中の Cinemachine Camera を Priority で切り替え、プレイヤー／敵へフォーカスする。
/// </summary>
public sealed class BattleCameraController : MonoBehaviour
{
    private const int EnemySlotCount = 3;

    [SerializeField]
    private CinemachineCamera _defaultCamera;

    [SerializeField]
    private CinemachineCamera _playerCamera;

    [SerializeField]
    [Tooltip("プレイヤー追従用。シーン上の Player をドラッグして指定する。")]
    private Transform _playerFollowTarget;

    [SerializeField]
    private CinemachineCamera[] _enemyCameras = new CinemachineCamera[EnemySlotCount];

    [SerializeField]
    private int _inactivePriority = 0;

    [SerializeField]
    private int _activePriority = 10;

    [Header("Damage camera shake (Cinemachine Impulse)")]
    [SerializeField]
    [Tooltip("BattleCameraRig 上の Cinemachine Impulse Source。未設定時は同一 GameObject から取得。")]
    private CinemachineImpulseSource _impulseSource;

    [SerializeField]
    [Tooltip("各軸の揺れの強さ（絶対値）の最小。符号は毎回ランダム。0付近の弱い揺れを避けるため実行時に最大値の一定割合未満へクランプする。")]
    private Vector3 _damageShakeMagnitudeMin = new Vector3(0.7f, 0.15f, 0f);

    [SerializeField]
    [Tooltip("各軸の揺れの強さ（絶対値）の最大。揺れの持続・減衰は Impulse Source の Impulse Definition で調整。")]
    private Vector3 _damageShakeMagnitudeMax = new Vector3(1f, 0.35f, 0f);

    [SerializeField]
    [Range(0.05f, 1f)]
    [Tooltip("最小絶対値が最大絶対値の何割未満にならないか（例: 0.2 なら最大1.0のとき最小0.2以上）。")]
    private float _damageShakeMinMagnitudeRatio = 0.2f;

    [Header("Attack hit camera shake")]
    [SerializeField]
    [Range(0f, 2f)]
    [Tooltip("プレイヤー攻撃ヒット時の揺れ強さ。被弾時 Min/Max にこの倍率を掛ける（既定 0.5 = 半分）。")]
    private float _attackHitShakeStrengthScale = 0.5f;

    [Header("Default camera blend")]
    [SerializeField]
    [Tooltip("FocusDefault 後、Cinemachine のブレンド完了待ち（Brain DefaultBlend.Time に合わせる）。")]
    private float _defaultBlendWaitSeconds = 0.35f;

    private void Awake()
    {
        if (_impulseSource == null)
        {
            _impulseSource = GetComponent<CinemachineImpulseSource>();
        }
        if (_defaultCamera == null)
        {
            Debug.LogError("[BattleCameraController] CM_Default が未設定です。", this);
        }

        if (_playerCamera == null)
        {
            Debug.LogError("[BattleCameraController] CM_Player が未設定です。", this);
        }

        if (_playerFollowTarget == null)
        {
            Debug.LogError("[BattleCameraController] Player Follow Target が未設定です。", this);
        }
        else
        {
            ApplyPlayerFollowTarget();
        }

        if (_enemyCameras == null || _enemyCameras.Length < EnemySlotCount)
        {
            Debug.LogError("[BattleCameraController] CM_Enemy 配列が未設定または不足しています。", this);
        }
    }

    /// <summary>
    /// ウェーブ開始後に敵 Follow を割り当てる（敵は Instantiate されるためランタイム更新が必要）。
    /// </summary>
    public void BindEnemyTargets(Transform[] enemyTransforms)
    {
        if (_enemyCameras == null || enemyTransforms == null)
        {
            return;
        }

        int count = Mathf.Min(_enemyCameras.Length, enemyTransforms.Length);
        for (int i = 0; i < count; i++)
        {
            CinemachineCamera enemyCamera = _enemyCameras[i];
            Transform enemy = enemyTransforms[i];
            if (enemyCamera == null)
            {
                continue;
            }

            enemyCamera.Follow = enemy;
            enemyCamera.Target.TrackingTarget = enemy;
        }
    }

    private void ApplyPlayerFollowTarget()
    {
        if (_playerCamera == null || _playerFollowTarget == null)
        {
            return;
        }

        _playerCamera.Follow = _playerFollowTarget;
        _playerCamera.Target.TrackingTarget = _playerFollowTarget;
    }

    /// <summary>全体ショット（コマンド未選択・QTE前など）。</summary>
    public void FocusDefault()
    {
        ActivateOnly(_defaultCamera);
    }

    /// <summary>
    /// 全体ショットへ切り替え、ブレンド完了まで待機する。
    /// </summary>
    public async UniTask FocusDefaultAsync(CancellationToken token)
    {
        FocusDefault();
        float wait = Mathf.Max(0f, _defaultBlendWaitSeconds);
        if (wait > 0f)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(wait), cancellationToken: token);
        }
    }

    /// <summary>プレイヤー寄り（HP/SP 回復スキル選択時）。</summary>
    public void FocusPlayer()
    {
        ActivateOnly(_playerCamera);
    }

    /// <summary>
    /// プレイヤー被弾時のカメラシェイク。Impulse Definition の Duration / Dissipation は Source 側で調整。
    /// </summary>
    public void PlayDamageShake()
    {
        GenerateShakeImpulse(_damageShakeMagnitudeMin, _damageShakeMagnitudeMax);
    }

    /// <summary>
    /// プレイヤー攻撃ヒット時のカメラシェイク（被弾強度 × <see cref="_attackHitShakeStrengthScale"/>）。
    /// </summary>
    public void PlayAttackHitShake()
    {
        float scale = Mathf.Max(0f, _attackHitShakeStrengthScale);
        if (scale < 0.0001f)
        {
            return;
        }

        GenerateShakeImpulse(_damageShakeMagnitudeMin * scale, _damageShakeMagnitudeMax * scale);
    }

    private void GenerateShakeImpulse(Vector3 magnitudeMin, Vector3 magnitudeMax)
    {
        if (_impulseSource == null)
        {
            return;
        }

        Vector3 velocity = SampleRandomShakeVelocity(magnitudeMin, magnitudeMax);
        if (velocity.sqrMagnitude < 0.0001f)
        {
            return;
        }

        _impulseSource.GenerateImpulseWithVelocity(velocity);
    }

    /// <summary>
    /// 軸ごとに「絶対値の Min〜Max」を抽選し、符号をランダムに付ける（Uniform(-1,1) は使わない）。
    /// </summary>
    private Vector3 SampleRandomShakeVelocity(Vector3 magnitudeMin, Vector3 magnitudeMax)
    {
        return new Vector3(
            SampleRandomAxisMagnitude(magnitudeMin.x, magnitudeMax.x),
            SampleRandomAxisMagnitude(magnitudeMin.y, magnitudeMax.y),
            SampleRandomAxisMagnitude(magnitudeMin.z, magnitudeMax.z));
    }

    private float SampleRandomAxisMagnitude(float magnitudeMin, float magnitudeMax)
    {
        float min = Mathf.Min(magnitudeMin, magnitudeMax);
        float max = Mathf.Max(magnitudeMin, magnitudeMax);
        if (max < 0.0001f)
        {
            return 0f;
        }

        min = Mathf.Max(0f, min);
        float floor = max * _damageShakeMinMagnitudeRatio;
        if (min < floor)
        {
            min = floor;
        }

        if (min > max)
        {
            min = max;
        }

        float magnitude = Random.Range(min, max);
        return Random.value < 0.5f ? -magnitude : magnitude;
    }

    /// <summary>敵スロット寄り（攻撃スキル選択・ターゲット切替時）。</summary>
    public void FocusEnemy(int slotIndex)
    {
        if (_enemyCameras == null || slotIndex < 0 || slotIndex >= _enemyCameras.Length)
        {
            FocusDefault();
            return;
        }

        CinemachineCamera enemyCamera = _enemyCameras[slotIndex];
        if (enemyCamera == null)
        {
            FocusDefault();
            return;
        }

        ActivateOnly(enemyCamera);
    }

    /// <summary>
    /// 敵スロット寄りへ切り替え、ブレンド完了まで待機する。
    /// </summary>
    public async UniTask FocusEnemyAsync(int slotIndex, CancellationToken token)
    {
        FocusEnemy(slotIndex);
        float wait = Mathf.Max(0f, _defaultBlendWaitSeconds);
        if (wait > 0f)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(wait), cancellationToken: token);
        }
    }

    private void ActivateOnly(CinemachineCamera activeCamera)
    {
        SetPriority(_defaultCamera, _defaultCamera == activeCamera);
        SetPriority(_playerCamera, _playerCamera == activeCamera);

        if (_enemyCameras != null)
        {
            for (int i = 0; i < _enemyCameras.Length; i++)
            {
                SetPriority(_enemyCameras[i], _enemyCameras[i] == activeCamera);
            }
        }
    }

    private void SetPriority(CinemachineCamera camera, bool active)
    {
        if (camera == null)
        {
            return;
        }

        int value = active ? _activePriority : _inactivePriority;
        PrioritySettings priority = camera.Priority;
        priority.Value = value;
        priority.Enabled = true;
        camera.Priority = priority;
    }
}
