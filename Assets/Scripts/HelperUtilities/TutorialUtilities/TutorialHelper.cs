using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using TMPro;
using DG.Tweening;
using UnityHelperSDK.Events;

namespace UnityHelperSDK.Tutorial
{
    /// <summary>
    /// A comprehensive tutorial system that handles complex, multi-step tutorials
    /// with branching paths, conditions, and dynamic content.
    /// </summary>
    public static class TutorialHelper
    {
        // Tutorial state tracking
        private static readonly Dictionary<string, TutorialSequence> _tutorials = new Dictionary<string, TutorialSequence>();
        private static TutorialSequence _activeTutorial;
        private static TutorialStep _currentStep;
        private static int _currentStepIndex;
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

        #region Initialization
        
        /// <summary>
        /// Initialize the tutorial system
        /// </summary>
        public static void Initialize()
        {
            _tutorials.Clear();
            _activeTutorial = null;
            _currentStep = null;
            _isTutorialActive = false;
            
            // Subscribe to events
            EventHelper.Subscribe<TutorialEvents.TutorialStartedEvent>(OnTutorialStarted);
            EventHelper.Subscribe<TutorialEvents.TutorialCompletedEvent>(OnTutorialCompleted);
            EventHelper.Subscribe<TutorialEvents.TutorialStepStartedEvent>(OnTutorialStepStarted);
            EventHelper.Subscribe<TutorialEvents.TutorialStepCompletedEvent>(OnTutorialStepCompleted);
        }

        #endregion

        #region Tutorial Management

        /// <summary>
        /// Register a new tutorial sequence
        /// </summary>
        public static void RegisterTutorial(TutorialSequence tutorial)
        {
            if (tutorial == null) return;
            
            if (!_tutorials.ContainsKey(tutorial.Id))
            {
                _tutorials[tutorial.Id] = tutorial;
            }
        }

        /// <summary>
        /// Start a tutorial sequence
        /// </summary>
        public static async Task StartTutorial(string tutorialId)
        {
            if (!_tutorials.TryGetValue(tutorialId, out var tutorial))
            {
                Debug.LogWarning($"Tutorial '{tutorialId}' not found!");
                return;
            }

            if (_isTutorialActive)
            {
                Debug.LogWarning("Cannot start tutorial while another is active!");
                return;
            }

            // Check start conditions
            if (!tutorial.CheckStartConditions())
            {
                Debug.LogWarning($"Tutorial '{tutorialId}' conditions not met!");
                return;
            }

            _activeTutorial = tutorial;
            _isTutorialActive = true;
            _currentStepIndex = 0;

            // Start the tutorial
            tutorial.Start();
            
            // Start first step
            await ProcessNextStep();
        }

        /// <summary>
        /// Process the next step in the tutorial sequence
        /// </summary>
        private static async Task ProcessNextStep()
        {
            if (!_isTutorialActive || _activeTutorial == null) return;

            // Check if we've completed all steps
            if (_currentStepIndex >= _activeTutorial.Steps.Count)
            {
                CompleteTutorial();
                return;
            }

            // Get the next step
            _currentStep = _activeTutorial.Steps[_currentStepIndex];

            // Wait for step conditions to be met
            while (!_currentStep.CheckConditions(_activeTutorial.Id))
            {
                await Task.Delay(100);
            }

            // Wait for step completion
            while (!_currentStep.CheckCompletionCondition(_activeTutorial.Id))
            {
                await Task.Delay(100);
            }

            // Move to next step
            _currentStepIndex++;
            await ProcessNextStep();
        }

        private static void CompleteTutorial()
        {
            if (_activeTutorial != null)
            {
                _activeTutorial.Complete();
                _activeTutorial = null;
                _currentStep = null;
                _isTutorialActive = false;
            }
        }

        #endregion

        #region Event Handlers

        private static void OnTutorialStarted(TutorialEvents.TutorialStartedEvent evt)
        {
            Debug.Log($"Tutorial started: {evt.TutorialId}");
        }

        private static void OnTutorialCompleted(TutorialEvents.TutorialCompletedEvent evt)
        {
            Debug.Log($"Tutorial completed: {evt.TutorialId}, Time: {evt.TimeSpent:F1}s");
        }

        private static void OnTutorialStepStarted(TutorialEvents.TutorialStepStartedEvent evt)
        {
            Debug.Log($"Tutorial step started: {evt.TutorialId} - Step {evt.StepId}");
        }

        private static void OnTutorialStepCompleted(TutorialEvents.TutorialStepCompletedEvent evt)
        {
            Debug.Log($"Tutorial step completed: {evt.TutorialId} - Step {evt.StepId}, Time: {evt.TimeSpent:F1}s");
        }

        #endregion
    }
}
