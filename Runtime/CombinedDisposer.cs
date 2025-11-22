using System;
using System.Collections.Generic;

namespace OSK.Bindings
{
    public sealed class CombinedDisposer : IDisposable
    {
        private List<IDisposable> _disposables = new List<IDisposable>();
        private bool _disposed;

        public CombinedDisposer(params IDisposable[] disposables)
        {
            _disposables.AddRange(disposables);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var d in _disposables)
            {
                d?.Dispose();
            }
            _disposables.Clear();
        }
    }
}