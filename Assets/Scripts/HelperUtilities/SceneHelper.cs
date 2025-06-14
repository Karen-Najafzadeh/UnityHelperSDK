using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine.UI;


namespace UnityHelperSDK.HelperUtilities
{
    /// <summary>
    /// SceneHelper provides a set of utilities for managing Unity scenes.
    /// It includes scene loading, transitions, additive scene management,
    /// and scene state persistence.
    /// </summary>

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
        /// Loads a scene with transition and optional state persistence
        /// </summary>
        public static async Task LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single, 
            bool showLoadingScreen = true, bool persistState = false)
        {
            if (persistState)
            {
                SaveSceneState();
            }

            if (showLoadingScreen)
            {
                await ShowLoadingScreen();
            }
            else
            {
                await FadeToBlack(_defaultFadeTime);
            }

            var operation = SceneManager.LoadSceneAsync(sceneName, mode);
            operation.allowSceneActivation = false;

            while (operation.progress < 0.9f)
            {
                await Task.Yield();
                if (showLoadingScreen)
                {
                    UpdateLoadingProgress(operation.progress);
                }
            }

            operation.allowSceneActivation = true;
            await Task.Yield(); // Wait for scene to actually change

            if (persistState)
            {
                RestoreSceneState();
            }

            if (showLoadingScreen)
            {
                await HideLoadingScreen();
            }
            else
            {
                await FadeFromBlack(_defaultFadeTime);
            }

            if (mode == LoadSceneMode.Single)
            {
                _activeScenes.Clear();
            }
            _activeScenes.Add(sceneName);
        }

        /// <summary>
        /// Unloads a scene with optional transition
        /// </summary>
        public static async Task UnloadSceneAsync(string sceneName, bool showTransition = true)
        {
            if (!_activeScenes.Contains(sceneName))
                return;

            if (showTransition)
            {
                await FadeToBlack(_defaultFadeTime);
            }

            var operation = SceneManager.UnloadSceneAsync(sceneName);
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            _activeScenes.Remove(sceneName);

            if (showTransition)
            {
                await FadeFromBlack(_defaultFadeTime);
            }
        }
        
        #endregion
        
        #region Scene State Management
        /// <summary>
        /// Saves the current scene state
        /// </summary>
        public static void SaveSceneState()
        {
            var currentScene = SceneManager.GetActiveScene();
            var state = new SceneState();

            // Get all game objects in the scene
            var rootObjects = currentScene.GetRootGameObjects();
            foreach (var obj in rootObjects)
            {
                var serializableObjects = obj.GetComponentsInChildren<ISceneStateSerializable>(true);
                foreach (var serializable in serializableObjects)
                {
                    string stateKey = serializable.GetStateKey();
                    if (!string.IsNullOrEmpty(stateKey))
                    {
                        state.ObjectStates[stateKey] = serializable.SaveState();
                    }
                }
            }

            _sceneStates[currentScene.name] = state;
        }

        /// <summary>
        /// Restores saved scene state
        /// </summary>
        public static void RestoreSceneState()
        {
            var currentScene = SceneManager.GetActiveScene();
            if (!_sceneStates.TryGetValue(currentScene.name, out SceneState state))
                return;

            var rootObjects = currentScene.GetRootGameObjects();
            foreach (var obj in rootObjects)
            {
                var serializableObjects = obj.GetComponentsInChildren<ISceneStateSerializable>(true);
                foreach (var serializable in serializableObjects)
                {
                    string stateKey = serializable.GetStateKey();
                    if (!string.IsNullOrEmpty(stateKey) && state.ObjectStates.TryGetValue(stateKey, out object savedState))
                    {
                        serializable.LoadState(savedState);
                    }
                }
            }
        }
        
        #endregion
        
        #region Scene Transitions
        
        private static CanvasGroup _fadeCanvas;

        private static async Task FadeToBlack(float duration)
        {
            var fadeScreen = CreateOrGetFadeScreen();
            var canvasGroup = fadeScreen.GetComponent<CanvasGroup>();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
                await Task.Yield();
            }
        }

        private static async Task FadeFromBlack(float duration)
        {
            var fadeScreen = CreateOrGetFadeScreen();
            var canvasGroup = fadeScreen.GetComponent<CanvasGroup>();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                await Task.Yield();
            }

            GameObject.Destroy(fadeScreen);
        }

        private static GameObject CreateOrGetFadeScreen()
        {
            var existing = GameObject.Find("SceneTransitionCanvas");
            if (existing != null)
                return existing;

            var canvas = new GameObject("SceneTransitionCanvas", typeof(Canvas), typeof(CanvasGroup));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 9999;

            var image = new GameObject("FadeImage", typeof(Image));
            image.transform.SetParent(canvas.transform, false);
            image.GetComponent<Image>().color = _defaultFadeColor;
            image.GetComponent<RectTransform>().sizeDelta = new Vector2(Screen.width * 1.5f, Screen.height * 1.5f);

            GameObject.DontDestroyOnLoad(canvas);
            return canvas;
        }
        
        #endregion
        
        #region Loading Screen

        private static GameObject _loadingScreen;
        private static Image _progressBar;
        private static TextMeshProUGUI _progressText;

        private static async Task ShowLoadingScreen()
        {
            _loadingScreen = CreateLoadingScreen();
            var canvasGroup = _loadingScreen.GetComponent<CanvasGroup>();

            float elapsed = 0f;
            while (elapsed < _defaultFadeTime)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / _defaultFadeTime);
                await Task.Yield();
            }
        }

        private static async Task HideLoadingScreen()
        {
            if (_loadingScreen == null) return;

            var canvasGroup = _loadingScreen.GetComponent<CanvasGroup>();

            float elapsed = 0f;
            while (elapsed < _defaultFadeTime)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / _defaultFadeTime);
                await Task.Yield();
            }

            GameObject.Destroy(_loadingScreen);
            _loadingScreen = null;
        }

        private static void UpdateLoadingProgress(float progress)
        {
            if (_progressBar != null)
                _progressBar.fillAmount = progress;
            
            if (_progressText != null)
                _progressText.text = $"{(progress * 100f):F0}%";
        }

        private static GameObject CreateLoadingScreen()
        {
            var canvas = new GameObject("LoadingScreen", typeof(Canvas), typeof(CanvasGroup));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 10000;

            var background = new GameObject("Background", typeof(Image));
            background.transform.SetParent(canvas.transform, false);
            background.GetComponent<Image>().color = _defaultFadeColor;
            background.GetComponent<RectTransform>().sizeDelta = new Vector2(Screen.width * 1.5f, Screen.height * 1.5f);

            var progressBarBg = new GameObject("ProgressBarBg", typeof(Image));
            progressBarBg.transform.SetParent(canvas.transform, false);
            progressBarBg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);
            progressBarBg.GetComponent<RectTransform>().sizeDelta = new Vector2(500f, 20f);

            var progressBarFill = new GameObject("ProgressBarFill", typeof(Image));
            progressBarFill.transform.SetParent(progressBarBg.transform, false);
            progressBarFill.GetComponent<Image>().color = Color.white;
            progressBarFill.GetComponent<RectTransform>().sizeDelta = new Vector2(500f, 20f);
            _progressBar = progressBarFill.GetComponent<Image>();

            var progressText = new GameObject("ProgressText", typeof(TextMeshProUGUI));
            progressText.transform.SetParent(canvas.transform, false);
            var tmp = progressText.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            _progressText = tmp;

            GameObject.DontDestroyOnLoad(canvas);
            return canvas;
        }

        #endregion

        #region Helper Classes
        private class SceneState
        {
            public Dictionary<string, object> ObjectStates = new Dictionary<string, object>();
        }    public interface ISceneStateSerializable
        {
            string GetStateKey();
            object SaveState();
            void LoadState(object state);
        }
        
        #endregion
    }
}