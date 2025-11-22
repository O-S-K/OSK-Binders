using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OSK.Bindings
{
    public static class Binder
    {
        /// <summary>
        /// AutoBind fields annotated with [Bind] on target.
        /// By default will not overwrite non-null fields unless force==true or attribute.SearchOnlyIfNull=false.
        /// </summary>
        public static void AutoBind(object target)
        {
            if (target == null)
            {
                Debug.LogWarning("[Binder] AutoBind called with null target.");
                return;
            }

            var targetType = target.GetType();
            var fields = targetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Debug.Log($"[Binder] AutoBind for {targetType.Name} - scanning {fields.Length} fields.");

            foreach (var f in fields)
            {
                var attr = f.GetCustomAttribute<BindAttribute>(true);
                if (attr == null) continue;

                object resolved = null;
                try
                {
                    resolved = ResolveField(target, f, attr);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Binder] ResolveField exception for '{f.Name}': {ex}");
                    resolved = null;
                }

                if (resolved == null)
                {
                    if (!attr.AllowNull)
                        Debug.LogWarning($"[Binder] Could not bind '{f.Name}' on {targetType.Name} using {attr.From}.");
                    else
                        Debug.Log($"[Binder] '{f.Name}' resolved to null (allowed).");

                    // if force and reference type -> clear
                    if (!f.FieldType.IsValueType)
                    {
                        try { f.SetValue(target, null); } catch { }
                    }
                    continue;
                }

                // try to assign value
                try
                {
                    // direct assign
                    if (f.FieldType.IsAssignableFrom(resolved.GetType()))
                    {
                        f.SetValue(target, resolved);
                        Debug.Log($"[Binder] Bound '{f.Name}' <- {resolved.GetType().Name} on {targetType.Name}");
                        continue;
                    }

                    // resolved GameObject -> field expects Component or GameObject
                    if (resolved is GameObject go)
                    {
                        if (f.FieldType == typeof(GameObject))
                        {
                            f.SetValue(target, go);
                            Debug.Log($"[Binder] Bound '{f.Name}' as GameObject.");
                            continue;
                        }

                        var comp = GetComponentByTypeOrInterface(go, f.FieldType);
                        if (comp != null)
                        {
                            f.SetValue(target, comp);
                            Debug.Log($"[Binder] Bound '{f.Name}' <- {f.FieldType.Name} (from GameObject)");
                            continue;
                        }
                    }

                    // resolved Component -> field expects GameObject
                    if (resolved is Component compResolved && f.FieldType == typeof(GameObject))
                    {
                        f.SetValue(target, compResolved.gameObject);
                        Debug.Log($"[Binder] Bound '{f.Name}' gameObject <- component {compResolved.GetType().Name}");
                        continue;
                    }

                    // if field is interface, try find component implementing that interface (in owner)
                    if (f.FieldType.IsInterface)
                    {
                        var ownerComp = target as Component;
                        if (ownerComp != null)
                        {
                            var found = ownerComp.GetComponentsInChildren<Component>(attr.IncludeInactive)
                                .FirstOrDefault(c => f.FieldType.IsAssignableFrom(c.GetType()));
                            if (found != null)
                            {
                                f.SetValue(target, found);
                                Debug.Log($"[Binder] Bound interface '{f.Name}' -> {found.GetType().Name}");
                                continue;
                            }
                        }
                    }

                    Debug.LogWarning($"[Binder] Resolved value for '{f.Name}' ({resolved.GetType().Name}) is not assignable to {f.FieldType.Name}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Binder] Failed to set '{f.Name}' on {targetType.Name}: {ex}");
                }
            }

            Debug.Log($"[Binder] AutoBind finished for {targetType.Name}");
        }

        static object ResolveField(object owner, FieldInfo field, BindAttribute attr)
        {
            var ownerComp = owner as Component;
            var fieldType = field.FieldType;

            switch (attr.From)
            {
                case From.Self:
                    if (ownerComp != null) return GetComponentByTypeOrInterface(ownerComp.gameObject, fieldType);
                    return null;

                case From.Children:
                    if (ownerComp != null) return GetComponentInChildrenByTypeOrInterface(ownerComp.gameObject, fieldType, attr.IncludeInactive);
                    return null;

                case From.Parent:
                    if (ownerComp != null) return GetComponentInParentByTypeOrInterface(ownerComp.gameObject, fieldType);
                    return null;

                case From.Scene:
                    // use FindBy if provided:
                    if (attr.FindMode.HasValue)
                    {
                        switch (attr.FindMode.Value)
                        {
                            case FindBy.Tag:
                                if (string.IsNullOrEmpty(attr.Tag)) { Debug.LogWarning("[Binder] FindBy.Tag requires Tag."); return null; }
                                var goTag = GameObject.FindWithTag(attr.Tag);
                                if (goTag == null) return null;
                                return ExtractFromGameObject(goTag, fieldType, attr);
                            case FindBy.Name:
                                if (string.IsNullOrEmpty(attr.Name)) { Debug.LogWarning("[Binder] FindBy.Name requires Name."); return null; }
                                var goName = FindGameObjectByName(attr.Name);
                                if (goName == null) return null;
                                return ExtractFromGameObject(goName, fieldType, attr);
                            case FindBy.Type:
                                return FindObjectOfType(fieldType, attr.Name);
                        }
                    }
                    // fallback: try first root object that contains matching component
                    var roots = ownerComp != null ? ownerComp.gameObject.scene.GetRootGameObjects() : UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                    foreach (var r in roots)
                    {
                        var res = GetComponentInChildrenByTypeOrInterface(r, fieldType, attr.IncludeInactive);
                        if (res != null) return res;
                    }
                    return null;

                case From.Resources:
                    if (string.IsNullOrEmpty(attr.ResourcePath)) { Debug.LogWarning("[Binder] Resources requires ResourcePath."); return null; }
                    return Resources.Load(attr.ResourcePath, fieldType);

                case From.StaticMethod:
                    if (attr.StaticType == null || string.IsNullOrEmpty(attr.MethodName)) { Debug.LogWarning("[Binder] StaticMethod requires StaticType and MethodName."); return null; }
                    return InvokeStaticMethod(attr.StaticType, attr.MethodName, fieldType);

                case From.Method:
                    if (string.IsNullOrEmpty(attr.MethodName)) { Debug.LogWarning("[Binder] Method requires MethodName."); return null; }
                    // try owner first
                    if (owner != null)
                    {
                        var inst = InvokeInstanceMethod(owner, attr.MethodName, fieldType);
                        if (inst != null) return inst;
                    }
                    if (ownerComp != null)
                    {
                        foreach (var c in ownerComp.GetComponents<Component>())
                        {
                            if (c == ownerComp) continue;
                            var r = InvokeInstanceMethod(c, attr.MethodName, fieldType);
                            if (r != null) return r;
                        }
                    }
                    return null;

                default:
                    return null;
            }
        }

        #region Resolve Helpers

        static object ExtractFromGameObject(GameObject go, Type desiredType, BindAttribute attr)
        {
            if (go == null) return null;
            if (desiredType == typeof(GameObject)) return go;
            if (desiredType == typeof(Transform)) return go.transform;
            return GetComponentByTypeOrInterface(go, desiredType);
        }

        static GameObject FindGameObjectByName(string name)
        {
            var all = Object.FindObjectsOfType<GameObject>();
            foreach (var go in all)
            {
                if (go.name == name) return go;
            }
            return null;
        }

        static object FindObjectOfType(Type targetType, string nameFilter = null)
        {
            if (targetType == null) return null;
            var all = Object.FindObjectsOfType(targetType);
            if (all == null || all.Length == 0) return null;
            if (string.IsNullOrEmpty(nameFilter)) return all[0];
            foreach (var o in all)
            {
                var c = o as Component;
                if (c != null && c.gameObject.name == nameFilter) return c;
                if (o is GameObject go && go.name == nameFilter) return go;
            }
            return null;
        }

        static object InvokeStaticMethod(Type staticType, string methodName, Type expectedReturn)
        {
            try
            {
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                var m = staticType.GetMethod(methodName, flags, null, Type.EmptyTypes, null);
                if (m == null) return null;
                var result = m.Invoke(null, null);
                if (result == null) return null;
                if (expectedReturn.IsAssignableFrom(result.GetType())) return result;
                if (result is GameObject go && typeof(Object).IsAssignableFrom(expectedReturn))
                {
                    var comp = GetComponentByTypeOrInterface(go, expectedReturn);
                    if (comp != null) return comp;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Binder] Static method invoke error: {ex}");
            }
            return null;
        }

        static object InvokeInstanceMethod(object instance, string methodName, Type expectedReturn)
        {
            if (instance == null) return null;
            try
            {
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var m = instance.GetType().GetMethod(methodName, flags, null, Type.EmptyTypes, null);
                if (m == null) return null;
                var result = m.Invoke(instance, null);
                if (result == null) return null;
                if (expectedReturn.IsAssignableFrom(result.GetType())) return result;
                if (result is GameObject go && typeof(Object).IsAssignableFrom(expectedReturn))
                {
                    var comp = GetComponentByTypeOrInterface(go, expectedReturn);
                    if (comp != null) return comp;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Binder] Instance method invoke error: {ex}");
            }
            return null;
        }

        static object GetComponentByTypeOrInterface(GameObject go, Type type)
        {
            if (go == null || type == null) return null;
            if (type == typeof(GameObject)) return go;
            if (type == typeof(Transform)) return go.transform;
            if (typeof(Component).IsAssignableFrom(type))
            {
                var comp = go.GetComponent(type);
                if (comp != null) return comp;

                // interface support: find any component implementing the interface
                if (type.IsInterface)
                {
                    foreach (var c in go.GetComponents<Component>())
                    {
                        if (type.IsAssignableFrom(c.GetType())) return c;
                    }
                }

                // fallback to children
                var found = go.GetComponentsInChildren<Component>(true)
                    .FirstOrDefault(c => type.IsAssignableFrom(c.GetType()));
                if (found != null) return found;
            }
            return null;
        }

        static object GetComponentInChildrenByTypeOrInterface(GameObject go, Type type, bool includeInactive)
        {
            if (go == null || type == null) return null;
            if (type == typeof(GameObject))
            {
                var t = go.transform;
                if (t.childCount > 0) return t.GetChild(0).gameObject;
                return null;
            }
            if (type == typeof(Transform))
            {
                return go.GetComponentsInChildren<Transform>(includeInactive).FirstOrDefault();
            }
            if (typeof(Component).IsAssignableFrom(type))
            {
                try
                {
                    var method = typeof(GameObject).GetMethod("GetComponentInChildren", new Type[] { typeof(Type), typeof(bool) });
                    if (method != null)
                    {
                        var res = method.Invoke(go, new object[] { type, includeInactive });
                        if (res != null) return res;
                    }
                }
                catch { /* fallback */ }

                var comps = go.GetComponentsInChildren<Component>(includeInactive);
                foreach (var c in comps)
                {
                    if (type.IsAssignableFrom(c.GetType())) return c;
                }
            }
            return null;
        }

        static object GetComponentInParentByTypeOrInterface(GameObject go, Type type)
        {
            if (go == null || type == null) return null;
            if (type == typeof(GameObject)) return go;
            if (type == typeof(Transform)) return go.transform.parent;
            try
            {
                var method = typeof(GameObject).GetMethod("GetComponentInParent", new Type[] { typeof(Type) });
                if (method != null)
                {
                    var res = method.Invoke(go, new object[] { type });
                    if (res != null) return res;
                }
            }
            catch { /* fallback */ }

            var t = go.transform.parent;
            while (t != null)
            {
                var c = t.gameObject.GetComponent(type);
                if (c != null) return c;
                t = t.parent;
            }
            return null;
        }
        #endregion
    }
}
