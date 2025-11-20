using System;
using System.Reflection;
using UnityEngine;

namespace OSK
{
    [ExecuteAlways]
    public abstract class BaseObservableOwner : MonoBehaviour, IObservableOwner
    {
        protected BindContext _bindContext;

        /// <summary>
        /// Called by editor drawer (or manually) after inspector changes.
        /// Default implementation: Unbind all -> call SetupBindings().
        /// </summary>
        public virtual void RebindObservables()
        {
            // Unbind old
            _bindContext?.UnbindAll();

            // Create new
            _bindContext = new BindContext();

            try
            {
                // Call user implementation to add bindings
                SetupBindings();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// Implement this in derived classes to add your bindings to _bindContext.
        /// Example: _bindContext.Add(PlayerNameText.Bind(PlayerName));
        /// </summary>
        protected abstract void SetupBindings();

        protected virtual void OnEnable()
        {
            // Ensure bindings exist when component enabled
            RebindObservables();
        }

        protected virtual void OnDisable()
        {
            _bindContext?.UnbindAll();
            _bindContext = null;
        }

        /// <summary>
        /// Utility: reflectively find Observable<> fields on this object and call ForceNotify()
        /// Useful if you want to refresh UIs after serialization changes.
        /// </summary>
        protected int AutoForceNotifyFields()
        {
            int count = 0;
            var t = this.GetType();
            var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var f in fields)
            {
                try
                {
                    var val = f.GetValue(this);
                    if (val == null) continue;
                    var vt = val.GetType();
                    if (vt.IsGenericType && vt.GetGenericTypeDefinition() == typeof(Observable<>))
                    {
                        var mi = vt.GetMethod("ForceNotify", BindingFlags.Instance | BindingFlags.Public);
                        mi?.Invoke(val, null);
                        count++;
                    }
                    // if field is collection, you may expand here
                }
                catch
                {
                    /* ignore */
                }
            }
            return count;
        }
    }
}