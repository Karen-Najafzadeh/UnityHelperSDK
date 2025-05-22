using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// A comprehensive time management helper that handles time-related operations,
/// including timers, cooldowns, and time manipulation.
/// 
/// Features:
/// - Timer management
/// - Cooldown tracking
/// - Time scale control
/// - Time-based triggers
/// - Scheduling system
/// - Stopwatch functionality
/// </summary>
public static class TimeHelper
{
    // Active timers
    private static readonly Dictionary<string, Timer> _timers = new Dictionary<string, Timer>();
    
    // Cooldown tracking
    private static readonly Dictionary<string, float> _cooldowns = new Dictionary<string, float>();
    
    // Scheduled actions
    private static readonly List<ScheduledAction> _scheduledActions = new List<ScheduledAction>();
    
    // Time scale settings
    private static float _defaultTimeScale = 1f;
    private static float _previousTimeScale;
    
    #region Timer Management
    
    /// <summary>
    /// Start a new timer
    /// </summary>
    public static void StartTimer(string id, float duration, Action onComplete = null)
    {
        if (_timers.ContainsKey(id))
        {
            _timers[id].Reset(duration);
        }
        else
        {
            _timers[id] = new Timer(duration, onComplete);
        }
    }
    
    /// <summary>
    /// Stop and remove a timer
    /// </summary>
    public static void StopTimer(string id)
    {
        if (_timers.ContainsKey(id))
        {
            _timers.Remove(id);
        }
    }
    
    /// <summary>
    /// Get remaining time on a timer
    /// </summary>
    public static float GetRemainingTime(string id)
    {
        return _timers.TryGetValue(id, out var timer) ? timer.RemainingTime : 0f;
    }
    
    /// <summary>
    /// Check if a timer is running
    /// </summary>
    public static bool IsTimerRunning(string id)
    {
        return _timers.ContainsKey(id) && !_timers[id].IsComplete;
    }
    
    #endregion
    
    #region Cooldown System
    
    /// <summary>
    /// Start a cooldown
    /// </summary>
    public static void StartCooldown(string id, float duration)
    {
        _cooldowns[id] = Time.time + duration;
    }
    
    /// <summary>
    /// Check if a cooldown is complete
    /// </summary>
    public static bool IsCooldownComplete(string id)
    {
        return !_cooldowns.ContainsKey(id) || Time.time >= _cooldowns[id];
    }
    
    /// <summary>
    /// Get remaining cooldown time
    /// </summary>
    public static float GetCooldownRemaining(string id)
    {
        if (_cooldowns.TryGetValue(id, out float endTime))
        {
            float remaining = endTime - Time.time;
            return remaining > 0f ? remaining : 0f;
        }
        return 0f;
    }
    
    #endregion
    
    #region Time Scale Control
    
    /// <summary>
    /// Set the game time scale
    /// </summary>
    public static void SetTimeScale(float scale)
    {
        _previousTimeScale = Time.timeScale;
        Time.timeScale = scale;
    }
    
    /// <summary>
    /// Pause the game
    /// </summary>
    public static void PauseGame()
    {
        _previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
    }
    
    /// <summary>
    /// Resume the game
    /// </summary>
    public static void ResumeGame()
    {
        Time.timeScale = _previousTimeScale;
    }
    
    /// <summary>
    /// Temporarily modify time scale and restore it after duration
    /// </summary>
    public static async Task SlowMotion(float scale, float duration)
    {
        float originalScale = Time.timeScale;
        SetTimeScale(scale);
        
        await Task.Delay((int)(duration * 1000));
        
        if (Math.Abs(Time.timeScale - scale) < 0.01f)
        {
            SetTimeScale(originalScale);
        }
    }
    
    #endregion
    
    #region Scheduling
    
    /// <summary>
    /// Schedule an action to occur after a delay
    /// </summary>
    public static void ScheduleAction(Action action, float delay, bool ignoreTimeScale = false)
    {
        _scheduledActions.Add(new ScheduledAction(
            action,
            ignoreTimeScale ? Time.unscaledTime + delay : Time.time + delay,
            ignoreTimeScale
        ));
    }
    
    /// <summary>
    /// Update scheduled actions (call this from Update)
    /// </summary>
    public static void UpdateScheduledActions()
    {
        for (int i = _scheduledActions.Count - 1; i >= 0; i--)
        {
            var action = _scheduledActions[i];
            float currentTime = action.IgnoreTimeScale ? Time.unscaledTime : Time.time;
            
            if (currentTime >= action.ExecuteTime)
            {
                action.Action?.Invoke();
                _scheduledActions.RemoveAt(i);
            }
        }
    }
    
    #endregion
    
    #region Helper Classes
    
    private class Timer
    {
        public float Duration { get; private set; }
        public float StartTime { get; private set; }
        public Action OnComplete { get; private set; }
        public bool IsComplete { get; private set; }
        
        public float RemainingTime
        {
            get
            {
                if (IsComplete) return 0f;
                float remaining = (StartTime + Duration) - Time.time;
                return remaining > 0f ? remaining : 0f;
            }
        }
        
        public Timer(float duration, Action onComplete = null)
        {
            Duration = duration;
            OnComplete = onComplete;
            Reset(duration);
        }
        
        public void Reset(float newDuration)
        {
            Duration = newDuration;
            StartTime = Time.time;
            IsComplete = false;
        }
        
        public void Update()
        {
            if (!IsComplete && Time.time >= StartTime + Duration)
            {
                IsComplete = true;
                OnComplete?.Invoke();
            }
        }
    }
    
    private class ScheduledAction
    {
        public Action Action { get; private set; }
        public float ExecuteTime { get; private set; }
        public bool IgnoreTimeScale { get; private set; }
        
        public ScheduledAction(Action action, float executeTime, bool ignoreTimeScale)
        {
            Action = action;
            ExecuteTime = executeTime;
            IgnoreTimeScale = ignoreTimeScale;
        }
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Format a time span in a human-readable format
    /// </summary>
    public static string FormatTime(float seconds)
    {
        TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
        
        if (timeSpan.Hours > 0)
        {
            return string.Format("{0:D2}:{1:D2}:{2:D2}", 
                timeSpan.Hours, 
                timeSpan.Minutes, 
                timeSpan.Seconds);
        }
        
        return string.Format("{0:D2}:{1:D2}", 
            timeSpan.Minutes, 
            timeSpan.Seconds);
    }
    
    /// <summary>
    /// Get current timestamp
    /// </summary>
    public static long GetTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
    
    #endregion
}
