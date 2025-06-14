using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;


namespace UnityHelperSDK.HelperUtilities{


    /// <summary>
    /// A robust, reusable infinite/recycling scroll implementation for Unity UI,
    /// with optional snapping, multi-column grid support, smooth snapping animations,
    /// visibility callbacks, and programmatic control.
    /// Derive from this class and override <c>UpdateItem(Transform item, int index)</c> to bind data.
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public abstract class SuperScrollRect : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        public enum ScrollDirection { Vertical, Horizontal }

        [Header("Configuration")]
        public ScrollDirection direction = ScrollDirection.Vertical;
        [Tooltip("Number of columns for grid layout (set to 1 for list)")]
        public int columns = 1;
        [Tooltip("Enable snapping to discrete item positions on drag end")]
        public bool enableSnapping = false;
        [Tooltip("Prefab for items (must have a RectTransform)")]
        public GameObject itemPrefab;
        [Tooltip("Total number of items (set dynamically or via inspector)")]
        public int totalItemCount;
        [Tooltip("Size of each item (height for vertical, width for horizontal)")]
        public float itemSize = 100f;
        [Tooltip("Spacing between items")]
        public float spacing = 10f;
        [Tooltip("Buffer rows (or columns) beyond viewport capacity")]
        public int bufferItems = 1;

        [Header("Scroll Events")]
        public UnityEvent OnScrollStarted;
        public UnityEvent OnScrollEnded;
        public UnityEvent<int> OnItemBecameVisible;
        public UnityEvent<int> OnItemBecameInvisible;
        public UnityEvent OnReachedStart;
        public UnityEvent OnReachedEnd;

        [Header("References")]
        private ScrollRect scrollRect;
        public RectTransform viewport;
        public RectTransform content;

        [Header("Advanced Configuration")]
        [Tooltip("Enable pull-to-refresh functionality")]
        public bool enablePullToRefresh = false;
        [Tooltip("Distance needed to pull before refresh triggers")]
        public float pullToRefreshThreshold = 100f;
        [Tooltip("Separate spacing for horizontal direction")]
        public float horizontalSpacing = 10f;
        [Tooltip("Separate spacing for vertical direction")]
        public float verticalSpacing = 10f;
        [Tooltip("Deceleration rate for scroll momentum (higher = less momentum)")]
        [Range(0.01f, 0.99f)]
        public float decelerationRate = 0.135f;
        [Tooltip("Whether to support variable item sizes")]
        public bool variableItemSizes = false;
        [Tooltip("Enable infinite scrolling (auto-loading more items)")]
        public bool enableInfiniteScrolling = false;
        [Tooltip("Threshold distance from end to trigger infinite scroll loading")]
        public float infiniteScrollThreshold = 0.2f;
        [Tooltip("Smooth scroll animation duration")]
        [Range(0.1f, 1f)]
        public float scrollAnimationDuration = 0.3f;
        [Tooltip("Enable scroll to center")]
        public bool scrollToCenter = false;

        [Header("Header & Footer")]
        public GameObject headerPrefab;
        public GameObject footerPrefab;
        public GameObject loadingIndicatorPrefab;
        
        [Header("Events")]
        public UnityEvent OnPullToRefreshTriggered;
        public UnityEvent OnLoadMore;
        
        // Internal
        private int poolSize;
        private List<RectTransform> itemPool = new List<RectTransform>();
        private int firstVisibleIndex = 0;
        private Vector2 lastContentPos;
        private HashSet<int> visibleIndices = new HashSet<int>();
        private Coroutine snapCoroutine;

        // Dictionary to store variable item sizes
        private Dictionary<int, Vector2> itemSizes = new Dictionary<int, Vector2>();
        private bool isPulling = false;
        private float pullDistance = 0f;
        private RectTransform headerInstance;
        private RectTransform footerInstance;
        private RectTransform loadingIndicator;
        private bool isLoading;
        private float originalSpacing;
        
