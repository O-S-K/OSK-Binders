#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

namespace OSK.Bindings
{
    // Áp dụng CustomEditor cho lớp VisualDataBinder
    [CustomEditor(typeof(VisualDataBinder))]
    public class VisualDataBinderEditor : UnityEditor.Editor
    {
        // Các SerializedProperty cho các trường trong VisualDataBinder
        private SerializedProperty targetComponentProp;
        private SerializedProperty targetPropertyNameProp;
        private SerializedProperty sourceMonoBehaviourProp;
        private SerializedProperty sourceObservableNameProp;
        private SerializedProperty isTwoWayProp;

        // Cần cho logic hiển thị
        private VisualDataBinder _targetBinder;

        private void OnEnable()
        {
            _targetBinder = (VisualDataBinder)target;
            targetComponentProp = serializedObject.FindProperty("TargetComponent");
            targetPropertyNameProp = serializedObject.FindProperty("TargetPropertyName");
            sourceMonoBehaviourProp = serializedObject.FindProperty("SourceMonoBehaviour");
            sourceObservableNameProp = serializedObject.FindProperty("SourceObservableName");
            isTwoWayProp = serializedObject.FindProperty("IsTwoWay");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawTargetSetup();
            EditorGUILayout.Space(10);
            DrawSourceSetup();
            EditorGUILayout.Space(10);

            // Vẽ các thuộc tính còn lại
            EditorGUILayout.PropertyField(isTwoWayProp, new GUIContent("Is Two-Way Binding"));

            // --- NÚT CẬP NHẬT LỰC LƯỠNG (FORCE UPDATE BUTTON) ---
            EditorGUILayout.Space(10);
            if (GUILayout.Button("FORCE UPDATE BINDING (Debug)", GUILayout.Height(30)))
            {
                // Lặp qua tất cả các đối tượng đang được chọn (hỗ trợ multi-editing)
                foreach (VisualDataBinder binder in targets)
                {
                    // Gọi phương thức ForceUpdateBinding() trên component runtime
                    binder.ForceUpdateBinding();
                }
            }
            
            // Hiển thị kết quả Binding
            if (Application.isPlaying && _targetBinder.GetComponent<VisualDataBinder>() != null)
            {
                EditorGUILayout.HelpBox("Binding is active at Runtime.", MessageType.Info);
            }

            serializedObject.ApplyModifiedProperties();
        }

        // ======================= PHẦN THIẾT LẬP TARGET (VIEW) =======================

        private void DrawTargetSetup()
        {
            // 1a. Kéo thả Target Component
            EditorGUILayout.PropertyField(targetComponentProp);
            Component targetComponent = targetComponentProp.objectReferenceValue as Component;

            // 1b. Dropdown chọn Property Name
            if (targetComponent != null)
            {
                var componentType = targetComponent.GetType();
                var bindableProperties = GetBindableTargetProperties(componentType);

                var currentName = targetPropertyNameProp.stringValue;
                var currentIndex = Array.IndexOf(bindableProperties, currentName);

                // Hiển thị Dropdown
                var newIndex = EditorGUILayout.Popup(new GUIContent("Target Property", "Thuộc tính sẽ được cập nhật."),
                    currentIndex, bindableProperties);

                if (newIndex >= 0 && newIndex < bindableProperties.Length)
                {
                    targetPropertyNameProp.stringValue = bindableProperties[newIndex];
                }
                else
                {
                    // Đặt lại thành giá trị đầu tiên nếu chưa có
                    targetPropertyNameProp.stringValue = bindableProperties.FirstOrDefault() ?? "";
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Kéo một UI Component vào trường Target Component để chọn thuộc tính.",
                    MessageType.Warning);
            }
        }

        // ======================= PHẦN THIẾT LẬP SOURCE (MODEL) =======================

        private void DrawSourceSetup()
        {
            // 2a. Kéo thả Source MonoBehaviour
            EditorGUILayout.PropertyField(sourceMonoBehaviourProp);
            MonoBehaviour sourceMono = sourceMonoBehaviourProp.objectReferenceValue as MonoBehaviour;

            // 2b. Dropdown chọn Observable Name
            if (sourceMono != null)
            {
                var sourceObservables = GetObservableFields(sourceMono);

                var currentName = sourceObservableNameProp.stringValue;
                var currentIndex = Array.IndexOf(sourceObservables, currentName);

                // Hiển thị Dropdown
                var newIndex =
                    EditorGUILayout.Popup(new GUIContent("Source Observable", "Observable<T> sẽ cung cấp dữ liệu."),
                        currentIndex, sourceObservables);

                if (newIndex >= 0 && newIndex < sourceObservables.Length)
                {
                    sourceObservableNameProp.stringValue = sourceObservables[newIndex];
                }
                else
                {
                    sourceObservableNameProp.stringValue = sourceObservables.FirstOrDefault() ?? "";
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Kéo một Component chứa Observable<T> vào trường Source Component.",
                    MessageType.Warning);
            }
        }

