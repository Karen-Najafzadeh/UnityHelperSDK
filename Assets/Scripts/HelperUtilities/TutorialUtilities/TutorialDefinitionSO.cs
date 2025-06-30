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
}

