using System.Collections;

namespace QuietRun;

/// <summary>
/// A first-in, first-out buffer with index access, oldest item at index 0. Adding at the back and removing at
/// the front are O(1). It doubles its storage when full, so a window measured in seconds can hold however many
/// samples arrive in it.
/// </summary>
public sealed class RingBuffer<T> : IReadOnlyList<T>
{
    private T[] _items;
    private int _head, _count;

    public RingBuffer(int capacity = 16)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        _items = new T[capacity];
    }

    public int Count => _count;

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            return _items[(_head + index) % _items.Length];
        }
    }

    public T First => this[0];
    public T Last => this[_count - 1];

    public void Push(T item)
    {
        if (_count == _items.Length) Grow();
        _items[(_head + _count) % _items.Length] = item;
        _count++;
    }

    public T PopFront()
    {
        if (_count == 0) throw new InvalidOperationException("The buffer is empty.");
        var item = _items[_head];
        _items[_head] = default!;
        _head = (_head + 1) % _items.Length;
        _count--;
        return item;
    }

    private void Grow()
    {
        var bigger = new T[_items.Length * 2];
        for (int i = 0; i < _count; i++) bigger[i] = this[i];
        _items = bigger;
        _head = 0;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < _count; i++) yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
