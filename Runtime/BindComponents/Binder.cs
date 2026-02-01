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

        // --- OPTIMIZATION 1: CACHING REFLECTION ---
        private static readonly Dictionary<Type, List<FieldBindData>> _typeCache = new Dictionary<Type, List<FieldBindData>>();

        // --- OPTIMIZATION 2: SHARED BUFFER (NON-ALLOC) ---
        // List này dùng chung để hứng kết quả tìm kiếm từ Unity, tránh tạo Array mới mỗi lần gọi
        private static readonly List<Component> _sharedComponentList = new List<Component>(32);

        private class FieldBindData
        {
            public FieldInfo Field;
            public BindAttribute Attribute;
            // Cache luôn logic check type để tránh gọi IsValueType nhiều lần
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

            // Loop dùng for thay vì foreach để tránh iterator allocation (dù List.Enumerator là struct nhưng cẩn thận vẫn hơn)
            var count = bindList.Count;
            for (int i = 0; i < count; i++)
            {
                BindField(target, bindList[i]);
            }
        }

        static void BindField(object target, FieldBindData data)
        {
            object resolved = ResolveField(target, data);

            // 1. Check nếu không tìm thấy gì cả (Object null)
            if (resolved == null)
            {
                if (!data.IsValueType && !data.Attribute.AllowNull)
                {
                    // Warning: Không tìm thấy Object nào
                    Debug.LogWarning($"[Binder] ⚠️ Missing Object for field '{data.Field.Name}' on '{target.GetType().Name}'", target as Object);
                }
                return;
            }

            try
            {
                // 2. Check gán trực tiếp (Ví dụ field là Transform, resolved là Transform)
                if (data.FieldType.IsAssignableFrom(resolved.GetType()))
                {
                    data.Field.SetValue(target, resolved);
                    return;
                }

                // 3. Xử lý logic GameObject -> Component
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
                        // --- ĐÂY LÀ CHỖ BẠN ĐANG THIẾU ---
                        // Tìm thấy GameObject, nhưng không có Component cần thiết trên đó
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
                    
                    // Interface binding
                    if (data.FieldType.IsInterface)
                    {
                         var comp = GetComponentNonAlloc(compResolved.gameObject, data.FieldType);
                         if (comp != null) 
                         {
                             data.Field.SetValue(target, comp);
                         }
                         else
                         {
                             // Tìm thấy component cha/con, nhưng không implement Interface cần thiết
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
                    // Sử dụng hàm tìm kiếm Non-Alloc
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
                        return Object.FindObjectOfType(type); // Hàm này của Unity vẫn sẽ alloc, ko tránh được nếu dùng From.Scene
                    }
                    return null;

                case From.Resources:
                    return Resources.Load(attr.ResourcePath, type);

                case From.StaticMethod:
                    if (attr.StaticType != null && !string.IsNullOrEmpty(attr.MethodName))
                    {
                        // Reflection Invoke vẫn có overhead nhỏ, nhưng ít dùng
                        var m = attr.StaticType.GetMethod(attr.MethodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (m != null) return m.Invoke(null, null);
                    }
                    return null;

                default:
                    return null;
            }
        }

        #region Zero-Alloc Helpers

        // Hàm thay thế GetComponent để đảm bảo không sinh rác
        static object GetComponentNonAlloc(GameObject go, Type type)
        {
            if (go == null) return null;
            if (type == typeof(GameObject)) return go;
            if (type == typeof(Transform)) return go.transform;

            // GetComponent(Type) của Unity cơ bản là Non-Alloc.
            // Tuy nhiên với Interface, GetComponents(List) an toàn hơn.
            if (type.IsInterface)
            {
                go.GetComponents(type, _sharedComponentList);
                if (_sharedComponentList.Count > 0)
                {
                    var res = _sharedComponentList[0];
                    _sharedComponentList.Clear(); // Quan trọng: Clear ngay sau khi dùng
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

            // FIX: Unity không hỗ trợ (Type, List). Ta dùng <Component> để lấy TẤT CẢ component.
            // Điều này vẫn Zero-Alloc vì dùng List đệm, nhưng sẽ tốn CPU hơn xíu để duyệt list.
            go.GetComponentsInChildren<Component>(includeInactive, _sharedComponentList);

            Component result = null;
            var count = _sharedComponentList.Count;
            
            for (int i = 0; i < count; i++)
            {
                var c = _sharedComponentList[i];
                // Tự kiểm tra Type bằng tay
                if (type.IsAssignableFrom(c.GetType()))
                {
                    result = c;
                    break; // Tìm thấy cái đầu tiên thì dừng ngay
                }
            }

            _sharedComponentList.Clear(); // Dọn sạch list ngay lập tức
            return result;
        }

        static object GetComponentInParentNonAlloc(GameObject go, Type type)
        {
            if (go == null) return null;
            if (type == typeof(GameObject)) return go;

            // FIX: Tương tự như trên
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