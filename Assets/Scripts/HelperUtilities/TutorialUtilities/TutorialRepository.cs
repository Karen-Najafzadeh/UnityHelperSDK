using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using UnityHelperSDK.Events;
using UnityHelperSDK.Data;

namespace UnityHelperSDK.Tutorial
{
    /// <summary>
    /// A centralized repository for managing game tutorials.
    /// Acts as a single source of truth for all tutorial data and integrates
    /// with TutorialHelper for execution and JsonHelper for persistence.
    /// </summary>
    public class TutorialRepository : MonoBehaviour
    {
        #region Tutorial Data Structures

        [Serializable]
        public class TutorialData
        {
            public string Id;
            public string CategoryId;
            public string Title;
            public string Description;
            public bool OnlyShowOnce;
            public int RequiredLevel;
            public List<string> Dependencies;
            public List<TutorialConditionData> StartConditions;
            public List<TutorialStepData> Steps;
        }

        [Serializable]
        public class TutorialStepData
        {
            public string Id;
            public string DialogueKey;
            public GameObject TargetObject;
            public List<TutorialConditionData> Conditions;
            public TutorialConditionData CompletionCondition;
        }

        [Serializable]
        public class TutorialConditionData
        {
            public string EventId;
            public TutorialConditionType ConditionType;
            public string[] Parameters;
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

        #region Helper Classes

        public abstract class TutorialCondition
        {
            public abstract bool IsMet();
            public abstract string GetDescription();
        }

        public class CustomTutorialCondition : TutorialCondition
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
                return true;
            }

            public override string GetDescription()
            {
                return $"Custom condition: {conditionId}";
            }
        }

        #endregion

        #region Fields

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

        private Dictionary<string, TutorialData> _tutorialDefinitions;
        private Dictionary<string, TutorialCategoryData> _categories;
        private Dictionary<string, TutorialSequence> _activeSequences;
        private HashSet<string> _completedTutorials;

        // Public accessors
        public IReadOnlyDictionary<string, TutorialData> TutorialDefinitions => _tutorialDefinitions;
        public IReadOnlyDictionary<string, TutorialCategoryData> Categories => _categories;
        public IReadOnlyDictionary<string, TutorialSequence> ActiveSequences => _activeSequences;
        public IReadOnlyCollection<string> CompletedTutorials => _completedTutorials;

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
            _activeSequences = new Dictionary<string, TutorialSequence>();
            _completedTutorials = new HashSet<string>();

