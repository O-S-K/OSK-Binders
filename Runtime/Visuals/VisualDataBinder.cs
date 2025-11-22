using System;
using UnityEngine;
 
namespace OSK.Bindings
{
    public class VisualDataBinder : MonoBehaviour
    {
        [Header("Target Component (The View)")]
        [SerializeField] private Component TargetComponent;
        [SerializeField] private string TargetPropertyName;

        [Header("Source Data (The Model)")]
        [SerializeField] private MonoBehaviour SourceMonoBehaviour;
        [SerializeField] private string SourceObservableName;

        [Header("Enable two-way binding if provided property supports it.")]
        [SerializeField] private bool IsTwoWay = false;

        // Lưu trữ IDisposable để hủy binding khi Component bị hủy
        private IDisposable _bindingDisposable;
        
        private void Awake()
        {
            InitializeBinding(Application.isPlaying);
        }
        
// #if UNITY_EDITOR
//         private void OnValidate()
//         {
//             InitializeBinding();
//         }
// #endif

        private void InitializeBinding(bool isPlaying = false)
        {
            // Reset các binding cũ nếu có
            _bindingDisposable?.Dispose();

            if (TargetComponent == null || SourceMonoBehaviour == null || string.IsNullOrEmpty(SourceObservableName) || string.IsNullOrEmpty(TargetPropertyName))
            {
                return;
            }

            _bindingDisposable = VisualBindingMapper.CreateBinding(
                TargetComponent,
                TargetPropertyName,
                SourceMonoBehaviour,
                SourceObservableName,
                IsTwoWay,
                isPlaying
            );
        }
        
        // <summary>
        /// Phương thức được gọi từ Editor để buộc Observable nguồn gửi thông báo.
        /// </summary>
        public void ForceUpdateBinding()
        {
            InitializeBinding();
        }
        
        private void OnDestroy()
        {
            // Đảm bảo binding được hủy để tránh rò rỉ bộ nhớ
            _bindingDisposable?.Dispose();
        }
    }
}