using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityHelperSDK.Tutorial;

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
        private List<string> dependencies = new List<string>();
        [SerializeField] 
        private List<TutorialConditionData> startConditions = new List<TutorialConditionData>();
        [SerializeField]
        private List<TutorialStepData> steps = new List<TutorialStepData>();

        // Public properties
        public string Id => id;
        public string CategoryId => categoryId;
        public string Title => title;
        public string Description => description;
        public bool OnlyShowOnce => onlyShowOnce;
        public int RequiredLevel => requiredLevel;
        public List<string> Dependencies => dependencies;
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
                Title = title,
                Description = description,
                OnlyShowOnce = onlyShowOnce,
                RequiredLevel = requiredLevel,
                Dependencies = dependencies,
                StartConditions = startConditions?.ConvertAll(c => new TutorialRepository.TutorialConditionData
                {
                    EventId = c.eventId,
                    ConditionType = c.conditionType,
                    Parameters = c.parameters
                }) ?? new List<TutorialRepository.TutorialConditionData>(),
                Steps = steps?.ConvertAll(s => new TutorialRepository.TutorialStepData
                {
                    Id = s.id,
                    DialogueKey = s.dialogueKey,
                    TargetObject = s.targetObject,
                    Conditions = s.conditions?.ConvertAll(c => new TutorialRepository.TutorialConditionData
                    {
                        EventId = c.eventId,
                        ConditionType = c.conditionType,
                        Parameters = c.parameters
                    }) ?? new List<TutorialRepository.TutorialConditionData>(),
                    CompletionCondition = s.completionCondition != null ? new TutorialRepository.TutorialConditionData
                    {
                        EventId = s.completionCondition.eventId,
                        ConditionType = s.completionCondition.conditionType,
                        Parameters = s.completionCondition.parameters
                    } : null
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
            definition.title = data.Title;
            definition.description = data.Description;
            definition.onlyShowOnce = data.OnlyShowOnce;
            definition.requiredLevel = data.RequiredLevel;
            definition.dependencies = data.Dependencies ?? new List<string>();
            definition.startConditions = data.StartConditions?.ConvertAll(c => new TutorialConditionData
            {
                eventId = c.EventId,
                conditionType = c.ConditionType,
                parameters = c.Parameters
            }) ?? new List<TutorialConditionData>();
            definition.steps = data.Steps?.ConvertAll(s => new TutorialStepData
            {
                id = s.Id,
                dialogueKey = s.DialogueKey,
                targetObject = s.TargetObject,
                conditions = s.Conditions?.ConvertAll(c => new TutorialConditionData
                {
                    eventId = c.EventId,
                    conditionType = c.ConditionType,
                    parameters = c.Parameters
                }) ?? new List<TutorialConditionData>(),
                completionCondition = s.CompletionCondition != null ? new TutorialConditionData
                {
                    eventId = s.CompletionCondition.EventId,
                    conditionType = s.CompletionCondition.ConditionType,
                    parameters = s.CompletionCondition.Parameters
                } : null
            }) ?? new List<TutorialStepData>();
            return definition;
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
        public UnityHelperSDK.Tutorial.TutorialConditionType conditionType;

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
