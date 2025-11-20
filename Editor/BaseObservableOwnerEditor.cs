#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;


namespace OSK
{
/// <summary>
/// CustomEditor cho BaseObservableOwner (và các lớp kế thừa).
/// Cung cấp nút Rebind, ForceNotify, auto-rebind on change, và list Observables để debug.
/// </summary>
[CustomEditor(typeof(BaseObservableOwner), true)]
[CanEditMultipleObjects]
public class BaseObservableOwnerEditor : Editor
{
    // Option to auto-call Rebind when inspector changes
    SerializedProperty _dummy; // we don't need specific serialized props, keep for pattern
    bool autoRebind = true;
    Vector2 scroll;

    void OnEnable()
    {
        // Load saved preference if you like (EditorPrefs) - optional
        autoRebind = EditorPrefs.GetBool("Observable_AutoRebind", true);
    }

    void OnDisable()
    {
        EditorPrefs.SetBool("Observable_AutoRebind", autoRebind);
    }

    public override void OnInspectorGUI()
    {
        // Draw default inspector first
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Observable Owner Tools", EditorStyles.boldLabel);
        // Auto-rebind toggle
        autoRebind = EditorGUILayout.ToggleLeft("Auto Rebind on Inspector Change", autoRebind, GUILayout.ExpandWidth(true));
        EditorGUILayout.Space();

        // If inspector got changed and autoRebind enabled -> call rebind
        if (autoRebind && GUI.changed)
        {
            DoRebindTargets();
        }
    }

    // Helpers for multi-target support
    void DoRebindTargets()
    {
        foreach (var t in targets())
        {
            if (t == null) continue;
            var method = t.GetType().GetMethod("RebindObservables", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method != null)
            {
                method.Invoke(t, null);
                Debug.Log($"[BaseObservableOwnerEditor] RebindObservables called on {t.name} ({t.GetType().Name})");
            }
            else
            {
                Debug.LogWarning($"[BaseObservableOwnerEditor] Target {t.name} does not implement RebindObservables()");
            }
        }
    }

    void DoForceNotifyTargets()
    {
        foreach (var t in targets())
        {
            int count = ForceNotifyOnObjectFields(t);
            Debug.Log($"[BaseObservableOwnerEditor] ForceNotify invoked on {t.name}: {count} observables");
        }
    }

    // Get current selected targets (cast to BaseObservableOwner)
    BaseObservableOwner[] targets()
    {
        return targetsCast();
    }

    BaseObservableOwner[] targetsCast()
    {
        var objs = serializedObject.targetObjects; // UnityEngine.Object[]
        return Array.ConvertAll<UnityEngine.Object, BaseObservableOwner>(objs, obj => obj as BaseObservableOwner);
    }
 
    bool targetsExist() => serializedObject.targetObjects != null && serializedObject.targetObjects.Length > 0;

    struct ObsInfo { public string FieldPath; public string TypeName; public string ValueString; }

    List<ObsInfo> GetObservablesInfo(UnityEngine.Object targetObj)
    {
        var list = new List<ObsInfo>();
        if (targetObj == null) return list;

        var t = targetObj.GetType();
        var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var f in fields)
        {
            try
            {
                var val = f.GetValue(targetObj);
                if (val == null) continue;
                var vt = val.GetType();
                if (vt.IsGenericType && vt.GetGenericTypeDefinition() == typeof(Observable<>))
                {
                    // try read value via reflection
                    var fv = vt.GetField("_value", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    object v = fv != null ? fv.GetValue(val) : "(unknown)";
                    list.Add(new ObsInfo { FieldPath = f.Name, TypeName = vt.Name, ValueString = v != null ? v.ToString() : "null" });
                }
            }
            catch { /* ignore reading errors */ }
        }
        return list;
    }

    // ForceNotify reflective traversal for single object
    int ForceNotifyOnObjectFields(object obj)
    {
        if (obj == null) return 0;
        int notified = 0;
        Type t = obj.GetType();
        var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var f in fields)
        {
            try
            {
                object value = f.GetValue(obj);
                notified += HandlePotentialObservable(value);
                if (value is IEnumerable enumerable && !(value is string))
                {
                    foreach (var item in enumerable) notified += HandlePotentialObservable(item);
                }
            }
            catch { }
        }
        return notified;
    }

    int HandlePotentialObservable(object instance)
    {
        if (instance == null) return 0;
        var it = instance.GetType();
        if (it.IsGenericType && it.GetGenericTypeDefinition() == typeof(Observable<>))
        {
            var mi = it.GetMethod("ForceNotify", BindingFlags.Instance | BindingFlags.Public);
            if (mi != null)
            {
                try { mi.Invoke(instance, null); return 1; }
                catch { return 0; }
            }
        }
        return 0;
    }
}
#endif

}
