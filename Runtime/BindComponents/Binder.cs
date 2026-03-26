using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OSK.Bindings
{
    public static class Binder
    {
        public static bool IsLogEnabled = false;
        private static readonly Dictionary<Type, List<FieldBindData>> _typeCache = new Dictionary<Type, List<FieldBindData>>();
        private static readonly List<Component> _sharedComponentList = new List<Component>(32);

        private class FieldBindData
        {
            public FieldInfo Field;
            public BindAttribute Attribute;
            public bool IsValueType; 
            public Type FieldType;
        }

        public static void AutoBind(object target)
        {
            if (target == null) return;

            var targetType = target.GetType();

            if (!_typeCache.TryGetValue(targetType, out var bindList))
            {
                bindList = new List<FieldBindData>();
                var fields = targetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var f in fields)
                {
                    var attr = f.GetCustomAttribute<BindAttribute>(true);
                    if (attr != null)
                    {
                        bindList.Add(new FieldBindData { 
                            Field = f, 
                            Attribute = attr,
                            IsValueType = f.FieldType.IsValueType,
                            FieldType = f.FieldType
                        });
                    }
                }
                _typeCache[targetType] = bindList;
            }
            var count = bindList.Count;
            for (int i = 0; i < count; i++)
            {
                BindField(target, bindList[i]);
            }
        }

        static void BindField(object target, FieldBindData data)
        {
            
            try 
            {
                var currentValue = data.Field.GetValue(target);
                
                // Kiểm tra xem field có null không (Support cả Unity Object "fake null")
                bool isAssigned = currentValue != null;
                if (currentValue is Object unityObj) 
                {
                    isAssigned = unityObj != null; // Check kiểu Unity (tránh trường hợp Missing Ref)
                }

                // Nếu đã có giá trị rồi -> Bỏ qua, không làm gì cả (Tiết kiệm CPU)
                if (isAssigned) 
                {
                    // Nếu muốn debug xem cái nào được skip thì bật dòng này
                    if (IsLogEnabled) Debug.Log($"[Binder] Skip '{data.Field.Name}' because it is already assigned.");
                    return; 
                }
            }
            catch 
            { 
                // Ignored: Nếu lỗi lúc GetValue thì cứ để nó chạy logic bind bên dưới thử xem sao
            }
            
            object resolved = ResolveField(target, data);
            if (resolved == null)
            {
                if (!data.IsValueType && !data.Attribute.AllowNull)
                {
                    Debug.LogWarning($"[Binder] ⚠️ Missing Object for field '{data.Field.Name}' on '{target.GetType().Name}'", target as Object);
                }
                return;
            }

            try
            {
                if (data.FieldType.IsAssignableFrom(resolved.GetType()))
                {
                    data.Field.SetValue(target, resolved);
                    return;
                }
                if (resolved is GameObject go)
                {
                    if (data.FieldType == typeof(GameObject))
                    {
                        data.Field.SetValue(target, go);
                        return;
                    }
                    
                    var comp = GetComponentNonAlloc(go, data.FieldType);
                    if (comp != null) 
                    {
                        data.Field.SetValue(target, comp);
                    }
                    else
                    {
                        Debug.LogError($"[Binder] ❌ Found GameObject '{go.name}' but it MISSING component '{data.FieldType.Name}' for field '{data.Field.Name}'", target as Object);
                    }
                }
                else if (resolved is Component compResolved)
                {
                    if (data.FieldType == typeof(GameObject))
                    {
                        data.Field.SetValue(target, compResolved.gameObject);
                        return;
                    }
                    if (data.FieldType.IsInterface)
                    {
                         var comp = GetComponentNonAlloc(compResolved.gameObject, data.FieldType);
                         if (comp != null) 
                         {
                             data.Field.SetValue(target, comp);
                         }
                         else
                         {
                             Debug.LogError($"[Binder] ❌ Found object '{compResolved.name}' but missing Interface '{data.FieldType.Name}' for field '{data.Field.Name}'", target as Object);
                         }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Binder] ☠️ Crash setting {data.Field.Name}: {ex}", target as Object);
            }
        }

        static object ResolveField(object owner, FieldBindData data)
        {
            var ownerComp = owner as Component;
            GameObject ownerGo = ownerComp != null ? ownerComp.gameObject : null;
            var attr = data.Attribute;
            var type = data.FieldType;

            switch (attr.From)
            {
                case From.Self:
                    return ownerGo ? GetComponentNonAlloc(ownerGo, type) : null;

                case From.Children:
                    return ownerGo ? GetComponentInChildrenNonAlloc(ownerGo, type, attr.IncludeInactive) : null;

                case From.Parent:
                    return ownerGo ? GetComponentInParentNonAlloc(ownerGo, type) : null;

                case From.Scene:
                    if (attr.FindMode == FindBy.Tag && !string.IsNullOrEmpty(attr.Tag))
                    {
                        var goTag = GameObject.FindWithTag(attr.Tag);
                        return ExtractFromGameObject(goTag, type);
                    }
                    
                    if (attr.FindMode == FindBy.Name && !string.IsNullOrEmpty(attr.Name))
                    {
                        var goName = GameObject.Find(attr.Name);
                        return ExtractFromGameObject(goName, type);
                    }

                    if (attr.FindMode == FindBy.Type)
                    {
                        return Object.FindObjectOfType(type); 
                    }
                    return null;

                case From.Resources:
                    return Resources.Load(attr.ResourcePath, type);

                case From.StaticMethod:
                    if (attr.StaticType != null && !string.IsNullOrEmpty(attr.MethodName))
                    {
                        var m = attr.StaticType.GetMethod(attr.MethodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (m != null) return m.Invoke(null, null);
                    }
                    return null;

                default:
                    return null;
            }
        }

        #region Zero-Alloc Helpers
        static object GetComponentNonAlloc(GameObject go, Type type)
        {
            if (go == null) return null;
            if (type == typeof(GameObject)) return go;
            if (type == typeof(Transform)) return go.transform;
            if (type.IsInterface)
            {
                go.GetComponents(type, _sharedComponentList);
                if (_sharedComponentList.Count > 0)
                {
                    var res = _sharedComponentList[0];
                    _sharedComponentList.Clear();
                    return res;
                }
                return null;
            }
            return go.GetComponent(type);
        }

        static object GetComponentInChildrenNonAlloc(GameObject go, Type type, bool includeInactive)
        {
            if (go == null) return null;
            if (type == typeof(GameObject)) return go;
            go.GetComponentsInChildren(includeInactive, _sharedComponentList);

            Component result = null;
            var count = _sharedComponentList.Count;
            
            for (int i = 0; i < count; i++)
            {
                var c = _sharedComponentList[i];
                if (type.IsAssignableFrom(c.GetType()))
                {
                    result = c;
                    break;
                }
            }

            _sharedComponentList.Clear();
            return result;
        }

        static object GetComponentInParentNonAlloc(GameObject go, Type type)
        {
            if (go == null) return null;
            if (type == typeof(GameObject)) return go;
            go.GetComponentsInParent(true, _sharedComponentList);

            Component result = null;
            var count = _sharedComponentList.Count;

            for (int i = 0; i < count; i++)
            {
                var c = _sharedComponentList[i];
                if (type.IsAssignableFrom(c.GetType()))
                {
                    result = c;
                    break;
                }
            }

            _sharedComponentList.Clear();
            return result;
        }

        static object ExtractFromGameObject(GameObject go, Type desiredType)
        {
            if (go == null) return null;
            if (desiredType == typeof(GameObject)) return go;
            return GetComponentNonAlloc(go, desiredType);
        }

        #endregion
    }
}