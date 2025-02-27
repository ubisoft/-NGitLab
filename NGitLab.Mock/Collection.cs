using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace NGitLab.Mock;

public abstract class Collection<T> : IReadOnlyCollection<T>, INotifyCollectionChanged, INotifyPropertyChanged
    where T : GitLabObject
{
    private readonly List<T> _items = [];

    public event NotifyCollectionChangedEventHandler CollectionChanged;

    public event PropertyChangedEventHandler PropertyChanged;

    protected Collection(GitLabObject parent)
    {
        Parent = parent ?? throw new ArgumentNullException(nameof(parent));
    }

    protected GitLabServer Server => Parent.Server;

    protected GitLabObject Parent { get; }

    public int Count => _items.Count;

    protected void Clear()
    {
        _items.Clear();

        CollectionChanged?.Invoke(this, EventArgsCache.ResetCollectionChanged);
        PropertyChanged?.Invoke(this, EventArgsCache.IndexerPropertyChanged);
        PropertyChanged?.Invoke(this, EventArgsCache.CountPropertyChanged);
    }

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Contains(T item)
    {
        if (item is null)
            throw new ArgumentNullException(nameof(item));

        return _items.Contains(item);
    }

    public virtual void Add(T item)
    {
        if (item is null)
            throw new ArgumentNullException(nameof(item));

        item.Parent = Parent;
        _items.Add(item);

        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, _items.Count - 1));
        PropertyChanged?.Invoke(this, EventArgsCache.IndexerPropertyChanged);
        PropertyChanged?.Invoke(this, EventArgsCache.CountPropertyChanged);
    }

    public bool Remove(T item)
    {
        if (item is null)
            throw new ArgumentNullException(nameof(item));

        var index = _items.IndexOf(item);
        if (index < 0)
            return false;

        item.Parent = null;
        _items.RemoveAt(index);

        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
        PropertyChanged?.Invoke(this, EventArgsCache.IndexerPropertyChanged);
        PropertyChanged?.Invoke(this, EventArgsCache.CountPropertyChanged);
        return true;
    }

    private static class EventArgsCache
    {
        internal static readonly PropertyChangedEventArgs CountPropertyChanged = new("Count");
        internal static readonly PropertyChangedEventArgs IndexerPropertyChanged = new("Item[]");
        internal static readonly NotifyCollectionChangedEventArgs ResetCollectionChanged = new(NotifyCollectionChangedAction.Reset);
    }
}
