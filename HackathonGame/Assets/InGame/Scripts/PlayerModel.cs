using System;
using R3;

/// <summary>
/// プレイヤーのバトル状態（HP/Speed）。
/// </summary>
public sealed class PlayerModel : IDisposable
{
    private readonly ReactiveProperty<int> _currentHp;
    private readonly ReactiveProperty<int> _maxHp;
    private readonly ReactiveProperty<int> _speed;
    private bool _disposed;

    /// <summary>
    /// プレイヤーモデルを生成する。
    /// </summary>
    /// <param name="maxHp">最大HP。</param>
    /// <param name="speed">行動順用Speed。</param>
    public PlayerModel(int maxHp, int speed)
    {
        _maxHp = new ReactiveProperty<int>(maxHp);
        _currentHp = new ReactiveProperty<int>(maxHp);
        _speed = new ReactiveProperty<int>(speed);
    }

    /// <summary>現在HP。</summary>
    public ReactiveProperty<int> CurrentHp => _currentHp;

    /// <summary>最大HP。</summary>
    public ReactiveProperty<int> MaxHp => _maxHp;

    /// <summary>Speed。</summary>
    public ReactiveProperty<int> Speed => _speed;

    /// <summary>
    /// HPを減少させる（最低0）。
    /// </summary>
    /// <param name="amount">減少量。</param>
    public void ApplyDamage(int amount)
    {
        int next = _currentHp.Value - amount;
        _currentHp.Value = next < 0 ? 0 : next;
    }

    /// <summary>
    /// HPを回復（Max上限）。
    /// </summary>
    /// <param name="amount">回復量（正の整数）。</param>
    public void Heal(int amount)
    {
        int next = _currentHp.Value + amount;
        int cap = _maxHp.Value;
        _currentHp.Value = next > cap ? cap : next;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _currentHp.Dispose();
        _maxHp.Dispose();
        _speed.Dispose();
    }
}
