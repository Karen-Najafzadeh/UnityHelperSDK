using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using DG.Tweening;

/// <summary>
/// A comprehensive UI helper for Unity that handles common UI operations,
/// animations, pooling, and state management. Integrates with ObjectPoolHelper
/// for efficient UI element reuse and LocalizationManager for text localization.
/// 
/// Features:
/// - UI element pooling and recycling
/// - Smooth animations and transitions
/// - Toast and popup management
/// - Dynamic UI creation
/// - Responsive layout helpers
/// - UI state management
/// - Integration with localization
/// </summary>
public static class UIHelper
{
    // Cached references
    private static Canvas _mainCanvas;
    private static readonly Dictionary<string, GameObject> _uiPrefabs = new Dictionary<string, GameObject>();
    private static readonly Dictionary<string, Queue<GameObject>> _uiPools = new Dictionary<string, Queue<GameObject>>();
    private static readonly Stack<GameObject> _activePopups = new Stack<GameObject>();
    
    // Toast notification settings
    private static readonly Vector2 ToastSize = new Vector2(400f, 80f);
    private static readonly float ToastDuration = 2f;
    private static readonly float ToastFadeTime = 0.3f;
    
    // Animation defaults
    private static readonly float DefaultAnimationDuration = 0.3f;
    private static readonly Ease DefaultEase = Ease.OutQuad;

    #region Initialization

    /// <summary>
    /// Initialize the UI helper with the main canvas
    /// </summary>
    public static void Initialize(Canvas mainCanvas)
    {
        _mainCanvas = mainCanvas;
        InitializeCommonPrefabs();
    }

    private static void InitializeCommonPrefabs()
    {
        // Register common UI prefabs for pooling
        RegisterUIPrefab("Toast", CreateToastPrefab());
        RegisterUIPrefab("LoadingSpinner", CreateLoadingSpinnerPrefab());
        RegisterUIPrefab("Popup", CreatePopupPrefab());
    }

    /// <summary>
    /// Register a UI prefab for pooling
    /// </summary>
    public static void RegisterUIPrefab(string key, GameObject prefab)
    {
        _uiPrefabs[key] = prefab;
        _uiPools[key] = new Queue<GameObject>();
    }

    #endregion

    #region Toast Notifications

    /// <summary>
    /// Show a toast notification
    /// </summary>
    public static async Task ShowToast(string message, Color? color = null)
    {
        var toast = GetOrCreateUIElement("Toast");
        var text = toast.GetComponentInChildren<TMP_Text>();
        text.text = LocalizationManager.Get(message); // Integrate with localization
        if (color.HasValue)
            text.color = color.Value;

        // Position at bottom of screen
        toast.transform.position = new Vector3(
            Screen.width / 2f,
            ToastSize.y,
            0f
        );

        // Animate in
        var canvasGroup = toast.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        toast.SetActive(true);

        await canvasGroup.DOFade(1f, ToastFadeTime).AsyncWaitForCompletion();
        await Task.Delay(TimeSpan.FromSeconds(ToastDuration));
        await canvasGroup.DOFade(0f, ToastFadeTime).AsyncWaitForCompletion();

        ReturnToPool("Toast", toast);
    }

    #endregion

    #region Popup Management

    /// <summary>
    /// Show a popup dialog with optional buttons
    /// </summary>
    public static async Task<int> ShowPopup(string title, string message, params string[] buttons)
    {
        var popup = GetOrCreateUIElement("Popup");
        var titleText = popup.transform.Find("Title").GetComponent<TMP_Text>();
        var messageText = popup.transform.Find("Message").GetComponent<TMP_Text>();
        var buttonContainer = popup.transform.Find("ButtonContainer");

        // Set content
        titleText.text = LocalizationManager.Get(title);
        messageText.text = LocalizationManager.Get(message);

        // Create buttons
        TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
        for (int i = 0; i < buttons.Length; i++)
        {
            var button = CreateButton(buttons[i], i, tcs);
            button.transform.SetParent(buttonContainer, false);
        }

        // Show with animation
        _activePopups.Push(popup);
        await AnimatePopupIn(popup);

        int result = await tcs.Task;
        await AnimatePopupOut(popup);
        _activePopups.Pop();
        ReturnToPool("Popup", popup);

        return result;
    }

    private static async Task AnimatePopupIn(GameObject popup)
    {
        popup.transform.localScale = Vector3.zero;
        popup.SetActive(true);
        await popup.transform.DOScale(1f, DefaultAnimationDuration)
            .SetEase(DefaultEase)
            .AsyncWaitForCompletion();
    }

    private static async Task AnimatePopupOut(GameObject popup)
    {
        await popup.transform.DOScale(0f, DefaultAnimationDuration)
            .SetEase(DefaultEase)
            .AsyncWaitForCompletion();
        popup.SetActive(false);
    }

    #endregion

    #region Loading Indicator

    /// <summary>
    /// Show a loading spinner
    /// </summary>
    public static GameObject ShowLoadingSpinner(Transform parent = null)
    {
        var spinner = GetOrCreateUIElement("LoadingSpinner");
        if (parent != null)
            spinner.transform.SetParent(parent, false);
        
        spinner.transform.DORotate(new Vector3(0, 0, -360), 1f, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1);
            
        spinner.SetActive(true);
        return spinner;
    }