        // ======================= HELPER METHODS (Dùng Reflection) =======================

        private string[] GetObservableFields(MonoBehaviour mono)
        {
            if (mono == null) return Array.Empty<string>();

            // Chỉ tìm các trường là Observable<T>
            var fields = mono.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(Observable<>))
                .Select(f => f.Name)
                .ToArray();

            return fields;
        }

        private string[] GetBindableTargetProperties(Type componentType)
        {
            var properties = new List<string>();

            if (componentType == null) return Array.Empty<string>();

            // --- 1. BINDING CƠ BẢN (ACTIVE / INTERACTABLE) ---

            // Bất kỳ Component nào cũng có thể điều khiển trạng thái active của GameObject chứa nó.
            if (typeof(Component).IsAssignableFrom(componentType))
            {
                properties.Add("active");
            }

            // Interactable (Dùng cho mọi lớp kế thừa từ Selectable: Button, Toggle, Slider, InputField...)
            if (typeof(Selectable).IsAssignableFrom(componentType))
            {
                properties.Add("interactable");
            }

            // --- 2. BINDING CHO TEXT VÀ GRAPHIC ---

            // TMP_Text & Legacy Text
            if (typeof(TMP_Text).IsAssignableFrom(componentType) || typeof(Text).IsAssignableFrom(componentType))
            {
                properties.Add("text");
                properties.Add("color"); // Qua Graphic
                properties.Add("alpha"); // Qua Graphic color.a
                properties.Add("fontSize");
                properties.Add("enableWordWrapping");
            }
            // Image, RawImage và Graphic cơ bản
            else if (typeof(Graphic).IsAssignableFrom(componentType))
            {
                properties.Add("color");
                properties.Add("alpha");
                if (typeof(Image).IsAssignableFrom(componentType))
                {
                    properties.Add("sprite");
                    properties.Add("fillAmount");
                    properties.Add("type");
                }
            }

            // --- 3. BINDING CHO INPUT VÀ CONTROLS ---

            // Slider
            if (typeof(Slider).IsAssignableFrom(componentType))
            {
                properties.Add("value");
                properties.Add("minValue");
                properties.Add("maxValue");
            }

            // Toggle
            if (typeof(Toggle).IsAssignableFrom(componentType))
            {
                properties.Add("isOn");
            }

            // Input Fields
            if (typeof(TMP_InputField).IsAssignableFrom(componentType) ||
                typeof(InputField).IsAssignableFrom(componentType))
            {
                properties.Add("text");
                properties.Add("readOnly");
            }

            // Dropdown
            if (typeof(TMP_Dropdown).IsAssignableFrom(componentType) ||
                typeof(Dropdown).IsAssignableFrom(componentType))
            {
                properties.Add("value"); // Chỉ mục được chọn (selectedIndex)
            }

            // --- 4. BINDING CHO LAYOUTS VÀ CONTAINERS ---

            // CanvasGroup
            if (typeof(CanvasGroup).IsAssignableFrom(componentType))
            {
                properties.Add("alpha");
                properties.Add("interactable");
                properties.Add("blocksRaycasts");
            }

            // --- 5. BINDING CHO ANIMATION VÀ TRANSFORM ---

            // Animator 
            if (typeof(Animator).IsAssignableFrom(componentType))
            {
                properties.Add("SetBool");
                properties.Add("SetFloat");
                properties.Add("SetTrigger");
                properties.Add("SetInteger");
            }

            // Transform/RectTransform 
            if (typeof(RectTransform).IsAssignableFrom(componentType) || componentType == typeof(Transform))
            {
                properties.Add("localPosition");
                properties.Add("localRotation");
                properties.Add("localScale");
                if (typeof(RectTransform).IsAssignableFrom(componentType))
                {
                    properties.Add("anchoredPosition");
                    properties.Add("sizeDelta");
                }
            }

            // Xóa các trùng lặp và sắp xếp
            return properties.Distinct().OrderBy(s => s).ToArray();
        }
    }
}
#endif