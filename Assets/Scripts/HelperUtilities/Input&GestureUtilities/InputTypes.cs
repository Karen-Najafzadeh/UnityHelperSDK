using UnityEngine;
using System;

namespace UnityHelperSDK.Input
{
    [Flags]
    public enum GestureType
    {
        None = 0,
        Swipe = 1 << 0,
        Combo = 1 << 1,
        Shape = 1 << 2,
        All = Swipe | Combo | Shape
    }

    [Serializable]
    public class InputComboStep
    {
        [SerializeField]
        private KeyCode key;
        [SerializeField]
        private ButtonEventType eventType;

        public KeyCode Key => key;
        public ButtonEventType EventType => eventType;
    }

    public enum ButtonEventType
    {
        Down,
        Up
    }
}
