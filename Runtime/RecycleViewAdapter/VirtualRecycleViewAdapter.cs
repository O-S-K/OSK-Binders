using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace OSK
{
    public enum JumpPosition
    {
        Top = 0,
        Center = 1,
        Bottom = 2
    }

    public enum ScrollDirection
    {
        Horizontal,
        Vertical
    }

    [RequireComponent(typeof(ScrollRect))]
    [AddComponentMenu("OSK-AI/Recycle View/Virtual Recycle View Adapter")]
    public class VirtualRecycleViewAdapter<TModel, TView> : MonoBehaviour where TView : Component, IRecyclerItem<TModel>
    {
        [Title(" ------ Virtual Recycle View Adapter Settings ------ ", HorizontalLine = true)]
        [ReadOnly, InfoBox("Not use Content Size Fitter and Layout Groups on Content RectTransform. " +
                           "Item size and positions are managed by the adapter.", InfoMessageType.Warning), Space(10)]
        [LabelText("Item Prefab")]
        [HorizontalGroup("Left", Width = 300)]
        [PreviewField(50, ObjectFieldAlignment.Left), HideLabel]
        public TView ItemPrefab;

        [VerticalGroup("Right")]
        [LabelWidth(90)]
        [HorizontalGroup("Right/Row1", MarginLeft = 4)]
        [LabelText("Item Height"), MinValue(1)]
        public float ItemHeight = 100f;

        [HorizontalGroup("Right/Row1")]
        [LabelText("Item Width"), MinValue(1)]
        [ShowIf("@Direction == ScrollDirection.Horizontal")]
        public float ItemWidth = 200f;


        // ---------- Buffer / Prewarm on one line ----------
        [HorizontalGroup("RowA", MarginLeft = 6, PaddingRight = 8)] [LabelWidth(90)] [LabelText("Buffer"), MinValue(0)]
        public int Buffer = 2;

        [HorizontalGroup("RowA")] [LabelText("Prewarm"), MinValue(0)]
        public int Prewarm = 5;


        // ---------- Direction (single compact row) ----------
        [HorizontalGroup("RowB", MarginLeft = 6)] [LabelWidth(90)] [LabelText("Scroll Direction")]
        public ScrollDirection Direction = ScrollDirection.Vertical;


        // ---------- Spacing X / Y on one line ----------
        [HorizontalGroup("RowC", MarginLeft = 6)] [LabelWidth(90)] [LabelText("Spacing X")]
        public float SpacingX = 0f;

        [HorizontalGroup("RowC")] [LabelText("Spacing Y")]
        public float SpacingY = 0f;


        // ---------- Jump settings compact ----------
        [HorizontalGroup("RowD", MarginLeft = 6)] [LabelWidth(90)] [LabelText("Disable Input")]
        public bool DisableInputDuringJump = true;

        [HorizontalGroup("RowD")] [LabelText("Use Unscaled")]
        public bool JumpUseUnscaledTime = true;

        // data sources (mutually exclusive modes)
        private ObservableCollection<TModel> _obsSource = null;
        private IList<TModel> _listSource = null; // for List<TModel> or TModel[]
        private IList<TModel> _dictValues = null; // for IDictionary mode (values in chosen order)
        private IDictionary _rawDict = null; // original dictionary (non-generic reference)
        private IList _dictKeyOrder = null; // ordered list of keys for dictionary mode (optional)

        // internals
        private SimplePool<TView> _pool;
        private readonly Dictionary<int, TView> _active = new Dictionary<int, TView>();
        private int _totalCount = 0;

        private Tweener _scrollTweener;
        private ScrollRect _scrollRect;
        private ScrollRect scrollRect
        {
            get
            {
                if (_scrollRect == null) _scrollRect = GetComponent<ScrollRect>();
                return _scrollRect;
            }
        }
        
        private RectTransform viewport => scrollRect.viewport;
        private RectTransform content => scrollRect.content;
        
        [Button("Add ScrollRect Component"), GUIColor(0.4f, 0.8f, 0.4f)]
        [ShowIf( "MissingScrollRectComponent")]
      
        private void AddScrollRectComponent()
        {
            if (GetComponent<ScrollRect>() == null)
            {
                _scrollRect = gameObject.AddComponent<ScrollRect>();
                Debug.Log("ScrollRect component added.");
            }
            else
            {
                Debug.LogWarning("ScrollRect component already exists.");
            }
        }
        private bool MissingScrollRectComponent()
        {
            return GetComponent<ScrollRect>() == null;
        }
        protected virtual void Awake()
        { 
            if (ItemPrefab == null) Debug.LogError("ItemPrefab not assigned.");
            if (ItemPrefab != null) _pool = new SimplePool<TView>(ItemPrefab, content);
            enabled = true;
        }

        protected virtual void Start()
        {
            PrewarmPool();
            scrollRect.onValueChanged.AddListener(OnScroll);
            UpdateContentSize();
            Refresh();
        }

        protected virtual void LateUpdate()
        { 
            ClampContentPosition();
        }

        // ---------- Public: support ObservableCollection (existing) ----------
        public void SetSource(ObservableCollection<TModel> source)
        {
            // clear other modes
            ClearListMode();
            ClearDictMode();

            if (_obsSource != null) UnbindSource();
            _obsSource = source;
            if (_obsSource != null) BindSource();
            _totalCount = GetCount();
            UpdateContentSize();
            Refresh();
        }

        private void BindSource()
        {
            if (_obsSource == null) return;
            _obsSource.OnAdd += OnAdd;
            _obsSource.OnRemove += OnRemove;
            _obsSource.OnReplace += OnReplace;
            _obsSource.OnReset += OnReset;
        }

        private void UnbindSource()
        {
            if (_obsSource == null) return;
            _obsSource.OnAdd -= OnAdd;
            _obsSource.OnRemove -= OnRemove;
            _obsSource.OnReplace -= OnReplace;
            _obsSource.OnReset -= OnReset;
            _obsSource = null;
        }

        // ---------- New: support IList<TModel> / arrays ----------
        /// <summary>Use a plain IList or array as data source (non-observable). Adapter will not auto-listen to changes.</summary>
        public void SetData(IList<TModel> list)
        {
            // clear observable/dict modes
            if (_obsSource != null)
            {
                UnbindSource();
                _obsSource = null;
            }

            ClearDictMode();

            _listSource = list;
            _totalCount = GetCount();
            UpdateContentSize();
            Refresh();
        }

        public void ClearListMode()
        {
            _listSource = null;
        }

        // ---------- New: support IDictionary<TKey,TModel> ----------
        /// <summary>
        /// Use a dictionary as a data source. If keyOrder==null, the dictionary's Keys enumeration order will be used (not guaranteed).
        /// You can provide keyOrder to guarantee ordering.
        /// </summary>
        public void SetData<TKey>(IDictionary<TKey, TModel> dict, IList<TKey> keyOrder = null)
        {
            if (dict == null)
            {
                ClearDictMode();
                _totalCount = GetCount();
                UpdateContentSize();
                Refresh();
                return;
            }

            // clear observable/list modes
            if (_obsSource != null)
            {
                UnbindSource();
                _obsSource = null;
            }

            ClearListMode();

            // store original dictionary in non-generic reference for internal use
            _rawDict = dict as IDictionary;

            // build ordered values list
            if (keyOrder != null)
            {
                // use provided order
                var vals = new List<TModel>(keyOrder.Count);
                foreach (var k in keyOrder)
                {
                    if (dict.TryGetValue(k, out var v)) vals.Add(v);
                    else vals.Add(default);
                }

                _dictValues = vals;
                // store keys as non-generic IList for lookup
                _dictKeyOrder = new List<TKey>(keyOrder) as IList;
            }
            else
            {
                // fallback: use dict.Keys order
                _dictKeyOrder = new List<TKey>(dict.Keys) as IList;
                var vals = new List<TModel>(_dictKeyOrder.Count);
                foreach (TKey k in _dictKeyOrder)
                {
                    vals.Add(dict[k]);
                }

                _dictValues = vals;
            }

            _totalCount = GetCount();
            UpdateContentSize();
            Refresh();
        }

        private void ClearDictMode()
        {
            _rawDict = null;
            _dictValues = null;
            _dictKeyOrder = null;
        }

        // Optional helper: Jump to item by dictionary key (generic)
        public void JumpToKey<TKey>(TKey key, float duration = 0.3f, JumpPosition pos = JumpPosition.Top,
            Ease ease = Ease.OutSine)
        {
            if (_dictKeyOrder == null) return;
            int idx = -1;
            for (int i = 0; i < _dictKeyOrder.Count; i++)
            {
                if (Equals(_dictKeyOrder[i], key))
                {
                    idx = i;
                    break;
                }
            }

            if (idx >= 0) JumpTo(idx, pos, duration, ease);
        }

        public bool IsNearEnd(float thresholdPercent = 0.8f)
        {
            if (content == null || viewport == null) return false;
            if (GetCount() == 0) return false;

            float viewportSize = Direction == ScrollDirection.Vertical ? viewport.rect.height : viewport.rect.width;
            float scrollPos = Direction == ScrollDirection.Vertical
                ? content.anchoredPosition.y
                : -content.anchoredPosition.x;

            float contentLen = Direction == ScrollDirection.Vertical ? content.rect.height : content.rect.width;
            scrollPos = Mathf.Clamp(scrollPos, 0f, Mathf.Max(0f, contentLen - viewportSize));

            float itemFull = ItemFullSize();
            int firstVisible = Mathf.FloorToInt(scrollPos / itemFull);
            int visibleItemCount = Mathf.CeilToInt(viewportSize / itemFull);

            int lastVisible = firstVisible + visibleItemCount - 1;
            int thresholdIndex = Mathf.FloorToInt(thresholdPercent * (GetCount() - 1));

            return lastVisible >= thresholdIndex;
        }

        // ---------- Notify APIs for non-observable modes ----------
        public void NotifyItemInserted(int index)
        {
            _totalCount = GetCount();
            UpdateContentSize();
            // shift active indices greater/equal index
            var keys = new List<int>(_active.Keys);
            keys.Sort((a, b) => b - a);
            foreach (var k in keys)
            {
                if (k >= index)
                {
                    var v = _active[k];
                    _active.Remove(k);
                    _active[k + 1] = v;
                    SetViewPosition(v, k + 1);
                }
            }

            Refresh();
        }

        public void NotifyItemRemoved(int index)
        {
            _totalCount = GetCount();
            if (_active.TryGetValue(index, out var v))
            {
                Recycle(index);
            }

            var keys = new List<int>(_active.Keys);
            keys.Sort();
            foreach (var k in keys)
            {
                if (k > index)
                {
                    var view = _active[k];
                    _active.Remove(k);
                    _active[k - 1] = view;
                    SetViewPosition(view, k - 1);
                    var item = GetItem(k - 1);
                    (view as IRecyclerItem<TModel>)?.SetData(item, k - 1);
                }
            }

            UpdateContentSize();
            Refresh();
        }

        public void NotifyItemChanged(int index)
        {
            if (_active.TryGetValue(index, out var v))
            {
                v?.SetData(GetItem(index), index);
            }
        }

        public void NotifyDataSetChanged()
        {
            ClearAll();
            _totalCount = GetCount();
            UpdateContentSize();
            Refresh();
        }

        // ---------- ObservableCollection callbacks (existing) ----------
        public void OnAdd(int index, TModel item) => NotifyItemInserted(index);
        public void OnRemove(int index, TModel item) => NotifyItemRemoved(index);
        public void OnReplace(int index, TModel oldItem, TModel newItem) => NotifyItemChanged(index);
        public void OnReset() => NotifyDataSetChanged();

        // ---------- Internal helpers to unify data access ----------
        public int GetCount()
        {
            if (_obsSource != null) return _obsSource.Count;
            if (_listSource != null) return _listSource.Count;
            if (_dictValues != null) return _dictValues.Count;
            return 0;
        }

        private TModel GetItem(int index)
        {
            if (_obsSource != null) return _obsSource[index];
            if (_listSource != null) return _listSource[index];
            if (_dictValues != null) return _dictValues[index];
            throw new IndexOutOfRangeException("No data source set or index out of range.");
        }

        // ---------- pool / UI logic (unchanged but using GetCount/GetItem) ----------
        private void PrewarmPool()
        {
            if (_pool == null) return;
            for (int i = 0; i < Prewarm; i++)
            {
                var v = _pool.Get();
                _pool.Release(v);
            }
        }

        private void UpdateContentSize()
        {
            _totalCount = GetCount();
            if (content == null || viewport == null) return;

            float itemFull = ItemFullSize();

            if (Direction == ScrollDirection.Vertical)
            {
                float desiredHeight = _totalCount * itemFull;
                float viewH = viewport.rect.height;

                if (_totalCount <= 0)
                {
                    content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewH);
                    content.anchoredPosition = Vector2.zero;
                    scrollRect.vertical = false;
                }
                else if (desiredHeight <= viewH)
                {
                    content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewH);
                    content.anchoredPosition = Vector2.zero;
                    scrollRect.vertical = false;
                }
                else
                {
                    content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, desiredHeight);
                    scrollRect.vertical = true;
                    ClampContentPosition();
                }

                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewport.rect.width);
            }
            else // Horizontal
            {
                float desiredWidth = _totalCount * itemFull;
                float viewW = viewport.rect.width;

                if (_totalCount <= 0)
                {
                    content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewW);
                    content.anchoredPosition = Vector2.zero;
                    scrollRect.horizontal = false;
                }
                else if (desiredWidth <= viewW)
                {
                    content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewW);
                    content.anchoredPosition = Vector2.zero;
                    scrollRect.horizontal = false;
                }
                else
                {
                    content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, desiredWidth);
                    scrollRect.horizontal = true;
                    ClampContentPosition();
                }

                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewport.rect.height);
            }

            Canvas.ForceUpdateCanvases();
        }

        private void ClampContentPosition()
        {
            if (content == null || viewport == null) return;

            Vector2 ap = content.anchoredPosition;

            if (Direction == ScrollDirection.Vertical)
            {
                float contentH = content.rect.height;
                float viewH = viewport.rect.height;
                float maxY = Mathf.Max(0f, contentH - viewH);
                ap.y = Mathf.Clamp(ap.y, 0f, maxY);
            }
            else
            {
                float contentW = content.rect.width;
                float viewW = viewport.rect.width;
                float maxX = Mathf.Max(0f, contentW - viewW);
                ap.x = Mathf.Clamp(ap.x, -maxX, 0f);
            }

            content.anchoredPosition = ap;
        }

        private void OnScroll(Vector2 v)
        {
            ClampContentPosition();
            Refresh();
        }

        private void Refresh()
        {
            if (GetCount() == 0 || content == null || viewport == null) return;

            _totalCount = GetCount();

            float viewportSize = Direction == ScrollDirection.Vertical ? viewport.rect.height : viewport.rect.width;
            float scrollPos = Direction == ScrollDirection.Vertical
                ? content.anchoredPosition.y
                : -content.anchoredPosition.x;

            float contentLen = Direction == ScrollDirection.Vertical ? content.rect.height : content.rect.width;
            scrollPos = Mathf.Clamp(scrollPos, 0f, Mathf.Max(0f, contentLen - viewportSize));

            float itemFull = ItemFullSize();
            int firstVisible = Mathf.FloorToInt(scrollPos / itemFull) - Buffer;
            int lastVisible = Mathf.CeilToInt((scrollPos + viewportSize) / itemFull) + Buffer;

            firstVisible = Mathf.Max(0, firstVisible);
            lastVisible = Mathf.Min(Mathf.Max(0, _totalCount - 1), lastVisible);

            var toRecycle = new List<int>();
            foreach (var kv in _active)
            {
                int idx = kv.Key;
                if (idx < firstVisible || idx > lastVisible) toRecycle.Add(idx);
            }

            foreach (var idx in toRecycle) Recycle(idx);

            for (int i = firstVisible; i <= lastVisible; i++)
            {
                if (i < 0 || i >= _totalCount) continue;
                if (!_active.ContainsKey(i))
                    CreateForIndex(i);
            }
        }

        private void CreateForIndex(int index)
        {
            if (_pool == null) return;
            if (index < 0 || index >= GetCount()) return;

            var view = _pool.Get();
            view.transform.SetParent(content, false);

            var rt = view.GetComponent<RectTransform>();
            if (rt != null)
            {
                if (Direction == ScrollDirection.Vertical)
                {
                    rt.anchorMin = new Vector2(0, 1);
                    rt.anchorMax = new Vector2(1, 1);
                    rt.pivot = new Vector2(0.5f, 1f);
                    float y = -index * ItemFullSize();
                    rt.anchoredPosition = new Vector2(SpacingX, y);
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ItemHeight);
                }
                else
                {
                    rt.anchorMin = new Vector2(0, 1);
                    rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(0f, 1f);
                    float x = index * ItemFullSize();
                    rt.anchoredPosition = new Vector2(x, -SpacingY);
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ItemWidth);
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ItemHeight);
                }
            }

            _active[index] = view;
            (view as IRecyclerItem<TModel>)?.SetData(GetItem(index), index);
        }

        private void SetViewPosition(TView view, int index)
        {
            var rt = view.GetComponent<RectTransform>();
            if (rt == null) return;

            if (Direction == ScrollDirection.Vertical)
            {
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 1);
                float y = -index * ItemFullSize();
                rt.anchoredPosition = new Vector2(SpacingX, y);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ItemHeight);
            }
            else
            {
                rt.anchorMin = new Vector2(0, 1);
                rt.anchorMax = new Vector2(0, 1);
                rt.pivot = new Vector2(0f, 1f);
                float x = index * ItemFullSize();
                rt.anchoredPosition = new Vector2(x, -SpacingY);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ItemWidth);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ItemHeight);
            }
        }

        private void Recycle(int index)
        {
            if (_active.TryGetValue(index, out var v))
            {
                (v as IRecyclerItem<TModel>)?.Clear();
                _active.Remove(index);
                _pool.Release(v);
            }
        }

        private void ClearAll()
        {
            var keys = new List<int>(_active.Keys);
            foreach (var k in keys) Recycle(k);
            _active.Clear();
        }

        protected virtual void OnDestroy()
        { 
            if (_obsSource != null) UnbindSource();
            _pool?.Clear();
            _active.Clear();
            if (_scrollTweener != null && _scrollTweener.IsActive())
            {
                _scrollTweener.Kill();
                _scrollTweener = null;
            }
        }

        private float ItemFullSize()
        {
            return (Direction == ScrollDirection.Vertical) ? (ItemHeight + SpacingY) : (ItemWidth + SpacingX);
        }

        // ----- Jump / DOTween scroll (unchanged API) -----
        private void JumpToIndex(int index, float duration, Ease jumpEase = Ease.OutSine, float normalizedViewportPos = 0f)
        {
            if (content == null || viewport == null) return;
            if (GetCount() == 0) return;

            index = Mathf.Clamp(index, 0, GetCount() - 1);

            UpdateContentSize();
            Canvas.ForceUpdateCanvases();

            float itemFull = ItemFullSize();
            float itemStart = index * itemFull;
            float viewportSize = Direction == ScrollDirection.Vertical ? viewport.rect.height : viewport.rect.width;
            float itemSize = Direction == ScrollDirection.Vertical ? ItemHeight : ItemWidth;
            float offsetInViewport = Mathf.Clamp01(normalizedViewportPos) * Mathf.Max(0f, viewportSize - itemSize);
            float rawTargetScroll = itemStart - offsetInViewport;

            float contentLen = Direction == ScrollDirection.Vertical ? content.rect.height : content.rect.width;
            float maxScroll = Mathf.Max(0f, contentLen - viewportSize);
            float clampedScroll = Mathf.Clamp(rawTargetScroll, 0f, maxScroll);

            if (duration <= 0f)
            {
                if (Direction == ScrollDirection.Vertical)
                    content.anchoredPosition = new Vector2(content.anchoredPosition.x, clampedScroll);
                else
                    content.anchoredPosition = new Vector2(-clampedScroll, content.anchoredPosition.y);
                ClampContentPosition();
                Refresh();
                return;
            }

            if (_scrollTweener != null && _scrollTweener.IsActive())
            {
                _scrollTweener.Kill();
                _scrollTweener = null;
            }

            if (DisableInputDuringJump)
            {
                enabled = false;
                scrollRect.velocity = Vector2.zero;
            }

            if (Direction == ScrollDirection.Vertical)
            {
                _scrollTweener = DOTween.To(() => content.anchoredPosition.y,
                        y => content.anchoredPosition = new Vector2(content.anchoredPosition.x, y),
                        clampedScroll, duration)
                    .SetEase(jumpEase)
                    .OnUpdate(() =>
                    {
                        ClampContentPosition();
                        Refresh();
                    })
                    .OnComplete(() =>
                    {
                        if (DisableInputDuringJump) enabled = true;
                        _scrollTweener = null;
                    });

                if (JumpUseUnscaledTime) _scrollTweener.SetUpdate(true);
            }
            else
            {
                _scrollTweener = DOTween.To(() => -content.anchoredPosition.x,
                        s => content.anchoredPosition = new Vector2(-s, content.anchoredPosition.y),
                        clampedScroll, duration)
                    .SetEase(jumpEase)
                    .OnUpdate(() =>
                    {
                        ClampContentPosition();
                        Refresh();
                    })
                    .OnComplete(() =>
                    {
                        if (DisableInputDuringJump) enabled = true;
                        _scrollTweener = null;
                    });

                if (JumpUseUnscaledTime) _scrollTweener.SetUpdate(true);
            }
        }

        public void JumpTo(int index, JumpPosition position = JumpPosition.Top, float duration = 0.3f,
            Ease jumpEase = Ease.OutSine)
        {
            float normalized = position == JumpPosition.Top ? 0f : (position == JumpPosition.Center ? 0.5f : 1f);
            JumpToIndex(index, duration, jumpEase, normalized);
        }

        public void JumpTo(int index, float duration, float normalizedViewportPos, Ease jumpEase = Ease.OutSine)
        {
            JumpToIndex(index, duration, jumpEase, normalizedViewportPos);
        }

        public void JumpToStart(float duration = 0.3f, Ease jumpEase = Ease.OutSine,
            JumpPosition pos = JumpPosition.Top) => JumpTo(0, pos, duration, jumpEase);

        public void JumpToMiddle(float duration = 0.3f, Ease jumpEase = Ease.OutSine,
            JumpPosition pos = JumpPosition.Top)
        {
            if (GetCount() == 0) return;
            int mid = Mathf.Clamp(GetCount() / 2, 0, GetCount() - 1);
            JumpTo(mid, pos, duration, jumpEase);
        }

        public void JumpToEnd(float duration = 0.3f, Ease jumpEase = Ease.OutSine, JumpPosition pos = JumpPosition.Top)
        {
            if (GetCount() == 0) return;
            int last = GetCount() - 1;
            JumpTo(last, pos, duration, jumpEase);
        }

        // public metrics
        public int ActiveCount => _active.Count;
        public int PoolInactiveCount => _pool?.CountInactive ?? 0;
    }
}