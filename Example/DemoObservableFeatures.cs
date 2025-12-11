using UnityEditor;
using UnityEngine;

namespace OSK.Bindings.Example
{

    public class DemoObservableFeatures : MonoBehaviour
    {
        // === 1. OBSERVABLES CHÍNH ===
        // PlayerScore sẽ được Load/Save tự động
        public Observable<int> PlayerScore = new Observable<int>(0);

        // MaxScore dùng để Combine
        public Observable<int> MaxScore = new Observable<int>(100);

        // PlayerName sẽ được Load/Save tự động
        public Observable<string> PlayerName = new Observable<string>("Guest");

        // Input giả lập sự kiện nhập liệu liên tục
        public Observable<string> SearchInput = new Observable<string>("");

        // Dùng để quản lý tất cả các binding và persistence
        private BindContext _disposer = new BindContext();

        // Observable được tạo ra từ việc kết hợp 2 Observable khác
        private Observable<string> _scoreProgressText;

        void Awake()
        {
            // === 2. DEMO OBSERVABLE PERSISTENCE (Load/Save) ===
            Debug.Log("--- DEMO PERSISTENCE ---");

            // 2a. Tải giá trị đã lưu khi khởi tạo (chỉ chạy một lần)
            PlayerScore.LoadFromPlayerPrefs("Demo_PlayerScore", 0);
            PlayerName.LoadFromPlayerPrefs("Demo_PlayerName", "Guest");
            Debug.Log($"Loaded Initial Data: {PlayerName.Value} with Score: {PlayerScore.Value}");

            // 2b. Tự động lưu giá trị mỗi khi thay đổi (trả về IDisposable)
            // Lưu trữ IDisposable này trong BindContext để hủy khi cần
            _disposer.Add(PlayerScore.SaveToPlayerPrefs("Demo_PlayerScore"));
            _disposer.Add(PlayerName.SaveToPlayerPrefs("Demo_PlayerName"));

            // Ví dụ: Đặt tên mới. Điều này sẽ tự động lưu vào PlayerPrefs
            PlayerName.Value = "OSK_Tester";

            // === 3. DEMO OBSERVABLE OPERATORS ===
            Debug.Log("\n--- DEMO OPERATORS ---");

            // 3a. COMBINE LATEST & SELECT (Kết hợp và Biến đổi)
            // Tạo một Observable mới (_scoreProgressText) từ PlayerScore và MaxScore
            // Nó sẽ được cập nhật bất cứ khi nào PlayerScore HOẶC MaxScore thay đổi.
            _scoreProgressText = PlayerScore.CombineLatest(MaxScore, (score, max) => $"Progress: {score}/{max} ({score / (float)max:P0})");

            // Bind giá trị kết hợp tới một Action
            _disposer.Add(_scoreProgressText.BindTo(progress => { Debug.Log($"[COMBINE] Progress Text Updated: {progress}"); }));

            // Cập nhật MaxScore để kích hoạt CombineLatest
            MaxScore.Value = 200; // Output sẽ là "Progress: 0/200 (0%)"

            // 3b. WHERE (Lọc điều kiện)
            // Chỉ thông báo nếu PlayerScore đạt mức chính xác là 100
            _disposer.Add(PlayerScore.Where(score => score == 100, score => { Debug.Log($"[WHERE] CONGRATS! Score hit the exact milestone: {score}"); }));

            // 3c. DISTINCT UNTIL CHANGED (Chỉ thông báo khi giá trị THỰC SỰ khác)
            // Dùng cho chuỗi tìm kiếm để tránh gọi hàm khi giá trị không đổi
            _disposer.Add(SearchInput.DistinctUntilChanged(input => { Debug.Log($"[DISTINCT] Perform Search Query: '{input}'"); }));

            // 3d. THROTTLE (Giới hạn tốc độ)
            // Mô phỏng sự kiện nhập liệu tần suất cao (ví dụ: kéo Slider liên tục)
            // Chỉ xử lý sự kiện tối đa 1 lần mỗi 0.5 giây.
            _disposer.Add(PlayerScore.Throttle(0.5f, score => { Debug.Log($"[THROTTLE] High-cost action (e.g., VFX update) triggered for score: {score}"); }));
        }

        void OnGUI()
        {
            // Chỉ để demo, không dùng OnGUI trong game thực tế
            GUILayout.BeginArea(new Rect(10, 700, 300, 250), GUI.skin.box);

#if UNITY_EDITOR
            GUILayout.Label("Observable Demo Controls", EditorStyles.boldLabel);
#endif

            // Hiển thị giá trị hiện tại
            GUILayout.Label($"Score: {PlayerScore.Value}");
            GUILayout.Label($"Name: {PlayerName.Value}");

            GUILayout.Space(10);

            // Button để tăng Score nhanh chóng (kích hoạt Throttle)
            if (GUILayout.Button("Increase Score Rapidly (x10)"))
            {
                for (int i = 0; i < 10; i++)
                {
                    PlayerScore.Value = PlayerScore.Value + 1;
                }
            }

            // Button để kích hoạt WHERE
            if (GUILayout.Button("Set Score to 100 (Trigger WHERE)"))
            {
                PlayerScore.Value = 100;
            }

            GUILayout.Space(10);

            // InputField mô phỏng nhập liệu (kích hoạt DistinctUntilChanged)
            GUILayout.Label("Search Input (Distinct):");
            string currentInput = SearchInput.Value;
            string newText = GUILayout.TextField(currentInput);
            if (newText != currentInput)
            {
                SearchInput.Value = newText;
            }

            GUILayout.EndArea();
        }

        void OnDestroy()
        {
            // Rất quan trọng: Hủy tất cả các binding và persistence
            _disposer?.UnbindAll();
            // Xóa PlayerPrefs cho demo tiếp theo
            PlayerPrefs.DeleteKey("Demo_PlayerScore");
            PlayerPrefs.DeleteKey("Demo_PlayerName");
            Debug.Log("PlayerPrefs keys cleared.");
        }
    }
}