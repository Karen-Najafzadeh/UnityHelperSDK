using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Robust animation management system with runtime modifications,
/// proper state tracking, and error handling
/// </summary>
public class AnimationManager : MonoBehaviour
{
    #region Singleton Pattern
    private static AnimationManager _instance;
    public static AnimationManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[AnimationManager]");
                _instance = go.AddComponent<AnimationManager>();
                DontDestroyOnLoad(go);
                _instance.Initialize();
            }
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInitialized()
    {
        var _ = Instance;
    }
    #endregion

    #region Data Structures
    private class AnimatorState
    {
        public Animator Animator;
        public string CurrentState;
        public string PreviousState;
        public float StateEnterTime;
        public Dictionary<string, AnimatorControllerParameter> Parameters;
        public Coroutine ActiveCoroutine;

        public Dictionary<string, AnimationEventCallback> EventCallbacks = new Dictionary<string, AnimationEventCallback>();
        public Dictionary<string, RuntimeAnimatorController> CachedControllers = new Dictionary<string, RuntimeAnimatorController>();
        public Queue<AnimationRecordFrame> RecordedFrames = new Queue<AnimationRecordFrame>();
        public bool IsRecording;
        public float RecordStartTime;
    }

    public class AnimationOverride
    {
        public AnimationClip OriginalClip;
        public AnimationClip OverrideClip;
    }

    public class AnimationEventCallback
    {
        public string EventName;
        public Action<AnimationEvent> Callback;
        public bool IsPersistent;
    }

    public class AnimationRecordFrame
    {
        public float TimeStamp;
        public Dictionary<string, float> Parameters;
        public Vector3 Position;
        public Quaternion Rotation;
        public Dictionary<AvatarIKGoal, IKData> IKData;
    }

    public class IKData
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float PositionWeight;
        public float RotationWeight;
    }

    public class AnimationProfile
    {
        public float TransitionSpeed = 1f;
        public bool UseSmoothing = true;
        public float SmoothingWeight = 0.5f;
        public bool EnableRootMotion = true;
        public float MaxVelocity = 5f;
        public AnimationCurve BlendCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    }
    #endregion

    #region Properties and Fields
    private readonly Dictionary<Animator, AnimatorState> _animatorStates = 
        new Dictionary<Animator, AnimatorState>();
    private readonly List<Animator> _pendingCleanup = new List<Animator>();

    [SerializeField] private float _defaultTransitionDuration = 0.25f;
    [SerializeField] private int _statePollingFrequency = 30;

    public event Action<Animator, string> OnStateEntered;
    public event Action<Animator, string> OnStateExited;
    #endregion

    #region Initialization
    private void Awake()
    {
        StartCoroutine(StatePollingRoutine());
        StartCoroutine(CleanupRoutine());
    }

    private IEnumerator StatePollingRoutine()
    {
        var wait = new WaitForSeconds(1f / _statePollingFrequency);
        while (true)
        {
            UpdateAllAnimatorStates();
            yield return wait;
        }
    }

    private IEnumerator CleanupRoutine()
    {
        var wait = new WaitForSeconds(5f);
        while (true)
        {
            CleanupInvalidAnimators();
            yield return wait;
        }
    }
    #endregion

    #region Public API

    /// <summary>
    /// Play animation state with transition
    /// </summary>
    /// <param name="animator">Target animator</param>
    /// <param name="stateName">State name hash or string</param>
    /// <param name="transitionDuration">Transition time in seconds</param>
    /// <param name="layer">Target layer index</param>
    public void PlayState(
        Animator animator,
        object stateName,
        float transitionDuration = -1f,
        int layer = 0)
    {
        if (!ValidateAnimator(animator)) return;

        var duration = transitionDuration < 0 ? 
            _defaultTransitionDuration : transitionDuration;

        var stateInfo = GetAnimatorState(animator);
        var nameHash = ConvertStateName(stateName);

        if (stateInfo.ActiveCoroutine != null)
        {
            StopCoroutine(stateInfo.ActiveCoroutine);
        }

        stateInfo.ActiveCoroutine = StartCoroutine(
            TransitionRoutine(animator, nameHash, duration, layer)
        );
    }

    /// <summary>
    /// Smoothly transition float parameter value
    /// </summary>
    public void LerpParameter(
        Animator animator,
        string parameterName,
        float targetValue,
        float duration,
        Action onComplete = null)
    {
        if (!ValidateAnimator(animator)) return;
        if (!ValidateParameter(animator, parameterName, 
            AnimatorControllerParameterType.Float)) return;

        var state = GetAnimatorState(animator);
        if (state.ActiveCoroutine != null)
        {
            StopCoroutine(state.ActiveCoroutine);
        }

        state.ActiveCoroutine = StartCoroutine(
            ParameterLerpRoutine(
                animator,
                parameterName,
                targetValue,
                duration,
                onComplete
            )
        );
    }

    /// <summary>
    /// Apply runtime animation overrides
    /// </summary>
    public void ApplyOverrides(
        Animator animator,
        List<AnimationOverride> overrides)
    {
        if (!ValidateAnimator(animator)) return;

        var overrideController = new AnimatorOverrideController(
            animator.runtimeAnimatorController
        );

        var clipPairs = overrides.Select(o => new KeyValuePair<AnimationClip, AnimationClip>(
            o.OriginalClip,
            o.OverrideClip
        )).ToList();

        overrideController.ApplyOverrides(clipPairs);
        animator.runtimeAnimatorController = overrideController;
    }
    #endregion

    #region Core Functionality
    private IEnumerator TransitionRoutine(
        Animator animator,
        int targetStateHash,
        float duration,
        int layer)
    {
        var state = GetAnimatorState(animator);
        var currentState = animator.GetCurrentAnimatorStateInfo(layer);

        // Trigger transition
        animator.CrossFade(targetStateHash, duration, layer);

        // Wait for transition completion
        yield return new WaitUntil(() =>
            animator.GetCurrentAnimatorStateInfo(layer).fullPathHash != currentState.fullPathHash
        );

        state.ActiveCoroutine = null;
    }

    private IEnumerator ParameterLerpRoutine(
        Animator animator,
        string parameterName,
        float targetValue,
        float duration,
        Action onComplete)
    {
        float startValue = animator.GetFloat(parameterName);
        float elapsed = 0;

        while (elapsed < duration)
        {
            if (!animator || !animator.gameObject.activeInHierarchy) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            animator.SetFloat(parameterName, Mathf.Lerp(startValue, targetValue, t));
            yield return null;
        }

        animator.SetFloat(parameterName, targetValue);
        onComplete?.Invoke();
    }
    #endregion

    #region State Management
    private void UpdateAllAnimatorStates()
    {
        foreach (var pair in _animatorStates)
        {
            if (pair.Key == null) continue;

            var currentState = pair.Key.GetCurrentAnimatorStateInfo(0);
            var currentStateName = GetStateName(pair.Key, currentState.fullPathHash);

            if (pair.Value.CurrentState != currentStateName)
            {
                OnStateExited?.Invoke(pair.Key, pair.Value.CurrentState);
                pair.Value.PreviousState = pair.Value.CurrentState;
                pair.Value.CurrentState = currentStateName;
                pair.Value.StateEnterTime = Time.time;
                OnStateEntered?.Invoke(pair.Key, currentStateName);
            }
        }
    }

    private void CleanupInvalidAnimators()
    {
        _pendingCleanup.Clear();

        foreach (var animator in _animatorStates.Keys)
        {
            if (animator == null || !animator.gameObject.activeInHierarchy)
            {
                _pendingCleanup.Add(animator);
            }
        }

        foreach (var animator in _pendingCleanup)
        {
            _animatorStates.Remove(animator);
        }
    }
    #endregion

    #region Helper Methods
    private AnimatorState GetAnimatorState(Animator animator)
    {
        if (!_animatorStates.TryGetValue(animator, out var state))
        {
            state = new AnimatorState
            {
                Animator = animator,
                Parameters = animator.parameters.ToDictionary(p => p.name)
            };
            _animatorStates[animator] = state;
        }
        return state;
    }

    private bool ValidateAnimator(Animator animator)
    {
        if (!animator)
        {
            Debug.LogError("Invalid animator reference!");
            return false;
        }

        if (!animator.isActiveAndEnabled)
        {
            Debug.LogWarning($"Animator {animator.name} is disabled!");
            return false;
        }

        return true;
    }

    private bool ValidateParameter(
        Animator animator,
        string parameterName,
        AnimatorControllerParameterType expectedType)
    {
        var state = GetAnimatorState(animator);
        if (!state.Parameters.TryGetValue(parameterName, out var parameter))
        {
            Debug.LogError($"Parameter {parameterName} not found on {animator.name}");
            return false;
        }

        if (parameter.type != expectedType)
        {
            Debug.LogError($"Parameter {parameterName} is type {parameter.type}, " +
                $"expected {expectedType}");
            return false;
        }

        return true;
    }

    private string GetStateName(Animator animator, int hash)
    {
        // Implementation requires custom state name lookup
        // Can be implemented via AnimationClip name database
        return hash.ToString();
    }

    private int ConvertStateName(object stateName)
    {
        return stateName switch
        {
            string s => Animator.StringToHash(s),
            int i => i,
            _ => throw new ArgumentException("Invalid state name type")
        };
    }
    #endregion

        #region New Features
    
    // =======================================================================
    // 1. Advanced Inverse Kinematics Control
    // =======================================================================
    
    /// <summary>
    /// Set IK target with smooth transition
    /// </summary>
    public void SetIKTarget(
        Animator animator,
        AvatarIKGoal ikGoal,
        Transform target,
        float positionWeight = 1f,
        float rotationWeight = 1f,
        float transitionDuration = 0.3f)
    {
        if (!ValidateAnimator(animator)) return;
        
        StartCoroutine(IKTransitionRoutine(
            animator,
            ikGoal,
            target,
            positionWeight,
            rotationWeight,
            transitionDuration
        ));
    }

    private IEnumerator IKTransitionRoutine(
        Animator animator,
        AvatarIKGoal ikGoal,
        Transform target,
        float positionWeight,
        float rotationWeight,
        float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            
            animator.SetIKPositionWeight(ikGoal, Mathf.Lerp(0, positionWeight, t));
            animator.SetIKRotationWeight(ikGoal, Mathf.Lerp(0, rotationWeight, t));
            
            if (target != null)
            {
                animator.SetIKPosition(ikGoal, target.position);
                animator.SetIKRotation(ikGoal, target.rotation);
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // =======================================================================
    // 2. Procedural Animation System
    // =======================================================================
    
    /// <summary>
    /// Create procedural sine wave movement
    /// </summary>
    public void AddProceduralMovement(
        Animator animator,
        string parameterName,
        float amplitude = 1f,
        float frequency = 1f,
        float phaseOffset = 0f)
    {
        if (!ValidateAnimator(animator)) return;
        
        StartCoroutine(ProceduralMovementRoutine(
            animator,
            parameterName,
            amplitude,
            frequency,
            phaseOffset
        ));
    }

    private IEnumerator ProceduralMovementRoutine(
        Animator animator,
        string parameterName,
        float amplitude,
        float frequency,
        float phaseOffset)
    {
        float timer = phaseOffset;
        while (animator != null && animator.gameObject.activeInHierarchy)
        {
            float value = Mathf.Sin(timer * frequency * 2 * Mathf.PI) * amplitude;
            animator.SetFloat(parameterName, value);
            timer += Time.deltaTime;
            yield return null;
        }
    }

    // =======================================================================
    // 3. Root Motion Integration
    // =======================================================================
    
    /// <summary>
    /// Enable root motion with velocity control
    /// </summary>
    public void EnableRootMotion(
        Animator animator,
        bool applyPosition = true,
        bool applyRotation = true,
        float maxVelocity = 5f)
    {
        if (!ValidateAnimator(animator)) return;
        
        animator.applyRootMotion = true;
        StartCoroutine(RootMotionControlRoutine(
            animator,
            applyPosition,
            applyRotation,
            maxVelocity
        ));
    }

    private IEnumerator RootMotionControlRoutine(
        Animator animator,
        bool applyPosition,
        bool applyRotation,
        float maxVelocity)
    {
        Vector3 previousPosition = animator.rootPosition;
        Quaternion previousRotation = animator.rootRotation;

        while (animator != null && animator.applyRootMotion)
        {
            Vector3 deltaPosition = animator.rootPosition - previousPosition;
            Quaternion deltaRotation = animator.rootRotation * Quaternion.Inverse(previousRotation);

            // Apply to character controller
            if (TryGetComponent<CharacterController>(out var controller))
            {
                if (applyPosition)
                {
                    Vector3 motion = Vector3.ClampMagnitude(
                        deltaPosition,
                        maxVelocity * Time.deltaTime
                    );
                    controller.Move(motion);
                }

                if (applyRotation)
                {
                    deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
                    controller.transform.Rotate(axis, angle * Time.deltaTime);
                }
            }

            previousPosition = animator.rootPosition;
            previousRotation = animator.rootRotation;
            yield return null;
        }
    }

    // =======================================================================
    // 4. Animation Blending System
    // =======================================================================
    
    /// <summary>
    /// Blend between two animation states
    /// </summary>
    public void BlendAnimations(
        Animator animator,
        string stateA,
        string stateB,
        float blendValue,
        float blendTime = 0.5f)
    {
        if (!ValidateAnimator(animator)) return;
        
        StartCoroutine(AnimationBlendRoutine(
            animator,
            stateA,
            stateB,
            blendValue,
            blendTime
        ));
    }

    private IEnumerator AnimationBlendRoutine(
        Animator animator,
        string stateA,
        string stateB,
        float targetBlend,
        float duration)
    {
        float currentBlend = animator.GetFloat("Blend");
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float value = Mathf.Lerp(currentBlend, targetBlend, t);
            
            animator.CrossFade(stateA, 0f, 0, 1 - value);
            animator.CrossFade(stateB, 0f, 0, value);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
    
    #endregion
    #region Animation Sequence System

    public class AnimationSequence
    {
        public Animator Animator;
        public string StateName;
        public float TransitionDuration;
        public int Layer;
        public float PostDelay;
        public Action OnComplete;
    }

    private readonly Dictionary<Animator, Coroutine> _activeSequences = 
        new Dictionary<Animator, Coroutine>();

    /// <summary>
    /// Execute animations in sequence with optional delays and callbacks
    /// </summary>
    public void PlaySequence(
        Animator animator,
        List<AnimationSequence> sequence,
        bool interruptCurrent = true)
    {
        if (!ValidateAnimator(animator)) return;

        // Cancel existing sequence if needed
        if (interruptCurrent && _activeSequences.TryGetValue(animator, out var existing))
        {
            StopCoroutine(existing);
        }

        _activeSequences[animator] = StartCoroutine(
            ProcessSequenceRoutine(animator, sequence)
        );
    }

    private IEnumerator ProcessSequenceRoutine(
        Animator animator,
        List<AnimationSequence> sequence)
    {
        foreach (var step in sequence)
        {
            // Validate step animator matches sequence animator
            if (step.Animator != animator)
            {
                Debug.LogError("Sequence contains mismatched animators!");
                yield break;
            }

            // Play the animation state
            PlayState(
                step.Animator,
                step.StateName,
                step.TransitionDuration,
                step.Layer
            );

            // Calculate total wait time
            float stateDuration = GetStateDuration(
                step.Animator,
                step.StateName,
                step.Layer
            );
            
            float totalWait = Mathf.Max(
                step.TransitionDuration + stateDuration,
                step.PostDelay
            );

            // Wait for completion
            yield return new WaitForSeconds(totalWait);

            // Execute callback
            step.OnComplete?.Invoke();
        }

        _activeSequences.Remove(animator);
    }

    private float GetStateDuration(Animator animator, string stateName, int layer)
    {
        var stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
        if (stateInfo.IsName(stateName))
        {
            return stateInfo.length * (1 - stateInfo.normalizedTime % 1);
        }
        return 0;
    }

    /// <summary>
    /// Stop current sequence for an animator
    /// </summary>
    public void StopSequence(Animator animator, bool executeFinalCallback = false)
    {
        if (_activeSequences.TryGetValue(animator, out var coroutine))
        {
            StopCoroutine(coroutine);
            _activeSequences.Remove(animator);

            if (executeFinalCallback)
            {
                // Get last executed callback and invoke
                // (Would need additional tracking for proper implementation)
            }
        }
    }

    #endregion  
    #region Animation Events
    
    /// <summary>
    /// Register a callback for an animation event
    /// </summary>
    public void RegisterEventCallback(
        Animator animator,
        string eventName,
        Action<AnimationEvent> callback,
        bool persistent = false)
    {
        if (!ValidateAnimator(animator)) return;
        
        var state = GetAnimatorState(animator);
        state.EventCallbacks[eventName] = new AnimationEventCallback
        {
            EventName = eventName,
            Callback = callback,
            IsPersistent = persistent
        };
    }

    /// <summary>
    /// Handle animation events from Unity's animation system
    /// </summary>
    private void OnAnimationEvent(AnimationEvent evt)
    {
        foreach (var state in _animatorStates.Values)
        {
            if (state.EventCallbacks.TryGetValue(evt.stringParameter, out var callback))
            {
                callback.Callback?.Invoke(evt);
                
                if (!callback.IsPersistent)
                {
                    state.EventCallbacks.Remove(evt.stringParameter);
                }
            }
        }
    }
    
    #endregion
    
    #region Animation Recording

    /// <summary>
    /// Start recording animation data for playback
    /// </summary>
    public void StartRecording(Animator animator)
    {
        if (!ValidateAnimator(animator)) return;
        
        var state = GetAnimatorState(animator);
        state.IsRecording = true;
        state.RecordStartTime = Time.time;
        state.RecordedFrames.Clear();
    }

    /// <summary>
    /// Stop recording and save animation data
    /// </summary>
    public void StopRecording(Animator animator, string saveKey)
    {
        if (!ValidateAnimator(animator)) return;
        
        var state = GetAnimatorState(animator);
        state.IsRecording = false;
        
        // Save recorded animation using JsonHelper
        var recordData = state.RecordedFrames.ToList();
        JsonHelper.Serialize(recordData);
        
        // Optional: Save to disk or cloud
        SaveRecordedAnimation(saveKey, recordData);
    }

    /// <summary>
    /// Play back a recorded animation
    /// </summary>
    public void PlayRecordedAnimation(
        Animator animator,
        string saveKey,
        bool loop = false)
    {
        if (!ValidateAnimator(animator)) return;
        
        StartCoroutine(PlaybackRoutine(animator, saveKey, loop));
    }

    private IEnumerator PlaybackRoutine(
        Animator animator,
        string saveKey,
        bool loop)
    {
        var recordData = LoadRecordedAnimation(saveKey);
        if (recordData == null || recordData.Count == 0)
        {
            Debug.LogError($"No recorded animation found for key: {saveKey}");
            yield break;
        }

        int frameIndex = 0;
        float startTime = Time.time;

        while (true)
        {
            if (frameIndex >= recordData.Count)
            {
                if (loop)
                {
                    frameIndex = 0;
                    startTime = Time.time;
                }
                else
                {
                    break;
                }
            }

            var frame = recordData[frameIndex];
            
            // Apply frame data
            foreach (var param in frame.Parameters)
            {
                animator.SetFloat(param.Key, param.Value);
            }
            
            animator.transform.position = frame.Position;
            animator.transform.rotation = frame.Rotation;
            
            // Apply IK data
            if (frame.IKData != null)
            {
                foreach (var ikData in frame.IKData)
                {
                    animator.SetIKPosition(ikData.Key, ikData.Value.Position);
                    animator.SetIKRotation(ikData.Key, ikData.Value.Rotation);
                    animator.SetIKPositionWeight(ikData.Key, ikData.Value.PositionWeight);
                    animator.SetIKRotationWeight(ikData.Key, ikData.Value.RotationWeight);
                }
            }

            frameIndex++;
            yield return null;
        }
    }

    private void SaveRecordedAnimation(string key, List<AnimationRecordFrame> data)
    {
        PlayerPrefs.SetString($"RecordedAnim_{key}", JsonHelper.Serialize(data));
    }

    private List<AnimationRecordFrame> LoadRecordedAnimation(string key)
    {
        string json = PlayerPrefs.GetString($"RecordedAnim_{key}");
        return string.IsNullOrEmpty(json) ? null : 
            JsonHelper.Deserialize<List<AnimationRecordFrame>>(json);
    }

    #endregion

    #region Performance Optimization

    /// <summary>
    /// Cache animator controller for quick switching
    /// </summary>
    public void CacheAnimatorController(
        Animator animator,
        string key,
        RuntimeAnimatorController controller)
    {
        if (!ValidateAnimator(animator)) return;
        
        var state = GetAnimatorState(animator);
        state.CachedControllers[key] = controller;
    }

    /// <summary>
    /// Switch to a cached animator controller
    /// </summary>
    public void SwitchToCachedController(
        Animator animator,
        string key)
    {
        if (!ValidateAnimator(animator)) return;
        
        var state = GetAnimatorState(animator);
        if (state.CachedControllers.TryGetValue(key, out var controller))
        {
            animator.runtimeAnimatorController = controller;
        }
    }

    /// <summary>
    /// Enable or disable animator culling for performance
    /// </summary>
    public void SetCullingMode(
        Animator animator,
        bool enableCulling,
        float cullDistance = 50f)
    {
        if (!ValidateAnimator(animator)) return;
        
        animator.cullingMode = enableCulling ? 
            AnimatorCullingMode.BasedOnRenderers : 
            AnimatorCullingMode.AlwaysAnimate;
            
        var renderer = animator.GetComponent<Renderer>();
        if (renderer)
        {
            renderer.allowOcclusionWhenDynamic = enableCulling;
        }
    }

    /// <summary>
    /// Optimize animator for specific use case
    /// </summary>
    public void OptimizeAnimator(
        Animator animator,
        bool disableRootMotion = true,
        bool useFixedUpdate = false,
        bool enableCulling = true)
    {
        if (!ValidateAnimator(animator)) return;
        
        animator.applyRootMotion = !disableRootMotion;
        animator.updateMode = useFixedUpdate ? 
            AnimatorUpdateMode.Fixed : 
            AnimatorUpdateMode.Normal;
        
        SetCullingMode(animator, enableCulling);
        
        // Disable unused components
        var rigidbody = animator.GetComponent<Rigidbody>();
        if (rigidbody && disableRootMotion)
        {
            rigidbody.isKinematic = true;
        }
    }

    /// <summary>
    /// Create a simplified version of an animation for LOD purposes
    /// </summary>
    public AnimationClip CreateSimplifiedAnimation(
        AnimationClip source,
        float keyframeReduction = 0.5f)
    {
        var simplified = new AnimationClip();
        simplified.name = $"{source.name}_Simplified";
        
        var curves = AnimationUtility.GetCurveBindings(source);
        foreach (var binding in curves)
        {
            var curve = AnimationUtility.GetEditorCurve(source, binding);
            var keys = curve.keys;
            
            // Reduce keyframes
            int newKeyCount = Mathf.Max(2, Mathf.FloorToInt(keys.Length * keyframeReduction));
            var newKeys = new Keyframe[newKeyCount];
            
            for (int i = 0; i < newKeyCount; i++)
            {
                float t = i / (float)(newKeyCount - 1);
                int originalIndex = Mathf.FloorToInt(t * (keys.Length - 1));
                newKeys[i] = keys[originalIndex];
            }
            
            var newCurve = new AnimationCurve(newKeys);
            AnimationUtility.SetEditorCurve(simplified, binding, newCurve);
        }
        
        return simplified;
    }
    
    #endregion
}