        protected virtual void Awake()
        {
            scrollRect = GetComponent<ScrollRect>();
            if (viewport == null || content == null || itemPrefab == null)
                Debug.LogError("[SuperScrollRect] Assign viewport, content, and itemPrefab in inspector.");

            originalSpacing = spacing;
            
            if (headerPrefab != null)
            {
                headerInstance = Instantiate(headerPrefab, content).GetComponent<RectTransform>();
                headerInstance.SetAsFirstSibling();
            }
            
            if (footerPrefab != null)
            {
                footerInstance = Instantiate(footerPrefab, content).GetComponent<RectTransform>();
                footerInstance.SetAsLastSibling();
            }
            
            if (loadingIndicatorPrefab != null)
            {
                loadingIndicator = Instantiate(loadingIndicatorPrefab, content).GetComponent<RectTransform>();
                loadingIndicator.gameObject.SetActive(false);
            }

            // Initialize default spacings if not set
            if (horizontalSpacing == 0) horizontalSpacing = spacing;
            if (verticalSpacing == 0) verticalSpacing = spacing;

            // Set scroll rect deceleration rate
            scrollRect.decelerationRate = decelerationRate;

            scrollRect.onValueChanged.AddListener(_ => ScrollUpdate());
        }

        protected virtual void OnEnable()
        {
            InitializePool();
            ForceUpdateAll();
        }

        protected virtual void OnDisable()
        {
            scrollRect.onValueChanged.RemoveListener(_ => ScrollUpdate());
        }

        /// <summary>Override to bind your data to the item.</summary>
        public abstract void UpdateItem(Transform item, int index);

        public void OnBeginDrag(PointerEventData eventData)
        {
            OnScrollStarted?.Invoke();
            if (snapCoroutine != null)
            {
                StopCoroutine(snapCoroutine);
                snapCoroutine = null;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            OnScrollEnded?.Invoke();
            if (!enableSnapping) return;
            // Smooth snap to nearest
            float primary = (direction == ScrollDirection.Vertical) ? -content.anchoredPosition.y : content.anchoredPosition.x;
            float unit = itemSize + spacing;
            int nearest = Mathf.RoundToInt(primary / unit);
            nearest = Mathf.Clamp(nearest, 0, Mathf.CeilToInt((float)totalItemCount / columns) - 1);
            float target = nearest * unit;
            Vector2 targetPos = (direction == ScrollDirection.Vertical)
                ? new Vector2(content.anchoredPosition.x, -target)
                : new Vector2(target, content.anchoredPosition.y);

            if (snapCoroutine != null) StopCoroutine(snapCoroutine);
            snapCoroutine = StartCoroutine(SmoothSnap(content.anchoredPosition, targetPos, 0.3f));
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!enablePullToRefresh) return;

            if (direction == ScrollDirection.Vertical && content.anchoredPosition.y > 0)
            {
                isPulling = true;
                pullDistance = content.anchoredPosition.y;
                
                if (pullDistance >= pullToRefreshThreshold)
                {
                    OnPullToRefreshTriggered?.Invoke();
                    // Reset pull state
                    isPulling = false;
                    pullDistance = 0;
                    content.anchoredPosition = Vector2.zero;
                }
            }
        }

        /// <summary>Programmatically scrolls to a specific item index.</summary>
        public void ScrollToIndex(int index, bool animated = true)
        {
            index = Mathf.Clamp(index, 0, totalItemCount - 1);
            int primaryIndex = index / columns;
            float unit = itemSize + spacing;
            float target = primaryIndex * unit;
            Vector2 targetPos = (direction == ScrollDirection.Vertical)
                ? new Vector2(content.anchoredPosition.x, -target)
                : new Vector2(target, content.anchoredPosition.y);

            if (animated)
            {
                if (snapCoroutine != null) StopCoroutine(snapCoroutine);
                snapCoroutine = StartCoroutine(SmoothSnap(content.anchoredPosition, targetPos, 0.3f));
            }
            else
            {
                content.anchoredPosition = targetPos;
                ScrollUpdate();
            }
        }

        private IEnumerator SmoothSnap(Vector2 from, Vector2 to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                content.anchoredPosition = Vector2.Lerp(from, to, elapsed / duration);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            content.anchoredPosition = to;
            ScrollUpdate();
            snapCoroutine = null;
        }

        private void InitializePool()
        {
            // Destroy existing pool
            foreach (var rt in itemPool)
                if (rt) Destroy(rt.gameObject);
            itemPool.Clear();
            visibleIndices.Clear();

            // Calculate pool size
            float viewSize = (direction == ScrollDirection.Vertical)
                ? viewport.rect.height
                : viewport.rect.width;
            int visibleUnits = Mathf.CeilToInt(viewSize / (itemSize + spacing));
            int visibleCount = visibleUnits * columns;
            poolSize = Mathf.Clamp(visibleCount + bufferItems * columns, 1, totalItemCount);

            // Resize content
            int rows = Mathf.CeilToInt((float)totalItemCount / columns);
            float contentLength = rows * (itemSize + spacing) - spacing;
            float width = (direction == ScrollDirection.Vertical)
                ? columns * (itemSize + spacing) - spacing
                : contentLength;
            float height = (direction == ScrollDirection.Vertical)
                ? contentLength
                : columns * (itemSize + spacing) - spacing;
            content.sizeDelta = new Vector2(width, height);

            // Instantiate pool
            for (int i = 0; i < poolSize; i++)
            {
                var go = Instantiate(itemPrefab, content);
                var rt = go.GetComponent<RectTransform>();
                itemPool.Add(rt);
            }
        }

