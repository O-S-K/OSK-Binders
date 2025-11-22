using System;
using System.Collections.Generic;
using UnityEngine;

namespace OSK.Bindings
{
    public static class ObservableOperators
    {
        // =========================================================================
        // I. KẾT HỢP VÀ BIẾN ĐỔI (Select, CombineLatest)
        // =========================================================================

        /// <summary>
        /// Biến đổi giá trị của Observable sang một dạng mới (ví dụ: Observable<int> -> Observable<string>).
        /// </summary>
        public static Observable<TResult> Select<TSource, TResult>(this Observable<TSource> source, Func<TSource, TResult> selector)
        {
            var result = new Observable<TResult>(selector(source.Value));
            Action<TSource> onSourceChanged = v =>
            {
                try
                {
                    result.Value = selector(v);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Observable.Select] Error: {ex}");
                }
            };

            source.OnChanged += onSourceChanged;

            return result; // Consumer chịu trách nhiệm Dispose Observable mới nếu cần.
        }

        /// <summary>
        /// Kết hợp các giá trị mới nhất từ hai Observable thành một Observable duy nhất.
        /// </summary>
        public static Observable<TResult> CombineLatest<T1, T2, TResult>(
            this Observable<T1> source1, 
            Observable<T2> source2, 
            Func<T1, T2, TResult> combiner)
        {
            var result = new Observable<TResult>(combiner(source1.Value, source2.Value));

            void UpdateResult()
            {
                try
                {
                    result.Value = combiner(source1.Value, source2.Value);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Observable.CombineLatest] Error: {ex}");
                }
            }

            source1.OnChanged += _ => UpdateResult();
            source2.OnChanged += _ => UpdateResult();

            return result;
        }

        // =========================================================================
        // II. LỌC (Where, DistinctUntilChanged)
        // =========================================================================

        /// <summary>
        /// Lọc luồng sự kiện dựa trên một điều kiện (predicate).
        /// </summary>
        public static IDisposable Where<T>(this Observable<T> source, Func<T, bool> predicate, Action<T> onPassed)
        {
            void FilterAndExecute(T v)
            {
                try
                {
                    if (predicate(v))
                    {
                        onPassed?.Invoke(v);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Observable.Where] Error: {ex}");
                }
            }

            source.OnChanged += FilterAndExecute;
            return new Disposer(() => source.OnChanged -= FilterAndExecute);
        }

        /// <summary>
        /// Chỉ thông báo nếu giá trị MỚI khác với giá trị TRƯỚC ĐÓ.
        /// </summary>
        public static IDisposable DistinctUntilChanged<T>(this Observable<T> source, Action<T> onPassed)
        {
            T lastValue = source.Value;

            void FilterAndExecute(T v)
            {
                try
                {
                    if (!EqualityComparer<T>.Default.Equals(lastValue, v))
                    {
                        lastValue = v;
                        onPassed?.Invoke(v);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Observable.DistinctUntilChanged] Error: {ex}");
                }
            }

            source.OnChanged += FilterAndExecute;
            return new Disposer(() => source.OnChanged -= FilterAndExecute);
        }
        
        // =========================================================================
        // III. GIỚI HẠN TẦN SUẤT (Throttle - Cần thời gian thực của Unity)
        // =========================================================================

        /// <summary>
        /// Hạn chế tốc độ xử lý sự kiện (chỉ xử lý sự kiện đầu tiên trong một khoảng thời gian).
        /// Sử dụng Time.timeSinceLevelLoad.
        /// </summary>
        public static IDisposable Throttle<T>(this Observable<T> source, float throttleTimeSeconds, Action<T> onPassed)
        {
            float lastInvokeTime = 0f;
            
            void FilterAndExecute(T v)
            {
                // Cần đảm bảo hàm này được gọi từ Main Thread.
                if (Time.timeSinceLevelLoad - lastInvokeTime >= throttleTimeSeconds)
                {
                    lastInvokeTime = Time.timeSinceLevelLoad;
                    onPassed?.Invoke(v);
                }
            }
            
            source.OnChanged += FilterAndExecute;
            return new Disposer(() => source.OnChanged -= FilterAndExecute);
        }
    }
}