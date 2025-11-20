using System;
using UnityEngine;

namespace OSK
{
    [Serializable]
    public class Observable<T> : ISerializationCallbackReceiver
    {
        [SerializeField] private T _value;

        public event Action<T, T> OnValueChanged;
        public event Action<T> OnChanged;

        public T Value
        {
            get => _value;
            set => SetValue(value, true);
        }

        public Observable() {}              
        public Observable(T v) => _value = v;  

        public virtual void SetValue(T newValue, bool notify = true)
        {
            if (!Equals(_value, newValue))
            {
                var old = _value;
                _value = newValue;
                if (notify)
                {
                    OnValueChanged?.Invoke(old, _value);
                    OnChanged?.Invoke(_value);
                }
            }
            else
            {
                Debug.Log($"Observable<{typeof(T).Name}> assigned same value: {_value}");
            }
        }

        public void ForceNotify()
        {
            OnValueChanged?.Invoke(_value, _value);
            OnChanged?.Invoke(_value);
        }

        public void OnAfterDeserialize() { }
        public void OnBeforeSerialize() { }

        public static implicit operator Observable<T>(T v)  => new Observable<T>(v);
        public static implicit operator T(Observable<T> obs) => obs != null ? obs._value : default;
    }
}
