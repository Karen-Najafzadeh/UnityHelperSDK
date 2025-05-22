using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cinemachine;

/// <summary>
/// A comprehensive camera helper that handles camera effects, transitions,
/// shakes, and viewport calculations. Integrates with Cinemachine for
/// advanced camera behaviors.
/// 
/// Features:
/// - Camera shake effects
/// - Smooth transitions
/// - Screen-to-world conversions
/// - Viewport calculations
/// - Camera composition helpers
/// - Cinemachine integration
/// </summary>
public static class CameraHelper
{
    // Cached camera references
    private static Camera _mainCamera;
    private static CinemachineBrain _brain;
    private static readonly Dictionary<string, CinemachineVirtualCamera> _virtualCameras 
        = new Dictionary<string, CinemachineVirtualCamera>();
    
    // Shake settings
    private static float _trauma;
    private static float _traumaDecay = 1.5f;
    private static float _shakeFrequency = 25f;
    private static float _shakeAmplitude = 1f;
    
    #region Initialization
    
    /// <summary>
    /// Initialize the camera helper
    /// </summary>
    public static void Initialize(Camera mainCamera)
    {
        _mainCamera = mainCamera;
        _brain = mainCamera.GetComponent<CinemachineBrain>();
    }
    
    /// <summary>
    /// Register a virtual camera
    /// </summary>
    public static void RegisterVirtualCamera(string id, CinemachineVirtualCamera vcam)
    {
        _virtualCameras[id] = vcam;
    }
    
    #endregion
    
    #region Camera Shake
    
    /// <summary>
    /// Add trauma to trigger camera shake
    /// </summary>
    public static void AddTrauma(float amount)
    {
        _trauma = Mathf.Clamp01(_trauma + amount);
    }
    
    /// <summary>
    /// Update camera shake (call from Update)
    /// </summary>
    public static void UpdateShake()
    {
        if (_trauma > 0)
        {
            float shake = _trauma * _trauma; // Quadratic falloff
            
            float offsetX = Mathf.PerlinNoise(Time.time * _shakeFrequency, 0) * 2 - 1;
            float offsetY = Mathf.PerlinNoise(0, Time.time * _shakeFrequency) * 2 - 1;
            
            _mainCamera.transform.localPosition = new Vector3(
                offsetX * shake * _shakeAmplitude,
                offsetY * shake * _shakeAmplitude,
                _mainCamera.transform.localPosition.z
            );
            
            _trauma = Mathf.Max(0, _trauma - _traumaDecay * Time.deltaTime);
        }
    }
    
    #endregion
    
    #region Camera Transitions
    
    /// <summary>
    /// Smoothly transition between virtual cameras
    /// </summary>
    public static async Task TransitionToCamera(string cameraId, float duration = 1f)
    {
        if (!_virtualCameras.TryGetValue(cameraId, out var targetCam))
            return;
            
        foreach (var vcam in _virtualCameras.Values)
        {
            vcam.Priority = 0;
        }
        
        targetCam.Priority = 100;
        
        if (duration > 0)
            await Task.Delay((int)(duration * 1000));
    }
    
    /// <summary>
    /// Set camera field of view with optional smooth transition
    /// </summary>
    public static async Task SetFieldOfView(float fov, float duration = 0f)
    {
        if (duration <= 0)
        {
            _mainCamera.fieldOfView = fov;
            return;
        }
        
        float startFov = _mainCamera.fieldOfView;
        float elapsed = 0;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            _mainCamera.fieldOfView = Mathf.Lerp(startFov, fov, t);
            await Task.Yield();
        }
        
        _mainCamera.fieldOfView = fov;
    }
    
    #endregion
    
    #region Screen Conversions
    
    /// <summary>
    /// Convert screen point to world position
    /// </summary>
    public static Vector3 ScreenToWorld(Vector2 screenPoint, float zDistance = 10f)
    {
        if (_mainCamera == null)
            return Vector3.zero;
            
        Vector3 point = new Vector3(screenPoint.x, screenPoint.y, zDistance);
        return _mainCamera.ScreenToWorldPoint(point);
    }
    
    /// <summary>
    /// Convert world position to screen point
    /// </summary>
    public static Vector2 WorldToScreen(Vector3 worldPoint)
    {
        if (_mainCamera == null)
            return Vector2.zero;
            
        return _mainCamera.WorldToScreenPoint(worldPoint);
    }
    
    /// <summary>
    /// Check if a world point is visible in the camera's view
    /// </summary>
    public static bool IsPointVisible(Vector3 worldPoint)
    {
        if (_mainCamera == null)
            return false;
            
        var viewportPoint = _mainCamera.WorldToViewportPoint(worldPoint);
        return viewportPoint.x >= 0 && viewportPoint.x <= 1 &&
               viewportPoint.y >= 0 && viewportPoint.y <= 1 &&
               viewportPoint.z > 0;
    }
    
    #endregion
    
    #region Composition Helpers
    
    /// <summary>
    /// Get a position offset from the camera's forward direction
    /// </summary>
    public static Vector3 GetPositionInFrontOfCamera(float distance = 10f)
    {
        if (_mainCamera == null)
            return Vector3.zero;
            
        return _mainCamera.transform.position + _mainCamera.transform.forward * distance;
    }
    
    /// <summary>
    /// Get camera frustum corners at specified distance
    /// </summary>
    public static Vector3[] GetFrustumCorners(float distance = 10f)
    {
        if (_mainCamera == null)
            return new Vector3[4];
            
        Vector3[] corners = new Vector3[4];
        _mainCamera.CalculateFrustumCorners(
            new Rect(0, 0, 1, 1),
            distance,
            Camera.MonoOrStereoscopicEye.Mono,
            corners
        );
        
        for (int i = 0; i < 4; i++)
        {
            corners[i] = _mainCamera.transform.TransformPoint(corners[i]);
        }
        
        return corners;
    }
    
    #endregion
    
    #region Viewport Management
    
    /// <summary>
    /// Get viewport bounds in world space
    /// </summary>
    public static Bounds GetViewportBounds(float distance = 10f)
    {
        Vector3[] corners = GetFrustumCorners(distance);
        
        Vector3 min = corners[0];
        Vector3 max = corners[0];
        
        for (int i = 1; i < corners.Length; i++)
        {
            min = Vector3.Min(min, corners[i]);
            max = Vector3.Max(max, corners[i]);
        }
        
        return new Bounds((min + max) * 0.5f, max - min);
    }
    
    /// <summary>
    /// Keep a target position within camera bounds
    /// </summary>
    public static Vector3 ClampToCameraBounds(Vector3 position, float padding = 0f)
    {
        if (_mainCamera == null)
            return position;
            
        Vector3 viewportPoint = _mainCamera.WorldToViewportPoint(position);
        
        viewportPoint.x = Mathf.Clamp(viewportPoint.x, padding, 1 - padding);
        viewportPoint.y = Mathf.Clamp(viewportPoint.y, padding, 1 - padding);
        
        return _mainCamera.ViewportToWorldPoint(viewportPoint);
    }
    
    #endregion
}
