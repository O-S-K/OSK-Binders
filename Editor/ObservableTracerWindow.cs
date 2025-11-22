#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OSK.Bindings
{
    /// <summary>
    /// Editor window to trace all events happening across all Observable instances in real-time.
    /// </summary>
    public class ObservableTracerWindow : EditorWindow
    {
        private static List<TraceEntry> _log = new List<TraceEntry>();
        private Vector2 _scrollPos;
        private bool _isTracing = false;

        private struct TraceEntry
        {
            public float Timestamp;
            public string TypeName;
            public string Source;
            public string OldValue;
            public string NewValue;
        }

        [MenuItem("OSK-Binders/Observable Tracer")]
        public static void ShowWindow()
        {
            GetWindow<ObservableTracerWindow>("Observable Tracer").StartTracing();
        }

        private void OnEnable()
        {
            StartTracing();
        }

        private void OnDisable()
        {
            StopTracing();
        }

        private void StartTracing()
        {
            if (_isTracing) return;
            // Đăng ký vào Global Tracker Hub
            ObservableTracker.OnGlobalObservableChanged += HandleTrace; 
            _isTracing = true;
            Debug.Log("[Observable Tracer] Tracing started.");
        }

        private void StopTracing()
        {
            if (!_isTracing) return;
            // Hủy đăng ký khỏi Global Tracker Hub
            ObservableTracker.OnGlobalObservableChanged -= HandleTrace; 
            _isTracing = false;
            Debug.Log("[Observable Tracer] Tracing stopped.");
        }

        // Chữ ký hàm HandleTrace phải được cập nhật để khớp với sự kiện mới
        private void HandleTrace(string typeName, string source, object oldValue, object newValue) 
        {
            // Giới hạn log để tránh tràn bộ nhớ
            if (_log.Count > 200) _log.RemoveAt(0);
            
            _log.Add(new TraceEntry
            {
                Timestamp = Time.timeSinceLevelLoad,
                TypeName = typeName,
                Source = source,
                OldValue = oldValue?.ToString() ?? "NULL",
                NewValue = newValue?.ToString() ?? "NULL"
            });
            Repaint();
        }

        public void OnGUI()
        {
            GUILayout.Label("OSK Observable Tracing", EditorStyles.boldLabel);
            
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(_isTracing ? "STOP TRACING" : "START TRACING"))
            {
                if (_isTracing) StopTracing();
                else StartTracing();
            }

            if (GUILayout.Button("Clear Log"))
            {
                _log.Clear();
            }
            GUILayout.EndHorizontal();

            if (!_isTracing)
            {
                EditorGUILayout.HelpBox("Tracing is currently stopped. Press START TRACING to capture observable events.", MessageType.Warning);
            }
            
            EditorGUILayout.Space();

            // Vùng hiển thị Log
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            // Header
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Time", GUILayout.Width(60));
            GUILayout.Label("Type", GUILayout.Width(80));
            GUILayout.Label("Source", GUILayout.Width(80));
            GUILayout.Label("Old Value", GUILayout.MinWidth(50));
            GUILayout.Label("New Value", GUILayout.MinWidth(50));
            GUILayout.EndHorizontal();

            for (int i = _log.Count - 1; i >= 0; i--)
            {
                var entry = _log[i];
                // Tô màu xen kẽ
                Color bgColor = (i % 2 == 0) ? Color.white * 0.95f : Color.white;
                GUI.backgroundColor = bgColor;

                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.Label($"{entry.Timestamp:F2}s", GUILayout.Width(60));
                GUILayout.Label(entry.TypeName, GUILayout.Width(80));
                GUILayout.Label(entry.Source, GUILayout.Width(80));
                GUILayout.Label(entry.OldValue);
                GUILayout.Label(entry.NewValue);
                GUILayout.EndHorizontal();
                
                GUI.backgroundColor = Color.white;
            }
            
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif