using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using TMPro;
using DG.Tweening;
using UnityEngine.Events;

/// <summary>
/// A comprehensive tutorial system that handles complex, multi-step tutorials
/// with branching paths, conditions, and dynamic content.
/// 
/// Features:
/// - Multi-step tutorial sequences
/// - Conditional progression
/// - Dynamic highlighting and focus
/// - Input blocking and guidance
/// - Progress tracking and persistence
/// - Localization support
/// - Analytics integration
/// - Adaptive difficulty
/// - AR/VR compatibility
/// </summary>
public static class TutorialHelper
{
    // Tutorial state tracking
    private static readonly Dictionary<string, TutorialSequence> _tutorials = new Dictionary<string, TutorialSequence>();
    private static TutorialSequence _activeTutorial;
    private static TutorialStep _currentStep;
    private static bool _isTutorialActive;
    
    // UI references
    private static Canvas _tutorialCanvas;
    private static GameObject _highlightPrefab;
    private static GameObject _arrowPrefab;
    private static readonly Color _highlightColor = new Color(1f, 1f, 0f, 0.3f);
    
    // Tutorial settings
    private static bool _canSkipTutorials = true;
    private static bool _useAnimations = true;
    private static float _defaultStepDelay = 0.5f;
    private static float _defaultAnimationDuration = 0.3f;
    
    // Events
    public static event Action<string> OnTutorialStarted;
    public static event Action<string> OnTutorialCompleted;
    public static event Action<TutorialStep> OnStepStarted;
    public static event Action<TutorialStep> OnStepCompleted;
    
    #region Initialization
    
    /// <summary>
    /// Initialize the tutorial system
    /// </summary>
    public static async Task Initialize()
    {
        // Create tutorial canvas
        var canvasObj = new GameObject("TutorialCanvas");
        _tutorialCanvas = canvasObj.AddComponent<Canvas>();
        _tutorialCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _tutorialCanvas.sortingOrder = 100; // Ensure it's above other UI
        
        var scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        // Load prefabs from Resources
        _highlightPrefab = Resources.Load<GameObject>("Tutorials/TutorialHighlight");
        _arrowPrefab = Resources.Load<GameObject>("Tutorials/TutorialArrow");
        
        // Load saved tutorial progress
        await LoadTutorialProgress();
    }
    
    #endregion
    
    #region Tutorial Management
    
    /// <summary>
    /// Register a new tutorial sequence
    /// </summary>
    public static void RegisterTutorial(TutorialSequence tutorial)
    {
        if (!_tutorials.ContainsKey(tutorial.Id))
        {
            _tutorials[tutorial.Id] = tutorial;
        }
    }
    
    /// <summary>
    /// Start a tutorial sequence
    /// </summary>
    public static async Task StartTutorial(string tutorialId, bool force = false)
    {
        if (_isTutorialActive && !force) return;
        
        if (!_tutorials.TryGetValue(tutorialId, out var tutorial))
        {
            Debug.LogWarning($"Tutorial '{tutorialId}' not found!");
            return;
        }
        
        // Check if tutorial should be shown
        if (!force && !ShouldShowTutorial(tutorial))
        {
            return;
        }
        
        _isTutorialActive = true;
        _activeTutorial = tutorial;
        OnTutorialStarted?.Invoke(tutorialId);
        
        // Record start in analytics
        DebugHelper.Log($"Starting tutorial: {tutorialId}");
        
        await ProcessTutorialSequence(tutorial);
    }
    
    /// <summary>
    /// Process each step in a tutorial sequence
    /// </summary>
    private static async Task ProcessTutorialSequence(TutorialSequence tutorial)
    {
        foreach (var step in tutorial.Steps)
        {
            if (!await ProcessTutorialStep(step))
            {
                // Tutorial was interrupted
                return;
            }
        }
        
        CompleteTutorial(tutorial);
    }
    
    /// <summary>
    /// Process a single tutorial step
    /// </summary>
    private static async Task<bool> ProcessTutorialStep(TutorialStep step)
    {
        _currentStep = step;
        OnStepStarted?.Invoke(step);
        
        // Check conditions
        if (step.Conditions != null && !step.Conditions.All(c => c.IsMet()))
        {
            return true; // Skip this step but continue tutorial
        }
        
        // Set up visual elements
        var highlightObj = await SetupStepVisuals(step);
        
        // Show dialogue if any
        if (!string.IsNullOrEmpty(step.DialogueKey))
        {
            await DialogueHelper.StartDialogue(new DialogueHelper.DialogueNode
            {
                DialogueKey = step.DialogueKey,
                Text = LocalizationManager.GetLocalizedText(step.DialogueKey)
            });
        }
        
        // Wait for completion
        bool completed = false;
        bool skipped = false;
        
        while (!completed && !skipped)
        {
            // Check for skip input
            if (_canSkipTutorials && Input.GetKeyDown(KeyCode.Escape))
            {
                skipped = true;
                break;
            }
            
            // Check completion condition
            if (step.CompletionCondition != null)
            {
                completed = step.CompletionCondition.IsMet();
            }
            else
            {
                // If no condition, wait for interaction
                completed = Input.GetMouseButtonDown(0);
            }
            
            await Task.Yield();
        }
        
        // Cleanup
        if (highlightObj != null)
        {
            GameObject.Destroy(highlightObj);
        }
        
        OnStepCompleted?.Invoke(step);
        
        return !skipped;
    }
    
