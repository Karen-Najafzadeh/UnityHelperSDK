using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections.Generic;
using UnityHelperSDK.Events;

namespace UnityHelperSDK.Tutorial
{
    /// <summary>
    /// Represents a single step in a tutorial sequence
    /// </summary>
    public class TutorialStep
    {
        public string Id { get; }
        public string DialogueKey { get; }
        public GameObject Target { get; }
        
        private readonly List<Action<TutorialEvents.TutorialStepConditionEvent>> _conditions = 
            new List<Action<TutorialEvents.TutorialStepConditionEvent>>();
        private Action<TutorialEvents.TutorialStepConditionEvent> _completionCondition;
        private float _startTime;

        public TutorialStep(string id, string dialogueKey = null, GameObject target = null)
        {
            Id = id;
            DialogueKey = dialogueKey;
            Target = target;
            _startTime = Time.time;

            // Trigger step started event
            EventHelper.Trigger(new TutorialEvents.TutorialStepStartedEvent
            {
                StepId = id,
                DialogueKey = dialogueKey,
                TargetObject = target
            });
        }

        public void AddCondition(Action<TutorialEvents.TutorialStepConditionEvent> condition)
        {
            _conditions.Add(condition);
            EventHelper.Subscribe(condition);
        }

        public void SetCompletionCondition(Action<TutorialEvents.TutorialStepConditionEvent> condition)
        {
            if (_completionCondition != null)
            {
                EventHelper.Unsubscribe(_completionCondition);
            }
            _completionCondition = condition;
            if (condition != null)
            {
                EventHelper.Subscribe(condition);
            }
        }

        public bool CheckConditions(string tutorialId)
        {
            var evt = new TutorialEvents.TutorialStepConditionEvent
            {
                TutorialId = tutorialId,
                StepId = Id,
                TargetObject = Target,
                HasMetConditions = false
            };

            foreach (var condition in _conditions)
            {
                condition.Invoke(evt);
                if (!evt.HasMetConditions)
                    return false;
            }

            return true;
        }

        public bool CheckCompletionCondition(string tutorialId)
        {
            if (_completionCondition == null)
                return false;

            var evt = new TutorialEvents.TutorialStepConditionEvent
            {
                TutorialId = tutorialId,
                StepId = Id,
                TargetObject = Target,
                HasMetConditions = false
            };

            _completionCondition.Invoke(evt);
            if (evt.HasMetConditions)
            {
                EventHelper.Trigger(new TutorialEvents.TutorialStepCompletedEvent
                {
                    TutorialId = tutorialId,
                    StepId = Id,
                    TimeSpent = Time.time - _startTime,
                    WasSkipped = false
                });

                // Cleanup events when step is completed
                Cleanup();
            }
            return evt.HasMetConditions;
        }

        public void Cleanup()
        {
            foreach (var condition in _conditions)
            {
                EventHelper.Unsubscribe(condition);
            }
            _conditions.Clear();

            if (_completionCondition != null)
            {
                EventHelper.Unsubscribe(_completionCondition);
                _completionCondition = null;
            }
        }
    }
}