        private void ScrollUpdate()
        {
            Vector2 pos = content.anchoredPosition;
            if (pos == lastContentPos) return;
            lastContentPos = pos;

            float offset = (direction == ScrollDirection.Vertical) ? -pos.y : pos.x;
            int unitIndex = Mathf.FloorToInt(offset / (itemSize + spacing));
            int newFirst = Mathf.Clamp(unitIndex * columns, 0, Math.Max(0, totalItemCount - poolSize));

            if (newFirst != firstVisibleIndex)
            {
                firstVisibleIndex = newFirst;
                ForceUpdateAll();
            }

            // Check for infinite scroll
            if (enableInfiniteScrolling && !isLoading)
            {
                float viewportSize = direction == ScrollDirection.Vertical 
                    ? viewport.rect.height 
                    : viewport.rect.width;
                float contentSize = direction == ScrollDirection.Vertical 
                    ? content.sizeDelta.y 
                    : content.sizeDelta.x;
                float scrollPosition = direction == ScrollDirection.Vertical 
                    ? -content.anchoredPosition.y 
                    : content.anchoredPosition.x;
                
                float remainingScroll = contentSize - (scrollPosition + viewportSize);
                if (remainingScroll <= viewportSize * infiniteScrollThreshold)
                {
                    OnLoadMore?.Invoke();
                }
            }

            // Start/end reached
            if (offset <= 0) OnReachedStart?.Invoke();
            if (direction == ScrollDirection.Vertical && offset >= content.sizeDelta.y - viewport.rect.height)
                OnReachedEnd?.Invoke();
        }

        /// <summary>
        /// Sets a custom size for a specific item index. Only works when variableItemSizes is true.
        /// </summary>
        public void SetItemSize(int index, Vector2 size)
        {
            if (!variableItemSizes) return;
            itemSizes[index] = size;
            RecalculateContentSize();
        }

        /// <summary>
        /// Refreshes the view of a specific item if it's currently visible.
        /// </summary>
        public void RefreshItem(int index)
        {
            if (index < firstVisibleIndex || index >= firstVisibleIndex + poolSize) return;
            
            int poolIndex = index - firstVisibleIndex;
            if (poolIndex >= 0 && poolIndex < itemPool.Count)
            {
                var rt = itemPool[poolIndex];
                if (rt.gameObject.activeSelf)
                {
                    UpdateItem(rt, index);
                }
            }
        }

        private void ForceUpdateAll()
        {
            var newVisible = new HashSet<int>();
            for (int i = 0; i < poolSize; i++)
            {
                int dataIndex = firstVisibleIndex + i;
                var rt = itemPool[i];
                bool active = dataIndex < totalItemCount;
                rt.gameObject.SetActive(active);
                if (!active) continue;

                // Positioning
                int row = dataIndex / columns;
                int col = dataIndex % columns;
                float x = col * (itemSize + spacing);
                float y = -row * (itemSize + spacing);
                rt.anchoredPosition = new Vector2(x, y);

                UpdateItem(rt, dataIndex);
                newVisible.Add(dataIndex);

                // Fire visibility events
                if (!visibleIndices.Contains(dataIndex))
                    OnItemBecameVisible?.Invoke(dataIndex);
            }
            // Items no longer visible
            foreach (var idx in visibleIndices)
            {
                if (!newVisible.Contains(idx))
                    OnItemBecameInvisible?.Invoke(idx);
            }
            visibleIndices = newVisible;
        }

