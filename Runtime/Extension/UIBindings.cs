using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

namespace OSK
{
    /// <summary>
    /// A collection of UI binding extension helpers for common Unity UI components and TextMeshPro.
    /// Use with your Observable<T> class. Each method returns IDisposable so caller can dispose/unbind.
    /// </summary>
    public static class UIBindings
    {
        #region Disposer

        // safe, idempotent disposer
        public sealed class Disposer : IDisposable
        {
            private Action _onDispose;
            private bool _disposed;

            public Disposer(Action onDispose) => _onDispose = onDispose;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                try
                {
                    _onDispose?.Invoke();
                }
                finally
                {
                    _onDispose = null;
                }
            }
        }

        #endregion

        #region TMP/Text bindings

        // TMP_Text <- Observable<T> with optional converter
        public static IDisposable Bind<T>(this TMP_Text txt, Observable<T> src, Func<T, string> toString = null)
        {
            if (txt == null) return new Disposer(() => { });
            if (src == null)
            {
                try
                {
                    txt.text = toString != null ? toString(default) : string.Empty;
                }
                catch
                {
                    txt.text = string.Empty;
                }

                return new Disposer(() => { });
            }

            void Apply(T v)
            {
                try
                {
                    txt.text = toString != null ? toString(v) : (v != null ? v.ToString() : "");
                }
                catch
                {
                    /* swallow to avoid breaking UI */
                }
            }

            src.OnChanged += Apply;
            Apply(src.Value);

            return new Disposer(() => src.OnChanged -= Apply);
        }

        // Legacy UI Text
        public static IDisposable Bind<T>(this Text uiText, Observable<T> src, Func<T, string> toString = null)
        {
            if (uiText == null) return new Disposer(() => { });
            if (src == null)
            {
                try
                {
                    uiText.text = toString != null ? toString(default) : string.Empty;
                }
                catch
                {
                    uiText.text = string.Empty;
                }

                return new Disposer(() => { });
            }

            void Apply(T v)
            {
                try
                {
                    uiText.text = toString != null ? toString(v) : (v != null ? v.ToString() : "");
                }
                catch
                {
                    /* ignore */
                }
            }

            src.OnChanged += Apply;
            Apply(src.Value);
            return new Disposer(() => src.OnChanged -= Apply);
        }

        // TMP_Text <- two-source formatter (A,B) => string
        public static IDisposable Bind<A, B>(this TMP_Text txt, Observable<A> a, Observable<B> b,
            Func<A, B, string> formatter)
        {
            if (txt == null) return new Disposer(() => { });
            if (a == null || b == null)
            {
                try
                {
                    txt.text = formatter != null ? formatter(default, default) : "";
                }
                catch
                {
                    txt.text = "";
                }

                return new Disposer(() => { });
            }

            void UpdateText(A aa, B bb)
            {
                try
                {
                    txt.text = formatter(aa, bb);
                }
                catch
                {
                    /* ignore */
                }
            }

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
            if (src == null)
            {
                img.sprite = null;
                return new Disposer(() => { });
            }

            void Apply(Sprite s)
            {
                if (img != null) img.sprite = s;
            }

            src.OnChanged += Apply;
            Apply(src.Value);
            return new Disposer(() => src.OnChanged -= Apply);
        }

        // SpriteRenderer.sprite <- Observable<Sprite>
        public static IDisposable Bind(this SpriteRenderer sr, Observable<Sprite> src)
        {
            if (sr == null) return new Disposer(() => { });
            if (src == null)
            {
                sr.sprite = null;
                return new Disposer(() => { });
            }

            void Apply(Sprite s)
            {
                if (sr != null) sr.sprite = s;
            }

            src.OnChanged += Apply;
            Apply(src.Value);
            return new Disposer(() => src.OnChanged -= Apply);
        }

        // Graphic (Image, Text) color binding
        public static IDisposable Bind(this Graphic g, Observable<Color> src)
        {
            if (g == null) return new Disposer(() => { });
            if (src == null)
            {
                return new Disposer(() => { });
            }

            void Apply(Color c)
            {
                if (g != null) g.color = c;
            }

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

            // Model -> View
            void UpdateUI(float v)
            {
                try
                {
                    var denom = (max.Value <= 0f) ? 1e-6f : max.Value;
                    slider.SetValueWithoutNotify(Mathf.Clamp01(v / denom));
                }
                catch
                {
                    /* ignore */
                }
            }

            // View -> Model
            UnityAction<float> onSliderChanged =
                normalized => value.SetValue(normalized * Mathf.Max(1e-6f, max.Value), true);
            Action<float> onValueChanged = _ => UpdateUI(value.Value);
            Action<float> onMaxChanged = _ => UpdateUI(value.Value);

            value.OnChanged += onValueChanged;
            max.OnChanged += onMaxChanged;
            UpdateUI(value.Value);

            if (twoWay)
                slider.onValueChanged.AddListener(onSliderChanged);

            return new Disposer(() =>
            {
                value.OnChanged -= onValueChanged;
                max.OnChanged -= onMaxChanged;
                if (twoWay)
                    slider.onValueChanged.RemoveListener(onSliderChanged);
            });
        }

