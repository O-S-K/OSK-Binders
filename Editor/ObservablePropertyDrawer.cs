#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;

namespace OSK
{
    /// <summary>
    /// PropertyDrawer cho Observable<T>, bổ sung hỗ trợ ObservableRangeAttribute cho Observable<float>.
    /// Đặt file vào Assets/Editor/ObservablePropertyDrawer.cs
    /// </summary>
    [CustomPropertyDrawer(typeof(Observable<>), true)]
    public class ObservablePropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var valueProp = property.FindPropertyRelative("_value");
            if (valueProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Observable missing _value");
                return;
            }

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();

            // Attempt to get ObservableRangeAttribute applied to the field (if any)
            ObservableRangeAttribute rangeAttr = null;
            try
            {
                // fieldInfo can be null in some cases (like property drawer used in certain contexts)
                if (fieldInfo != null)
                {
                    rangeAttr = fieldInfo.GetCustomAttribute<ObservableRangeAttribute>(true);
                }
            }
            catch
            {
                rangeAttr = null;
            }

            // If we have a range attribute AND the inner serialized property is a float, draw slider
            if (rangeAttr != null && valueProp.propertyType == SerializedPropertyType.Float)
            {
                // Draw slider and clamp value to min/max
                float current = valueProp.floatValue;
                float newVal = EditorGUI.Slider(position, label, current, rangeAttr.Min, rangeAttr.Max);
                // assign via serialized property - this will be applied below in ApplyModifiedProperties
                valueProp.floatValue = Mathf.Clamp(newVal, rangeAttr.Min, rangeAttr.Max);
            }
            else
            {
                // Default drawing for inner value (handles all types)
                position.height = EditorGUI.GetPropertyHeight(valueProp, label, true);
                EditorGUI.PropertyField(position, valueProp, label, true);
            }

            if (EditorGUI.EndChangeCheck())
            {
                // Apply changes to serialized object
                property.serializedObject.ApplyModifiedProperties();

                // Support Undo & mark dirty for all targets
                foreach (var target in property.serializedObject.targetObjects)
                {
                    if (target is UnityEngine.Object uo)
                    {
                        Undo.RecordObject(uo, $"Modify {property.displayName}");
                        EditorUtility.SetDirty(uo);
                    }
                }

                // ForceNotify on the observable instance(s)
                foreach (var t in property.serializedObject.targetObjects)
                    TryForceNotify(t, property.propertyPath);

                // If owner implements IObservableOwner, ask it to rebind (handles instance replacement)
                foreach (var t in property.serializedObject.targetObjects)
                    TryCallRebind(t);

                // Repaint scene so changes appear
                SceneView.RepaintAll();
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var v = property.FindPropertyRelative("_value");
            return v == null ? base.GetPropertyHeight(property, label) : EditorGUI.GetPropertyHeight(v, label, true);
        }

        // Walk property path to find the Observable<T> instance and call ForceNotify()
        private void TryForceNotify(object targetObj, string propertyPath)
        {
            if (targetObj == null) return;

            try
            {
                object container = targetObj;
                var parts = propertyPath.Split('.');

                for (int i = 0; i < parts.Length && container != null; i++)
                {
                    var part = parts[i];
                    if (part == "Array")
                    {
                        i++;
                        continue;
                    }

                    if (part.StartsWith("data["))
                    {
                        continue;
                    }

                    var t = container.GetType();
                    var f = t.GetField(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f != null)
                    {
                        container = f.GetValue(container);
                        continue;
                    }

                    var p = t.GetProperty(part, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null)
                    {
                        container = p.GetValue(container);
                        continue;
                    }

                    container = null;
                }

                if (container == null) return;

                var ct = container.GetType();
                if (ct.IsGenericType && ct.GetGenericTypeDefinition() == typeof(Observable<>))
                {
                    var mi = ct.GetMethod("ForceNotify", BindingFlags.Instance | BindingFlags.Public);
                    mi?.Invoke(container, null);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ObservableDrawer] TryForceNotify failed for path '{propertyPath}': {ex.Message}");
            }
        }

        // If target implements IObservableOwner then call RebindObservables()
        private void TryCallRebind(object targetObj)
        {
            if (targetObj == null) return;
            var ttype = targetObj.GetType();
            if (typeof(IObservableOwner).IsAssignableFrom(ttype))
            {
                try
                {
                    var mi = ttype.GetMethod("RebindObservables",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    mi?.Invoke(targetObj, null);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ObservableDrawer] RebindObservables failed on {ttype.FullName}: {ex.Message}");
                }
            }
        }
    }
#endif
}