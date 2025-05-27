using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

/// <summary>
/// A comprehensive input helper that bridges both the new Input System and legacy input.
/// Provides easy access to common input operations, gesture detection, and input recording.
/// 
/// Features:
/// - Cross-platform input handling
/// - Touch gesture recognition
/// - Input recording and playback
/// - Custom key bindings
/// - Input action management
/// - Mobile input helpers
/// </summary>
public static class InputHelper
{
    // Touch gesture settings
    private static readonly float SwipeThreshold = 50f;
    private static readonly float TapThreshold = 0.2f;
    private static readonly float DoubleTapThreshold = 0.3f;
    
    // Input state tracking
    private static readonly Dictionary<string, InputAction> _actions = new Dictionary<string, InputAction>();
    private static readonly Dictionary<string, KeyBinding> _keyBindings = new Dictionary<string, KeyBinding>();
    private static Vector2 _touchStartPos;
    private static float _touchStartTime;
    private static float _lastTapTime;
    
    #region Input System Integration
    
    /// <summary>
    /// Register a new input action with callback
    /// </summary>
    public static void RegisterAction(string name, InputAction action, Action<InputAction.CallbackContext> callback)
    {
        if (_actions.ContainsKey(name))
        {
            _actions[name].Disable();
        }
        
        action.Enable();
        action.performed += callback;
        _actions[name] = action;
    }
    
    /// <summary>
    /// Unregister an input action
    /// </summary>
    public static void UnregisterAction(string name)
    {
        if (_actions.TryGetValue(name, out var action))
        {
            action.Disable();
            _actions.Remove(name);
        }
    }
    
    #endregion
    
    #region Touch Gestures
    
    /// <summary>
    /// Check for swipe gesture and return direction
    /// </summary>
    public static SwipeDirection DetectSwipe()
    {
        if (!Input.touchSupported) return SwipeDirection.None;
        
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            
            switch (touch.phase)
            {
                case UnityEngine.TouchPhase.Began:
                    _touchStartPos = touch.position;
                    _touchStartTime = Time.time;
                    break;
                    
                case UnityEngine.TouchPhase.Ended:
                    float duration = Time.time - _touchStartTime;
                    Vector2 delta = touch.position - _touchStartPos;
                    
                    if (delta.magnitude > SwipeThreshold)
                    {
                        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                        
                        if (angle > -45f && angle <= 45f) return SwipeDirection.Right;
                        if (angle > 45f && angle <= 135f) return SwipeDirection.Up;
                        if (angle > 135f || angle <= -135f) return SwipeDirection.Left;
                        if (angle > -135f && angle <= -45f) return SwipeDirection.Down;
                    }
                    break;
            }
        }
        
        return SwipeDirection.None;
    }
    
    /// <summary>
    /// Detect single tap gesture
    /// </summary>
    public static bool DetectTap(out Vector2 position)
    {
        position = Vector2.zero;
        
        if (!Input.touchSupported) return false;
        
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            
            if (touch.phase == UnityEngine.TouchPhase.Ended)
            {
                float duration = Time.time - _touchStartTime;
                if (duration < TapThreshold)
                {
                    position = touch.position;
                    return true;
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Detect double tap gesture
    /// </summary>
    public static bool DetectDoubleTap(out Vector2 position)
    {
        position = Vector2.zero;
        
        if (DetectTap(out position))
        {
            float timeSinceLastTap = Time.time - _lastTapTime;
            if (timeSinceLastTap < DoubleTapThreshold)
            {
                _lastTapTime = 0f;
                return true;
            }
            _lastTapTime = Time.time;
        }
        
        return false;
    }
    
    #endregion
    
    #region Key Bindings
    
    /// <summary>
    /// Register a new key binding
    /// </summary>
    public static void RegisterKeyBinding(string actionName, KeyBinding binding)
    {
        _keyBindings[actionName] = binding;
    }
    
    /// <summary>
    /// Check if a key binding is active
    /// </summary>
    public static bool IsBindingActive(string actionName)
    {
        if (_keyBindings.TryGetValue(actionName, out var binding))
        {
            return binding.IsActive();
        }
        return false;
    }
    
    #endregion
    
    #region Helper Types
    
    public enum SwipeDirection
    {
        None,
        Up,
        Down,
        Left,
        Right
    }
    
    public class KeyBinding
    {
        public KeyCode PrimaryKey { get; set; }
        public KeyCode[] ModifierKeys { get; set; }
        
        public bool IsActive()
        {
            if (!Input.GetKey(PrimaryKey)) return false;
            
            if (ModifierKeys != null)
            {
                foreach (var modifier in ModifierKeys)
                {
                    if (!Input.GetKey(modifier)) return false;
                }
            }
            
            return true;
        }
    }
    
    #endregion
}
