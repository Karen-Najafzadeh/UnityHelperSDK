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
    /// Set the global time scale
    /// </summary>
    public static void SetTimeScale(float scale)
    {
        _previousTimeScale = Time.timeScale;
        Time.timeScale = scale;
    }

    /// <summary>
    /// Restore the previous time scale
    /// </summary>
    public static void RestoreTimeScale()
    {
        Time.timeScale = _previousTimeScale;
    }

    /// <summary>
    /// Pause/unpause the game
    /// </summary>
    public static void SetPaused(bool paused)
    {
        if (paused)
        {
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = _previousTimeScale;
        }
    }

    #endregion
    
    #region Scheduling System

    /// <summary>
    /// Schedule an action to be executed after a delay
    /// </summary>
    public static void ScheduleAction(Action action, float delay, bool ignoreTimeScale = false)
    {
        if (action == null)
            return;

        float executeTime = (ignoreTimeScale ? Time.unscaledTime : Time.time) + delay;
        _scheduledActions.Add(new ScheduledAction(action, executeTime, ignoreTimeScale));
    }

    /// <summary>
    /// Schedule an action to be executed at a specific time
    /// </summary>
    public static void ScheduleActionAt(Action action, float executeTime, bool ignoreTimeScale = false)
    {
        if (action == null)
            return;

        _scheduledActions.Add(new ScheduledAction(action, executeTime, ignoreTimeScale));
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

    /// <summary>
    /// Clear all scheduled actions
    /// </summary>
    public static void ClearScheduledActions()
    {
        _scheduledActions.Clear();
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
        public Action Action { get; }
        public float ExecuteTime { get; }
        public bool IgnoreTimeScale { get; }
        
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
