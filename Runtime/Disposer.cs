using System;

public class Disposer : IDisposable
{
    private readonly Action _dispose;
    private bool _disposed;

    public Disposer(Action dispose)
    {
        _dispose = dispose;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _dispose?.Invoke();
    }
}