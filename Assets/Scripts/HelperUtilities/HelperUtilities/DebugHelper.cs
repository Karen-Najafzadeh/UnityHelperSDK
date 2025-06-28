using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Debug = UnityEngine.Debug;


namespace UnityHelperSDK.HelperUtilities{

    /// <summary>
    /// A comprehensive debug helper for Unity that provides enhanced logging,
    /// performance monitoring, and visual debugging tools.
    /// 
    /// Features:
    /// - Conditional logging
    /// - Performance monitoring
    /// - Visual debugging
    /// - Stack trace analysis
    /// - Memory tracking
    /// - FPS counter
    /// </summary>
    public static class DebugHelper
    {
        // Log settings
        private static bool _enableLogging = true;
        private static readonly Dictionary<string, LogLevel> _categoryLevels = new Dictionary<string, LogLevel>();
        private static readonly StringBuilder _logBuilder = new StringBuilder();
        
        // Performance monitoring
        private static readonly Dictionary<string, Stopwatch> _stopwatches = new Dictionary<string, Stopwatch>();
        
        // FPS tracking
        private static readonly Queue<float> _fpsBuffer = new Queue<float>();
        private static readonly int _fpsBufferSize = 60;
        private static float _lastFpsUpdate;
        private static readonly float _fpsUpdateInterval = 0.5f;
        
        #region Logging
        
        /// <summary>
        /// Log a message with category and level
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Log(string message, string category = "General", LogLevel level = LogLevel.Info)
        {
            if (!ShouldLog(category, level))
                return;

            var formattedMessage = $"[{category}] {message}";
            
            switch (level)
            {
                case LogLevel.Debug:
                    Debug.Log(formattedMessage);
                    break;
                case LogLevel.Info:
                    Debug.Log(formattedMessage);
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(formattedMessage);
                    break;
                case LogLevel.Error:
                    Debug.LogError(formattedMessage);
                    break;
            }
        }

        /// <summary>
        /// Set minimum log level for a category
        /// </summary>
        public static void SetCategoryLevel(string category, LogLevel level)
        {
            _categoryLevels[category] = level;
        }

        private static bool ShouldLog(string category, LogLevel level)
        {
            return !_categoryLevels.TryGetValue(category, out var minLevel) || level >= minLevel;
        }
        
        #endregion
        
        #region Performance Monitoring
        
        /// <summary>
        /// Start timing a section of code
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void StartTimer(string id)
        {
            if (!_stopwatches.TryGetValue(id, out var sw))
            {
                sw = new Stopwatch();
                _stopwatches[id] = sw;
            }
            
            sw.Restart();
        }
        
        /// <summary>
        /// Stop timing and log result
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void StopTimer(string id)
        {
            if (_stopwatches.TryGetValue(id, out var sw))
            {
                sw.Stop();
                Log($"{id}: {sw.ElapsedMilliseconds}ms", "Performance", LogLevel.Debug);
            }
        }
        
        #endregion
        
        #region Visual Debugging
        
        /// <summary>
        /// Draw a debug sphere that persists
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void DrawPersistentSphere(Vector3 position, float radius, Color color)
        {
            Debug.DrawLine(position + Vector3.up * radius, position - Vector3.up * radius, color, float.MaxValue);
            Debug.DrawLine(position + Vector3.right * radius, position - Vector3.right * radius, color, float.MaxValue);
            Debug.DrawLine(position + Vector3.forward * radius, position - Vector3.forward * radius, color, float.MaxValue);
        }

        /// <summary>
        /// Draw a debug box that persists
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void DrawPersistentBox(Vector3 center, Vector3 size, Color color)
        {
            Vector3 halfSize = size * 0.5f;
            Debug.DrawLine(center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z), 
                        center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z), color, float.MaxValue);
            Debug.DrawLine(center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z), 
                        center + new Vector3(halfSize.x, -halfSize.y, halfSize.z), color, float.MaxValue);
            Debug.DrawLine(center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z), 
                        center + new Vector3(halfSize.x, halfSize.y, -halfSize.z), color, float.MaxValue);
            Debug.DrawLine(center + new Vector3(-halfSize.x, halfSize.y, halfSize.z), 
                        center + new Vector3(halfSize.x, halfSize.y, halfSize.z), color, float.MaxValue);
            
            Debug.DrawLine(center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z), 
                        center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z), color, float.MaxValue);
            Debug.DrawLine(center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z), 
                        center + new Vector3(halfSize.x, halfSize.y, -halfSize.z), color, float.MaxValue);
            Debug.DrawLine(center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z), 
                        center + new Vector3(-halfSize.x, halfSize.y, halfSize.z), color, float.MaxValue);
            Debug.DrawLine(center + new Vector3(halfSize.x, -halfSize.y, halfSize.z), 
                        center + new Vector3(halfSize.x, halfSize.y, halfSize.z), color, float.MaxValue);
            
            Debug.DrawLine(center + new Vector3(-halfSize.x, -halfSize.y, -halfSize.z), 
                        center + new Vector3(-halfSize.x, -halfSize.y, halfSize.z), color, float.MaxValue);
            Debug.DrawLine(center + new Vector3(halfSize.x, -halfSize.y, -halfSize.z), 
                        center + new Vector3(halfSize.x, -halfSize.y, halfSize.z), color, float.MaxValue);
            Debug.DrawLine(center + new Vector3(-halfSize.x, halfSize.y, -halfSize.z), 
                        center + new Vector3(-halfSize.x, halfSize.y, halfSize.z), color, float.MaxValue);
            Debug.DrawLine(center + new Vector3(halfSize.x, halfSize.y, -halfSize.z), 
                        center + new Vector3(halfSize.x, halfSize.y, halfSize.z), color, float.MaxValue);
        }

        #endregion
        
        #region Memory Tracking

        /// <summary>
        /// Get current memory usage statistics
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void LogMemoryStats()
        {
            var stats = new StringBuilder();
            stats.AppendLine("Memory Statistics:");
            stats.AppendLine($"Total Allocated: {UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1024 / 1024}MB");
            stats.AppendLine($"Total Reserved: {UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1024 / 1024}MB");
            stats.AppendLine($"Mono Heap: {UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong() / 1024 / 1024}MB");
            stats.AppendLine($"Mono Used: {UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1024 / 1024}MB");
            Log(stats.ToString(), "Memory", LogLevel.Debug);
        }

        #endregion
        
        #region FPS Counter

        /// <summary>
        /// Update FPS counter (call this from Update)
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void UpdateFPS()
        {
            if (Time.unscaledTime > _lastFpsUpdate + _fpsUpdateInterval)
            {
                _lastFpsUpdate = Time.unscaledTime;
                float fps = 1f / Time.unscaledDeltaTime;
                _fpsBuffer.Enqueue(fps);
                if (_fpsBuffer.Count > _fpsBufferSize)
                    _fpsBuffer.Dequeue();
            }
        }

        /// <summary>
        /// Get average FPS over the last buffer period
        /// </summary>
        public static float GetAverageFPS()
        {
            if (_fpsBuffer.Count == 0)
                return 0f;
            float sum = 0f;
            foreach (var fps in _fpsBuffer)
                sum += fps;
            return sum / _fpsBuffer.Count;
        }

        #endregion

        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error
        }
    }
}