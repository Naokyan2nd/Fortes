using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// インゲーム SE 一覧（id + Clip + 音量・ピッチ）。
/// </summary>
[CreateAssetMenu(fileName = "InGameSeCatalog", menuName = "InGame/SE Catalog")]
public sealed class InGameSeCatalogSO : ScriptableObject
{
    [SerializeField]
    private InGameSeEntry[] _entries;

#if UNITY_EDITOR
    [SerializeField]
    [HideInInspector]
    private AudioClip[] _clips;
#endif

    private Dictionary<string, InGameSeEntry> _entryById;

    private void OnEnable()
    {
        RebuildLookup();
    }

    /// <summary>キーに対応するエントリ。未登録は false。</summary>
    public bool TryGetEntry(string key, out InGameSeEntry entry)
    {
        entry = default;
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (_entryById == null)
        {
            RebuildLookup();
        }

        return _entryById != null && _entryById.TryGetValue(key, out entry);
    }

    /// <summary>キーに対応する Clip。未登録は null。</summary>
    public AudioClip GetClip(string key)
    {
        return TryGetEntry(key, out InGameSeEntry entry) ? entry.Clip : null;
    }

    private void RebuildLookup()
    {
        if (_entries == null || _entries.Length == 0)
        {
            _entryById = null;
            return;
        }

        var map = new Dictionary<string, InGameSeEntry>(_entries.Length);
        for (int i = 0; i < _entries.Length; i++)
        {
            InGameSeEntry entry = _entries[i];
            string id = entry.Id;
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            map[id] = entry;
        }

        _entryById = map;
    }

#if UNITY_EDITOR
    /// <summary>AllKeys にあって Catalog に無い id を末尾に追加する。</summary>
    public void EnsureMissingKeys()
    {
        string[] ids = InGameSeKey.AllKeys;
        if (ids == null || ids.Length == 0)
        {
            return;
        }

        var existing = new HashSet<string>();
        if (_entries != null)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                string id = _entries[i].Id;
                if (!string.IsNullOrEmpty(id))
                {
                    existing.Add(id);
                }
            }
        }

        var merged = new List<InGameSeEntry>();
        if (_entries != null)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                merged.Add(_entries[i]);
            }
        }

        bool added = false;
        for (int i = 0; i < ids.Length; i++)
        {
            if (existing.Add(ids[i]))
            {
                merged.Add(new InGameSeEntry(ids[i], null));
                added = true;
            }
        }

        if (!added)
        {
            return;
        }

        _entries = merged.ToArray();
        UnityEditor.EditorUtility.SetDirty(this);
        RebuildLookup();
    }

    /// <summary>エントリが空のとき InGameSeKey の既定 id でシードする。</summary>
    public void SeedDefaultsIfEmpty()
    {
        if (_entries != null && _entries.Length > 0)
        {
            return;
        }

        string[] ids = InGameSeKey.AllKeys;
        _entries = new InGameSeEntry[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            _entries[i] = new InGameSeEntry(ids[i], null);
        }

        UnityEditor.EditorUtility.SetDirty(this);
        RebuildLookup();
    }

    /// <summary>レガシー _clips 配列から _entries へ移行する。</summary>
    public void MigrateLegacyClips()
    {
        if (_clips == null || _clips.Length == 0)
        {
            return;
        }

        string[] ids = InGameSeKey.AllKeys;
        int count = Mathf.Min(_clips.Length, ids.Length);
        _entries = new InGameSeEntry[count];
        for (int i = 0; i < count; i++)
        {
            _entries[i] = new InGameSeEntry(ids[i], _clips[i]);
        }

        _clips = null;
        UnityEditor.EditorUtility.SetDirty(this);
        RebuildLookup();
    }

    private void OnValidate()
    {
        if (ShouldMigrateLegacyClips())
        {
            MigrateLegacyClips();
        }
        else if (_entries == null || _entries.Length == 0)
        {
            SeedDefaultsIfEmpty();
        }

        NormalizeLegacyEntryVolumePitch();
        ValidateEntries();
        RebuildLookup();
    }

    private void NormalizeLegacyEntryVolumePitch()
    {
        if (_entries == null || _entries.Length == 0)
        {
            return;
        }

        bool changed = false;
        for (int i = 0; i < _entries.Length; i++)
        {
            InGameSeEntry entry = _entries[i];
            if (!entry.IsLegacyUnset)
            {
                continue;
            }

            _entries[i] = new InGameSeEntry(entry.Id, entry.Clip, 1f, 1f);
            changed = true;
        }

#if UNITY_EDITOR
        if (changed)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }

    private bool ShouldMigrateLegacyClips()
    {
        if (_clips == null || _clips.Length == 0)
        {
            return false;
        }

        return _entries == null || _entries.Length == 0;
    }

    private void ValidateEntries()
    {
        if (_entries == null || _entries.Length == 0)
        {
            return;
        }

        var seen = new HashSet<string>();
        for (int i = 0; i < _entries.Length; i++)
        {
            string id = _entries[i].Id;
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"[InGameSeCatalog] 空の Id があります（index={i}）", this);
                continue;
            }

            if (!seen.Add(id))
            {
                Debug.LogWarning($"[InGameSeCatalog] 重複 Id: {id}", this);
            }
        }
    }
#endif
}
