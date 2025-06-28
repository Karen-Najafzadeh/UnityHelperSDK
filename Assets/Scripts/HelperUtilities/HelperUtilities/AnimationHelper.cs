using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;


namespace UnityHelperSDK.HelperUtilities
{
    /// <summary>
    /// Animation helper utilities for managing animations, events, and coroutines.
    /// </summary>

    /// <summary>
    /// Helper component for managing animation events and coroutines
    /// </summary>
    public class AnimationEventHelper : MonoBehaviour
    {
        // Delegate for animation events
        public delegate void AnimationEventCallback(string eventName);
        public event AnimationEventCallback OnAnimationEvent;

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        // Called by animation events
        public void TriggerAnimationEvent(string eventName)
        {
            OnAnimationEvent?.Invoke(eventName);
        }
    }

    /// <summary>
    /// Robust animation management system with runtime modifications,
    /// proper state tracking, and error handling.
    /// Features:
    /// - Safe animation playback with error checking
    /// - Animation speed control and state management
    /// - Animation event handling and callbacks
    /// - Runtime animation modifications
    /// - Cross-fade and transition support
    /// - Animation state queries and validation
    /// - Parameter management
    /// - Animation clip utilities
    /// </summary>
    public static class AnimationHelper
    {
        private static readonly Dictionary<Animator, Dictionary<string, float>> _storedSpeeds = 
            new Dictionary<Animator, Dictionary<string, float>>();
        
        private static readonly Dictionary<Animator, AnimationEventHelper> _eventHelpers = 
            new Dictionary<Animator, AnimationEventHelper>();

        #region Animation Control

