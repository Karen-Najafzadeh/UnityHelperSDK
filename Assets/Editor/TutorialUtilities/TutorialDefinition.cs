using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityHelperSDK.Tutorial;
using Newtonsoft.Json;

namespace UnityHelperSDK.Editor
{
    /// <summary>
    /// Serializable class for storing tutorial data
    /// </summary>
    [Serializable]
    public class TutorialDefinition
    {
        public string Id { get; set; }
        public string CategoryId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public bool OnlyShowOnce { get; set; } = true;
        public int RequiredLevel { get; set; }
        public List<string> Dependencies { get; set; } = new();
        public List<TutorialConditionData> StartConditions { get; set; } = new();
        public List<TutorialStepData> Steps { get; set; } = new();

        public TutorialDefinition() { }

        public void Initialize(string newId, string catId, string newTitle = "", string newDescription = "", int reqLevel = 1, bool showOnce = true)
        {
            Id = newId;
            CategoryId = catId;
            Title = newTitle;
            Description = newDescription;
            RequiredLevel = reqLevel;
            OnlyShowOnce = showOnce;
        }

        public UnityHelperSDK.Tutorial.TutorialRepository.TutorialData ToRuntimeData()
        {
            return new UnityHelperSDK.Tutorial.TutorialRepository.TutorialData
            {
                Id = Id,
                CategoryId = CategoryId,
                Title = Title,
                Description = Description,
                OnlyShowOnce = OnlyShowOnce,
                RequiredLevel = RequiredLevel,
                Dependencies = Dependencies,
                StartConditions = StartConditions?.ConvertAll(c => new UnityHelperSDK.Tutorial.TutorialRepository.TutorialConditionData
                {
                    EventId = c.EventId,
                    ConditionType = c.ConditionType,
                    Parameters = c.Parameters
                }) ?? new(),
                Steps = Steps?.ConvertAll(s => new UnityHelperSDK.Tutorial.TutorialRepository.TutorialStepData
                {
                    Id = s.Id,
                    DialogueKey = s.DialogueKey,
                    TargetObject = s.TargetObject,
                    Conditions = s.Conditions?.ConvertAll(c => new UnityHelperSDK.Tutorial.TutorialRepository.TutorialConditionData
                    {
                        EventId = c.EventId,
                        ConditionType = c.ConditionType,
                        Parameters = c.Parameters
                    }) ?? new(),
                    CompletionCondition = s.CompletionCondition != null ? new UnityHelperSDK.Tutorial.TutorialRepository.TutorialConditionData
                    {
                        EventId = s.CompletionCondition.EventId,
                        ConditionType = s.CompletionCondition.ConditionType,
                        Parameters = s.CompletionCondition.Parameters
                    } : null
                }) ?? new()
            };
        }

        public static TutorialDefinition FromRuntimeData(UnityHelperSDK.Tutorial.TutorialRepository.TutorialData data)
        {
            return new TutorialDefinition
            {
                Id = data.Id,
                CategoryId = data.CategoryId,
                Title = data.Title,
                Description = data.Description,
                OnlyShowOnce = data.OnlyShowOnce,
                RequiredLevel = data.RequiredLevel,
                Dependencies = data.Dependencies ?? new(),
                StartConditions = data.StartConditions?.ConvertAll(c => new TutorialConditionData
                {
                    EventId = c.EventId,
                    ConditionType = c.ConditionType,
                    Parameters = c.Parameters
                }) ?? new(),
                Steps = data.Steps?.ConvertAll(s => new TutorialStepData
                {
                    Id = s.Id,
                    DialogueKey = s.DialogueKey,
                    TargetObject = s.TargetObject,
                    Conditions = s.Conditions?.ConvertAll(c => new TutorialConditionData
                    {
                        EventId = c.EventId,
                        ConditionType = c.ConditionType,
                        Parameters = c.Parameters
                    }) ?? new(),
                    CompletionCondition = s.CompletionCondition != null ? new TutorialConditionData
                    {
                        EventId = s.CompletionCondition.EventId,
                        ConditionType = s.CompletionCondition.ConditionType,
                        Parameters = s.CompletionCondition.Parameters
                    } : null
                }) ?? new()
            };
        }
    }

    [Serializable]
    public class TutorialStepData
    {
        public string Id { get; set; }
        public string DialogueKey { get; set; }
        public GameObject TargetObject { get; set; }
        public List<TutorialConditionData> Conditions { get; set; } = new();
        public TutorialConditionData CompletionCondition { get; set; }
    }

    [Serializable]
    public class TutorialConditionData
    {
        public string EventId { get; set; }
        public UnityHelperSDK.Tutorial.TutorialConditionType ConditionType { get; set; }
        public string[] Parameters { get; set; }

        public object[] GetParameterValues()
        {
            return Parameters?.Select(p => (object)p).ToArray() ?? Array.Empty<object>();
        }
    }

    public enum TutorialConditionType
    {
        Start,
        Step,
        Custom
    }
}