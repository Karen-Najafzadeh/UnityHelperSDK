using UnityEngine;

    public enum CompositeTriggerLogic
    {
        And,
        Or,
        Not
    }

    [CreateAssetMenu(menuName = "Unity Helper SDK/Tutorial System/Composite Trigger Definition")]
    public class CompositeTriggerDefinitionSO : TriggerDefinitionSO
    {
        public CompositeTriggerLogic Logic;
        public TriggerDefinitionSO[] SubTriggers;
    }

