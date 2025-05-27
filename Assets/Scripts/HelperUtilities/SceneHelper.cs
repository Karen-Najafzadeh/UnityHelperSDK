using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;

/// <summary>
/// A comprehensive scene management helper that handles scene loading, transitions,
/// additive scene loading, and scene state management.
/// 
/// Features:
/// - Async scene loading with progress
/// - Scene transitions and fading
/// - Additive scene management
/// - Scene state persistence
/// - Scene dependencies
/// - Loading screen management
/// </summary>
public static class SceneHelper
{
    // Scene transition settings
    private static float _defaultFadeTime = 0.5f;
    private static Color _defaultFadeColor = Color.black;
    
    // Active scenes tracking
    private static readonly HashSet<string> _activeScenes = new HashSet<string>();
    private static readonly Dictionary<string, SceneState> _sceneStates = new Dictionary<string, SceneState>();
    
    #region Scene Loading
    
    /// <summary>
    /// Load a scene with optional transition
    /// </summary>
    public static async Task LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single, 
        bool showLoadingScreen = true, Action<float> onProgress = null)
    {
        if (showLoadingScreen)
        {
            await UIHelper.ShowLoadingScreen();
        }

        var operation = SceneManager.LoadSceneAsync(sceneName, mode);
        operation.allowSceneActivation = false;

        while (operation.progress < 0.9f)
        {
            onProgress?.Invoke(operation.progress);
            await Task.Yield();
        }

        operation.allowSceneActivation = true;
        _activeScenes.Add(sceneName);

        if (showLoadingScreen)
        {
            await UIHelper.HideLoadingScreen();
        }
    }

    /// <summary>
    /// Unload a scene
    /// </summary>
    public static async Task UnloadSceneAsync(string sceneName)
    {
        if (_activeScenes.Contains(sceneName))
        {
            SaveSceneState(sceneName);
            var unloadOperation = SceneManager.UnloadSceneAsync(sceneName);
            while (!unloadOperation.isDone)
            {
                await Task.Yield();
            }
            _activeScenes.Remove(sceneName);
        }
    }
    
    #endregion
    
    #region Scene State Management
    
    /// <summary>
    /// Save the current state of a scene
    /// </summary>
    public static void SaveSceneState(string sceneName)
    {
        var state = new SceneState
        {
            TimeStamp = DateTime.Now,
            PlayerPosition = GameObject.FindGameObjectWithTag("Player")?.transform.position ?? Vector3.zero,
            ActiveObjects = new List<string>()
        };

        // Save active object states
        var sceneObjects = GameObject.FindObjectsOfType<GameObject>();
        foreach (var obj in sceneObjects)
        {
            if (obj.scene.name == sceneName)
            {
                state.ActiveObjects.Add(obj.name);
            }
        }

        _sceneStates[sceneName] = state;
    }

    /// <summary>
    /// Restore a previously saved scene state
    /// </summary>
    public static void RestoreSceneState(string sceneName)
    {
        if (_sceneStates.TryGetValue(sceneName, out var state))
        {
            // Restore player position
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                player.transform.position = state.PlayerPosition;
            }

            // Restore object states
            var sceneObjects = GameObject.FindObjectsOfType<GameObject>();
            foreach (var obj in sceneObjects)
            {
                if (obj.scene.name == sceneName)
                {
                    obj.SetActive(state.ActiveObjects.Contains(obj.name));
                }
            }
        }
    }
    
    #endregion
    
    #region Scene Transitions
    
    /// <summary>
    /// Create a smooth fade transition between scenes
    /// </summary>
    public static async Task FadeTransitionAsync(float duration = 0.5f, Color? color = null)
    {
        var fadeColor = color ?? _defaultFadeColor;
        
        // Create fade overlay using UIHelper
        var overlay = UIHelper.CreateOverlay(fadeColor);
        
        // Fade in
        await overlay.GetComponent<CanvasGroup>().DOFade(1f, duration / 2).AsyncWaitForCompletion();
        
        await Task.Yield(); // Allow scene change to occur here
        
        // Fade out
        await overlay.GetComponent<CanvasGroup>().DOFade(0f, duration / 2).AsyncWaitForCompletion();
        
        GameObject.Destroy(overlay);
    }
    
    #endregion

    #region Helper Classes
    
    /// <summary>
    /// Stores the state of a scene for persistence
    /// </summary>
    private class SceneState
    {
        public DateTime TimeStamp { get; set; }
        public Vector3 PlayerPosition { get; set; }
        public List<string> ActiveObjects { get; set; }
        public Dictionary<string, object> CustomData { get; set; } = new Dictionary<string, object>();
    }
    
    #endregion
}
