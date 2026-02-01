#if  UNITY_EDITOR
using UnityEngine;
using System.Diagnostics;
using UnityEngine.Profiling;
using Sirenix.OdinInspector;
using Debug = UnityEngine.Debug;

namespace OSK.Bindings.Example
{

    public class BinderBenchmark : MonoBehaviour
    {
        public class DummyTarget : MonoBehaviour
        {
            [Bind(From.Self)] public Transform t;
            [Bind(From.Self)] public BoxCollider box;
            [Bind(From.Self, IncludeInactive = true)] public MeshRenderer mesh; 
        }

        [Header("Settings")]
        public int iterations = 1000; // Số lần chạy lặp lại
        public GameObject dummyPrefab; // Gán một prefab rỗng vào đây để test instantiate

        [Button]
        void star()
        {
            Binder.IsLogEnabled = true;
            // Tạo object mẫu để test
            var go = new GameObject("Benchmark_Dummy");
            go.AddComponent<BoxCollider>();
            var dummy = go.AddComponent<DummyTarget>();

            Debug.Log("<color=yellow>=== BẮT ĐẦU TEST HIỆU NĂNG ===</color>");

            // --- TEST 1: COLD START (Lần chạy đầu tiên - Thường sẽ chậm nhất do chưa Cache) ---
            Profiler.BeginSample("Binder_ColdStart"); // Đánh dấu bắt đầu đo cho Profiler
        
            var watch = Stopwatch.StartNew();
            Binder.AutoBind(dummy); // Chạy lần 1
            watch.Stop();
        
            Profiler.EndSample();
            Debug.Log($"Cold Start (Lần đầu): {watch.Elapsed.TotalMilliseconds:F4} ms");

            // --- TEST 2: WARM START (Chạy lặp lại - Để xem Caching có hoạt động không) ---
            // Chúng ta sẽ test bind trên chính object đó nhiều lần
        
            watch.Restart();
            Profiler.BeginSample("Binder_Loop_SameObject");
        
            for (int i = 0; i < iterations; i++)
            {
                Binder.AutoBind(dummy);
            }
        
            Profiler.EndSample();
            watch.Stop();
            Debug.Log($"Loop {iterations} lần (Cùng Object): {watch.Elapsed.TotalMilliseconds:F4} ms | Trung bình: {(watch.Elapsed.TotalMilliseconds/iterations):F5} ms/lần");


            // --- TEST 3: REAL WORLD (Giả lập Spawn lính - Mỗi lần bind 1 object mới) ---
            // Test này quan trọng nhất vì nó kiểm tra việc tái sử dụng Cache Metadata cho các instance khác nhau
            var targets = new DummyTarget[iterations];
            for (int i = 0; i < iterations; i++) 
            {
                var g = new GameObject("Temp");
                g.AddComponent<BoxCollider>();
                g.AddComponent<MeshRenderer>();
                targets[i] = g.AddComponent<DummyTarget>();
            }

            watch.Restart();
            Profiler.BeginSample("Binder_Loop_NewObjects"); // <--- TÌM TỪ KHÓA NÀY TRONG PROFILER

            for (int i = 0; i < iterations; i++)
            {
                Binder.AutoBind(targets[i]);
            }

            Profiler.EndSample();
            watch.Stop();

            Debug.Log($"Loop {iterations} lần (Object Mới): {watch.Elapsed.TotalMilliseconds:F4} ms | Trung bình: {(watch.Elapsed.TotalMilliseconds / iterations):F5} ms/lần");

            // Dọn dẹp
            // Destroy(go);
            // foreach (var t in targets) Destroy(t.gameObject);
        }
    }
}
#endif