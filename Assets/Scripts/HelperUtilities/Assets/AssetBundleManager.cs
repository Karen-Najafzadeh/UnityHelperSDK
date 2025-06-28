using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Centralized AssetBundle loader/unloader with dependency support.
/// </summary>

namespace UnityHelperSDK.Assets{


    public static class AssetBundleManager
    {
        // Path where bundles are stored locally
        public static string BundleDirectory = Path.Combine(Application.streamingAssetsPath, "AssetBundles");

        // Manifest that lists all bundles and dependencies
        private static AssetBundleManifest _manifest;

        // Loaded bundle instances
        private static readonly Dictionary<string, AssetBundle> _loadedBundles = new(StringComparer.OrdinalIgnoreCase);

        // ------------------------------------------------------------------------
        // 2.1. Initialization & Manifest
        // ------------------------------------------------------------------------

        /// <summary>
        /// Loads the AssetBundleManifest from disk (must run before loading any bundle).
        /// </summary>
        public static void Initialize()
        {
            // Load the manifest bundle
            string manifestBundlePath = Path.Combine(BundleDirectory, Application.platform.ToString());
            var bundle = AssetBundle.LoadFromFile(manifestBundlePath);
            if (bundle == null)
                throw new FileNotFoundException($"Manifest bundle not found at: {manifestBundlePath}");
            _manifest = bundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            bundle.Unload(false);
        }
        // ------------------------------------------------------------------------
        // 2.2. Synchronous Load
        // ------------------------------------------------------------------------

        /// <summary>
        /// Loads an AssetBundle and all its dependencies synchronously.
        /// </summary>
        public static AssetBundle LoadBundle(string bundleName)
        {
            // Already loaded?
            if (_loadedBundles.TryGetValue(bundleName, out var existing))
                return existing;

            // Load dependencies first
            foreach (var dep in _manifest.GetAllDependencies(bundleName))
                LoadBundle(dep);

            string path = Path.Combine(BundleDirectory, bundleName);
            var bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
                Debug.LogError($"Failed to load bundle: {bundleName}");
            else
                _loadedBundles[bundleName] = bundle;
            return bundle;
        }
        // ------------------------------------------------------------------------
        // 2.3. Asynchronous Load
        // ------------------------------------------------------------------------

        /// <summary>
        /// Asynchronously loads an AssetBundle with dependencies.
        /// </summary>
        public static async Task<AssetBundle> LoadBundleAsync(string bundleName)
        {
            if (_loadedBundles.TryGetValue(bundleName, out var existing))
                return existing;

            // Load dependencies
            foreach (var dep in _manifest.GetAllDependencies(bundleName))
                await LoadBundleAsync(dep);

            string path = Path.Combine(BundleDirectory, bundleName);
            var request = AssetBundle.LoadFromFileAsync(path); // non-blocking :contentReference[oaicite:1]{index=1}
            await Task.Yield();
            while (!request.isDone)
                await Task.Yield();

            var bundle = request.assetBundle;
            if (bundle == null)
                Debug.LogError($"Async load failed: {bundleName}");
            else
                _loadedBundles[bundleName] = bundle;
            return bundle;
        }
        // ------------------------------------------------------------------------
        // 2.4. Remote Download
        // ------------------------------------------------------------------------

        /// <summary>
        /// Downloads an AssetBundle from a URL (with optional cache) and dependencies.
        /// </summary>
        public static async Task<AssetBundle> DownloadBundleAsync(
            string url,
            string bundleName,
            uint crc = 0,
            bool useCache = true)
        {
            // Ensure dependencies are downloaded first
            foreach (var dep in _manifest.GetAllDependencies(bundleName))
                await DownloadBundleAsync(url.TrimEnd('/') + "/" + dep, dep, crc, useCache);

            UnityWebRequest req = UnityWebRequestAssetBundle.GetAssetBundle(
                $"{url}/{bundleName}", crc, 0);          // GET :contentReference[oaicite:2]{index=2}
            if (useCache) req.SetRequestHeader("Cache-Control", "max-age=0"); // browser-style caching

            var asyncOp = req.SendWebRequest();
            while (!asyncOp.isDone)
                await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"Download error: {req.error}");

            var bundle = DownloadHandlerAssetBundle.GetContent(req); // Helper API :contentReference[oaicite:3]{index=3}
            _loadedBundles[bundleName] = bundle;
            return bundle;
        }
        // ------------------------------------------------------------------------
        // 2.5. Asset Loading Helpers
        // ------------------------------------------------------------------------

        /// <summary>
        /// Gets the absolute file path for a bundle.
        /// </summary>
        public static string GetBundleFilePath(string bundleName)
        {
            return Path.Combine(BundleDirectory, bundleName);
        }

        /// <summary>
        /// Loads an asset of type T from a loaded bundle.
        /// </summary>
        public static T LoadAsset<T>(string bundleName, string assetName) where T : UnityEngine.Object
        {
            var bundle = LoadBundle(bundleName);
            return bundle?.LoadAsset<T>(assetName);
        }

        /// <summary>
        /// Asynchronously loads an asset of type T.
        /// </summary>
        public static async Task<T> LoadAssetAsync<T>(string bundleName, string assetName) where T : UnityEngine.Object
        {
            var bundle = await LoadBundleAsync(bundleName);
            var req = bundle.LoadAssetAsync<T>(assetName);
            while (!req.isDone) await Task.Yield();
            return req.asset as T;
        }
        // ------------------------------------------------------------------------
        // 2.6. Unloading
        // ------------------------------------------------------------------------

        /// <summary>
        /// Unloads a single AssetBundle (optionally unloads loaded assets).
        /// </summary>
        public static void UnloadBundle(string bundleName, bool unloadAllLoadedObjects = false)
        {
            if (_loadedBundles.TryGetValue(bundleName, out var bundle))
            {
                bundle.Unload(unloadAllLoadedObjects);    // AssetBundle.Unload :contentReference[oaicite:4]{index=4}
                _loadedBundles.Remove(bundleName);
            }
        }

        /// <summary>
        /// Unloads all loaded AssetBundles.
        /// </summary>
        public static void UnloadAllBundles(bool unloadAllLoadedObjects = false)
        {
            foreach (var kv in _loadedBundles)
                kv.Value.Unload(unloadAllLoadedObjects);
            _loadedBundles.Clear();
        }
    }
}