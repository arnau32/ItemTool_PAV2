using System.Collections.Generic;

internal sealed class SelectionModel<TId>
{
    // Simple selection container with stable operations and no LINQ/alloc patterns.

    #region Fields

    private readonly HashSet<TId> _set = new HashSet<TId>();
    private TId _primary;
    private bool _hasPrimary;

    #endregion

    #region Properties

    public int Count => _set.Count;
    public bool HasPrimary => _hasPrimary;
    public TId Primary => _primary;

    public bool Contains(TId id) => _set.Contains(id);

    #endregion

    #region Public API

    public void Clear()
    {
        _set.Clear();
        _hasPrimary = false;
        _primary = default(TId);
    }

    public void SetSingle(TId id)
    {
        _set.Clear();
        _set.Add(id);
        _primary = id;
        _hasPrimary = true;
    }

    public void Add(TId id)
    {
        _set.Add(id);
        _primary = id;
        _hasPrimary = true;
    }

    public void Remove(TId id)
    {
        if (!_set.Remove(id)) return;

        if (!_hasPrimary || !EqualityComparer<TId>.Default.Equals(_primary, id)) return;

        _hasPrimary = false;
        _primary = default(TId);
    }

    public void Toggle(TId id)
    {
        if (_set.Contains(id))
        {
            Remove(id);
        }
        else
        {
            Add(id);
        }
    }

    public void SetPrimary(TId id)
    {
        if (!_set.Contains(id)) return;

        _primary = id;
        _hasPrimary = true;
    }

    public void CopyTo(List<TId> dest)
    {
        dest.Clear();
        foreach (var id in _set)
        {
            dest.Add(id);
        }
    }

    #endregion
}
