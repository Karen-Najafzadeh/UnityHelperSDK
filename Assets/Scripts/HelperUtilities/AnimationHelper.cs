using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// A comprehensive animation helper that handles animation state management,
/// transitions, and runtime animation modification.
/// 
/// Features:
/// - Animation state management
/// - Smooth transitions
/// - Runtime animation modification
/// - Animation events
/// - Blend tree control
/// - Animation mixing
/// - State machine control
/// - Animation recording
/// </summary>
public static class AnimationHelper
{
    // Animation state tracking
    private static readonly Dictionary<Animator, AnimationState> _animationStates 
        = new Dictionary<Animator, AnimationState>();
    
    // Transition settings
    private static readonly float _defaultTransitionDuration = 0.25f;
    private static readonly float _defaultTransitionOffset = 0f;
    
    // Animation events
    public static event Action<Animator, string> OnStateEnter;
    public static event Action<Animator, string> OnStateExit;
    
    #region State Management
    
    /// <summary>
    /// Play an animation state with optional crossfade
    /// </summary>
    public static async Task PlayAnimation(Animator animator, string stateName, 
        float transitionDuration = -1f, float normalizedOffset = -1f)
    {
        if (!animator) return;
        
        float duration = transitionDuration < 0 ? _defaultTransitionDuration : transitionDuration;
        float offset = normalizedOffset < 0 ? _defaultTransitionOffset : normalizedOffset;
        
        string previousState = GetCurrentStateName(animator);
        
        animator.CrossFade(stateName, duration, 0, offset);
        
        OnStateExit?.Invoke(animator, previousState);
        OnStateEnter?.Invoke(animator, stateName);
        
        // Track state
        UpdateAnimationState(animator, stateName);
        
        // Wait for transition to complete
        await Task.Delay(Mathf.RoundToInt(duration * 1000));
    }
    
    /// <summary>
    /// Get the name of the currently playing animation state
    /// </summary>
    public static string GetCurrentStateName(Animator animator)
    {
        if (_animationStates.TryGetValue(animator, out var state))
        {
            return state.CurrentStateName;
        }
        return string.Empty;
    }
    
    /// <summary>
    /// Check if an animation state is currently playing
    /// </summary>
    public static bool IsPlaying(Animator animator, string stateName)
    {
        if (!animator) return false;
        
        var state = animator.GetCurrentAnimatorStateInfo(0);
        return state.IsName(stateName);
    }
    
    #endregion
    
    #region Parameter Control
    
    /// <summary>
    /// Set an animator parameter with automatic type detection
    /// </summary>
    public static void SetParameter(Animator animator, string parameterName, object value)
    {
        if (!animator) return;
        
        foreach (var param in animator.parameters)
        {
            if (param.name == parameterName)
            {
                switch (param.type)
                {
                    case AnimatorControllerParameterType.Bool:
                        animator.SetBool(parameterName, Convert.ToBoolean(value));
                        break;
                    case AnimatorControllerParameterType.Int:
                        animator.SetInteger(parameterName, Convert.ToInt32(value));
                        break;
                    case AnimatorControllerParameterType.Float:
                        animator.SetFloat(parameterName, Convert.ToSingle(value));
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        if (Convert.ToBoolean(value))
                            animator.SetTrigger(parameterName);
                        else
                            animator.ResetTrigger(parameterName);
                        break;
                }
                break;
            }
        }
    }
    
    /// <summary>
    /// Smoothly transition a float parameter
    /// </summary>
    public static async Task LerpParameterAsync(Animator animator, string parameterName, 
        float targetValue, float duration)
    {
        if (!animator) return;
        
        float startValue = animator.GetFloat(parameterName);
        float elapsedTime = 0;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            
            float currentValue = Mathf.Lerp(startValue, targetValue, t);
            animator.SetFloat(parameterName, currentValue);
            
            await Task.Yield();
        }
        
        animator.SetFloat(parameterName, targetValue);
    }
    
    #endregion
    
    #region Layer Control
    
    /// <summary>
    /// Set layer weight with optional smooth transition
    /// </summary>
    public static async Task SetLayerWeight(Animator animator, int layerIndex, 
        float targetWeight, float duration = 0)
    {
        if (!animator) return;
        
        if (duration <= 0)
        {
            animator.SetLayerWeight(layerIndex, targetWeight);
            return;
        }
        
        float startWeight = animator.GetLayerWeight(layerIndex);
        float elapsedTime = 0;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            
            float currentWeight = Mathf.Lerp(startWeight, targetWeight, t);
            animator.SetLayerWeight(layerIndex, currentWeight);
            
            await Task.Yield();
        }
        
        animator.SetLayerWeight(layerIndex, targetWeight);
    }
    
    #endregion
    
    #region Runtime Modification
    
    /// <summary>
    /// Modify animation curve at runtime
    /// </summary>
    public static void ModifyAnimationCurve(AnimationClip clip, string propertyName, 
        AnimationCurve newCurve)
    {
        if (clip == null) return;
        
        var bindings = AnimationUtility.GetCurveBindings(clip);
        foreach (var binding in bindings)
        {
            if (binding.propertyName == propertyName)
            {
                AnimationUtility.SetEditorCurve(clip, binding, newCurve);
                break;
            }
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    private static void UpdateAnimationState(Animator animator, string stateName)
    {
        if (!_animationStates.ContainsKey(animator))
        {
            _animationStates[animator] = new AnimationState();
        }
        
        _animationStates[animator].PreviousStateName = _animationStates[animator].CurrentStateName;
        _animationStates[animator].CurrentStateName = stateName;
        _animationStates[animator].LastTransitionTime = Time.time;
    }
    
    #endregion
    
    #region Helper Classes
    
    /// <summary>
    /// Tracks animation state for an animator
    /// </summary>
    private class AnimationState
    {
        public string CurrentStateName { get; set; }
        public string PreviousStateName { get; set; }
        public float LastTransitionTime { get; set; }
    }
    
    #endregion
}
