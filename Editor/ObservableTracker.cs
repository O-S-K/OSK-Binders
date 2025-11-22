#if UNITY_EDITOR
using System;

namespace OSK.Bindings
{
    /// <summary>
    /// Lớp tĩnh không generic dùng làm hub toàn cục để thu thập sự kiện từ mọi Observable<T>
    /// Chỉ tồn tại trong Editor.
    /// </summary>
    public static class ObservableTracker
    {
        // Sự kiện toàn cục mà Tracer Window sẽ subscribe.
        // Sử dụng object cho các giá trị để chứa mọi loại T.
        public static event Action<string, string, object, object> OnGlobalObservableChanged;

        /// <summary>
        /// Được gọi từ Observable<T>.SetValue để gửi thông tin trace.
        /// </summary>
        public static void Notify(string typeName, string source, object oldValue, object newValue)
        {
            // Kiểm tra null để đảm bảo không gọi sự kiện nếu chưa có listener (performance)
            OnGlobalObservableChanged?.Invoke(typeName, source, oldValue, newValue);
        }
    }
}
#endif