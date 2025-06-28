using UnityEngine;

namespace UnityHelperSDK.Tutorial
{
    /// <summary>
    /// Contains all tutorial-related event definitions.
    /// These events are used for communication between tutorial components.
    /// </summary>
    public static class TutorialEvents
    {
        /// <summary>
        /// Event triggered when checking tutorial start conditions
        /// </summary>
        public struct TutorialStartConditionEvent
        {
            public string TutorialId;
            public int PlayerLevel;
            public bool HasMetConditions;
        }

        /// <summary>
        /// Event triggered when checking tutorial step conditions
        /// </summary>
        public struct TutorialStepConditionEvent
        {
            public string TutorialId;
            public string StepId;
            public GameObject TargetObject;
            public bool HasMetConditions;
        }

        /// <summary>
        /// Event triggered when a tutorial starts
        /// </summary>
        public struct TutorialStartedEvent
        {
            public string TutorialId;
            public string CategoryId;
            public int RequiredLevel;
        }

        /// <summary>
        /// Event triggered when a tutorial completes
        /// </summary>
        public struct TutorialCompletedEvent
        {
            public string TutorialId;
            public string CategoryId;
            public float TimeSpent;
            public bool WasSkipped;
        }

        /// <summary>
        /// Event triggered when a tutorial step starts
        /// </summary>
        public struct TutorialStepStartedEvent
        {
            public string TutorialId;
            public string StepId;
            public string DialogueKey;
            public GameObject TargetObject;
        }

        /// <summary>
        /// Event triggered when a tutorial step completes
        /// </summary>
        public struct TutorialStepCompletedEvent
        {
            public string TutorialId;
            public string StepId;
            public float TimeSpent;
            public bool WasSkipped;
        }

        /// <summary>
        /// Event triggered when custom tutorial conditions need to be evaluated
        /// </summary>
        public struct CustomTutorialConditionEvent
        {
            public string ConditionId;
            public object[] Parameters;
            public bool HasMetCondition;
        }

        /// <summary>
        /// Event triggered to track tutorial analytics
        /// </summary>
        public struct TutorialAnalyticsEvent
        {
            public string TutorialId;
            public string EventType;
            public string StepId;
            public float Duration;
            public bool Success;
            public string AdditionalData;
        }
    }
}
