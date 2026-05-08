namespace GDD.Collections;

public sealed class RingBuffer<T>
{
    private readonly T[] _buffer;
    private readonly object _lock = new();
    private int _head;
    private int _count;

    public RingBuffer(int capacity = 500)
    {
        _buffer = new T[capacity];
    }

    public int Count
    {
        get { lock (_lock) return _count; }
    }

    public void Add(T item)
    {
        lock (_lock)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length)
                _count++;
        }
    }

    public List<T> ToList()
    {
        lock (_lock)
        {
            var result = new List<T>(_count);
            var start = _count < _buffer.Length ? 0 : _head;
            for (var i = 0; i < _count; i++)
                result.Add(_buffer[(start + i) % _buffer.Length]);
            return result;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _head = 0;
            _count = 0;
        }
    }
}
