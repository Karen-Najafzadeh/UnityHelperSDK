using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;


namespace UnityHelperSDK.HelperUtilities
{
    /// <summary>
    /// ParticleHelper provides a set of utilities for managing particle systems in Unity.
    /// It includes pooling, runtime modification, and performance optimization.
    /// </summary>

    /// <summary>
    /// A comprehensive particle system helper that handles particle effect management,
    /// pooling, and runtime modification.
    /// 
    /// Features:
    /// - Particle system pooling
    /// - Runtime particle modification
    /// - Particle presets
    /// - Particle triggers and events
    /// - Performance optimization
    /// - Particle system cleanup
    /// </summary>
    public static class ParticleHelper
    {
        // Particle system pools
        private static readonly Dictionary<string, Queue<ParticleSystem>> _particlePools 
            = new Dictionary<string, Queue<ParticleSystem>>();
        
        // Active particle systems
        private static readonly HashSet<ParticleSystem> _activeParticleSystems 
            = new HashSet<ParticleSystem>();
        
        // Common particle presets
        private static readonly Dictionary<string, ParticlePreset> _presets 
            = new Dictionary<string, ParticlePreset>();
        
        #region Pooling System
        
        /// <summary>
        /// Initialize a particle system pool
        /// </summary>
        public static void InitializePool(string poolKey, ParticleSystem prefab, int initialSize = 10)
        {
            if (!_particlePools.ContainsKey(poolKey))
            {
                var pool = new Queue<ParticleSystem>();
                
                for (int i = 0; i < initialSize; i++)
                {
                    var ps = GameObject.Instantiate(prefab);
                    ps.gameObject.SetActive(false);
                    pool.Enqueue(ps);
                }
                
                _particlePools[poolKey] = pool;
            }
        }
        
        /// <summary>
        /// Play a particle effect from pool
        /// </summary>
        public static ParticleSystem PlayEffect(string poolKey, Vector3 position, Quaternion rotation = default, 
            Transform parent = null, float duration = -1f)
        {
            if (!_particlePools.TryGetValue(poolKey, out var pool))
            {
                Debug.LogWarning($"Particle pool '{poolKey}' not found!");
                return null;
            }
            
            ParticleSystem ps;
            if (pool.Count > 0)
            {
                ps = pool.Dequeue();
            }
            else
            {
                // Create new instance if pool is empty
                ps = GameObject.Instantiate(_particlePools[poolKey].Peek());
            }
            
            ps.transform.position = position;
            ps.transform.rotation = rotation;
            if (parent) ps.transform.SetParent(parent);
            
            ps.gameObject.SetActive(true);
            ps.Play();
            _activeParticleSystems.Add(ps);
            
            if (duration > 0)
            {
                ReturnToPoolAfterDuration(ps, poolKey, duration);
            }
            
            return ps;
        }
        
        /// <summary>
        /// Return a particle system to its pool
        /// </summary>
        public static void ReturnToPool(ParticleSystem ps, string poolKey)
        {
            if (ps == null) return;
            
            ps.Stop();
            ps.gameObject.SetActive(false);
            ps.transform.SetParent(null);
            
            if (_particlePools.TryGetValue(poolKey, out var pool))
            {
                pool.Enqueue(ps);
            }
            
            _activeParticleSystems.Remove(ps);
        }
        
        #endregion
        
        #region Particle Modification
        
        /// <summary>
        /// Apply a preset to a particle system
        /// </summary>
        public static void ApplyPreset(ParticleSystem ps, string presetName)
        {
            if (_presets.TryGetValue(presetName, out var preset))
            {
                var main = ps.main;
                main.startColor = preset.StartColor;
                main.startSize = preset.StartSize;
                main.startSpeed = preset.StartSpeed;
                main.maxParticles = preset.MaxParticles;
                
                var emission = ps.emission;
                emission.rateOverTime = preset.EmissionRate;
                
                var shape = ps.shape;
                shape.shapeType = preset.ShapeType;
                
                // Apply other preset properties...
            }
        }
        
        /// <summary>
        /// Modify particle properties at runtime
        /// </summary>
        public static void ModifyParticles(ParticleSystem ps, ParticleModifier modifier)
        {
            var main = ps.main;
            
            if (modifier.StartColor.HasValue)
                main.startColor = modifier.StartColor.Value;
                
            if (modifier.StartSize.HasValue)
                main.startSize = modifier.StartSize.Value;
                
            if (modifier.StartSpeed.HasValue)
                main.startSpeed = modifier.StartSpeed.Value;
                
            if (modifier.EmissionRate.HasValue)
            {
                var emission = ps.emission;
                emission.rateOverTime = modifier.EmissionRate.Value;
            }
        }
        
        #endregion
        
        #region Performance Optimization
        
        /// <summary>
        /// Clean up inactive particle systems
        /// </summary>
        public static void CleanupInactiveParticles()
        {
            foreach (var ps in _activeParticleSystems.ToArray())
            {
                if (!ps.IsAlive())
                {
                    foreach (var pool in _particlePools)
                    {
                        if (pool.Value.Contains(ps))
                        {
                            ReturnToPool(ps, pool.Key);
                            break;
                        }
                    }
                }
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private static async void ReturnToPoolAfterDuration(ParticleSystem ps, string poolKey, float duration)
        {
            await Task.Delay(Mathf.RoundToInt(duration * 1000));
            ReturnToPool(ps, poolKey);
        }
        
        #endregion
        
        #region Helper Classes
        
        /// <summary>
        /// Preset configuration for particle systems
        /// </summary>
        public class ParticlePreset
        {
            public Color StartColor { get; set; }
            public float StartSize { get; set; }
            public float StartSpeed { get; set; }
            public int MaxParticles { get; set; }
            public float EmissionRate { get; set; }
            public ParticleSystemShapeType ShapeType { get; set; }
        }
        
        /// <summary>
        /// Runtime particle system modifier
        /// </summary>
        public class ParticleModifier
        {
            public Color? StartColor { get; set; }
            public float? StartSize { get; set; }
            public float? StartSpeed { get; set; }
            public float? EmissionRate { get; set; }
        }
        
        #endregion
    }
}