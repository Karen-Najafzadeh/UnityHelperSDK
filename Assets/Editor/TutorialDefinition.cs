using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityHelperSDK.Editor
{
    /// <summary>
    /// ScriptableObject wrapper for runtime TutorialData
    /// </summary>
    public class TutorialDefinition : ScriptableObject
    {
        [SerializeField]
        private string id;
        [SerializeField]
        private string categoryId;
        [SerializeField]
        private string title;
        [SerializeField]
        private string description;
        [SerializeField]
        private bool onlyShowOnce = true;
        [SerializeField]
        private int requiredLevel;
        [SerializeField]
        private List<string> dependencies = new List<string>();        [SerializeField]
        private List<TutorialConditionData> startConditions = new List<TutorialConditionData>();
        [SerializeField]
        private List<TutorialStepData> steps = new List<TutorialStepData>();

        // Public properties
        public string Id => id;
        public string CategoryId => categoryId;
        public string Title => title;
        public string Description => description;
        public bool OnlyShowOnce => onlyShowOnce;
        public int RequiredLevel => requiredLevel;        public List<string> Dependencies => dependencies;
        public List<TutorialConditionData> StartConditions => startConditions;
        public List<TutorialStepData> Steps => steps;

        public void Initialize(string newId, string catId, string newTitle = "", string newDescription = "", int reqLevel = 1, bool showOnce = true)
        {
            id = newId;
            categoryId = catId;
            title = newTitle;
            description = newDescription;
            requiredLevel = reqLevel;
            onlyShowOnce = showOnce;
        }

        /// <summary>
        /// Convert this ScriptableObject to runtime TutorialData
        /// </summary>
        public TutorialRepository.TutorialData ToRuntimeData()
        {
            return new TutorialRepository.TutorialData
            {
                Id = id,
                CategoryId = categoryId,
                OnlyShowOnce = onlyShowOnce,
                RequiredLevel = requiredLevel,
                Dependencies = dependencies ?? new List<string>(),
                StartConditions = startConditions?.Select(c => c.eventId).ToList() ?? new List<string>(),
                Steps = steps?.ConvertAll(s => new TutorialRepository.TutorialStepData
                {
                    Id = s.id,
                    DialogueKey = s.dialogueKey,
                    TargetObject = s.targetObject,
                    Conditions = s.conditions?.Select(c => c.eventId).ToList() ?? new List<string>(),
                    CompletionCondition = s.completionCondition?.eventId
                }) ?? new List<TutorialRepository.TutorialStepData>()
            };
        }

        /// <summary>
        /// Create a new TutorialDefinition from runtime TutorialData
        /// </summary>
        public static TutorialDefinition FromRuntimeData(TutorialRepository.TutorialData data)
        {
            var definition = CreateInstance<TutorialDefinition>();
            definition.id = data.Id;
            definition.categoryId = data.CategoryId;
            definition.onlyShowOnce = data.OnlyShowOnce;
            definition.requiredLevel = data.RequiredLevel;
            definition.dependencies = data.Dependencies ?? new List<string>();
            definition.startConditions = data.StartConditions?.Select(eventId => new TutorialConditionData 
            { 
                eventId = eventId,
                conditionType = TutorialConditionType.Start 
            }).ToList() ?? new List<TutorialConditionData>();
            definition.steps = data.Steps?.ConvertAll(s => new TutorialStepData
            {
                id = s.Id,
                dialogueKey = s.DialogueKey,
                conditions = s.Conditions?.Select(eventId => new TutorialConditionData 
                { 
                    eventId = eventId,
                    conditionType = TutorialConditionType.Step 
                }).ToList() ?? new List<TutorialConditionData>(),
                completionCondition = !string.IsNullOrEmpty(s.CompletionCondition) ? new TutorialConditionData
                {
                    eventId = s.CompletionCondition,
                    conditionType = TutorialConditionType.Step
                } : null
            }) ?? new List<TutorialStepData>();
            return definition;
        }
    }

    /// <summary>
    /// ScriptableObject wrapper for runtime TutorialCategoryData
    /// </summary>
    public class TutorialCategory : ScriptableObject
    {
        [SerializeField]
        private string id;
        [SerializeField]
        private string displayName;
        [SerializeField]
        private string description;
        [SerializeField]
        private int sortOrder;
        [SerializeField]
        private List<string> tutorialIds = new List<string>();

        // Public properties
        public string Id => id;
        public string Name => displayName;
        public string Description => description;
        public int SortOrder => sortOrder;
        public List<string> TutorialIds => tutorialIds;

        public void Initialize(string newId, string newName = "", string newDescription = "", int order = 0)
        {
            id = newId;
            displayName = newName;
            description = newDescription;
            sortOrder = order;
        }

        /// <summary>
        /// Convert this ScriptableObject to runtime TutorialCategoryData
        /// </summary>
        public TutorialRepository.TutorialCategoryData ToRuntimeData()
        {
            return new TutorialRepository.TutorialCategoryData
            {
                Id = id,
                Name = displayName,
                Description = description,
                Order = sortOrder
            };
        }

        /// <summary>
        /// Create a new TutorialCategory from runtime TutorialCategoryData
        /// </summary>
        public static TutorialCategory FromRuntimeData(TutorialRepository.TutorialCategoryData data)
        {
            var category = CreateInstance<TutorialCategory>();
            category.id = data.Id;
            category.displayName = data.Name;
            category.description = data.Description;
            category.sortOrder = data.Order;
            return category;
        }
    }

    [Serializable]
    public class TutorialStepData
    {
        [SerializeField]
        public string id;
        [SerializeField]
        public string dialogueKey;

        [SerializeField]
        public GameObject targetObject;

        [SerializeField]
        public List<TutorialConditionData> conditions = new List<TutorialConditionData>();

        [SerializeField]
        public TutorialConditionData completionCondition;
    }

    [Serializable]
    public class TutorialConditionData
    {
        [SerializeField]
        public string eventId;
        
        [SerializeField]
        public TutorialConditionType conditionType;

        [SerializeField]
        public string[] parameters;

        public object[] GetParameterValues()
        {
            return parameters?.Select(p => (object)p).ToArray() ?? Array.Empty<object>();
        }
    }

    public enum TutorialConditionType
    {
        Start,
        Step,
        Custom
    }
}
