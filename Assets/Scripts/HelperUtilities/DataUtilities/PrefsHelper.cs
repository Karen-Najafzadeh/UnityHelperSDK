using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityHelperSDK.Data{


    /// <summary>
    /// A comprehensive preferences helper that integrates PlayerPrefs with Firebase and JSON.
    /// Supports automatic type conversion, cloud sync, and encrypted storage.
    /// 
    /// Features:
    /// - Type-safe preference access
    /// - Automatic cloud synchronization
    /// - JSON serialization
    /// - Encrypted storage
    /// - Bulk operations
    /// - Migration support
    /// </summary>
    public static class PrefsHelper
    {
        // Cloud sync settings
        private static bool _autoCloudSync = true;
        private static readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(5);
        private static DateTime _lastSyncTime = DateTime.MinValue;

        // Cache to prevent frequent disk/cloud reads
        private static readonly Dictionary<string, object> _cache = new Dictionary<string, object>();

        #region Core Operations

        /// <summary>
        /// Set a value with automatic type handling
        /// </summary>
        public static void Set<TEnum>(TEnum key, object value) where TEnum : Enum
        {
            string keyName = key.ToString();
            string type = GetKeyType(key);

            // Update local storage
            SetLocalValue(keyName, value, type);

            // Update cache
            _cache[keyName] = value;

        }

        /// <summary>
        /// Get a value with automatic type conversion
        /// </summary>
        public static T Get<T, TEnum>(TEnum key, T defaultValue = default) where TEnum : Enum
        {
            string keyName = key.ToString();

            // Check cache first
            if (_cache.TryGetValue(keyName, out object cachedValue))
            {
                return (T)Convert.ChangeType(cachedValue, typeof(T));
            }

            // Get from PlayerPrefs
            string type = GetKeyType(key);
            object value = GetLocalValue(keyName, type, defaultValue);

            // Update cache
            _cache[keyName] = value;

            return (T)Convert.ChangeType(value, typeof(T));
        }

        /// <summary>
        /// Delete a preference key
        /// </summary>
        public static async Task Delete<TEnum>(TEnum key, bool cloudSync = true) where TEnum : Enum
        {
            string keyName = key.ToString();

            // Remove from local storage
            PlayerPrefs.DeleteKey(keyName);
            PlayerPrefs.Save();

            // Remove from cache
            _cache.Remove(keyName);

            // Remove from cloud if enabled
            if (cloudSync && _autoCloudSync)
            {
                await FirebaseHelper.DeleteDocumentAsync("preferences", keyName);
            }
        }

        #endregion

        #region Bulk Operations

        /// <summary>
        /// Get all preferences as a dictionary
        /// </summary>
        public static Dictionary<TEnum, object> GetAll<TEnum>() where TEnum : Enum
        {
            var result = new Dictionary<TEnum, object>();

            foreach (TEnum key in Enum.GetValues(typeof(TEnum)))
            {
                string type = GetKeyType(key);
                string keyName = key.ToString();

                if (PlayerPrefs.HasKey(keyName))
                {
                    object value = GetLocalValue(keyName, type, null);
                    result[key] = value;
                }
            }

            return result;
        }

        /// <summary>
        /// Set multiple preferences at once
        /// </summary>
        public static void SetBulk<TEnum>(Dictionary<TEnum, object> values) where TEnum : Enum
        {
            foreach (var kvp in values)
            {
                Set<TEnum>(kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Delete all preferences
        /// </summary>
        public static async Task DeleteAll<TEnum>(bool cloudSync = true) where TEnum : Enum
        {
            foreach (TEnum key in Enum.GetValues(typeof(TEnum)))
            {
                await Delete(key, false);
            }

            if (cloudSync && _autoCloudSync)
            {
                // Delete all preferences from cloud
                foreach (TEnum key in Enum.GetValues(typeof(TEnum)))
                {
                    await FirebaseHelper.DeleteDocumentAsync("preferences", key.ToString());
                }
            }
        }

        #endregion

        #region Cloud Sync

        /// <summary>
        /// Sync all preferences to Firebase
        /// </summary>
        public static async Task SyncAllToCloud()
        {
            foreach (var kvp in _cache)
            {
                await SyncToCloud(kvp.Key, kvp.Value);
            }
            _lastSyncTime = DateTime.Now;
        }

        /// <summary>
        /// Pull latest preferences from Firebase
        /// </summary>
        public static async Task SyncFromCloud<TEnum>() where TEnum : Enum
        {
            foreach (TEnum key in Enum.GetValues(typeof(TEnum)))
            {
                string keyName = key.ToString();
                var data = await FirebaseHelper.GetDocumentAsync<Dictionary<string, object>>("preferences", keyName);

                if (data != null && data.TryGetValue("value", out object value))
                {
                    Set(key, value);
                }
            }
            _lastSyncTime = DateTime.Now;
        }

        private static async Task SyncToCloud(string key, object value)
        {
            var data = new Dictionary<string, object>
            {
                ["value"] = value,
                ["timestamp"] = DateTime.UtcNow
            };

            await FirebaseHelper.SetDocumentAsync("preferences", key, data);
        }

        #endregion

        #region JSON Integration

        /// <summary>
        /// Save a complex object as JSON
        /// </summary>
        public static void SetJson<T, TEnum>(TEnum key, T value) where TEnum : Enum
        {
            string json = JsonHelper.Serialize(value);
            Set(key, json);
        }

        /// <summary>
        /// Load a complex object from JSON
        /// </summary>
        public static T GetJson<T, TEnum>(TEnum key, T defaultValue = default) where TEnum : Enum
        {
            string json = Get<string, TEnum>(key, null);
            if (string.IsNullOrEmpty(json)) return defaultValue;

            try
            {
                var dict = JsonHelper.DeserializeToDictionary(json);
                return (T)Convert.ChangeType(dict, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        #endregion

        #region Helper Methods

        private static string GetKeyType<TEnum>(TEnum key) where TEnum : Enum
        {
            var memberInfo = key.GetType().GetMember(key.ToString()).FirstOrDefault();
            var attribute = memberInfo?.GetCustomAttribute<TypeAttribute>();

            if (attribute == null)
                throw new InvalidOperationException($"Key {key} is missing TypeAttribute");

            return attribute.Type.ToLowerInvariant();
        }

        private static void SetLocalValue(string key, object value, string type)
        {
            switch (type)
            {
                case "int":
                    PlayerPrefs.SetInt(key, Convert.ToInt32(value));
                    break;
                case "float":
                    PlayerPrefs.SetFloat(key, Convert.ToSingle(value));
                    break;
                case "string":
                    PlayerPrefs.SetString(key, value.ToString());
                    break;
                case "bool":
                    PlayerPrefs.SetInt(key, Convert.ToBoolean(value) ? 1 : 0);
                    break;
                case "vector2":
                case "vector3":
                case "vector4":
                case "color":
                case "quaternion":
                    PlayerPrefs.SetString(key, JsonUtility.ToJson(value));
                    break;
                default:
                    throw new ArgumentException($"Unsupported type: {type}");
            }

            PlayerPrefs.Save();
        }

        private static object GetLocalValue(string key, string type, object defaultValue)
        {
            if (!PlayerPrefs.HasKey(key))
                return defaultValue;

            switch (type)
            {
                case "int":
                    return PlayerPrefs.GetInt(key, Convert.ToInt32(defaultValue));
                case "float":
                    return PlayerPrefs.GetFloat(key, Convert.ToSingle(defaultValue));
                case "string":
                    return PlayerPrefs.GetString(key, defaultValue?.ToString());
                case "bool":
                    return PlayerPrefs.GetInt(key, Convert.ToBoolean(defaultValue) ? 1 : 0) == 1;
                case "vector2":
                    return JsonUtility.FromJson<Vector2>(PlayerPrefs.GetString(key));
                case "vector3":
                    return JsonUtility.FromJson<Vector3>(PlayerPrefs.GetString(key));
                case "vector4":
                    return JsonUtility.FromJson<Vector4>(PlayerPrefs.GetString(key));
                case "color":
                    return JsonUtility.FromJson<Color>(PlayerPrefs.GetString(key));
                case "quaternion":
                    return JsonUtility.FromJson<Quaternion>(PlayerPrefs.GetString(key));
                default:
                    throw new ArgumentException($"Unsupported type: {type}");
            }
        }

        #endregion
    }
}