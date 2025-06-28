using UnityEngine;
using System;
using System.Collections.Generic;
using UnityHelperSDK.Input;

namespace UnityHelperSDK.Input
{
    [Serializable]
    public class GestureDefinition
    {
        public string Name { get; set; }
        public GestureType Type { get; set; }
        public float TimeWindow { get; set; }
        public float DeadZone { get; set; }
        public Vector2 SwipeDirection { get; set; }
        public float AngleThreshold { get; set; }
        public List<InputComboStep> ComboSteps { get; set; }
        public Vector2[] TemplatePoints { get; set; }
        public float MatchThreshold { get; set; }
        public Action OnDetected { get; set; }

        public GestureDefinition()
        {
            TimeWindow = 1f;
            DeadZone = 50f;
            SwipeDirection = Vector2.up;
            AngleThreshold = 45f;
            ComboSteps = new List<InputComboStep>();
            TemplatePoints = new Vector2[0];
            MatchThreshold = 0.3f;
        }
    }
}
