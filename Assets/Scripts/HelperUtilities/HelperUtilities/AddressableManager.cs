using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnityHelperSDK.Assets
{
    public static class AddressableManager
    {
        private static Dictionary<string, AsyncOperationHandle> _loadedAssets = new Dictionary<string, AsyncOperationHandle>();

        /// <summary>
        /// Loads an addressable asset asynchronously.
        /// </summary>
        public static async Task<T> LoadAssetAsync<T>(string key) where T : UnityEngine.Object
        {
            if (_loadedAssets.TryGetValue(key, out AsyncOperationHandle existingHandle))
            {
                return (T)existingHandle.Result;
            }

            var handle = Addressables.LoadAssetAsync<T>(key);
            _loadedAssets[key] = handle;
            
            try
            {
                await handle.Task;
                return (T)handle.Result;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to load addressable asset {key}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Loads multiple addressable assets asynchronously.
        /// </summary>
        public static async Task<IList<T>> LoadAssetsAsync<T>(IEnumerable<string> keys) where T : UnityEngine.Object
        {
            var tasks = new List<Task<T>>();
            foreach (var key in keys)
            {
                tasks.Add(LoadAssetAsync<T>(key));
            }
            return await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Releases a loaded addressable asset.
        /// </summary>
        public static void ReleaseAsset(string key)
        {
            if (_loadedAssets.TryGetValue(key, out AsyncOperationHandle handle))
            {
                Addressables.Release(handle);
                _loadedAssets.Remove(key);
            }
        }

        /// <summary>
        /// Releases all loaded addressable assets.
        /// </summary>
        public static void ReleaseAllAssets()
        {
            foreach (var handle in _loadedAssets.Values)
            {
                Addressables.Release(handle);
            }
            _loadedAssets.Clear();
        }

        /// <summary>
        /// Downloads the content of an addressable asset without loading it.
        /// </summary>
        public static async Task<long> PreloadAssetAsync(string key)
        {
            try
            {
                var sizeHandle = Addressables.GetDownloadSizeAsync(key);
                await sizeHandle.Task;
                
                long size = sizeHandle.Result;
                Addressables.Release(sizeHandle);

                if (size > 0)
                {
                    var downloadHandle = Addressables.DownloadDependenciesAsync(key);
                    await downloadHandle.Task;
                    Addressables.Release(downloadHandle);
                }

                return size;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to preload addressable asset {key}: {e.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Checks if an addressable asset is already loaded.
        /// </summary>
        public static bool IsAssetLoaded(string key)
        {
            return _loadedAssets.ContainsKey(key);
        }
    }
}
