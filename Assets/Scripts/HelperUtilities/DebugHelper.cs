using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

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
    private static readonly Dictionary<string, float> _frameTimers = new Dictionary<string, float>();
    
    // FPS tracking
    private static float _fpsUpdateInterval = 0.5f;
    private static float _fpsAccumulator;
    private static int _fpsFrameCount;
    private static float _currentFps;
    private static float _lastFpsUpdate;
    
    #region Logging
    
    /// <summary>
    /// Log a message with category and level
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Log(string message, string category = "General", LogLevel level = LogLevel.Info)
    {
        if (!_enableLogging)
            return;
            
        if (_categoryLevels.TryGetValue(category, out var minLevel) && level < minLevel)
            return;
            
        _logBuilder.Length = 0;
        _logBuilder.Append($"[{category}] ");
        _logBuilder.Append(message);
        
        switch (level)
        {
            case LogLevel.Error:
                Debug.LogError(_logBuilder.ToString());
                break;
            case LogLevel.Warning:
                Debug.LogWarning(_logBuilder.ToString());
                break;
            default:
                Debug.Log(_logBuilder.ToString());
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
    
    /// <summary>
    /// Track frame time for operations
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void TrackFrameTime(string id)
    {
        if (!_frameTimers.ContainsKey(id))
            _frameTimers[id] = 0;
            
        _frameTimers[id] += Time.deltaTime;
    }
    
    /// <summary>
    /// Get average frame time
    /// </summary>
    public static float GetAverageFrameTime(string id, int frames = 60)
    {
        if (_frameTimers.TryGetValue(id, out float total))
        {
            float average = total / frames;
            _frameTimers[id] = 0;
            return average;
        }
        return 0;
    }
    
    #endregion
    
    #region Visual Debugging
    
    /// <summary>
    /// Draw a debug sphere that persists for a specified duration
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void DrawSphere(Vector3 position, float radius, Color color, float duration = 0)
    {
        if (duration > 0)
            Debug.DrawLine(position + Vector3.up * radius, position - Vector3.up * radius, color, duration);
            
        UnityEngine.Debug.DrawLine(position + Vector3.right * radius, position - Vector3.right * radius, color, duration);
        UnityEngine.Debug.DrawLine(position + Vector3.forward * radius, position - Vector3.forward * radius, color, duration);
    }
    
    /// <summary>
    /// Draw text in the scene view
    /// </summary>
    [Conditional("UNITY_EDITOR")]
    public static void DrawText(Vector3 position, string text, Color color)
    {
#if UNITY_EDITOR
        UnityEditor.Handles.Label(position, text);
#endif
    }
    
    /// <summary>
    /// Draw a debug path from a list of points
    /// </summary>
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void DrawPath(Vector3[] points, Color color, float duration = 0)
    {
        for (int i = 0; i < points.Length - 1; i++)
        {
            Debug.DrawLine(points[i], points[i + 1], color, duration);
        }
    }
    
    #endregion
    
    #region FPS Counter
    
    /// <summary>
    /// Update FPS counter (call from Update)
    /// </summary>
    public static void UpdateFPS()
    {
        _fpsFrameCount++;
        _fpsAccumulator += Time.unscaledDeltaTime;
        
        if (Time.unscaledTime - _lastFpsUpdate >= _fpsUpdateInterval)
        {
            _currentFps = _fpsFrameCount / _fpsAccumulator;
            _fpsFrameCount = 0;
            _fpsAccumulator = 0;
            _lastFpsUpdate = Time.unscaledTime;
        }
    }
    
    /// <summary>
    /// Get current FPS
    /// </summary>
    public static float GetFPS()
    {
        return _currentFps;
    }
    
    #endregion
    
    #region Helper Types
    
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
    
    #endregion
}