        /// <summary>
        /// Safely plays an animation with error checking and optional speed control
        /// </summary>
        /// <param name="animator">Target animator component</param>
        /// <param name="stateName">Name of the animation state to play</param>
        /// <param name="layer">Target animation layer (default: 0)</param>
        /// <param name="normalizedTime">Start time of the animation (default: 0)</param>
        /// <param name="speed">Playback speed (default: 1)</param>
        /// <returns>True if animation started successfully</returns>
        public static bool PlayAnimation(Animator animator, string stateName, int layer = 0, float normalizedTime = 0f, float speed = 1f)
        {
            if (animator == null || string.IsNullOrEmpty(stateName))
                return false;

            try
            {
                animator.speed = speed;
                animator.Play(stateName, layer, normalizedTime);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to play animation {stateName}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cross-fades between animation states with smooth transitions
        /// </summary>
        /// <param name="animator">Target animator</param>
        /// <param name="stateName">Target animation state</param>
        /// <param name="duration">Transition duration in seconds</param>
        /// <param name="layer">Target layer (default: -1 for all layers)</param>
        public static void CrossFadeAnimation(Animator animator, string stateName, float duration, int layer = -1)
        {
            if (animator != null && !string.IsNullOrEmpty(stateName))
            {
                animator.CrossFade(stateName, duration, layer);
            }
        }

        /// <summary>
        /// Blends between two animation layers
        /// </summary>
        /// <param name="animator">Target animator</param>
        /// <param name="layer">Layer to blend</param>
        /// <param name="weight">Blend weight (0-1)</param>
        /// <param name="duration">Blend duration</param>
        public static IEnumerator BlendLayer(Animator animator, int layer, float weight, float duration)
        {
            if (animator == null) yield break;

            float startWeight = animator.GetLayerWeight(layer);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                animator.SetLayerWeight(layer, Mathf.Lerp(startWeight, weight, t));
                yield return null;
            }

            animator.SetLayerWeight(layer, weight);
        }

        #endregion

        #region Runtime Modifications

        /// <summary>
        /// Modifies animation speed with state preservation
        /// </summary>
        /// <param name="animator">Target animator</param>
        /// <param name="stateName">Animation state name</param>
        /// <param name="speedMultiplier">Speed multiplier</param>
        public static void SetAnimationSpeed(Animator animator, string stateName, float speedMultiplier)
        {
            if (animator == null) return;

            if (!_storedSpeeds.ContainsKey(animator))
            {
                _storedSpeeds[animator] = new Dictionary<string, float>();
            }

            _storedSpeeds[animator][stateName] = speedMultiplier;
            
            if (IsPlaying(animator, stateName))
            {
                animator.speed = speedMultiplier;
            }
        }

        /// <summary>
        /// Restores original animation speed
        /// </summary>
        /// <param name="animator">Target animator</param>
        /// <param name="stateName">Animation state name</param>
        public static void RestoreAnimationSpeed(Animator animator, string stateName)
        {
            if (animator == null || !_storedSpeeds.ContainsKey(animator)) return;

            if (_storedSpeeds[animator].ContainsKey(stateName))
            {
                animator.speed = 1f;
                _storedSpeeds[animator].Remove(stateName);
            }
        }

        #endregion

        #region State Management

        /// <summary>
        /// Checks if a specific animation is currently playing
        /// </summary>
        /// <param name="animator">Target animator</param>
        /// <param name="stateName">Animation state name</param>
        /// <param name="layer">Animation layer (default: 0)</param>
        /// <returns>True if the animation is playing</returns>
        public static bool IsPlaying(Animator animator, string stateName, int layer = 0)
        {
            if (animator == null) return false;

            var stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            return stateInfo.IsName(stateName);
        }

        /// <summary>
        /// Gets the normalized time of the current animation
        /// </summary>
        /// <param name="animator">Target animator</param>
        /// <param name="layer">Animation layer</param>
        /// <returns>Normalized time (0-1) of current animation</returns>
        public static float GetNormalizedTime(Animator animator, int layer = 0)
        {
            if (animator == null) return 0f;

            var stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            return stateInfo.normalizedTime;
        }

        /// <summary>
        /// Waits for the current animation to complete
        /// </summary>
        /// <param name="animator">Target animator</param>
        /// <param name="stateName">Animation state name</param>
        /// <param name="layer">Animation layer</param>
        public static IEnumerator WaitForAnimationComplete(Animator animator, string stateName, int layer = 0)
        {
            if (animator == null) yield break;

            while (!IsPlaying(animator, stateName, layer))
                yield return null;

            var stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            yield return new WaitForSeconds(stateInfo.length);
        }

        #endregion

        #region Event Handling

        /// <summary>
        /// Registers a callback for animation events
        /// </summary>
        /// <param name="animator">Target animator</param>
        /// <param name="callback">Event callback</param>
        public static void RegisterAnimationEvent(Animator animator, AnimationEventHelper.AnimationEventCallback callback)
        {
            if (animator == null) return;

            if (!_eventHelpers.ContainsKey(animator))
            {
                var helper = animator.gameObject.AddComponent<AnimationEventHelper>();
                _eventHelpers[animator] = helper;
            }

            _eventHelpers[animator].OnAnimationEvent += callback;
        }

        /// <summary>
        /// Unregisters an animation event callback
        /// </summary>
        /// <param name="animator">Target animator</param>
        /// <param name="callback">Event callback to remove</param>
        public static void UnregisterAnimationEvent(Animator animator, AnimationEventHelper.AnimationEventCallback callback)
        {
            if (animator == null || !_eventHelpers.ContainsKey(animator)) return;

            _eventHelpers[animator].OnAnimationEvent -= callback;
        }

        #endregion

        #region Parameter Management

        /// <summary>
        /// Safely sets an animator parameter with type checking
        /// </summary>
        /// <param name="animator">Target animator</param>
        /// <param name="paramName">Parameter name</param>
        /// <param name="value">Parameter value</param>
        public static void SetParameter(Animator animator, string paramName, object value)
        {
            if (animator == null || string.IsNullOrEmpty(paramName)) return;

            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == paramName)
                {
                    switch (param.type)
                    {
                        case AnimatorControllerParameterType.Bool when value is bool boolValue:
                            animator.SetBool(paramName, boolValue);
                            break;
                        case AnimatorControllerParameterType.Int when value is int intValue:
                            animator.SetInteger(paramName, intValue);
                            break;
                        case AnimatorControllerParameterType.Float when value is float floatValue:
                            animator.SetFloat(paramName, floatValue);
                            break;
                        case AnimatorControllerParameterType.Trigger when value is bool triggerValue:
                            if (triggerValue)
                                animator.SetTrigger(paramName);
                            else
                                animator.ResetTrigger(paramName);
                            break;
                    }
                    break;
                }
            }
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Cleans up resources associated with an animator
        /// </summary>
        /// <param name="animator">Target animator</param>
        public static void Cleanup(Animator animator)
        {
            if (animator == null) return;

            if (_storedSpeeds.ContainsKey(animator))
                _storedSpeeds.Remove(animator);

            if (_eventHelpers.ContainsKey(animator))
            {
                if (_eventHelpers[animator] != null)
                    UnityEngine.Object.Destroy(_eventHelpers[animator]);
                _eventHelpers.Remove(animator);
            }
        }

        #endregion
    }

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
        // public void StopRecording(Animator animator, string saveKey)
        // {
        //     if (!ValidateAnimator(animator)) return;
            
        //     var state = GetAnimatorState(animator);
        //     state.IsRecording = false;
            
        //     // Save recorded animation using JsonHelper
        //     var recordData = state.RecordedFrames.ToList();
        //     JsonHelper.Serialize(recordData);
            
        //     // Optional: Save to disk or cloud
        //     SaveRecordedAnimation(saveKey, recordData);
        // }

