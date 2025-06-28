using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityHelperSDK.HelperUtilities;

namespace UnityHelperSDK.DesignPatterns
{
    /// <summary>
    /// Generic factory pattern implementation that manages object creation with integrated pooling support
    /// </summary>
    /// <typeparam name="TProduct">The type of product to create. For GameObjects, use GameObject directly</typeparam>
    public abstract class Factory<TProduct> where TProduct : class
    {
        protected readonly Dictionary<string, Func<TProduct>> _creators = new Dictionary<string, Func<TProduct>>();
        protected readonly HashSet<string> _pooledTypes = new HashSet<string>();

        /// <summary>
        /// Register a non-pooled product type
        /// </summary>
        public void RegisterProduct(string productType, Func<TProduct> creator)
        {
            if (_creators.ContainsKey(productType))
            {
                Debug.LogWarning($"Product type {productType} already registered");
                return;
            }
            _creators[productType] = creator;
        }

        /// <summary>
        /// Register a pooled GameObject product type
        /// </summary>
        public async Task RegisterPooledProduct(string productType, GameObject prefab, ObjectPoolHelper.PoolSettings settings = null)
        {
            if (typeof(TProduct) != typeof(GameObject))
            {
                throw new InvalidOperationException("Pooled products must be GameObjects");
            }

            if (_creators.ContainsKey(productType))
            {
                Debug.LogWarning($"Product type {productType} already registered");
                return;
            }

            // Initialize the pool
            bool success = await ObjectPoolHelper.InitializePoolAsync(productType, prefab, settings);
            if (!success)
            {
                Debug.LogError($"Failed to initialize pool for {productType}");
                return;
            }

            // Register the creator function to get from pool
            _creators[productType] = () => ObjectPoolHelper.Get(productType) as TProduct;
            _pooledTypes.Add(productType);
        }

        /// <summary>
        /// Unregister a product type and clean up its pool if pooled
        /// </summary>
        public void UnregisterProduct(string productType)
        {
            if (!_creators.ContainsKey(productType))
            {
                Debug.LogWarning($"Product type {productType} not found");
                return;
            }

            if (_pooledTypes.Contains(productType))
            {
                ObjectPoolHelper.ClearPool(productType);
                _pooledTypes.Remove(productType);
            }

            _creators.Remove(productType);
        }

        /// <summary>
        /// Create or get from pool a product instance
        /// </summary>
        public virtual TProduct CreateProduct(string productType)
        {
            if (!_creators.ContainsKey(productType))
            {
                Debug.LogError($"Product type {productType} not found");
                return null;
            }
            return _creators[productType]();
        }

        /// <summary>
        /// Return a pooled product to its pool. Only valid for GameObject products.
        /// </summary>
        public virtual void ReturnProduct(string productType, TProduct product)
        {
            if (!_pooledTypes.Contains(productType))
            {
                Debug.LogWarning($"Product type {productType} is not pooled");
                return;
            }

            if (product is GameObject gameObject)
            {
                ObjectPoolHelper.Return(gameObject);
            }
        }

        /// <summary>
        /// Pre-warm a pool with additional instances
        /// </summary>
        public virtual async Task PreWarmPoolAsync(string productType, int count)
        {
            if (!_pooledTypes.Contains(productType))
            {
                Debug.LogWarning($"Product type {productType} is not pooled");
                return;
            }

            await ObjectPoolHelper.PreWarmAsync(productType, count);
        }

        /// <summary>
        /// Get current pool statistics for a pooled product type
        /// </summary>
        public virtual ObjectPoolHelper.PoolStats GetPoolStats(string productType)
        {
            if (!_pooledTypes.Contains(productType))
            {
                Debug.LogWarning($"Product type {productType} is not pooled");
                return default;
            }

            return ObjectPoolHelper.GetPoolStats(productType);
        }

        /// <summary>
        /// Get all registered product types
        /// </summary>
        public IEnumerable<string> GetRegisteredTypes()
        {
            return _creators.Keys;
        }

        /// <summary>
        /// Get all pooled product types
        /// </summary>
        public IEnumerable<string> GetPooledTypes()
        {
            return _pooledTypes;
        }
    }

    /// <summary>
    /// Example usage:
    /// 
    /// public class Enemy { }
    /// public class Goblin : Enemy { }
    /// public class Orc : Enemy { }
    /// 
    /// public class EnemyFactory : Factory<Enemy>
    /// {
    ///     public EnemyFactory()
    ///     {
    ///         RegisterProduct("Goblin", () => new Goblin());
    ///         RegisterProduct("Orc", () => new Orc());
    ///     }
    /// }
    /// 
    /// var factory = new EnemyFactory();
    /// var enemy = factory.CreateProduct("Goblin");
    /// </summary>
}
