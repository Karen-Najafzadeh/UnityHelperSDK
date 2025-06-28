using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityHelperSDK.Events;

namespace UnityHelperSDK.Tutorial
{
    /// <summary>
    /// Represents a sequence of tutorial steps with their conditions
    /// </summary>
    public class TutorialSequence
    {
        public string Id { get; }
        public string CategoryId { get; }
        public bool OnlyShowOnce { get; }
        public int RequiredLevel { get; }

        private readonly List<TutorialStep> _steps = new List<TutorialStep>();
        public IReadOnlyList<TutorialStep> Steps => _steps;
        
        private readonly List<Action<TutorialEvents.TutorialStartConditionEvent>> _startConditions = 
            new List<Action<TutorialEvents.TutorialStartConditionEvent>>();
        
        private bool _isActive;
        private float _startTime;

        public TutorialSequence(string id, bool onlyShowOnce, int requiredLevel, string categoryId)
        {
            Id = id;
            OnlyShowOnce = onlyShowOnce;
            RequiredLevel = requiredLevel;
            CategoryId = categoryId;
        }

        public void AddStartCondition(Action<TutorialEvents.TutorialStartConditionEvent> condition)
        {
            _startConditions.Add(condition);
            EventHelper.Subscribe(condition);
        }

        public void AddStep(TutorialStep step)
        {
            _steps.Add(step);
        }

        public bool CheckStartConditions()
        {
            var evt = new TutorialEvents.TutorialStartConditionEvent
            {
                TutorialId = Id,
                PlayerLevel = TutorialRepository.Instance.GetPlayerLevel(),
                HasMetConditions = false
            };

            foreach (var condition in _startConditions)
            {
                condition.Invoke(evt);
                if (!evt.HasMetConditions)
                    return false;
            }

            return true;
        }

        public void Start()
        {
            _isActive = true;
            _startTime = Time.time;

            // Log analytics
            EventHelper.Trigger(new TutorialEvents.TutorialAnalyticsEvent
            {
                TutorialId = Id,
                EventType = "started",
                Success = true
            });

            // Trigger started event
            EventHelper.Trigger(new TutorialEvents.TutorialStartedEvent
            {
                TutorialId = Id,
                CategoryId = CategoryId,
                RequiredLevel = RequiredLevel
            });
        }

        public void Complete()
        {
            _isActive = false;

            // Log analytics
            EventHelper.Trigger(new TutorialEvents.TutorialAnalyticsEvent
            {
                TutorialId = Id,
                EventType = "completed",
                Duration = Time.time - _startTime,
                Success = true
            });

            // Trigger completed event
            EventHelper.Trigger(new TutorialEvents.TutorialCompletedEvent
            {
                TutorialId = Id,
                CategoryId = CategoryId,
                TimeSpent = Time.time - _startTime,
                WasSkipped = false
            });

            TutorialRepository.Instance.CompleteTutorial(Id);
            Cleanup();
        }

        private void Cleanup()
        {
            foreach (var condition in _startConditions)
            {
                EventHelper.Unsubscribe(condition);
            }
            _startConditions.Clear();

            foreach (var step in _steps)
            {
                step.Cleanup();
            }
        }
    }
}
