using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace OSK.Bindings
{
    public static class UIBindingExtensions
    {
        private static void ForceEditorUpdate(Component c)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && c != null)
            {
                EditorUtility.SetDirty(c); 
                if (c.gameObject != null) EditorUtility.SetDirty(c.gameObject); 
                if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.Repaint();
            }
#endif
        }

        #region TMP/Text bindings

        // TMP_Text <- Observable<T> with optional converter
        public static IDisposable Bind<T>(this TMP_Text txt, Observable<T> src, Func<T, string> toString = null)
        {
            if (txt == null) return new Disposer(() => { });
            
            void Apply(T v)
            {
                try
                {
                    txt.text = toString != null ? toString(v) : (v != null ? v.ToString() : "");
                    ForceEditorUpdate(txt); // Cập nhật Editor
                }
                catch
                {
                    txt.text = string.Empty;
                }
            }
            
            if (src == null) { Apply(default); return new Disposer(() => { }); }

            src.OnChanged += Apply;
            Apply(src.Value);

            return new Disposer(() => src.OnChanged -= Apply);
        }

        // Legacy UI Text
        public static IDisposable Bind<T>(this Text uiText, Observable<T> src, Func<T, string> toString = null)
        {
            if (uiText == null) return new Disposer(() => { });
            
            void Apply(T v)
            {
                try
                {
                    uiText.text = toString != null ? toString(v) : (v != null ? v.ToString() : "");
                    ForceEditorUpdate(uiText); // Cập nhật Editor
                }
                catch
                {
                    uiText.text = string.Empty;
                }
            }
            
            if (src == null) { Apply(default); return new Disposer(() => { }); }

            src.OnChanged += Apply;
            Apply(src.Value);
            return new Disposer(() =>
            {
                src.OnChanged -= Apply;
            });
        }

        // TMP_Text <- two-source formatter (A,B) => string
        public static IDisposable Bind<A, B>(this TMP_Text txt, Observable<A> a, Observable<B> b,
            Func<A, B, string> formatter)
        {
            if (txt == null) return new Disposer(() => { });
            
            void UpdateText(A aa, B bb)
            {
                try
                {
                    txt.text = formatter(aa, bb);
                    ForceEditorUpdate(txt); // Cập nhật Editor
                }
                catch { txt.text = ""; }
            }
            
            if (a == null || b == null) { UpdateText(default, default); return new Disposer(() => { }); }

            void OnA(A aa) => UpdateText(aa, b.Value);
            void OnB(B bb) => UpdateText(a.Value, bb);

            a.OnChanged += OnA;
            b.OnChanged += OnB;
            UpdateText(a.Value, b.Value);

            return new Disposer(() =>
            {
                a.OnChanged -= OnA;
                b.OnChanged -= OnB;
            });
        }

        #endregion

        #region Image / SpriteRenderer / Graphic / Color bindings

        // Image.sprite <- Observable<Sprite>
        public static IDisposable Bind(this Image img, Observable<Sprite> src)
        {
            if (img == null) return new Disposer(() => { });

            void Apply(Sprite s)
            {
                if (img != null) img.sprite = s;
                ForceEditorUpdate(img);
            }

            if (src == null) { Apply(null); return new Disposer(() => { }); }

            src.OnChanged += Apply;
            Apply(src.Value);
            return new Disposer(() => src.OnChanged -= Apply);
        }

        // SpriteRenderer.sprite <- Observable<Sprite>
        public static IDisposable Bind(this SpriteRenderer sr, Observable<Sprite> src)
        {
            if (sr == null) return new Disposer(() => { });

            void Apply(Sprite s)
            {
                if (sr != null) sr.sprite = s;
                ForceEditorUpdate(sr);
            }
            
            if (src == null) { Apply(null); return new Disposer(() => { }); }

            src.OnChanged += Apply;
            Apply(src.Value);
            return new Disposer(() => src.OnChanged -= Apply);
        }

        // Graphic (Image, Text) color binding
        public static IDisposable Bind(this Graphic g, Observable<Color> src)
        {
            if (g == null) return new Disposer(() => { });

            void Apply(Color c)
            {
                if (g != null) g.color = c;
                ForceEditorUpdate(g);
            }
            
            if (src == null) { Apply(Color.white); return new Disposer(() => { }); }

            src.OnChanged += Apply;
            Apply(src.Value);
            return new Disposer(() => src.OnChanged -= Apply);
        }

        // TMP_Text color
        public static IDisposable BindColor(this TMP_Text txt, Observable<Color> src)
        {
            return txt == null ? new Disposer(() => { }) : Bind((Graphic)txt, src);
        }

        #endregion

        #region Slider bindings (two-way)

        /// <summary>
        /// Bind slider normalized to value/max (float). If twoWay true, UI -> model updates.
        /// </summary>
        public static IDisposable Bind(this Slider slider, Observable<float> value, Observable<float> max,
            bool twoWay = false)
        {
            if (slider == null) return new Disposer(() => { });
            if (value == null || max == null)
            {
                slider.SetValueWithoutNotify(0f);
                return new Disposer(() => { });
            }

            // Model -> View (One-way)
            void UpdateUI(float v)
            {
                try
                {
                    var denom = (max.Value <= 0f) ? 1e-6f : max.Value;
                    slider.SetValueWithoutNotify(Mathf.Clamp01(v / denom));
                    ForceEditorUpdate(slider); // Cập nhật Editor
                }
                catch { /* ignore */ }
            }

            // View -> Model (Two-way logic)
            UnityAction<float> onSliderChanged =
                normalized => value.SetValue(normalized * Mathf.Max(1e-6f, max.Value), true);
            
            Action<float> onValueChanged = _ => UpdateUI(value.Value);
            Action<float> onMaxChanged = _ => UpdateUI(value.Value);

            value.OnChanged += onValueChanged;
            max.OnChanged += onMaxChanged;
            UpdateUI(value.Value);

            IDisposable oneWayDisposer = new Disposer(() =>
            {
                value.OnChanged -= onValueChanged;
                max.OnChanged -= onMaxChanged;
            });
            
            if (twoWay)
            {
                slider.onValueChanged.AddListener(onSliderChanged);
                return new CombinedDisposer(
                    oneWayDisposer, 
                    new Disposer(() => slider.onValueChanged.RemoveListener(onSliderChanged))
                );
            }
            
            return oneWayDisposer;
        }

        #endregion

        #region Toggle / Button / Interactable

        // Toggle two-way with Observable<bool>
        public static IDisposable Bind(this Toggle toggle, Observable<bool> src, bool twoWay = true)
        {
            if (toggle == null) return new Disposer(() => { });

            // Model -> View (One-way)
            void UpdateUI(bool v)
            {
                try
                {
                    toggle.SetIsOnWithoutNotify(v);
                    ForceEditorUpdate(toggle); // Cập nhật Editor
                }
                catch { toggle.isOn = v; }
            }
            
            if (src == null) { UpdateUI(false); return new Disposer(() => { }); }

            UnityAction<bool> onToggleChanged = newVal => src.SetValue(newVal, true);

            src.OnChanged += UpdateUI;
            UpdateUI(src.Value);

            IDisposable oneWayDisposer = new Disposer(() => src.OnChanged -= UpdateUI);

            if (twoWay)
            {
                toggle.onValueChanged.AddListener(onToggleChanged);
                return new CombinedDisposer(
                    oneWayDisposer,
                    new Disposer(() => toggle.onValueChanged.RemoveListener(onToggleChanged))
                );
            }
            return oneWayDisposer;
        }

        // Button click -> Action (returns IDisposable to remove listener)
        public static IDisposable BindOnClick(this Button btn, Action onClick)
        {
            if (btn == null || onClick == null) return new Disposer(() => { });

            UnityAction wrapper = () => onClick();
            btn.onClick.AddListener(wrapper);
            return new Disposer(() =>
            {
                if (btn != null) btn.onClick.RemoveListener(wrapper);
            });
        }

        // Bind Interactable property to Observable<bool>
        public static IDisposable BindInteractable(this Selectable selectable, Observable<bool> interactable)
        {
            if (selectable == null) return new Disposer(() => { });
            
            void Apply(bool v)
            {
                if (selectable != null) selectable.interactable = v;
                ForceEditorUpdate(selectable);
            }
            
            if (interactable == null) { Apply(true); return new Disposer(() => { }); }

            interactable.OnChanged += Apply;
            Apply(interactable.Value);
            return new Disposer(() => interactable.OnChanged -= Apply);
        }

        #endregion

        #region InputField / TMP_InputField (two-way)

        // TMP_InputField two-way
        public static IDisposable Bind(this TMP_InputField input, Observable<string> src, bool twoWay)
        {
            if (input == null) return new Disposer(() => { });

            // 1. Model -> View (One-way)
            void Apply(string v)
            {
                if (input != null && input.text != v)
                {
                    input.SetTextWithoutNotify(v ?? "");
                    ForceEditorUpdate(input); 
                }
            }
    
            if (src == null) { Apply(string.Empty); return new Disposer(() => { }); }

            src.OnChanged += Apply;
            Apply(src.Value); 

            IDisposable oneWayDisposer = new Disposer(() => src.OnChanged -= Apply);
            
            if (twoWay)
            {
                // 2. View -> Model (Two-way)
                UnityAction<string> onInputChanged = (v) =>
                {
                    if (src.Value != v)
                    {
                        // Cập nhật Observable
                        src.Value = v;
                    }
                };

                input.onValueChanged.AddListener(onInputChanged);

                return new CombinedDisposer(
                    oneWayDisposer, 
                    new Disposer(() => input.onValueChanged.RemoveListener(onInputChanged))
                );
            }
            
            return oneWayDisposer;
        }

        // Legacy InputField two-way
        public static IDisposable Bind(this InputField input, Observable<string> src, bool twoWay)
        {
            if (input == null) return new Disposer(() => { });

            // 1. Model -> View (One-way)
            void Apply(string v)
            {
                if (input != null && input.text != v)
                {
                    input.SetTextWithoutNotify(v ?? "");
                    ForceEditorUpdate(input); 
                }
            }
            
            if (src == null) { Apply(string.Empty); return new Disposer(() => { }); }

            src.OnChanged += Apply;
            Apply(src.Value);

            IDisposable oneWayDisposer = new Disposer(() => src.OnChanged -= Apply);

            if (twoWay)
            {
                // 2. View -> Model (Two-way)
                UnityAction<string> onInputChanged = (v) =>
                {
                    if (src.Value != v)
                    {
                        // Cập nhật Observable
                        src.Value = v;
                    }
                };

                input.onValueChanged.AddListener(onInputChanged);

                return new CombinedDisposer(
                    oneWayDisposer, 
                    new Disposer(() => input.onValueChanged.RemoveListener(onInputChanged))
                );
            }
            
            return oneWayDisposer;
        }

        #endregion

        #region Dropdown (TMP + legacy)

        // Bind selected index (two-way)
        public static IDisposable Bind(this TMP_Dropdown dp, Observable<int> selectedIndex, bool twoWay = true)
        {
            if (dp == null) return new Disposer(() => { });

            // Model -> View (One-way)
            void UpdateUI(int v)
            {
                try
                {
                    dp.SetValueWithoutNotify(Mathf.Clamp(v, 0, dp.options.Count - 1));
                    ForceEditorUpdate(dp); 
                }
                catch { dp.SetValueWithoutNotify(0); }
            }

            if (selectedIndex == null) { UpdateUI(0); return new Disposer(() => { }); }

            UnityAction<int> onChanged = idx => selectedIndex.SetValue(idx, true);

            selectedIndex.OnChanged += UpdateUI;
            UpdateUI(selectedIndex.Value);
            
            IDisposable oneWayDisposer = new Disposer(() => selectedIndex.OnChanged -= UpdateUI);

            if (twoWay)
            {
                dp.onValueChanged.AddListener(onChanged);
                return new CombinedDisposer(
                    oneWayDisposer,
                    new Disposer(() => dp.onValueChanged.RemoveListener(onChanged))
                );
            }
            return oneWayDisposer;
        }

        public static IDisposable Bind(this Dropdown dp, Observable<int> selectedIndex, bool twoWay = true)
        {
            if (dp == null) return new Disposer(() => { });

            // Model -> View (One-way)
            void UpdateUI(int v)
            {
                try
                {
                    dp.SetValueWithoutNotify(Mathf.Clamp(v, 0, dp.options.Count - 1));
                    ForceEditorUpdate(dp); 
                }
                catch { dp.SetValueWithoutNotify(0); }
            }

            if (selectedIndex == null) { UpdateUI(0); return new Disposer(() => { }); }

            UnityAction<int> onChanged = idx => selectedIndex.SetValue(idx, true);

            selectedIndex.OnChanged += UpdateUI;
            UpdateUI(selectedIndex.Value);
            
            IDisposable oneWayDisposer = new Disposer(() => selectedIndex.OnChanged -= UpdateUI);

            if (twoWay)
            {
                dp.onValueChanged.AddListener(onChanged);
                return new CombinedDisposer(
                    oneWayDisposer,
                    new Disposer(() => dp.onValueChanged.RemoveListener(onChanged))
                );
            }
            return oneWayDisposer;
        }

        #endregion

        #region GameObject / CanvasGroup bindings

        // GameObject active state binding
        public static IDisposable BindActive(this GameObject go, Observable<bool> src)
        {
            if (go == null) return new Disposer(() => { });
            
            void Apply(bool v)
            {
                if (go != null) go.SetActive(v);
                ForceEditorUpdate(go.transform); // SetDirty trên Transform
            }
            
            if (src == null) { Apply(true); return new Disposer(() => { }); }

            src.OnChanged += Apply;
            Apply(src.Value);
            return new Disposer(() => src.OnChanged -= Apply);
        }

        // CanvasGroup alpha
        public static IDisposable BindAlpha(this CanvasGroup cg, Observable<float> a)
        {
            if (cg == null) return new Disposer(() => { });
            
            void Apply(float v)
            {
                if (cg != null) cg.alpha = Mathf.Clamp01(v);
                ForceEditorUpdate(cg);
            }
            
            if (a == null) { Apply(1f); return new Disposer(() => { }); }

            a.OnChanged += Apply;
            Apply(a.Value);
            return new Disposer(() => a.OnChanged -= Apply);
        }

        // CanvasGroup interactable/blocksRaycasts from Observable<bool>
        public static IDisposable BindInteractable(this CanvasGroup cg, Observable<bool> o)
        {
            if (cg == null) return new Disposer(() => { });

            void Apply(bool v)
            {
                if (cg == null) return;
                cg.interactable = v;
                cg.blocksRaycasts = v;
                ForceEditorUpdate(cg);
            }
            
            if (o == null) { Apply(true); return new Disposer(() => { }); }

            o.OnChanged += Apply;
            Apply(o.Value);
            return new Disposer(() => o.OnChanged -= Apply);
        }

        #endregion

        #region Animator bindings (model -> animator)

        // Set animator bool param from Observable<bool>
        public static IDisposable BindBool(this Animator animator, string paramName, Observable<bool> src)
        {
            if (animator == null || string.IsNullOrEmpty(paramName)) return new Disposer(() => { });
            
            void Apply(bool v)
            {
                if (animator != null) animator.SetBool(paramName, v);
                ForceEditorUpdate(animator);
            }
            
            if (src == null) { Apply(false); return new Disposer(() => { }); }

            src.OnChanged += Apply;
            Apply(src.Value);
            return new Disposer(() => src.OnChanged -= Apply);
        }

        // Trigger animator when Observable<int> changes (optional mapping)
        public static IDisposable BindTriggerOnChange(this Animator animator, string triggerName, Observable<int> src)
        {
            if (animator == null || string.IsNullOrEmpty(triggerName) || src == null) return new Disposer(() => { });

            int last = src.Value;

            void OnChanged(int v)
            {
                if (v != last)
                {
                    last = v;
                    if (animator != null) animator.SetTrigger(triggerName);
                    ForceEditorUpdate(animator);
                }
            }

            src.OnChanged += OnChanged;
            return new Disposer(() => src.OnChanged -= OnChanged);
        }

        #endregion

        
        #region BindOnce Bindings (Snapshot)
        
        // TMP_Text BindOnce
        public static void BindOnce<T>(this TMP_Text txt, Observable<T> src, Func<T, string> toString = null)
        {
            if (txt == null || src == null) return;
            
            string textValue = (toString != null) ? toString(src.Value) : (src.Value != null ? src.Value.ToString() : string.Empty);
            
            txt.text = textValue;
            
            ForceEditorUpdate(txt); 
        }

        // Legacy Text BindOnce
        public static void BindOnce<T>(this Text uiText, Observable<T> src, Func<T, string> toString = null)
        {
            if (uiText == null || src == null) return;
            
            string textValue = (toString != null) ? toString(src.Value) : (src.Value != null ? src.Value.ToString() : string.Empty);
            
            uiText.text = textValue;
            
            ForceEditorUpdate(uiText); 
        }

        // Image BindOnce (Sprite)
        public static void BindOnce(this Image img, Observable<Sprite> src)
        {
            if (img == null || src == null) return;
            
            img.sprite = src.Value;
            
            ForceEditorUpdate(img); 
        }

        // Graphic BindOnce (Color)
        public static void BindOnce(this Graphic g, Observable<Color> src)
        {
            if (g == null || src == null) return;

            g.color = src.Value;
            ForceEditorUpdate(g); 
        }

        // GameObject.SetActive BindOnce
        public static void BindOnceActive(this GameObject go, Observable<bool> src)
        {
            if (go == null || src == null) return;

            go.SetActive(src.Value);
            ForceEditorUpdate(go.transform); 
        }
        
        #endregion 
        
        
        #region Utility helpers

        // Convenience: bind any Action<T> to Observable<T>
        public static IDisposable BindTo<T>(this Observable<T> src, Action<T> apply)
        {
            if (src == null) return new Disposer(() => { });
            apply?.Invoke(src.Value);
            src.OnChanged += apply;
            return new Disposer(() => src.OnChanged -= apply);
        }

        #endregion 
    }
}