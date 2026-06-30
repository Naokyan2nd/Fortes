using System;
using R3;

/// <summary>
/// 敵1体分のバトル状態。
/// </summary>
public sealed class EnemyModel : IDisposable
{
    private readonly ReactiveProperty<int> _currentHp;
    private readonly ReactiveProperty<int> _maxHp;
    private readonly ReactiveProperty<int> _speed;
    private readonly ReactiveProperty<bool> _isSelected;
    private bool _disposed;

    /// <summary>
    /// 敵モデルを生成する。
    /// </summary>
    /// <param name="slotIndex">スロット番号（0〜）。</param>
    /// <param name="data">マスタデータ。</param>
    public EnemyModel(int slotIndex, EnemyDataSO data)
    {
        SlotIndex = slotIndex;
        Data = data;
        int maxHp = data != null ? data.MaxHp : 0;
        int speed = data != null ? data.Speed : 0;
        _maxHp = new ReactiveProperty<int>(maxHp);
        _currentHp = new ReactiveProperty<int>(maxHp);
        _speed = new ReactiveProperty<int>(speed);
        _isSelected = new ReactiveProperty<bool>(false);
    }

    /// <summary>スロットインデックス。</summary>
    public int SlotIndex { get; }

    /// <summary>参照している敵データ。</summary>
    public EnemyDataSO Data { get; }

    /// <summary>現在HP。</summary>
    public ReactiveProperty<int> CurrentHp => _currentHp;

    /// <summary>最大HP。</summary>
    public ReactiveProperty<int> MaxHp => _maxHp;

    /// <summary>Speed。</summary>
    public ReactiveProperty<int> Speed => _speed;

    /// <summary>ターゲット選択中か。</summary>
    public ReactiveProperty<bool> IsSelected => _isSelected;

    /// <summary>
    /// 選択状態を設定する。
    /// </summary>
    /// <param name="selected">選択中ならtrue。</param>
    public void SetSelected(bool selected)
    {
        _isSelected.Value = selected;
    }

    /// <summary>
    /// ダメージ適用（最低0）。戻り値は撃破したか。
    /// </summary>
    /// <param name="amount">ダメージ量。</param>
    /// <returns>HPが0になった場合true。</returns>
    public bool ApplyDamage(int amount)
    {
        int next = _currentHp.Value - amount;
        if (next < 0)
        {
            next = 0;
        }

        _currentHp.Value = next;
        return _currentHp.Value == 0;
    }

    /// <summary>
    /// 生存しているか。
    /// </summary>
    /// <returns>HPが1以上ならtrue。</returns>
    public bool IsAlive()
    {
        return _currentHp.Value > 0;
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
        _isSelected.Dispose();
    }
}
