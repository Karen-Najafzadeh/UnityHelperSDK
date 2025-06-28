using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace UnityHelperSDK.HelperUtilities
{
    /// <summary>
    /// TimeHelper provides a set of utilities for managing time-related operations in Unity.
    /// It includes timers, cooldowns, time scale control, and scheduling.
    /// </summary>

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
        /// Creates and starts a new timer with the specified duration
        /// </summary>
        /// <param name="id">Unique identifier for the timer</param>
        /// <param name="duration">Duration in seconds</param>
        /// <param name="onComplete">Action to execute when timer completes</param>
        /// <param name="loop">Whether the timer should loop</param>
        public static void StartTimer(string id, float duration, Action onComplete = null, bool loop = false)
        {
            if (_timers.ContainsKey(id))
            {
                Debug.LogWarning($"Timer {id} already exists. Stopping existing timer.");
                StopTimer(id);
            }

            var timer = new Timer
            {
                Duration = duration,
                TimeRemaining = duration,
                OnComplete = onComplete,
                IsLooping = loop,
                IsRunning = true
            };

            _timers[id] = timer;
        }

        /// <summary>
        /// Stops and removes a timer
        /// </summary>
        public static void StopTimer(string id)
        {
            if (_timers.ContainsKey(id))
            {
                _timers.Remove(id);
            }
        }

        /// <summary>
        /// Pauses a running timer
        /// </summary>
        public static void PauseTimer(string id)
        {
            if (_timers.TryGetValue(id, out Timer timer))
            {
                timer.IsRunning = false;
            }
        }

        /// <summary>
        /// Resumes a paused timer
        /// </summary>
        public static void ResumeTimer(string id)
        {
            if (_timers.TryGetValue(id, out Timer timer))
            {
                timer.IsRunning = true;
            }
        }

        /// <summary>
        /// Gets the remaining time for a timer
        /// </summary>
        public static float GetRemainingTime(string id)
        {
            return _timers.TryGetValue(id, out Timer timer) ? timer.TimeRemaining : 0f;
        }
        
        #endregion
        
        #region Cooldown System
        
        /// <summary>
        /// Starts a cooldown with the specified duration
        /// </summary>
        /// <param name="id">Unique identifier for the cooldown</param>
        /// <param name="duration">Duration in seconds</param>
        public static void StartCooldown(string id, float duration)
        {
            _cooldowns[id] = Time.time + duration;
        }

        /// <summary>
        /// Checks if a cooldown has finished
        /// </summary>
        /// <returns>True if cooldown is complete or doesn't exist</returns>
        public static bool IsCooldownComplete(string id)
        {
            if (!_cooldowns.TryGetValue(id, out float endTime))
                return true;

            if (Time.time >= endTime)
            {
                _cooldowns.Remove(id);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the remaining time for a cooldown
        /// </summary>
        public static float GetCooldownRemaining(string id)
        {
            if (!_cooldowns.TryGetValue(id, out float endTime))
                return 0f;

            return Mathf.Max(0f, endTime - Time.time);
        }
        
        #endregion
        
        #region Time Scale Control

        /// <summary>
        /// Sets the game time scale with optional fade
        /// </summary>
        public static async Task SetTimeScale(float targetScale, float duration = 0f)
        {
            if (duration <= 0)
            {
                Time.timeScale = targetScale;
                return;
            }

            float startScale = Time.timeScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                Time.timeScale = Mathf.Lerp(startScale, targetScale, elapsed / duration);
                await Task.Yield();
            }

            Time.timeScale = targetScale;
        }

        /// <summary>
        /// Pauses the game and returns a function to resume
        /// </summary>
        public static Action PauseGame(bool smooth = true)
        {
            _previousTimeScale = Time.timeScale;
            if (smooth)
            {
                _ = SetTimeScale(0f, 0.2f);
            }
            else
            {
                Time.timeScale = 0f;
            }

            return () => ResumeGame(smooth);
        }

        /// <summary>
        /// Resumes the game from a paused state
        /// </summary>
        public static void ResumeGame(bool smooth = true)
        {
            if (smooth)
            {
                _ = SetTimeScale(_previousTimeScale, 0.2f);
            }
            else
            {
                Time.timeScale = _previousTimeScale;
            }
        }

        #endregion
        
        #region Scheduling System

        /// <summary>
        /// Schedules an action to be executed after a delay
        /// </summary>
        public static string ScheduleAction(Action action, float delay, bool useUnscaledTime = false)
        {
            string id = Guid.NewGuid().ToString();
            var scheduledAction = new ScheduledAction
            {
                Id = id,
                Action = action,
                ExecutionTime = (useUnscaledTime ? Time.unscaledTime : Time.time) + delay,
                UseUnscaledTime = useUnscaledTime
            };

            _scheduledActions.Add(scheduledAction);
            return id;
        }

        /// <summary>
        /// Cancels a scheduled action
        /// </summary>
        public static void CancelScheduledAction(string id)
        {
            _scheduledActions.RemoveAll(action => action.Id == id);
        }
        
        #endregion
        
        #region Helper Classes
        
        private class Timer
        {
            public float Duration { get; set; }
            public float TimeRemaining { get; set; }
            public Action OnComplete { get; set; }
            public bool IsLooping { get; set; }
            public bool IsRunning { get; set; }
        }

        private class ScheduledAction
        {
            public string Id { get; set; }
            public Action Action { get; set; }
            public float ExecutionTime { get; set; }
            public bool UseUnscaledTime { get; set; }
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Updates all active timers and scheduled actions. Should be called in Update
        /// </summary>
        public static void UpdateTimers()
        {
            var completedTimers = new List<string>();

            foreach (var kvp in _timers)
            {
                var timer = kvp.Value;
                if (!timer.IsRunning) continue;

                timer.TimeRemaining -= Time.deltaTime;

                if (timer.TimeRemaining <= 0f)
                {
                    timer.OnComplete?.Invoke();

                    if (timer.IsLooping)
                    {
                        timer.TimeRemaining = timer.Duration;
                    }
                    else
                    {
                        completedTimers.Add(kvp.Key);
                    }
                }
            }

            foreach (var id in completedTimers)
            {
                _timers.Remove(id);
            }

            // Update scheduled actions
            var currentTime = Time.time;
            var unscaledTime = Time.unscaledTime;
            var completedActions = new List<ScheduledAction>();

            foreach (var action in _scheduledActions)
            {
                float timeToCheck = action.UseUnscaledTime ? unscaledTime : currentTime;
                if (timeToCheck >= action.ExecutionTime)
                {
                    action.Action?.Invoke();
                    completedActions.Add(action);
                }
            }

            foreach (var action in completedActions)
            {
                _scheduledActions.Remove(action);
            }
        }

        /// <summary>
        /// Formats a time value into a string (MM:SS or HH:MM:SS)
        /// </summary>
        public static string FormatTime(float timeInSeconds, bool includeHours = false)
        {
            int hours = (int)(timeInSeconds / 3600f);
            int minutes = (int)((timeInSeconds % 3600f) / 60f);
            int seconds = (int)(timeInSeconds % 60f);

            return includeHours || hours > 0 
                ? $"{hours:00}:{minutes:00}:{seconds:00}"
                : $"{minutes:00}:{seconds:00}";
        }

        /// <summary>
        /// Formats a TimeSpan into a human-readable string
        /// </summary>
        public static string FormatTimeSpan(TimeSpan timeSpan, bool shortFormat = false)
        {
            if (shortFormat)
            {
                if (timeSpan.TotalDays >= 1)
                    return $"{timeSpan.Days}d";
                if (timeSpan.TotalHours >= 1)
                    return $"{timeSpan.Hours}h";
                if (timeSpan.TotalMinutes >= 1)
                    return $"{timeSpan.Minutes}m";
                return $"{timeSpan.Seconds}s";
            }

            List<string> parts = new List<string>();
            if (timeSpan.Days > 0)
                parts.Add($"{timeSpan.Days} day{(timeSpan.Days != 1 ? "s" : "")}");
            if (timeSpan.Hours > 0)
                parts.Add($"{timeSpan.Hours} hour{(timeSpan.Hours != 1 ? "s" : "")}");
            if (timeSpan.Minutes > 0)
                parts.Add($"{timeSpan.Minutes} minute{(timeSpan.Minutes != 1 ? "s" : "")}");
            if (timeSpan.Seconds > 0 || parts.Count == 0)
                parts.Add($"{timeSpan.Seconds} second{(timeSpan.Seconds != 1 ? "s" : "")}");

            return string.Join(", ", parts);
        }

        #endregion
        
        #region Time Measurement

        /// <summary>
        /// Measures the execution time of an action
        /// </summary>
        public static TimeSpan MeasureExecutionTime(Action action)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }

        /// <summary>
        /// Measures the execution time of an async action
        /// </summary>
        public static async Task<TimeSpan> MeasureExecutionTimeAsync(Func<Task> action)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            await action();
            stopwatch.Stop();
            return stopwatch.Elapsed;
        }

        #endregion
        
        #region Update Management

        /// <summary>
        /// Updates all active timers and scheduled actions
        /// </summary>
        public static void Update()
        {
            UpdateTimers();
            UpdateCooldowns();
            UpdateScheduledActions();
        }

        private static void UpdateCooldowns()
        {
            var completedCooldowns = new List<string>();
            var currentTime = Time.time;

            foreach (var cooldown in _cooldowns)
            {
                if (currentTime >= cooldown.Value)
                {
                    completedCooldowns.Add(cooldown.Key);
                }
            }

            foreach (var key in completedCooldowns)
            {
                _cooldowns.Remove(key);
            }
        }

        private static void UpdateScheduledActions()
        {
            var currentTime = Time.time;
            var unscaledTime = Time.unscaledTime;
            var completedActions = new List<ScheduledAction>();

            foreach (var action in _scheduledActions)
            {
                float timeToCheck = action.UseUnscaledTime ? unscaledTime : currentTime;
                if (timeToCheck >= action.ExecutionTime)
                {
                    try
                    {
                        action.Action?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error executing scheduled action: {e.Message}");
                    }
                    completedActions.Add(action);
                }
            }

            foreach (var action in completedActions)
            {
                _scheduledActions.Remove(action);
            }
        }

        #endregion
    }
}