        // Safer version that returns explicit dispose Action if caller wants it
        public static IDisposable BindNormalized(
            this Slider slider,
            Observable<float> value,
            Observable<float> max,
            bool twoWay,
            out Action disposeAction)
        {
            disposeAction = null;
            if (slider == null) return new Disposer(() => { });

            if (value == null || max == null)
            {
                slider.SetValueWithoutNotify(0f);
                return new Disposer(() => { });
            }

            void UpdateUI()
            {
                var denom = (max.Value <= 0f) ? 1e-6f : max.Value;
                slider.SetValueWithoutNotify(Mathf.Clamp01(value.Value / denom));
            }

            UnityAction<float> onSliderChanged =
                normalized => value.SetValue(normalized * Mathf.Max(1e-6f, max.Value), true);

            Action<float> onValueChanged = _ => UpdateUI();
            Action<float> onMaxChanged = _ => UpdateUI();

            value.OnChanged += onValueChanged;
            max.OnChanged += onMaxChanged;
            UpdateUI();

            if (twoWay)
                slider.onValueChanged.AddListener(onSliderChanged);

            // 🔥 FIX: capture action in a local variable
            Action localDispose = () =>
            {
                value.OnChanged -= onValueChanged;
                max.OnChanged -= onMaxChanged;
                if (twoWay)
                    slider.onValueChanged.RemoveListener(onSliderChanged);
            };

            disposeAction = localDispose;
            return new Disposer(() => localDispose());
        }

        #endregion

        #region Toggle / Button / Interactable

        // Toggle two-way with Observable<bool>
        public static IDisposable Bind(this Toggle toggle, Observable<bool> src, bool twoWay = true)
        {
            if (toggle == null) return new Disposer(() => { });
            if (src == null)
            {
                toggle.isOn = false;
                return new Disposer(() => { });
            }

            void UpdateUI(bool v)
            {
                try
                {
                    toggle.SetIsOnWithoutNotify(v);
                }
                catch
                {
                    toggle.isOn = v;
                }
            }

            UnityAction<bool> onToggleChanged = newVal => src.SetValue(newVal, true);

            src.OnChanged += UpdateUI;
            UpdateUI(src.Value);

            if (twoWay)
                toggle.onValueChanged.AddListener(onToggleChanged);

            return new Disposer(() =>
            {
                src.OnChanged -= UpdateUI;
                if (twoWay)
                    toggle.onValueChanged.RemoveListener(onToggleChanged);
            });
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
            if (interactable == null)
            {
                selectable.interactable = true;
                return new Disposer(() => { });
            }

            void Apply(bool v)
            {
                if (selectable != null) selectable.interactable = v;
            }

            interactable.OnChanged += Apply;
            Apply(interactable.Value);
            return new Disposer(() => interactable.OnChanged -= Apply);
        }

        #endregion

        #region InputField / TMP_InputField (two-way)

        // TMP_InputField two-way
        public static IDisposable Bind(this TMP_InputField input, Observable<string> src, bool twoWay = true)
        {
            if (input == null) return new Disposer(() => { });
            if (src == null)
            {
                input.text = "";
                return new Disposer(() => { });
            }

            void UpdateUI(string v)
            {
                try
                {
#if UNITY_2019_1_OR_NEWER
                    input.SetTextWithoutNotify(v ?? "");
#else
                    input.text = v ?? "";
#endif
                }
                catch
                {
                    /* ignore */
                }
            }

            UnityAction<string> onInputChanged = newVal => src.SetValue(newVal, true);

            src.OnChanged += UpdateUI;
            UpdateUI(src.Value);

            if (twoWay)
                input.onValueChanged.AddListener(onInputChanged);

            return new Disposer(() =>
            {
                src.OnChanged -= UpdateUI;
                if (twoWay) input.onValueChanged.RemoveListener(onInputChanged);
            });
        }

