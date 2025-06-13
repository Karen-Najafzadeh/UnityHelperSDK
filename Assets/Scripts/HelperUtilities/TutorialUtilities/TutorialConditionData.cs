using System;
using UnityEngine;

namespace UnityHelperSDK
{
    /// <summary>
    /// Data structure for tutorial conditions that can be serialized
    /// </summary>
    [Serializable]
    public class TutorialConditionData
    {
        [SerializeField]
        public string EventId;
        
        [SerializeField]
        public TutorialConditionType ConditionType;

        [SerializeField]
        public string[] Parameters;
    }
}