    /// <summary>
    /// Hide the loading spinner
    /// </summary>
    public static void HideLoadingSpinner(GameObject spinner)
    {
        if (spinner != null)
        {
            spinner.transform.DOKill();
            ReturnToPool("LoadingSpinner", spinner);
        }
    }

    #endregion

    #region Dynamic UI Creation

    /// <summary>
    /// Create a text label
    /// </summary>
    public static TMP_Text CreateLabel(string text, Transform parent = null)
    {
        var go = new GameObject("Label");
        var tmp = go.AddComponent<TMP_Text>();
        tmp.text = LocalizationManager.Get(text);
        tmp.font = Resources.Load<TMP_FontAsset>("Fonts/Default");
        tmp.fontSize = 24;
        tmp.alignment = TextAlignmentOptions.Center;

        if (parent != null)
            go.transform.SetParent(parent, false);

        return tmp;
    }

    /// <summary>
    /// Create a button with text
    /// </summary>
    public static Button CreateButton(string text, int result, TaskCompletionSource<int> tcs)
    {
        var go = new GameObject("Button");
        var button = go.AddComponent<Button>();
        var image = go.AddComponent<Image>();
        
        // Setup button visuals
        image.sprite = Resources.Load<Sprite>("UI/ButtonBackground");
        image.type = Image.Type.Sliced;

        // Add text
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var tmp = textGo.AddComponent<TMP_Text>();
        tmp.text = LocalizationManager.Get(text);
        tmp.font = Resources.Load<TMP_FontAsset>("Fonts/Default");
        tmp.fontSize = 20;
        tmp.alignment = TextAlignmentOptions.Center;

        // Add click handler
        button.onClick.AddListener(() => tcs.SetResult(result));

        return button;
    }

    #endregion

    #region Responsive Layout

    /// <summary>
    /// Make a UI element respond to screen size changes
    /// </summary>
    public static void MakeResponsive(RectTransform rect, float minWidth = 0, float maxWidth = float.MaxValue)
    {
        var responsive = rect.gameObject.AddComponent<ResponsiveElement>();
        responsive.MinWidth = minWidth;
        responsive.MaxWidth = maxWidth;
    }

    /// <summary>
    /// Adjust layout for current screen size
    /// </summary>
    public static void UpdateResponsiveLayout()
    {
        var responsiveElements = GameObject.FindObjectsOfType<ResponsiveElement>();
        foreach (var element in responsiveElements)
        {
            element.UpdateLayout();
        }
    }

    #endregion

    #region UI Pool Management

    private static GameObject GetOrCreateUIElement(string prefabKey)
    {
        if (_uiPools[prefabKey].Count > 0)
            return _uiPools[prefabKey].Dequeue();

        return CreateNewUIElement(prefabKey);
    }

    private static GameObject CreateNewUIElement(string prefabKey)
    {
        var prefab = _uiPrefabs[prefabKey];
        var instance = GameObject.Instantiate(prefab, _mainCanvas.transform);
        instance.SetActive(false);
        return instance;
    }

    private static void ReturnToPool(string prefabKey, GameObject element)
    {
        element.SetActive(false);
        element.transform.SetParent(_mainCanvas.transform);
        _uiPools[prefabKey].Enqueue(element);
    }

    #endregion

    #region Helper Classes

    private static GameObject CreateToastPrefab()
    {
        var go = new GameObject("Toast");
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = ToastSize;

        var image = go.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.8f);

        var text = new GameObject("Text").AddComponent<TMP_Text>();
        text.transform.SetParent(go.transform, false);
        text.alignment = TextAlignmentOptions.Center;
        text.font = Resources.Load<TMP_FontAsset>("Fonts/Default");
        text.fontSize = 24;
        text.color = Color.white;

        var canvasGroup = go.AddComponent<CanvasGroup>();

        return go;
    }

    private static GameObject CreateLoadingSpinnerPrefab()
    {
        var go = new GameObject("LoadingSpinner");
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(80f, 80f);

        var image = go.AddComponent<Image>();
        image.sprite = Resources.Load<Sprite>("UI/LoadingSpinner");
        image.color = Color.white;

        return go;
    }

    private static GameObject CreatePopupPrefab()
    {
        var go = new GameObject("Popup");
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(500f, 300f);

        // Background
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.9f);

        // Title
        var title = CreateLabel("", go.transform);
        title.fontSize = 28;
        title.alignment = TextAlignmentOptions.Center;

        // Message
        var message = CreateLabel("", go.transform);
        message.fontSize = 20;
        message.alignment = TextAlignmentOptions.Center;

        // Button container
        new GameObject("ButtonContainer", typeof(RectTransform))
            .transform.SetParent(go.transform, false);

        return go;
    }

    #endregion
}

/// <summary>
/// Component for handling responsive UI layouts
/// </summary>
public class ResponsiveElement : MonoBehaviour
{
    public float MinWidth { get; set; }
    public float MaxWidth { get; set; }
    private RectTransform _rect;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    public void UpdateLayout()
    {
        float screenWidth = Screen.width;
        float scale = 1f;

        if (screenWidth < MinWidth)
            scale = screenWidth / MinWidth;
        else if (screenWidth > MaxWidth)
            scale = screenWidth / MaxWidth;

        transform.localScale = Vector3.one * scale;
    }
}
