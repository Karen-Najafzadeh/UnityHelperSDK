using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

/// <summary>
/// A centralized repository for managing game tutorials.
/// Acts as a single source of truth for all tutorial data and integrates
/// with TutorialHelper for execution and JsonHelper for persistence.
/// 
/// Features:
/// - Tutorial data management
/// - JSON-based tutorial definitions
/// - Tutorial sequence creation
/// - Progress tracking
/// - Analytics integration
/// - Category management
/// - Tutorial dependencies
/// </summary>
public class TutorialRepository : MonoBehaviour
{
    #region Singleton Pattern
    
    private static TutorialRepository _instance;
    public static TutorialRepository Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[TutorialRepository]");
                _instance = go.AddComponent<TutorialRepository>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    
    #endregion

    #region Tutorial Data Structures

    [Serializable]
    public class TutorialData
    {
        public string Id;
        public string Category;
        public int RequiredLevel;
        public bool OnlyShowOnce = true;
        public List<TutorialStepData> Steps;
        public List<string> Dependencies; // IDs of tutorials that must be completed first
        public Dictionary<string, object> CustomData;
    }

    [Serializable]
    public class TutorialStepData
    {
        public string Id;
        public string DialogueKey;
        public string TargetObjectPath;  // Scene path to target object
        public List<string> RequiredConditions;
        public string CompletionCondition;
        public Dictionary<string, object> CustomData;
    }

    [Serializable]
    public class TutorialCategoryData
    {
        public string Id;
        public string Name;
        public string Description;
        public List<string> TutorialIds;
        public int SortOrder;
    }

    #endregion

    #region Fields

    private Dictionary<string, TutorialData> _tutorialDefinitions = new Dictionary<string, TutorialData>();
    private Dictionary<string, TutorialCategoryData> _categories = new Dictionary<string, TutorialCategoryData>();
    private HashSet<string> _completedTutorials = new HashSet<string>();
    private Dictionary<string, TutorialHelper.TutorialSequence> _activeSequences = new Dictionary<string, TutorialHelper.TutorialSequence>();

    // Events
    public event Action<string> OnTutorialRegistered;
    public event Action<string> OnTutorialUnregistered;
    public event Action<string> OnTutorialCompleted;

    #endregion

    #region Initialization

    private async void Awake()
    {
        await LoadTutorialDefinitions();
        await LoadTutorialProgress();
    }

