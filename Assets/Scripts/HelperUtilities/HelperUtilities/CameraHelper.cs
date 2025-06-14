using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cinemachine;


namespace UnityHelperSDK.HelperUtilities{


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
        /// Initialize the camera helper.
        /// </summary>
        public static void Initialize()
        {
            _mainCamera = Camera.main;
            _brain = _mainCamera?.GetComponent<CinemachineBrain>();
            if (!_mainCamera)
                Debug.LogError("No main camera found in scene");
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
        /// Update camera shake. Call this from Update()
        /// </summary>
        public static void UpdateShake()
        {
            if (_trauma <= 0) return;

            float shake = Mathf.Pow(_trauma, 2);
            _mainCamera.transform.localPosition = UnityEngine.Random.insideUnitSphere * shake * _shakeAmplitude;
            _trauma = Mathf.Max(0, _trauma - _traumaDecay * Time.deltaTime);
        }
        
        #endregion
        
        #region Camera Transitions
        
        /// <summary>
        /// Smoothly transition between virtual cameras
        /// </summary>
        public static void TransitionToCamera(string cameraName, float blendTime = 1f)
        {
            if (_virtualCameras.TryGetValue(cameraName, out var vcam))
            {
                foreach (var cam in _virtualCameras.Values)
                    cam.Priority = 0;
                vcam.Priority = 10;
            }
        }
        
        #endregion
        
        #region Screen Conversions
        
        /// <summary>
        /// Convert screen point to world position
        /// </summary>
        public static Vector3 ScreenToWorld(Vector2 screenPoint, float z = 10f)
        {
            if (!_mainCamera) Initialize();
            Vector3 point = new Vector3(screenPoint.x, screenPoint.y, z);
            return _mainCamera.ScreenToWorldPoint(point);
        }

        /// <summary>
        /// Convert world position to screen point
        /// </summary>
        public static Vector2 WorldToScreen(Vector3 worldPoint)
        {
            if (!_mainCamera) Initialize();
            return _mainCamera.WorldToScreenPoint(worldPoint);
        }
        
        #endregion
        
        #region Viewport Management
        
        /// <summary>
        /// Check if a world position is visible in the camera's viewport
        /// </summary>
        public static bool IsInViewport(Vector3 worldPoint)
        {
            if (!_mainCamera) Initialize();
            Vector3 viewportPoint = _mainCamera.WorldToViewportPoint(worldPoint);
            return viewportPoint.x >= 0 && viewportPoint.x <= 1 && 
                viewportPoint.y >= 0 && viewportPoint.y <= 1 && 
                viewportPoint.z > 0;
        }
        
        #endregion
    }
}