        private void RecalculateContentSize()
        {
            float headerHeight = headerInstance != null ? (headerInstance.gameObject.activeSelf ? headerInstance.rect.height + spacing : 0) : 0;
            float footerHeight = footerInstance != null ? (footerInstance.gameObject.activeSelf ? footerInstance.rect.height + spacing : 0) : 0;

            if (!variableItemSizes)
            {
                int rows = Mathf.CeilToInt((float)totalItemCount / columns);
                float contentLength = rows * (itemSize + (direction == ScrollDirection.Vertical ? verticalSpacing : horizontalSpacing)) - spacing;
                
                if (direction == ScrollDirection.Vertical)
                {
                    contentLength += headerHeight + footerHeight;
                }
                
                float width = (direction == ScrollDirection.Vertical)
                    ? columns * (itemSize + horizontalSpacing) - horizontalSpacing
                    : contentLength;
                float height = (direction == ScrollDirection.Vertical)
                    ? contentLength
                    : columns * (itemSize + verticalSpacing) - verticalSpacing;
                
                content.sizeDelta = new Vector2(width, height);
                return;
            }

            // Calculate size based on variable item sizes
            float maxWidth = 0;
            float totalHeight = headerHeight;  // Start with header height
            float currentRowWidth = 0;
            float currentRowHeight = 0;
            float totalWidth = 0;

            for (int i = 0; i < totalItemCount; i++)
            {
                Vector2 size = itemSizes.TryGetValue(i, out Vector2 customSize) ? customSize : new Vector2(itemSize, itemSize);
                
                if (direction == ScrollDirection.Vertical)
                {
                    if (i % columns == 0 && i > 0)
                    {
                        totalHeight += currentRowHeight + verticalSpacing;
                        currentRowHeight = 0;
                    }
                    currentRowHeight = Mathf.Max(currentRowHeight, size.y);
                    currentRowWidth = (i % columns) * (size.x + horizontalSpacing);
                    maxWidth = Mathf.Max(maxWidth, currentRowWidth + size.x);
                }
                else
                {
                    if (i % columns == 0 && i > 0)
                    {
                        totalWidth += currentRowWidth + horizontalSpacing;
                        currentRowWidth = 0;
                    }
                    currentRowWidth = Mathf.Max(currentRowWidth, size.x);
                    currentRowHeight = (i % columns) * (size.y + verticalSpacing);
                    totalHeight = Mathf.Max(totalHeight, currentRowHeight + size.y);
                }
            }

            // Add the last row/column and footer
            if (direction == ScrollDirection.Vertical)
            {
                totalHeight += currentRowHeight + footerHeight;
                content.sizeDelta = new Vector2(maxWidth, totalHeight);
            }
            else
            {
                totalWidth += currentRowWidth;
                content.sizeDelta = new Vector2(totalWidth, totalHeight + footerHeight);
            }
        }

        /// <summary>
        /// Shows or hides the loading indicator
        /// </summary>
        public void SetLoading(bool loading)
        {
            if (loadingIndicator != null)
            {
                isLoading = loading;
                loadingIndicator.gameObject.SetActive(loading);
                
                // Position the loading indicator
                if (loading)
                {
                    Vector2 position = direction == ScrollDirection.Vertical
                        ? new Vector2(0, -(content.sizeDelta.y + spacing))
                        : new Vector2(content.sizeDelta.x + spacing, 0);
                    loadingIndicator.anchoredPosition = position;
                }
            }
        }

        /// <summary>
        /// Scrolls to make an item centered in the viewport
        /// </summary>
        public void ScrollToCenter(int index, bool animated = true)
        {
            if (!scrollToCenter) return;
            
            index = Mathf.Clamp(index, 0, totalItemCount - 1);
            int primaryIndex = index / columns;
            float unit = itemSize + spacing;
            
            // Calculate center position
            float viewportSize = direction == ScrollDirection.Vertical 
                ? viewport.rect.height 
                : viewport.rect.width;
            float itemOffset = primaryIndex * unit;
            float centerOffset = (viewportSize - itemSize) * 0.5f;
            float target = itemOffset - centerOffset;
            
            Vector2 targetPos = direction == ScrollDirection.Vertical
                ? new Vector2(content.anchoredPosition.x, -target)
                : new Vector2(target, content.anchoredPosition.y);

            if (animated)
            {
                if (snapCoroutine != null) StopCoroutine(snapCoroutine);
                snapCoroutine = StartCoroutine(SmoothSnap(content.anchoredPosition, targetPos, scrollAnimationDuration));
            }
            else
            {
                content.anchoredPosition = targetPos;
                ScrollUpdate();
            }
        }

        /// <summary>
        /// Shows or hides the header view
        /// </summary>
        public void SetHeaderVisible(bool visible)
        {
            if (headerInstance != null)
            {
                headerInstance.gameObject.SetActive(visible);
                RecalculateContentSize();
            }
        }

        /// <summary>
        /// Shows or hides the footer view
        /// </summary>
        public void SetFooterVisible(bool visible)
        {
            if (footerInstance != null)
            {
                footerInstance.gameObject.SetActive(visible);
                RecalculateContentSize();
            }
        }
    }
}