    private async Task LoadTutorialDefinitions()
    {
        try
        {
            // Load tutorial definitions from JSON
            var json = await LoadTutorialJson();
            if (string.IsNullOrEmpty(json)) return;

            var data = JsonHelper.Deserialize<Dictionary<string, TutorialData>>(json);
            if (data != null)
            {
                _tutorialDefinitions = data;
                foreach (var tutorial in _tutorialDefinitions)
                {
                    CreateTutorialSequence(tutorial.Value);
                }
            }

            // Load categories
            json = await LoadCategoryJson();
            if (!string.IsNullOrEmpty(json))
            {
                _categories = JsonHelper.Deserialize<Dictionary<string, TutorialCategoryData>>(json);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading tutorial definitions: {e.Message}");
        }
    }

    private async Task LoadTutorialProgress()
    {
        try
        {
            var progressJson = PlayerPrefs.GetString("TutorialProgress");
            if (!string.IsNullOrEmpty(progressJson))
            {
                var progress = JsonHelper.Deserialize<HashSet<string>>(progressJson);
                if (progress != null)
                {
                    _completedTutorials = progress;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading tutorial progress: {e.Message}");
        }
    }

    #endregion

    #region Tutorial Management

    /// <summary>
    /// Start a tutorial by its ID
    /// </summary>
    public async Task<bool> StartTutorial(string tutorialId)
    {
        if (!_tutorialDefinitions.ContainsKey(tutorialId))
        {
            Debug.LogWarning($"Tutorial '{tutorialId}' not found!");
            return false;
        }

        var tutorial = _tutorialDefinitions[tutorialId];

        // Check dependencies
        if (tutorial.Dependencies != null)
        {
            foreach (var dependencyId in tutorial.Dependencies)
            {
                if (!_completedTutorials.Contains(dependencyId))
                {
                    Debug.LogWarning($"Tutorial '{tutorialId}' requires '{dependencyId}' to be completed first!");
                    return false;
                }
            }
        }

        // Create sequence if not exists
        if (!_activeSequences.ContainsKey(tutorialId))
        {
            CreateTutorialSequence(tutorial);
        }

        await TutorialHelper.StartTutorial(tutorialId);
        return true;
    }

    /// <summary>
    /// Create a TutorialSequence from TutorialData
    /// </summary>
    private void CreateTutorialSequence(TutorialData data)
    {
        var sequence = new TutorialHelper.TutorialSequence
        {
            Id = data.Id,
            Category = data.Category,
            RequiredLevel = data.RequiredLevel,
            OnlyShowOnce = data.OnlyShowOnce,
            Steps = new List<TutorialHelper.TutorialStep>()
        };

        foreach (var stepData in data.Steps)
        {
            var step = new TutorialHelper.TutorialStep
            {
                Id = stepData.Id,
                DialogueKey = stepData.DialogueKey,
                Target = !string.IsNullOrEmpty(stepData.TargetObjectPath) ? 
                    GameObject.Find(stepData.TargetObjectPath) : null,
                Conditions = CreateConditions(stepData.RequiredConditions),
                CompletionCondition = CreateCondition(stepData.CompletionCondition)
            };

            sequence.Steps.Add(step);
        }

        _activeSequences[data.Id] = sequence;
        TutorialHelper.RegisterTutorial(sequence);
    }

    /// <summary>
    /// Create condition instances from condition identifiers
    /// </summary>
    private List<TutorialHelper.TutorialCondition> CreateConditions(List<string> conditionIds)
    {
        if (conditionIds == null) return null;

        var conditions = new List<TutorialHelper.TutorialCondition>();
        foreach (var id in conditionIds)
        {
            var condition = CreateCondition(id);
            if (condition != null)
            {
                conditions.Add(condition);
            }
        }
        return conditions;
    }

    /// <summary>
    /// Create a condition instance from a condition identifier
    /// </summary>
    private TutorialHelper.TutorialCondition CreateCondition(string conditionId)
    {
        if (string.IsNullOrEmpty(conditionId)) return null;

        // Example condition creation - expand based on your needs
        switch (conditionId)
        {
            case "PlayerMoved":
                return new PlayerMovedCondition();
            case "ItemCollected":
                return new ItemCollectedCondition();
            // Add more conditions as needed
            default:
                Debug.LogWarning($"Unknown condition type: {conditionId}");
                return null;
        }
    }

    #endregion

    #region Progress Management

    /// <summary>
    /// Mark a tutorial as completed
    /// </summary>
    public void CompleteTutorial(string tutorialId)
    {
        if (!_completedTutorials.Contains(tutorialId))
        {
            _completedTutorials.Add(tutorialId);
            SaveProgress();
            OnTutorialCompleted?.Invoke(tutorialId);
        }
    }

    /// <summary>
    /// Check if a tutorial is completed
    /// </summary>
    public bool IsTutorialCompleted(string tutorialId)
    {
        return _completedTutorials.Contains(tutorialId);
    }

    /// <summary>
    /// Save tutorial progress
    /// </summary>
    private void SaveProgress()
    {
        try
        {
            var json = JsonHelper.Serialize(_completedTutorials);
            PlayerPrefs.SetString("TutorialProgress", json);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving tutorial progress: {e.Message}");
        }
    }

    #endregion

    #region Category Management

    /// <summary>
    /// Get all tutorials in a category
    /// </summary>
    public List<TutorialData> GetTutorialsByCategory(string categoryId)
    {
        if (!_categories.TryGetValue(categoryId, out var category))
        {
            return new List<TutorialData>();
        }

        return category.TutorialIds
            .Where(id => _tutorialDefinitions.ContainsKey(id))
            .Select(id => _tutorialDefinitions[id])
            .ToList();
    }

    /// <summary>
    /// Get all available categories
    /// </summary>
    public List<TutorialCategoryData> GetCategories()
    {
        return _categories.Values.OrderBy(c => c.SortOrder).ToList();
    }

    #endregion

    #region Helper Methods

    private async Task<string> LoadTutorialJson()
    {
        var textAsset = Resources.Load<TextAsset>("Tutorials/tutorial_definitions");
        return textAsset?.text;
    }

    private async Task<string> LoadCategoryJson()
    {
        var textAsset = Resources.Load<TextAsset>("Tutorials/tutorial_categories");
        return textAsset?.text;
    }

    #endregion
}

#region Custom Conditions

/// <summary>
/// Example condition for checking if player has moved
/// </summary>
public class PlayerMovedCondition : TutorialHelper.TutorialCondition
{
    private Vector3 _initialPosition;
    private bool _initialized;

    public override bool IsMet()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return false;

        if (!_initialized)
        {
            _initialPosition = player.transform.position;
            _initialized = true;
            return false;
        }

        return Vector3.Distance(_initialPosition, player.transform.position) > 0.1f;
    }
}

/// <summary>
/// Example condition for checking if an item was collected
/// </summary>
public class ItemCollectedCondition : TutorialHelper.TutorialCondition
{
    public override bool IsMet()
    {
        // Implement your item collection check logic here
        return false;
    }
}

#endregion