        /// <summary>
        /// Play back a recorded animation
        /// </summary>
        // public void PlayRecordedAnimation(
        //     Animator animator,
        //     string saveKey,
        //     bool loop = false)
        // {
        //     if (!ValidateAnimator(animator)) return;
            
        //     StartCoroutine(PlaybackRoutine(animator, saveKey, loop));
        // }

        // private IEnumerator PlaybackRoutine(
        //     Animator animator,
        //     string saveKey,
        //     bool loop)
        // {
        //     var recordData = LoadRecordedAnimation(saveKey);
        //     if (recordData == null || recordData.Count == 0)
        //     {
        //         Debug.LogError($"No recorded animation found for key: {saveKey}");
        //         yield break;
        //     }

        //     int frameIndex = 0;
        //     float startTime = Time.time;

        //     while (true)
        //     {
        //         if (frameIndex >= recordData.Count)
        //         {
        //             if (loop)
        //             {
        //                 frameIndex = 0;
        //                 startTime = Time.time;
        //             }
        //             else
        //             {
        //                 break;
        //             }
        //         }

        //         var frame = recordData[frameIndex];
                
        //         // Apply frame data
        //         foreach (var param in frame.Parameters)
        //         {
        //             animator.SetFloat(param.Key, param.Value);
        //         }
                
        //         animator.transform.position = frame.Position;
        //         animator.transform.rotation = frame.Rotation;
                
        //         // Apply IK data
        //         if (frame.IKData != null)
        //         {
        //             foreach (var ikData in frame.IKData)
        //             {
        //                 animator.SetIKPosition(ikData.Key, ikData.Value.Position);
        //                 animator.SetIKRotation(ikData.Key, ikData.Value.Rotation);
        //                 animator.SetIKPositionWeight(ikData.Key, ikData.Value.PositionWeight);
        //                 animator.SetIKRotationWeight(ikData.Key, ikData.Value.RotationWeight);
        //             }
        //         }

        //         frameIndex++;
        //         yield return null;
        //     }
        // }

        // private void SaveRecordedAnimation(string key, List<AnimationRecordFrame> data)
        // {
        //     PlayerPrefs.SetString($"RecordedAnim_{key}", JsonHelper.Serialize(data));
        // }

        // private List<AnimationRecordFrame> LoadRecordedAnimation(string key)
        // {
        //     string json = PlayerPrefs.GetString($"RecordedAnim_{key}");
        //     return string.IsNullOrEmpty(json) ? null : 
        //         JsonHelper.Deserialize<List<AnimationRecordFrame>>(json);
        // }

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
                AnimatorCullingMode.CullUpdateTransforms : 
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
        // public void OptimizeAnimator(
        //     Animator animator,
        //     bool disableRootMotion = true,
        //     bool useFixedUpdate = false,
        //     bool enableCulling = true)
        // {
        //     if (!ValidateAnimator(animator)) return;
            
        //     animator.applyRootMotion = !disableRootMotion;
        //     animator.updateMode = useFixedUpdate ? 
        //         AnimatorUpdateMode.Fixed : 
        //         AnimatorUpdateMode.Normal;
            
        //     SetCullingMode(animator, enableCulling);
            
        //     // Disable unused components
        //     var rigidbody = animator.GetComponent<Rigidbody>();
        //     if (rigidbody && disableRootMotion)
        //     {
        //         rigidbody.isKinematic = true;
        //     }
        // }

        /// <summary>
        /// Create a simplified version of an animation for LOD purposes
        /// </summary>
        // public AnimationClip CreateSimplifiedAnimation(
        //     AnimationClip source,
        //     float keyframeReduction = 0.5f)
        // {
        //     var simplified = new AnimationClip();
        //     simplified.name = $"{source.name}_Simplified";
            
        //     var curves = AnimationUtility.GetCurveBindings(source);
        //     foreach (var binding in curves)
        //     {
        //         var curve = AnimationUtility.GetEditorCurve(source, binding);
        //         var keys = curve.keys;
                
        //         // Reduce keyframes
        //         int newKeyCount = Mathf.Max(2, Mathf.FloorToInt(keys.Length * keyframeReduction));
        //         var newKeys = new Keyframe[newKeyCount];
                
        //         for (int i = 0; i < newKeyCount; i++)
        //         {
        //             float t = i / (float)(newKeyCount - 1);
        //             int originalIndex = Mathf.FloorToInt(t * (keys.Length - 1));
        //             newKeys[i] = keys[originalIndex];
        //         }
                
        //         var newCurve = new AnimationCurve(newKeys);
        //         AnimationUtility.SetEditorCurve(simplified, binding, newCurve);
        //     }
            
        //     return simplified;
        // }
        
        #endregion
    }
}