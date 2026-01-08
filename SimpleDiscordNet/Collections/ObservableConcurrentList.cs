using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SimpleDiscordNet.Collections;

/// <summary>
/// Thread-safe observable list that supports UI binding and batch operations.
/// Uses ReaderWriterLockSlim for efficient concurrent reads.
/// </summary>
public sealed class ObservableConcurrentList<T> : INotifyCollectionChanged, INotifyPropertyChanged, IReadOnlyList<T>, IDisposable
{
    private readonly List<T> _list;
    private readonly ReaderWriterLockSlim _lock;
    private readonly SynchronizationContext? _synchronizationContext;
    private int _batchUpdateDepth;
    private readonly object _batchLock = new();
    private bool _hasPendingChanges;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableConcurrentList()
    {
        _list = new List<T>();
        _lock = new ReaderWriterLockSlim();
    }

    public ObservableConcurrentList(SynchronizationContext? synchronizationContext)
    {
        _list = new List<T>();
        _lock = new ReaderWriterLockSlim();
        _synchronizationContext = synchronizationContext;
    }

    public ObservableConcurrentList(int capacity)
    {
        _list = new List<T>(capacity);
        _lock = new ReaderWriterLockSlim();
    }

    public ObservableConcurrentList(int capacity, SynchronizationContext? synchronizationContext)
    {
        _list = new List<T>(capacity);
        _lock = new ReaderWriterLockSlim();
        _synchronizationContext = synchronizationContext;
    }

    /// <summary>
    /// Gets the number of items in the list.
    /// </summary>
    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _list.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Gets the item at the specified index.
    /// </summary>
    public T this[int index]
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _list[index];
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Begins a batch update operation. Collection change notifications are suppressed
    /// until EndBatchUpdate() is called. Can be nested.
    /// </summary>
    public void BeginBatchUpdate()
    {
        lock (_batchLock)
        {
            _batchUpdateDepth++;
        }
    }

    /// <summary>
    /// Ends a batch update operation. If this was the outermost batch operation,
    /// a single Reset notification is raised if any changes occurred.
    /// </summary>
    public void EndBatchUpdate()
    {
        bool shouldNotify = false;
        lock (_batchLock)
        {
            if (_batchUpdateDepth > 0)
            {
                _batchUpdateDepth--;
                if (_batchUpdateDepth == 0 && _hasPendingChanges)
                {
                    shouldNotify = true;
                    _hasPendingChanges = false;
                }
            }
        }

        if (shouldNotify)
        {
            OnCollectionReset();
        }
    }

