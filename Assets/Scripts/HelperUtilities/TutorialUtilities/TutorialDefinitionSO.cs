using UnityEngine;
using System;
using UnityHelperSDK.TutorialUtilities;


// Each Tutorial SO holds steps and initial trigger
[CreateAssetMenu(menuName = "Unity Helper SDK/Tutorial System/Tutorial Definition")]
public class TutorialDefinitionSO : ScriptableObject
{
    public string TutorialID;
    public TriggerDefinitionSO InitialTrigger;
    public StepDefinitionSO[] Steps;
    public UnityHelperSDK.TutorialUtilities.ConditionSO[] StartConditions;      // New: conditions to start this tutorial
    public UnityHelperSDK.TutorialUtilities.ConditionSO[] CompleteConditions;   // New: conditions to complete this tutorial
}

// Each Step SO holds its content and completion triggers
[CreateAssetMenu(menuName="Unity Helper SDK/Tutorial System/Step Definition")]
public class StepDefinitionSO : ScriptableObject
{
    public string StepID;
    [TextArea] public string Title;
    [TextArea] public string Body;
    public Sprite Icon;
    public TriggerDefinitionSO[] CompletionTriggers;
    public UnityHelperSDK.TutorialUtilities.ConditionSO[] StartConditions;      // New: conditions to start this step
    public UnityHelperSDK.TutorialUtilities.ConditionSO[] CompleteConditions;   // New: conditions to complete this step
}

// A generic wrapper for different trigger types
[CreateAssetMenu(menuName = "Unity Helper SDK/Tutorial System/Trigger Definition")]
public class TriggerDefinitionSO : ScriptableObject
{
    public TriggerType Type;      // e.g. ButtonPressed, HealthAbove, SceneLoaded…
    public TriggerParameter[] Parameters;   // Strongly-typed parameters
}


// Defines every kind of trigger your tutorial system supports
public enum TriggerType
{
    // Built‑in lifecycle
    OnGameInitialized,    // fires when you call EventHelper.Trigger<OnGameInitialized>()
    SceneLoaded,          // fires on scene load (parameter: sceneName)
    
    // Player actions
    PlayerMoved,          // fires when player moves (parameter: minDistance)
    PlayerJumped,         // fires when jump input detected
    PlayerHealthChanged,  // fires when health changes (parameter: comparison e.g. “<50”)
    
    // UI interactions
    ButtonPressed,        // fires when a named UI button is clicked (parameter: buttonName)
    UIElementVisible,     // fires when a UI element becomes visible (parameter: elementId)
    
    // Custom / catch‑all
    CustomEvent           // fires on any user‑defined struct event (parameter: eventTypeName)
}

namespace UnityHelperSDK.TutorialUtilities
{
    [System.Serializable]
    public class TriggerParameter
    {
        public string Key;
        public string StringValue;
        public float FloatValue;
        public int IntValue;
        public bool BoolValue;
        public UnityEngine.Object ObjectValue;
    }

    [System.Serializable]
    public class ConditionSO : ScriptableObject
    {
        public string ConditionID;
        public ConditionType Type;
        public ConditionParameter[] Parameters;   // Strongly-typed parameters
    }

    public enum ConditionType
    {
        // Define your condition types here
        PlayerHasItem,
        EnemyDefeated,
        TimeElapsed,
        // Add more as needed
    }

    [System.Serializable]
    public class ConditionParameter
    {
        public string Key;
        public string StringValue;
        public float FloatValue;
        public int IntValue;
        public bool BoolValue;
        public UnityEngine.Object ObjectValue;
    }
}

