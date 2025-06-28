using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityHelperSDK.Tutorial
{
    /// <summary>
    /// ScriptableObject for storing tutorial data
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialDefinition", menuName = "Tutorials/Tutorial Definition", order = 1)]
    public class TutorialDefinition : ScriptableObject
    {
        public string Id;
        public string CategoryId;
        public string Title;
        [TextArea]
        public string Description;
        public bool OnlyShowOnce = true;
        public int RequiredLevel;
        public List<string> Dependencies = new();
        public List<TutorialConditionData> StartConditions = new();
        public List<TutorialStepData> Steps = new();

        public static TutorialDefinition[] LoadAllDefinitions()
        {
            return Resources.LoadAll<TutorialDefinition>("Tutorials");
        }
    }

    [Serializable]
    public class TutorialStepData
    {
        public string Id;
        public string DialogueKey;
        public GameObject TargetObject;
        public List<TutorialConditionData> Conditions = new();
        public TutorialConditionData CompletionCondition;
    }
}