        // Legacy InputField two-way
        public static IDisposable Bind(this InputField input, Observable<string> src, bool twoWay = true)
        {
            if (input == null) return new Disposer(() => { });
            if (src == null)
            {
                input.text = "";
                return new Disposer(() => { });
            }

            void UpdateUI(string v)
            {
#if UNITY_2019_1_OR_NEWER
                try
                {
                    input.SetTextWithoutNotify(v ?? "");
                }
                catch
                {
                    input.text = v ?? "";
                }
#else
                input.text = v ?? "";
#endif
            }

            UnityAction<string> onInputChanged = newVal => src.SetValue(newVal, true);

            src.OnChanged += UpdateUI;
            UpdateUI(src.Value);

            if (twoWay)
                input.onValueChanged.AddListener(onInputChanged);

            return new Disposer(() =>
            {
                src.OnChanged -= UpdateUI;
                if (twoWay) input.onValueChanged.RemoveListener(onInputChanged);
            });
        }

        #endregion

        #region Dropdown (TMP + legacy)

        // Bind selected index (two-way)
        public static IDisposable Bind(this TMP_Dropdown dp, Observable<int> selectedIndex, bool twoWay = true)
        {
            if (dp == null) return new Disposer(() => { });
            if (selectedIndex == null)
            {
                dp.SetValueWithoutNotify(0);
                return new Disposer(() => { });
            }

            void UpdateUI(int v)
            {
                try
                {
                    dp.SetValueWithoutNotify(Mathf.Clamp(v, 0, dp.options.Count - 1));
                }
                catch
                {
                    dp.SetValueWithoutNotify(0);
                }
            }

            UnityAction<int> onChanged = idx => selectedIndex.SetValue(idx, true);

            selectedIndex.OnChanged += UpdateUI;
            UpdateUI(selectedIndex.Value);

            if (twoWay)
                dp.onValueChanged.AddListener(onChanged);

            return new Disposer(() =>
            {
                selectedIndex.OnChanged -= UpdateUI;
                if (twoWay) dp.onValueChanged.RemoveListener(onChanged);
            });
        }

        public static IDisposable Bind(this Dropdown dp, Observable<int> selectedIndex, bool twoWay = true)
        {
            if (dp == null) return new Disposer(() => { });
            if (selectedIndex == null)
            {
                dp.SetValueWithoutNotify(0);
                return new Disposer(() => { });
            }

            void UpdateUI(int v)
            {
                try
                {
                    dp.SetValueWithoutNotify(Mathf.Clamp(v, 0, dp.options.Count - 1));
                }
                catch
                {
                    dp.SetValueWithoutNotify(0);
                }
            }

            UnityAction<int> onChanged = idx => selectedIndex.SetValue(idx, true);

            selectedIndex.OnChanged += UpdateUI;
            UpdateUI(selectedIndex.Value);

            if (twoWay)
                dp.onValueChanged.AddListener(onChanged);

            return new Disposer(() =>
            {
                selectedIndex.OnChanged -= UpdateUI;
                if (twoWay) dp.onValueChanged.RemoveListener(onChanged);
            });
        }

        #endregion

        #region GameObject / CanvasGroup bindings

        // GameObject active state binding
        public static IDisposable BindActive(this GameObject go, Observable<bool> src)
        {
            if (go == null) return new Disposer(() => { });
            if (src == null)
            {
                go.SetActive(true);
                return new Disposer(() => { });
            }

            void Apply(bool v)
            {
                if (go != null) go.SetActive(v);
            }

            src.OnChanged += Apply;
            Apply(src.Value);
            return new Disposer(() => src.OnChanged -= Apply);
        }

        // CanvasGroup alpha
        public static IDisposable BindAlpha(this CanvasGroup cg, Observable<float> a)
        {
            if (cg == null) return new Disposer(() => { });
            if (a == null)
            {
                cg.alpha = 1f;
                return new Disposer(() => { });
            }

            void Apply(float v)
            {
                if (cg != null) cg.alpha = Mathf.Clamp01(v);
            }

            a.OnChanged += Apply;
            Apply(a.Value);
            return new Disposer(() => a.OnChanged -= Apply);
        }

        // CanvasGroup interactable/blocksRaycasts from Observable<bool>
        public static IDisposable BindInteractable(this CanvasGroup cg, Observable<bool> o)
        {
            if (cg == null) return new Disposer(() => { });
            if (o == null)
            {
                cg.interactable = true;
                cg.blocksRaycasts = true;
                return new Disposer(() => { });
            }

            void Apply(bool v)
            {
                if (cg == null) return;
                cg.interactable = v;
                cg.blocksRaycasts = v;
            }

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
            if (src == null)
            {
                animator.SetBool(paramName, false);
                return new Disposer(() => { });
            }

            void Apply(bool v)
            {
                if (animator != null) animator.SetBool(paramName, v);
            }

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
                }
            }

            src.OnChanged += OnChanged;
            return new Disposer(() => src.OnChanged -= OnChanged);
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