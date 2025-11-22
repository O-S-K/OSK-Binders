using System;

namespace OSK.Bindings
{
    #region Disposer

    public class Disposer : IDisposable
    {
        private Action _onDispose;
        private bool _disposed;

        public Disposer(Action onDispose) => _onDispose = onDispose;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                _onDispose?.Invoke();
            }
            finally
            {
                _onDispose = null;
            }
        }
    }

    #endregion
}