using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OSK.Bindings
{
 /// <summary>
    /// Utility extensions for automatically saving and loading Observable values to/from PlayerPrefs.
    /// Supports int, float, string, and bool (via int).
    /// </summary>
    public static class ObservablePersistence
    {
        private static IDisposable Save<T>(Observable<T> source, string key, Action<string, T> saveAction)
        {
            // Initial save (đảm bảo giá trị ban đầu được lưu)
            saveAction(key, source.Value);

            void OnChanged(T v) => saveAction(key, v);
            source.OnChanged += OnChanged;
            
            return new Disposer(() => source.OnChanged -= OnChanged);
        }

        // --- PUBLIC SAVE METHODS (Trả về IDisposable để hủy việc lưu tự động) ---

        public static IDisposable SaveToPlayerPrefs(this Observable<int> source, string key)
        {
            return Save(source, key, (k, v) => PlayerPrefs.SetInt(k, v));
        }

        public static IDisposable SaveToPlayerPrefs(this Observable<float> source, string key)
        {
            return Save(source, key, (k, v) => PlayerPrefs.SetFloat(k, v));
        }

        public static IDisposable SaveToPlayerPrefs(this Observable<string> source, string key)
        {
            return Save(source, key, (k, v) => PlayerPrefs.SetString(k, v));
        }

        public static IDisposable SaveToPlayerPrefs(this Observable<bool> source, string key)
        {
            return Save(source, key, (k, v) => PlayerPrefs.SetInt(k, v ? 1 : 0));
        }

        // --- PUBLIC LOAD METHODS (Chạy một lần, không Disposable) ---
        
        public static Observable<int> LoadFromPlayerPrefs(this Observable<int> source, string key, int defaultValue = 0)
        {
            if (PlayerPrefs.HasKey(key))
            {
                source.SetValue(PlayerPrefs.GetInt(key, defaultValue), notify: false);
            }
            return source;
        }

        public static Observable<string> LoadFromPlayerPrefs(this Observable<string> source, string key, string defaultValue = "")
        {
            if (PlayerPrefs.HasKey(key))
            {
                source.SetValue(PlayerPrefs.GetString(key, defaultValue), notify: false);
            }
            return source;
        }
        
        public static Observable<bool> LoadFromPlayerPrefs(this Observable<bool> source, string key, bool defaultValue = false)
        {
            if (PlayerPrefs.HasKey(key))
            {
                int val = PlayerPrefs.GetInt(key, defaultValue ? 1 : 0);
                source.SetValue(val == 1, notify: false);
            }
            return source;
        }
    }
}
