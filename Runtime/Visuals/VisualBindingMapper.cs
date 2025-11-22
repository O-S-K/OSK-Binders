using System;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OSK.Bindings
{
    public static class VisualBindingMapper
    {
        public static IDisposable CreateBinding(
            Component targetComponent,
            string targetPropertyName,
            MonoBehaviour sourceMonoBehaviour,
            string sourceObservableName,
            bool isTwoWay,
            bool isPlaying)
        {
            // 1. Lấy Observable Instance
            var sourceFieldInfo = sourceMonoBehaviour.GetType().GetField(sourceObservableName);

            if (sourceFieldInfo == null)
            {
                Debug.LogError(
                    $"[VisualMapper] Field '{sourceObservableName}' not found on {sourceMonoBehaviour.GetType().Name}.");
                return null;
            }

            var sourceInstance = sourceFieldInfo.GetValue(sourceMonoBehaviour);
            if (sourceInstance == null || !sourceInstance.GetType().IsGenericType ||
                sourceInstance.GetType().GetGenericTypeDefinition() != typeof(Observable<>))
            {
                Debug.LogError($"[VisualMapper] Field '{sourceObservableName}' is not a valid Observable<T>.");
                return null;
            }

            // Lấy kiểu T của Observable 
            Type observableTypeT = sourceInstance.GetType().GetGenericArguments()[0];

            // 2. Định nghĩa Target Method và Xử lý Binding
            MethodInfo bindMethod = null;
            Type uiBindingsType = typeof(UIBindingExtensions);
            string prop = targetPropertyName.ToLower();

            // --- XỬ LÝ ĐẶC BIỆT CHO 'active' (Sử dụng cho bất kỳ Component nào với Observable<bool>) ---
            if (prop == "active" && observableTypeT == typeof(bool))
            {
                // Signature: public static IDisposable BindActive(this GameObject go, Observable<bool> src)
                bindMethod =
                    uiBindingsType.GetMethod("BindActive", new[] { typeof(GameObject), typeof(Observable<bool>) });

                if (bindMethod != null)
                {
                    // Lô-gic gọi hàm đặc biệt: tham số 'this' là GameObject, không phải Component
                    try
                    {
                        var result = bindMethod.Invoke(null,
                            new object[] { targetComponent.gameObject, sourceInstance });

                        // Bắt buộc refresh Editor cho GameObject
#if UNITY_EDITOR
                        if (!isPlaying && targetComponent != null && targetComponent.gameObject != null)
                        {
                            EditorUtility.SetDirty(targetComponent.gameObject);
                        }
#endif

                        Debug.Log(
                            $"[VisualMapper] Binding successful: {targetComponent.GetType().Name}.active to {sourceObservableName}.");
                        return result as IDisposable;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError(
                            $"[VisualMapper] ERROR: Failed to invoke BindActive method. Inner Exception: {ex.InnerException?.Message ?? ex.Message}");
                        return null;
                    }
                }
            }
            // --- END XỬ LÝ ĐẶC BIỆT CHO 'active' ---


            // --- BẮT ĐẦU CÁC CASE BINDING UI CHUNG (Chỉ hiển thị logic tìm kiếm) ---
            if (targetComponent is TMP_Text || targetComponent is Text)
            {
                if (prop == "text")
                {
                    bindMethod = uiBindingsType.GetMethods(BindingFlags.Static | BindingFlags.Public)
                        .FirstOrDefault(m => m.Name == "Bind" && m.IsGenericMethodDefinition &&
                                             m.GetParameters().Length == 3
                                             && (typeof(TMP_Text).IsAssignableFrom(m.GetParameters()[0]
                                                     .ParameterType) ||
                                                 typeof(Text).IsAssignableFrom(m.GetParameters()[0].ParameterType))
                                             && m.GetParameters()[2].ParameterType.IsGenericType &&
                                             m.GetParameters()[2].ParameterType.GetGenericTypeDefinition() ==
                                             typeof(Func<,>));
                }
            }
            else if ((targetComponent is Image || targetComponent is SpriteRenderer) && prop == "sprite" &&
                     observableTypeT == typeof(Sprite))
            {
                bindMethod = uiBindingsType.GetMethod("Bind",
                    new[] { targetComponent.GetType(), typeof(Observable<Sprite>) });
            }
            else if (targetComponent is Slider && prop == "value" && observableTypeT == typeof(float))
            {
                bindMethod = uiBindingsType.GetMethod("Bind",
                    new[] { typeof(Slider), typeof(Observable<float>), typeof(Observable<float>), typeof(bool) });
            }
            else if (targetComponent is Toggle && prop == "ison" && observableTypeT == typeof(bool))
            {
                bindMethod = uiBindingsType.GetMethod("Bind",
                    new[] { typeof(Toggle), typeof(Observable<bool>), typeof(bool) });
            }
            else if (targetComponent is TMP_InputField && prop == "text" && observableTypeT == typeof(string))
            {
                // Tìm hàm Bind(this TMP_InputField, Observable<string>, bool twoWay)
                bindMethod = uiBindingsType.GetMethod("Bind",
                    new[] { typeof(TMP_InputField), typeof(Observable<string>), typeof(bool) });
            }
            else if (targetComponent is InputField && prop == "text" && observableTypeT == typeof(string))
            {
                // Tìm hàm Bind(this InputField, Observable<string>, bool twoWay)
                bindMethod = uiBindingsType.GetMethod("Bind",
                    new[] { typeof(InputField), typeof(Observable<string>), typeof(bool) });
            }
            else if (prop == "value" && observableTypeT == typeof(int))
            {
                // Phân biệt TMP_Dropdown và Legacy Dropdown
                if (targetComponent is TMP_Dropdown)
                {
                    // Tìm hàm Bind(this TMP_Dropdown, Observable<int>, bool twoWay)
                    bindMethod = uiBindingsType.GetMethod("Bind",
                        new[] { typeof(TMP_Dropdown), typeof(Observable<int>), typeof(bool) });
                }
                else if (targetComponent is Dropdown)
                {
                    // Tìm hàm Bind(this Dropdown, Observable<int>, bool twoWay)
                    bindMethod = uiBindingsType.GetMethod("Bind",
                        new[] { typeof(Dropdown), typeof(Observable<int>), typeof(bool) });
                }
            }
            else if (targetComponent is Scrollbar && prop == "value" && observableTypeT == typeof(float))
            {
                // Tìm hàm Bind(this Scrollbar, Observable<float>, bool twoWay)
                bindMethod = uiBindingsType.GetMethod("Bind",
                    new[] { typeof(Scrollbar), typeof(Observable<float>), typeof(bool) });
            }
            else if (prop == "interactable" && observableTypeT == typeof(bool))
            {
                // Hàm BindInteractable được định nghĩa cho Selectable (lớp cha của Button)
                // Signature: BindInteractable(this Selectable selectable, Observable<bool> interactable)
                bindMethod = uiBindingsType.GetMethod("BindInteractable",
                    new[] { typeof(Selectable), typeof(Observable<bool>) });
            }
            // --- KẾT THÚC CÁC CASE BINDING UI CHUNG ---


            if (bindMethod == null)
            {
                Debug.LogError(
                    $"[VisualMapper] No matching Bind extension found for {targetComponent.GetType().Name}.{targetPropertyName} with source Observable<{observableTypeT.Name}>.");
                return null;
            }

            // 3. Chuẩn bị tham số và gọi phương thức (Cho các case còn lại)

            if (bindMethod.IsGenericMethodDefinition)
            {
                bindMethod = bindMethod.MakeGenericMethod(observableTypeT);
            }

            ParameterInfo[] parameters = bindMethod.GetParameters();
            object[] args = new object[parameters.Length];

            args[0] = targetComponent;
            args[1] = sourceInstance;

            if (parameters.Length > 2)
            {
                for (int i = 2; i < parameters.Length; i++)
                {
                    Type paramType = parameters[i].ParameterType;

                    if (paramType.IsGenericType && paramType.GetGenericTypeDefinition() == typeof(Func<,>))
                    {
                        args[i] = null;
                    }
                    else if (paramType == typeof(bool))
                    {
                        args[i] = isTwoWay;
                    }
                    else if (paramType == typeof(Observable<float>))
                    {
                        args[i] = null;
                    }
                    else
                    {
                        args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
                    }
                }
            }

            // 4. Gọi phương thức Bind()
            try
            {
                var result = bindMethod.Invoke(null, args);

#if UNITY_EDITOR
                if (!isPlaying && targetComponent != null)
                {
                    EditorUtility.SetDirty(targetComponent);
                }
#endif

                Debug.Log(
                    $"[VisualMapper] Binding successful: {targetComponent.GetType().Name}.{targetPropertyName} to {sourceObservableName}.");
                return result as IDisposable;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[VisualMapper] ERROR: Failed to invoke Bind method. Inner Exception: {ex.InnerException?.Message ?? ex.Message}");
                return null;
            }
        }
    }
}