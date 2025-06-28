using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using UnityHelperSDK.Assets;

/// <summary>
/// A comprehensive object pooling system for Unity that handles runtime pool creation,
/// prefab instantiation, and automatic pool expansion.
/// Features:
/// - Automatic pool expansion
/// - Multiple pool configurations
/// - Async pool initialization
/// - Integration with AssetBundleManager for prefab loading
/// - Pool statistics and monitoring
/// - Component pooling support
/// </summary>

namespace UnityHelperSDK.HelperUtilities{

    public static class ObjectPoolHelper
    {
        // Dictionary to store all active pools
        private static readonly Dictionary<string, ObjectPool> _pools = new Dictionary<string, ObjectPool>();
        
        // Default settings
        private static readonly PoolSettings _defaultSettings = new PoolSettings
        {
            InitialSize = 10,
            MaxSize = 100,
            ExpandBy = 5,
            AutoExpand = true
        };

        #region Pool Management

        /// <summary>
        /// Initialize a new pool with the given prefab
        /// </summary>
        public static async Task<bool> InitializePoolAsync(string poolKey, GameObject prefab, PoolSettings settings = null)
        {
            if (_pools.ContainsKey(poolKey))
            {
                Debug.LogWarning($"Pool '{poolKey}' already exists!");
                return false;
            }

            try
            {
                var pool = new ObjectPool(prefab, settings ?? _defaultSettings);
                await pool.InitializeAsync();
                _pools[poolKey] = pool;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize pool '{poolKey}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Initialize a pool from an asset bundle
        /// </summary>
        public static async Task<bool> InitializePoolFromBundleAsync(
            string poolKey, 
            string bundleName, 
            string prefabName,
            PoolSettings settings = null)
        {
            try
            {
                // Load prefab from asset bundle
                var bundle = await AssetBundleManager.LoadBundleAsync(bundleName);
                if (bundle == null) return false;

                var prefab = await AssetBundleManager.LoadAssetAsync<GameObject>(bundleName, prefabName);
                if (prefab == null) return false;

                return await InitializePoolAsync(poolKey, prefab, settings);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize pool from bundle: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get an object from the pool. Returns null if pool doesn't exist.
        /// </summary>
        public static GameObject Get(string poolKey, Vector3 position = default, Quaternion rotation = default)
        {
            if (!_pools.TryGetValue(poolKey, out var pool))
            {
                Debug.LogError($"Pool '{poolKey}' not found!");
                return null;
            }

            return pool.Get(position, rotation);
        }

        /// <summary>
        /// Return an object to its pool
        /// </summary>
        public static void Return(GameObject obj)
        {
            var poolable = obj.GetComponent<PoolableObject>();
            if (poolable == null || string.IsNullOrEmpty(poolable.PoolKey))
            {
                Debug.LogWarning($"Object '{obj.name}' is not poolable!");
                return;
            }

            if (_pools.TryGetValue(poolable.PoolKey, out var pool))
            {
                pool.Return(obj);
            }
            else
            {
                Debug.LogWarning($"Pool '{poolable.PoolKey}' not found! Destroying object instead.");
                GameObject.Destroy(obj);
            }
        }

        /// <summary>
        /// Pre-warm a pool by instantiating additional objects
        /// </summary>
        public static async Task PreWarmAsync(string poolKey, int count)
        {
            if (!_pools.TryGetValue(poolKey, out var pool))
            {
                Debug.LogError($"Pool '{poolKey}' not found!");
                return;
            }

            await pool.ExpandAsync(count);
        }

        #endregion

        #region Pool Information

        /// <summary>
        /// Get current pool statistics
        /// </summary>
        public static PoolStats GetPoolStats(string poolKey)
        {
            if (!_pools.TryGetValue(poolKey, out var pool))
                return default;

            return new PoolStats
            {
                TotalObjects = pool.TotalCount,
                ActiveObjects = pool.ActiveCount,
                AvailableObjects = pool.AvailableCount,
                PeakObjects = pool.PeakCount
            };
        }

        /// <summary>
        /// Clear a specific pool
        /// </summary>
        public static void ClearPool(string poolKey)
        {
            if (_pools.TryGetValue(poolKey, out var pool))
            {
                pool.Clear();
                _pools.Remove(poolKey);
            }
        }

        /// <summary>
        /// Clear all pools
        /// </summary>
        public static void ClearAllPools()
        {
            foreach (var pool in _pools.Values)
            {
                pool.Clear();
            }
            _pools.Clear();
        }

        #endregion

        #region Helper Classes

        private class ObjectPool
        {
            private readonly GameObject _prefab;
            private readonly PoolSettings _settings;
            private readonly Queue<GameObject> _available;
            private readonly HashSet<GameObject> _active;
            private int _peakCount;

            public int TotalCount => _available.Count + _active.Count;
            public int ActiveCount => _active.Count;
            public int AvailableCount => _available.Count;
            public int PeakCount => _peakCount;

            public ObjectPool(GameObject prefab, PoolSettings settings)
            {
                _prefab = prefab;
                _settings = settings;
                _available = new Queue<GameObject>();
                _active = new HashSet<GameObject>();
            }

            public async Task InitializeAsync()
            {
                await ExpandAsync(_settings.InitialSize);
            }

            public async Task ExpandAsync(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    if (TotalCount >= _settings.MaxSize)
                        break;

                    await Task.Yield(); // Spread instantiation across frames
                    var obj = CreateNewObject();
                    _available.Enqueue(obj);
                }
            }

            public GameObject Get(Vector3 position, Quaternion rotation)
            {
                GameObject obj;

                if (_available.Count == 0 && _settings.AutoExpand && TotalCount < _settings.MaxSize)
                {
                    obj = CreateNewObject();
                }
                else if (_available.Count > 0)
                {
                    obj = _available.Dequeue();
                }
                else
                {
                    Debug.LogWarning($"Pool for {_prefab.name} is empty and cannot expand!");
                    return null;
                }

                obj.transform.position = position;
                obj.transform.rotation = rotation;
                obj.SetActive(true);
                _active.Add(obj);

                _peakCount = Mathf.Max(_peakCount, _active.Count);

                return obj;
            }

            public void Return(GameObject obj)
            {
                if (_active.Remove(obj))
                {
                    obj.SetActive(false);
                    _available.Enqueue(obj);
                }
            }

            public void Clear()
            {
                foreach (var obj in _available.Concat(_active))
                {
                    if (obj != null)
                        GameObject.Destroy(obj);
                }
                _available.Clear();
                _active.Clear();
            }

            private GameObject CreateNewObject()
            {
                var obj = GameObject.Instantiate(_prefab);
                var poolable = obj.GetComponent<PoolableObject>();
                if (poolable == null)
                {
                    poolable = obj.AddComponent<PoolableObject>();
                }
                obj.SetActive(false);
                return obj;
            }
        }

        /// <summary>
        /// Settings for pool configuration
        /// </summary>
        public class PoolSettings
        {
            public int InitialSize { get; set; }
            public int MaxSize { get; set; }
            public int ExpandBy { get; set; }
            public bool AutoExpand { get; set; }
        }

        /// <summary>
        /// Statistics about a pool's current state
        /// </summary>
        public struct PoolStats
        {
            public int TotalObjects { get; set; }
            public int ActiveObjects { get; set; }
            public int AvailableObjects { get; set; }
            public int PeakObjects { get; set; }
        }

        #endregion
    }

    /// <summary>
    /// Component added to pooled objects to track their pool membership
    /// </summary>
    public class PoolableObject : MonoBehaviour
    {
        public string PoolKey { get; set; }

        public void ReturnToPool()
        {
            ObjectPoolHelper.Return(gameObject);
        }
    }
}