            await LoadTutorialDefinitions();
            await LoadTutorialProgress();
        }

        private async Task LoadTutorialDefinitions()
        {
            try
            {
                var json = await LoadTutorialJson();
                if (!string.IsNullOrEmpty(json))
                {
                    var data = UnityHelperSDK.Data.JsonHelper.Deserialize<Dictionary<string, TutorialData>>(json);
                    if (data != null)
                    {
                        _tutorialDefinitions = data;
                        foreach (var tutorial in _tutorialDefinitions)
                        {
                            CreateTutorialSequence(tutorial.Value);
                        }
                    }
                }

                json = await LoadCategoryJson();
                if (!string.IsNullOrEmpty(json))
                {
                    _categories = UnityHelperSDK.Data.JsonHelper.Deserialize<Dictionary<string, TutorialCategoryData>>(json);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error loading tutorial definitions: {e.Message}");
            }
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

        private Task LoadTutorialProgress()
        {
            var json = PlayerPrefs.GetString("CompletedTutorials", "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var completed = UnityHelperSDK.Data.JsonHelper.Deserialize<List<string>>(json);
                    _completedTutorials = new HashSet<string>(completed);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error loading tutorial progress: {e.Message}");
                }
            }
            return Task.CompletedTask;
        }

        private void SaveProgress()
        {
            var json = UnityHelperSDK.Data.JsonHelper.Serialize(_completedTutorials.ToList());
            PlayerPrefs.SetString("CompletedTutorials", json);
            PlayerPrefs.Save();
        }

        #endregion

        #region Tutorial Management

        public int GetPlayerLevel()
        {
            // TODO: Implement this based on your game's level system
            return 1;
        }        
        protected virtual void CreateTutorialSequence(TutorialData data)
        {
            var sequence = new TutorialSequence(data.Id, data.OnlyShowOnce, data.RequiredLevel, data.CategoryId);

            // Create event handlers for start conditions
            foreach (var condition in data.StartConditions ?? Enumerable.Empty<TutorialConditionData>())
            {
                if (condition.ConditionType == TutorialConditionType.Start)
                {
                    var handler = CreateStartConditionHandler(condition, data);
                    if (handler != null)
                    {
                        sequence.AddStartCondition(handler);
                    }
                }
            }

            // Create steps with their conditions
            foreach (var stepData in data.Steps)
            {
                var step = new TutorialStep(stepData.Id, stepData.DialogueKey);

                // Add step conditions
                foreach (var condition in stepData.Conditions ?? Enumerable.Empty<TutorialConditionData>())
                {
                    var handler = (condition.ConditionType == TutorialConditionType.Custom) ?
                        CreateCustomConditionHandler(condition, data, stepData.Id) :
                        CreateStepConditionHandler(condition, data, stepData.Id);
                        
                    if (handler != null)
                    {
                        step.AddCondition(handler);
                    }
                }

                // Add completion condition
                if (stepData.CompletionCondition != null)
                {
                    var handler = (stepData.CompletionCondition.ConditionType == TutorialConditionType.Custom) ?
                        CreateCustomConditionHandler(stepData.CompletionCondition, data, stepData.Id) :
                        CreateStepConditionHandler(stepData.CompletionCondition, data, stepData.Id);
                        
                    if (handler != null)
                    {
                        step.SetCompletionCondition(handler);
                    }
                }

                sequence.AddStep(step);
            }

            _activeSequences[data.Id] = sequence;
            TutorialHelper.RegisterTutorial(sequence);
            OnTutorialRegistered?.Invoke(data.Id);
        }        private Action<TutorialEvents.TutorialStartConditionEvent> CreateStartConditionHandler(
            TutorialConditionData condition,
            TutorialData tutorial)
        {
            if (condition == null) return null;
            return (evt) => 
            {
                if (evt.TutorialId == tutorial.Id)
                {
                    evt.HasMetConditions = tutorial.RequiredLevel <= GetPlayerLevel();
                }
            };
        }

        private Action<TutorialEvents.TutorialStepConditionEvent> CreateStepConditionHandler(
            TutorialConditionData condition,
            TutorialData tutorial,
            string stepId)
        {
            if (condition == null) return null;
            return (evt) => 
            {
                if (evt.TutorialId == tutorial.Id && evt.StepId == stepId)
                {
                    var customEvt = new TutorialEvents.CustomTutorialConditionEvent
                    {
                        ConditionId = condition.EventId,
                        Parameters = condition.Parameters?.Cast<object>().ToArray()
                    };
                    EventHelper.Trigger(customEvt);
                    evt.HasMetConditions = customEvt.HasMetCondition;
                }
            };
        }

        private Action<TutorialEvents.TutorialStepConditionEvent> CreateCustomConditionHandler(
            TutorialConditionData condition,
            TutorialData tutorial,
            string stepId)
        {
            if (condition == null) return null;
            return (evt) => 
            {
                if (evt.TutorialId == tutorial.Id &&
                    (string.IsNullOrEmpty(stepId) || evt.StepId == stepId))
                {
                    var customEvt = new TutorialEvents.CustomTutorialConditionEvent
                    {
                        ConditionId = condition.EventId,
                        Parameters = condition.Parameters?.Cast<object>().ToArray()
                    };
                    EventHelper.Trigger(customEvt);
                    evt.HasMetConditions = customEvt.HasMetCondition;
                }
            };
        }

        private Action<TutorialEvents.TutorialStepConditionEvent> CreateEventHandler(
            TutorialConditionData condition, 
            TutorialData tutorial, 
            string stepId = null)
        {
            switch (condition.ConditionType)
            {
                case TutorialConditionType.Start:
                    return (evt) => 
                    {
                        if (evt.TutorialId == tutorial.Id && 
                            (string.IsNullOrEmpty(stepId) || evt.StepId == stepId))
                        {
                            evt.HasMetConditions = tutorial.RequiredLevel <= GetPlayerLevel();
                        }
                    };

                case TutorialConditionType.Step:
                    return (evt) => 
                    {
                        if (evt.TutorialId == tutorial.Id && evt.StepId == stepId)
                        {
                            var customEvt = new TutorialEvents.CustomTutorialConditionEvent
                            {
                                ConditionId = condition.EventId,
                                Parameters = condition.Parameters?.Cast<object>().ToArray()
                            };
                            EventHelper.Trigger(customEvt);
                            evt.HasMetConditions = customEvt.HasMetCondition;
                        }
                    };

                case TutorialConditionType.Custom:
                    return (evt) => 
                    {
                        if (evt.TutorialId == tutorial.Id &&
                            (string.IsNullOrEmpty(stepId) || evt.StepId == stepId))
                        {
                            var customEvt = new TutorialEvents.CustomTutorialConditionEvent
                            {
                                ConditionId = condition.EventId,
                                Parameters = condition.Parameters?.Cast<object>().ToArray()
                            };
                            EventHelper.Trigger(customEvt);
                            evt.HasMetConditions = customEvt.HasMetCondition;
                        }
                    };

                default:
                    Debug.LogError($"Unsupported condition type: {condition.ConditionType}");
                    return null;
            }
        }

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

            // Trigger tutorial started event
            EventHelper.Trigger(new TutorialEvents.TutorialStartedEvent
            {
                TutorialId = tutorialId,
                CategoryId = tutorial.CategoryId,
                RequiredLevel = tutorial.RequiredLevel
            });

            await TutorialHelper.StartTutorial(tutorialId);
            return true;
        }

        public void CompleteTutorial(string tutorialId)
        {
            if (!_completedTutorials.Contains(tutorialId))
            {
                _completedTutorials.Add(tutorialId);
                SaveProgress();
                
                // Log analytics
                EventHelper.Trigger(new TutorialEvents.TutorialAnalyticsEvent
                {
                    TutorialId = tutorialId,
                    EventType = "completed",
                    Success = true
                });
                
                OnTutorialCompleted?.Invoke(tutorialId);
            }
        }

        public bool IsTutorialCompleted(string tutorialId)
        {
            return _completedTutorials.Contains(tutorialId);
        }

        public List<TutorialData> GetTutorialsByCategory(string categoryId)
        {
            return _tutorialDefinitions.Values
                .Where(t => t.CategoryId == categoryId)
                .ToList();
        }

        public List<TutorialCategoryData> GetCategories()
        {
            return _categories.Values.ToList();
        }

        #endregion
    }
}
