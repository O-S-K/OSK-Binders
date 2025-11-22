using System; 
using System.Collections.Generic; 

namespace OSK.Bindings
{
    public class BindContext
    {
        private readonly List<IDisposable> _list = new();

        public void Add(IDisposable d)
        {
            if (d != null)
                _list.Add(d);
        }

        public void UnbindAll()
        {
            foreach (var d in _list)
                d.Dispose();
            _list.Clear();
        }
    }
}