    /// <summary>
    /// Adds an item to the end of the list.
    /// </summary>
    public void Add(T item)
    {
        _lock.EnterWriteLock();
        try
        {
            int index = _list.Count;
            _list.Add(item);
            OnItemAdded(item, index);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Adds multiple items to the list in a single batch operation.
    /// Only one collection change notification is raised after all items are added.
    /// </summary>
    public void AddRange(IEnumerable<T> items)
    {
        BeginBatchUpdate();
        _lock.EnterWriteLock();
        try
        {
            _list.AddRange(items);
        }
        finally
        {
            _lock.ExitWriteLock();
            EndBatchUpdate();
        }
    }

    /// <summary>
    /// Removes the first occurrence of the specified item.
    /// Returns true if the item was found and removed.
    /// </summary>
    public bool Remove(T item)
    {
        _lock.EnterWriteLock();
        try
        {
            int index = _list.IndexOf(item);
            if (index >= 0)
            {
                _list.RemoveAt(index);
                OnItemRemoved(item, index);
                return true;
            }
            return false;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Removes the item at the specified index.
    /// </summary>
    public void RemoveAt(int index)
    {
        _lock.EnterWriteLock();
        try
        {
            T item = _list[index];
            _list.RemoveAt(index);
            OnItemRemoved(item, index);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Removes multiple items in a single batch operation.
    /// Only one collection change notification is raised after all items are removed.
    /// </summary>
    public void RemoveRange(IEnumerable<T> items)
    {
        BeginBatchUpdate();
        _lock.EnterWriteLock();
        try
        {
            foreach (var item in items)
            {
                _list.Remove(item);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
            EndBatchUpdate();
        }
    }

    /// <summary>
    /// Clears all items from the list.
    /// </summary>
    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _list.Clear();
            OnCollectionReset();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Clears the list and replaces it with new items in a single batch operation.
    /// Only one collection change notification is raised after completion.
    /// </summary>
    public void ReplaceAll(IEnumerable<T> items)
    {
        BeginBatchUpdate();
        _lock.EnterWriteLock();
        try
        {
            _list.Clear();
            _list.AddRange(items);
        }
        finally
        {
            _lock.ExitWriteLock();
            EndBatchUpdate();
        }
    }

    /// <summary>
    /// Updates an item at the specified index found by a predicate.
    /// Returns true if an item was found and updated.
    /// </summary>
    public bool Update(Func<T, bool> predicate, T newValue)
    {
        _lock.EnterWriteLock();
        try
        {
            int index = _list.FindIndex(i => predicate(i));
            if (index >= 0)
            {
                T oldValue = _list[index];
                _list[index] = newValue;
                OnItemReplaced(oldValue, newValue, index);
                return true;
            }
            return false;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Finds the index of the first item that matches the predicate.
    /// Returns -1 if no match is found.
    /// </summary>
    public int FindIndex(Func<T, bool> predicate)
    {
        _lock.EnterReadLock();
        try
        {
            return _list.FindIndex(i => predicate(i));
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Returns the first item that matches the predicate, or default if not found.
    /// </summary>
    public T? FirstOrDefault(Func<T, bool> predicate)
    {
        _lock.EnterReadLock();
        try
        {
            return _list.FirstOrDefault(predicate);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Determines whether the list contains the specified item.
    /// </summary>
    public bool Contains(T item)
    {
        _lock.EnterReadLock();
        try
        {
            return _list.Contains(item);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Copies the list to a new array.
    /// </summary>
    public T[] ToArray()
    {
        _lock.EnterReadLock();
        try
        {
            return _list.ToArray();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private void OnItemAdded(T item, int index)
    {
        lock (_batchLock)
        {
            if (_batchUpdateDepth > 0)
            {
                _hasPendingChanges = true;
                return;
            }
        }

        RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Add,
            item,
            index));
        RaisePropertyChanged(nameof(Count));
    }

    private void OnItemRemoved(T item, int index)
    {
        lock (_batchLock)
        {
            if (_batchUpdateDepth > 0)
            {
                _hasPendingChanges = true;
                return;
            }
        }

        RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Remove,
            item,
            index));
        RaisePropertyChanged(nameof(Count));
    }

    private void OnItemReplaced(T oldItem, T newItem, int index)
    {
        lock (_batchLock)
        {
            if (_batchUpdateDepth > 0)
            {
                _hasPendingChanges = true;
                return;
            }
        }

        RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Replace,
            newItem,
            oldItem,
            index));
    }

    private void OnCollectionReset()
    {
        RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        RaisePropertyChanged(nameof(Count));
    }

    private void RaiseCollectionChanged(NotifyCollectionChangedEventArgs args)
    {
        var handler = CollectionChanged;
        if (handler == null) return;

        if (_synchronizationContext != null)
        {
            _synchronizationContext.Post(_ => handler(this, args), null);
        }
        else
        {
            handler(this, args);
        }
    }

    private void RaisePropertyChanged(string propertyName)
    {
        var handler = PropertyChanged;
        if (handler == null) return;

        var args = new PropertyChangedEventArgs(propertyName);
        if (_synchronizationContext != null)
        {
            _synchronizationContext.Post(_ => handler(this, args), null);
        }
        else
        {
            handler(this, args);
        }
    }

    // IEnumerable implementation
    public IEnumerator<T> GetEnumerator()
    {
        _lock.EnterReadLock();
        try
        {
            // Return a snapshot to avoid holding the lock during enumeration
            return _list.ToList().GetEnumerator();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Dispose()
    {
        _lock?.Dispose();
    }
}