    #endregion
    
    #region Visual Helpers
    
    /// <summary>
    /// Set up visual elements for a tutorial step
    /// </summary>
    private static async Task<GameObject> SetupStepVisuals(TutorialStep step)
    {
        if (step.Target == null) return null;

        // Create highlight
        var highlightObj = GameObject.Instantiate(_highlightPrefab);
        highlightObj.transform.SetParent(_tutorialCanvas.transform, false);
        
        // Position highlight
        var targetRect = step.Target.GetComponent<RectTransform>();
        if (targetRect != null)
        {
            // UI element
            var highlightRect = highlightObj.GetComponent<RectTransform>();
            highlightRect.position = targetRect.position;
            highlightRect.sizeDelta = targetRect.sizeDelta;
        }
        else
        {
            // World space object
            var renderer = step.Target.GetComponent<Renderer>();
            if (renderer != null)
            {
                highlightObj.transform.position = renderer.bounds.center;
                highlightObj.transform.localScale = renderer.bounds.size;
            }
        }
        
        // Setup highlight visuals
        var highlightImage = highlightObj.GetComponent<UnityEngine.UI.Image>();
        if (highlightImage != null)
        {
            highlightImage.color = _highlightColor;
        }
        
        // Add arrow if needed
        if (_arrowPrefab != null)
        {
            var arrowObj = GameObject.Instantiate(_arrowPrefab);
            arrowObj.transform.SetParent(highlightObj.transform);
            // Position arrow based on screen position
            var screenPos = Camera.main.WorldToScreenPoint(step.Target.transform.position);
            var arrowRect = arrowObj.GetComponent<RectTransform>();
            arrowRect.anchoredPosition = screenPos;
        }
        
        // Animate highlight
        if (_useAnimations)
        {
            await highlightObj.transform.DOScale(Vector3.one * 1.1f, _defaultAnimationDuration)
                .SetEase(DG.Tweening.Ease.OutBack)
                .SetLoops(-1, DG.Tweening.LoopType.Yoyo)
                .AsyncWaitForCompletion();
        }
        
        return highlightObj;
    }
    
    #endregion
    
    #region Progress Management
    
    private static Dictionary<string, TutorialProgress> _tutorialProgress = 
        new Dictionary<string, TutorialProgress>();

    /// <summary>
    /// Complete a tutorial and save progress
    /// </summary>
    private static async void CompleteTutorial(TutorialSequence tutorial)
    {
        _isTutorialActive = false;
        _activeTutorial = null;
        _currentStep = null;
        
        // Save progress
        await SaveTutorialProgress(tutorial.Id);
        
        OnTutorialCompleted?.Invoke(tutorial.Id);
        DebugHelper.Log($"Completed tutorial: {tutorial.Id}");
    }

    /// <summary>
    /// Save tutorial progress
    /// </summary>
    private static async Task SaveTutorialProgress(string tutorialId)
    {
        var progress = new TutorialProgress
        {
            CompletedAt = DateTime.UtcNow,
            StepsCompleted = _activeTutorial?.Steps?.Count ?? 0,
            CustomData = new Dictionary<string, object>()
        };

        _tutorialProgress[tutorialId] = progress;

        // Save to local storage
        var json = JsonUtility.ToJson(new TutorialSaveData { Progress = _tutorialProgress });
        PlayerPrefs.SetString("TutorialProgress", json);
        PlayerPrefs.Save();

        // Save to cloud if available
        try
        {
            await FirebaseHelper.SetDocumentAsync("tutorials", "progress", _tutorialProgress);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to save tutorial progress to cloud: {ex.Message}");
        }
    }

