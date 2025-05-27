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
        public string CategoryId;
        public bool OnlyShowOnce;
        public int RequiredLevel;
        public List<string> Dependencies;
        public List<string> StartConditions;
        public List<TutorialStepData> Steps;
    }

    [Serializable]
    public class TutorialStepData
    {
        public string Id;
        public string DialogueKey;
        public List<string> Conditions;
        public string CompletionCondition;
    }

    [Serializable]
    public class TutorialCategoryData
    {
        public string Id;
        public string Name;
        public string Description;
        public int Order;
    }

    #endregion

    #region Fields

    private Dictionary<string, TutorialData> _tutorialDefinitions;
    private Dictionary<string, TutorialCategoryData> _categories;
    private Dictionary<string, TutorialHelper.TutorialSequence> _activeSequences;
    private HashSet<string> _completedTutorials;

    // Events
    public event Action<string> OnTutorialRegistered;
    public event Action<string> OnTutorialUnregistered;
    public event Action<string> OnTutorialCompleted;

    #endregion

    #region Initialization

    private void Awake()
    {
        InitializeAsync().ContinueWith(task => {
            if (task.Exception != null)
                Debug.LogError($"Error initializing TutorialRepository: {task.Exception}");
        });
    }

    private async Task InitializeAsync()
    {
        _tutorialDefinitions = new Dictionary<string, TutorialData>();
        _categories = new Dictionary<string, TutorialCategoryData>();
        _activeSequences = new Dictionary<string, TutorialHelper.TutorialSequence>();
        _completedTutorials = new HashSet<string>();

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

    private Task LoadTutorialProgress()
    {
        var json = PlayerPrefs.GetString("CompletedTutorials", "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var completed = JsonHelper.Deserialize<List<string>>(json);
                _completedTutorials = new HashSet<string>(completed);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading tutorial progress: {e.Message}");
            }
        }
        return Task.CompletedTask;
    }

    private Task<string> LoadTutorialJson()
    {
        var textAsset = Resources.Load<TextAsset>("Tutorials/tutorial_definitions");
        return Task.FromResult(textAsset?.text ?? "");
    }

    private Task<string> LoadCategoryJson()
    {
        var textAsset = Resources.Load<TextAsset>("Tutorials/tutorial_categories");
        return Task.FromResult(textAsset?.text ?? ""); 
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
    /// Create a tutorial sequence from data
    /// </summary>
    protected virtual void CreateTutorialSequence(TutorialData data)
    {
        var sequence = new TutorialHelper.TutorialSequence
        {
            Id = data.Id,
            OnlyShowOnce = data.OnlyShowOnce,
            RequiredLevel = data.RequiredLevel,
            Steps = new List<TutorialHelper.TutorialStep>()
        };

        foreach (var stepData in data.Steps)
        {
            var step = new TutorialHelper.TutorialStep
            {
                Id = stepData.Id,
                DialogueKey = stepData.DialogueKey,
                Conditions = CreateConditions(stepData.Conditions),
                CompletionCondition = CreateCondition(stepData.CompletionCondition)
            };

            sequence.Steps.Add(step);
        }

        sequence.StartConditions = CreateConditions(data.StartConditions);
        _activeSequences[data.Id] = sequence;
        TutorialHelper.RegisterTutorial(sequence);
        OnTutorialRegistered?.Invoke(data.Id);
    }

    /// <summary>
    /// Create a single condition instance based on a condition ID
    /// </summary>
    protected virtual TutorialHelper.TutorialCondition CreateCondition(string conditionId)
    {
        if (string.IsNullOrEmpty(conditionId))
            return null;

        // Parse condition ID to determine type and parameters
        var parts = conditionId.Split(':');
        var type = parts[0].ToLower();

        switch (type)
        {
            case "tutorial":
                // Format: tutorial:tutorialId1,tutorialId2
                var tutorials = parts[1].Split(',');
                return new TutorialCompletionCondition(conditionId, tutorials);

            case "level":
                // Format: level:5
                if (int.TryParse(parts[1], out int level))
                {
                    return new LevelCondition(conditionId, level);
                }
                break;

            case "custom":
                // Format: custom:conditionId
                return new CustomTutorialCondition(parts[1]);

            default:
                // For backward compatibility or unknown types
                return new CustomTutorialCondition(conditionId);
        }

        Debug.LogWarning($"Invalid condition format: {conditionId}");
        return null;
    }

    /// <summary>
    /// Create condition instances from condition identifiers
    /// </summary>
    protected virtual List<TutorialHelper.TutorialCondition> CreateConditions(List<string> conditionIds)
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

    #endregion

    #region Tutorial Info

    /// <summary>
    /// Get all tutorials in a category
    /// </summary>
    public List<TutorialData> GetTutorialsByCategory(string categoryId)
    {
        return _tutorialDefinitions.Values
            .Where(t => t.CategoryId == categoryId)
            .ToList();
    }

    /// <summary>
    /// Get all available categories
    /// </summary>
    public List<TutorialCategoryData> GetCategories()
    {
        return _categories.Values.ToList();
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
        var json = JsonHelper.Serialize(_completedTutorials.ToList());
        PlayerPrefs.SetString("CompletedTutorials", json);
        PlayerPrefs.Save();
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// Base implementation of a custom tutorial condition.
    /// </summary>
    public class CustomTutorialCondition : TutorialHelper.TutorialCondition
    {
        protected readonly string conditionId;
        private readonly Func<bool> _checkCondition;

        public CustomTutorialCondition(string conditionId, Func<bool> checkCondition = null)
        {
            this.conditionId = conditionId;
            _checkCondition = checkCondition;
        }

        public override bool IsMet()
        {
            if (_checkCondition != null)
            {
                return _checkCondition();
            }

            // Default implementation - override this in derived classes
            // or provide a check delegate in constructor
            return true;
        }

        public override string GetDescription()
        {
            return $"Custom condition: {conditionId}";
        }
    }

    /// <summary>
    /// A condition that checks if the player has completed certain tutorials
    /// </summary>
    public class TutorialCompletionCondition : CustomTutorialCondition
    {
        private readonly string[] _requiredTutorials;

        public TutorialCompletionCondition(string conditionId, params string[] requiredTutorials) 
            : base(conditionId)
        {
            _requiredTutorials = requiredTutorials;
        }

        public override bool IsMet()
        {
            return _requiredTutorials == null || 
                   _requiredTutorials.All(t => Instance.IsTutorialCompleted(t));
        }

        public override string GetDescription()
        {
            return $"Requires tutorials: {string.Join(", ", _requiredTutorials)}";
        }
    }

    /// <summary>
    /// A condition that checks if the player has reached a certain level
    /// </summary>
    public class LevelCondition : CustomTutorialCondition
    {
        private readonly int _requiredLevel;

        public LevelCondition(string conditionId, int requiredLevel) 
            : base(conditionId)
        {
            _requiredLevel = requiredLevel;
        }

        public override bool IsMet()
        {
            // Implementation depends on your level system
            int playerLevel = 1; // Get from your player system
            return playerLevel >= _requiredLevel;
        }

        public override string GetDescription()
        {
            return $"Requires level: {_requiredLevel}";
        }
    }

    #endregion
}
