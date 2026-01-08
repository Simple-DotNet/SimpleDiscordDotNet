using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SimpleDiscordNet.Collections;

/// <summary>
/// Thread-safe observable wrapper around ConcurrentDictionary that supports UI binding
/// and batch operations to prevent UI thrashing on large updates.
/// </summary>
public sealed class ObservableConcurrentDictionary<TKey, TValue> : INotifyCollectionChanged, INotifyPropertyChanged, IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TValue> _dictionary;
    private readonly SynchronizationContext? _synchronizationContext;
    private int _batchUpdateDepth;
    private readonly object _batchLock = new();
    private bool _hasPendingChanges;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableConcurrentDictionary()
    {
        _dictionary = new ConcurrentDictionary<TKey, TValue>();
    }

    public ObservableConcurrentDictionary(SynchronizationContext? synchronizationContext)
    {
        _dictionary = new ConcurrentDictionary<TKey, TValue>();
        _synchronizationContext = synchronizationContext;
    }

    public ObservableConcurrentDictionary(IEqualityComparer<TKey> comparer)
    {
        _dictionary = new ConcurrentDictionary<TKey, TValue>(comparer);
    }

    public ObservableConcurrentDictionary(IEqualityComparer<TKey> comparer, SynchronizationContext? synchronizationContext)
    {
        _dictionary = new ConcurrentDictionary<TKey, TValue>(comparer);
        _synchronizationContext = synchronizationContext;
    }

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// Setting a value will raise change notifications.
    /// </summary>
    public TValue this[TKey key]
    {
        get => _dictionary[key];
        set
        {
            bool isNew = !_dictionary.ContainsKey(key);
            _dictionary[key] = value;
            OnItemChanged(key, value, isNew);
        }
    }

    /// <summary>
    /// Gets the number of key/value pairs in the dictionary.
    /// </summary>
    public int Count => _dictionary.Count;

    /// <summary>
    /// Gets all keys in the dictionary.
    /// </summary>
    public IEnumerable<TKey> Keys => _dictionary.Keys;

    /// <summary>
    /// Gets all values in the dictionary.
    /// </summary>
    public IEnumerable<TValue> Values => _dictionary.Values;

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
    /// Attempts to add the specified key and value.
    /// </summary>
    public bool TryAdd(TKey key, TValue value)
    {
        bool added = _dictionary.TryAdd(key, value);
        if (added)
        {
            OnItemAdded(key, value);
        }
        return added;
    }

    /// <summary>
    /// Attempts to remove the value with the specified key.
    /// </summary>
    public bool TryRemove(TKey key, out TValue? value)
    {
        bool removed = _dictionary.TryRemove(key, out value);
        if (removed && value != null)
        {
            OnItemRemoved(key, value);
        }
        return removed;
    }

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    public bool TryGetValue(TKey key, out TValue value)
    {
        return _dictionary.TryGetValue(key, out value!);
    }

    /// <summary>
    /// Determines whether the dictionary contains the specified key.
    /// </summary>
    public bool ContainsKey(TKey key)
    {
        return _dictionary.ContainsKey(key);
    }

    /// <summary>
    /// Adds a key/value pair if the key does not exist, or updates it if it does.
    /// Returns the new value.
    /// </summary>
    public TValue AddOrUpdate(TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory)
    {
        bool isNew = !_dictionary.ContainsKey(key);
        TValue result = _dictionary.AddOrUpdate(key, addValue, updateValueFactory);
        OnItemChanged(key, result, isNew);
        return result;
    }

    /// <summary>
    /// Adds a key/value pair if the key does not exist, or updates it if it does.
    /// Returns the new value.
    /// </summary>
    public TValue AddOrUpdate(TKey key, Func<TKey, TValue> addValueFactory, Func<TKey, TValue, TValue> updateValueFactory)
    {
        bool isNew = !_dictionary.ContainsKey(key);
        TValue result = _dictionary.AddOrUpdate(key, addValueFactory, updateValueFactory);
        OnItemChanged(key, result, isNew);
        return result;
    }

    /// <summary>
    /// Gets the value associated with the specified key, or adds it using the factory if not found.
    /// </summary>
    public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        bool isNew = !_dictionary.ContainsKey(key);
        TValue result = _dictionary.GetOrAdd(key, valueFactory);
        if (isNew)
        {
            OnItemAdded(key, result);
        }
        return result;
    }

    /// <summary>
    /// Gets the value associated with the specified key, or adds it if not found.
    /// </summary>
    public TValue GetOrAdd(TKey key, TValue value)
    {
        bool isNew = !_dictionary.ContainsKey(key);
        TValue result = _dictionary.GetOrAdd(key, value);
        if (isNew)
        {
            OnItemAdded(key, result);
        }
        return result;
    }

    /// <summary>
    /// Adds or updates multiple key/value pairs in a single batch operation.
    /// Only one collection change notification is raised after all items are processed.
    /// </summary>
    public void AddOrUpdateRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
        BeginBatchUpdate();
        try
        {
            foreach (var kvp in items)
            {
                _dictionary[kvp.Key] = kvp.Value;
            }
        }
        finally
        {
            EndBatchUpdate();
        }
    }

    /// <summary>
    /// Removes multiple keys in a single batch operation.
    /// Only one collection change notification is raised after all items are processed.
    /// </summary>
    public void RemoveRange(IEnumerable<TKey> keys)
    {
        BeginBatchUpdate();
        try
        {
            foreach (var key in keys)
            {
                _dictionary.TryRemove(key, out _);
            }
        }
        finally
        {
            EndBatchUpdate();
        }
    }

    /// <summary>
    /// Clears the dictionary and replaces it with new items in a single batch operation.
    /// Only one collection change notification is raised after completion.
    /// </summary>
    public void ReplaceAll(IEnumerable<KeyValuePair<TKey, TValue>> items)
    {
        BeginBatchUpdate();
        try
        {
            _dictionary.Clear();
            foreach (var kvp in items)
            {
                _dictionary[kvp.Key] = kvp.Value;
            }
        }
        finally
        {
            EndBatchUpdate();
        }
    }

    /// <summary>
    /// Clears all items from the dictionary.
    /// </summary>
    public void Clear()
    {
        _dictionary.Clear();
        OnCollectionReset();
    }

    private void OnItemAdded(TKey key, TValue value)
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
            new KeyValuePair<TKey, TValue>(key, value)));
        RaisePropertyChanged(nameof(Count));
    }

    private void OnItemRemoved(TKey key, TValue value)
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
            new KeyValuePair<TKey, TValue>(key, value)));
        RaisePropertyChanged(nameof(Count));
    }

    private void OnItemChanged(TKey key, TValue value, bool isNew)
    {
        lock (_batchLock)
        {
            if (_batchUpdateDepth > 0)
            {
                _hasPendingChanges = true;
                return;
            }
        }

        // For dictionary updates, we use Reset because WPF doesn't support Replace well for dictionaries
        RaiseCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        RaisePropertyChanged(nameof(Count));
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
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dictionary.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