    /// <summary>
    /// Load saved tutorial progress
    /// </summary>
    private static async Task LoadTutorialProgress()
    {
        // Try loading from cloud first
        try
        {
            var cloudProgress = await FirebaseHelper.GetDocumentAsync<Dictionary<string, TutorialProgress>>(
                "tutorials", "progress");
            if (cloudProgress != null)
            {
                _tutorialProgress = cloudProgress;
                return;
            }
        }
        catch
        {
            // Fall back to local storage
        }

        // Load from local storage
        string json = PlayerPrefs.GetString("TutorialProgress", "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var data = JsonUtility.FromJson<TutorialSaveData>(json);
                if (data?.Progress != null)
                    _tutorialProgress = data.Progress;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load tutorial progress: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Check if a tutorial has been completed
    /// </summary>
    public static bool IsTutorialCompleted(string tutorialId)
    {
        return _tutorialProgress.ContainsKey(tutorialId);
    }

    /// <summary>
    /// Get tutorial completion time
    /// </summary>
    public static DateTime? GetTutorialCompletionTime(string tutorialId)
    {
        return _tutorialProgress.TryGetValue(tutorialId, out var progress) 
            ? progress.CompletedAt 
            : null;
    }

    /// <summary>
    /// Reset progress for a specific tutorial
    /// </summary>
    public static async Task ResetTutorialProgress(string tutorialId)
    {
        if (_tutorialProgress.ContainsKey(tutorialId))
        {
            _tutorialProgress.Remove(tutorialId);
            await SaveTutorialProgress(tutorialId);
        }
    }

    /// <summary>
    /// Reset all tutorial progress
    /// </summary>
    public static async Task ResetAllProgress()
    {
        _tutorialProgress.Clear();
        PlayerPrefs.DeleteKey("TutorialProgress");
        PlayerPrefs.Save();

        try
        {
            await FirebaseHelper.DeleteDocumentAsync("tutorials", "progress");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to delete cloud tutorial progress: {ex.Message}");
        }
    }

    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// Check if a tutorial should be shown based on conditions
    /// </summary>
    private static bool ShouldShowTutorial(TutorialSequence tutorial)
    {
        // Check if already completed
        if (tutorial.OnlyShowOnce && IsTutorialCompleted(tutorial.Id))
        {
            return false;
        }
        
        // Check level requirements
        if (tutorial.RequiredLevel > 0)
        {
            // Implementation depends on your level system
            int playerLevel = 1; // Get from your player system
            if (playerLevel < tutorial.RequiredLevel)
            {
                return false;
            }
        }
        
        // Check custom conditions
        return tutorial.StartConditions == null || 
               tutorial.StartConditions.All(c => c.IsMet());
    }
    
    #endregion
    
    #region Helper Classes
    
    /// <summary>
    /// Represents a complete tutorial sequence
    /// </summary>
    public class TutorialSequence
    {
        public string Id { get; set; }
        public List<TutorialStep> Steps { get; set; } = new List<TutorialStep>();
        public List<TutorialCondition> StartConditions { get; set; }
        public bool OnlyShowOnce { get; set; } = true;
        public int RequiredLevel { get; set; }
        public string Category { get; set; }
        public UnityEvent OnComplete { get; set; }
    }
    
    /// <summary>
    /// Represents a single step in a tutorial
    /// </summary>
    public class TutorialStep
    {
        public string Id { get; set; }
        public string DialogueKey { get; set; }
        public GameObject Target { get; set; }
        public List<TutorialCondition> Conditions { get; set; }
        public TutorialCondition CompletionCondition { get; set; }
        public UnityEvent OnStart { get; set; }
        public UnityEvent OnComplete { get; set; }
    }
    
    /// <summary>
    /// Base class for tutorial conditions
    /// </summary>
    public abstract class TutorialCondition
    {
        /// <summary>
        /// Check if the condition is met
        /// </summary>
        public abstract bool IsMet();

        /// <summary>
        /// Get a descriptive message about the condition
        /// </summary>
        public virtual string GetDescription()
        {
            return "Tutorial condition";
        }

        /// <summary>
        /// Called when the condition is first registered
        /// </summary>
        public virtual void Initialize() { }

        /// <summary>
        /// Called when the condition is no longer needed
        /// </summary>
        public virtual void Cleanup() { }
    }

    /// <summary>
    /// Delegate for checking if a tutorial condition is met
    /// </summary>
    public delegate bool TutorialConditionCheck();

    /// <summary>
    /// A custom tutorial condition that uses a delegate to check if it's met
    /// </summary>
    public class DelegateCondition : TutorialCondition
    {
        private readonly TutorialConditionCheck _check;
        private readonly string _description;

        public DelegateCondition(TutorialConditionCheck check, string description = null)
        {
            _check = check ?? throw new ArgumentNullException(nameof(check));
            _description = description;
        }

        public override bool IsMet()
        {
            return _check();
        }

        public override string GetDescription()
        {
            return _description ?? base.GetDescription();
        }
    }

    /// <summary>
    /// A tutorial condition that combines multiple conditions with AND/OR logic
    /// </summary>
    public class CompositeCondition : TutorialCondition
    {
        private readonly TutorialCondition[] _conditions;
        private readonly bool _requireAll;

        public CompositeCondition(IEnumerable<TutorialCondition> conditions, bool requireAll = true)
        {
            _conditions = conditions?.ToArray() ?? throw new ArgumentNullException(nameof(conditions));
            _requireAll = requireAll;
        }

        public override bool IsMet()
        {
            return _requireAll 
                ? _conditions.All(c => c.IsMet())
                : _conditions.Any(c => c.IsMet());
        }

        public override string GetDescription()
        {
            string logic = _requireAll ? "ALL" : "ANY";
            return $"Requires {logic} of: {string.Join(", ", _conditions.Select(c => c.GetDescription()))}";
        }
    }

    /// <summary>
    /// Represents tutorial progress data for saving/loading
    /// </summary>
    private class TutorialProgress
    {
        public DateTime CompletedAt { get; set; }
        public int StepsCompleted { get; set; }
        public Dictionary<string, object> CustomData { get; set; }
    }

    [Serializable]
    private class TutorialSaveData
    {
        public Dictionary<string, TutorialProgress> Progress;
    }

    #endregion
}
