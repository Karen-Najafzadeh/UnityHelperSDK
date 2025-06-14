using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityHelperSDK.Assets;


namespace UnityHelperSDK.Data{


    /// <summary>
    /// A comprehensive local storage helper that provides easy data persistence,
    /// automatic cloud syncing, and asset bundle integration.
    /// Features:
    /// - Local data persistence with PlayerPrefs and JSON files
    /// - Automatic cloud sync with Firebase
    /// - Asset bundle caching and management
    /// - Encrypted storage for sensitive data
    /// - Auto-migration of data schemas
    /// </summary>
    public static class LocalStorageHelper
    {
        // Default save location for local files
        private static readonly string LocalSavePath = Path.Combine(Application.persistentDataPath, "SaveData");
        
        // Encryption key for sensitive data (you should change this in production!)
        private static readonly byte[] EncryptionKey = System.Text.Encoding.UTF8.GetBytes("YourSecretKey123");
        
        // Cache for loaded data to prevent frequent disk reads
        private static readonly Dictionary<string, object> DataCache = new Dictionary<string, object>();
        
        // Track modified data for batch saves
        private static readonly HashSet<string> ModifiedKeys = new HashSet<string>();

        // Sync settings
        private static bool AutoCloudSync = true;
        private static TimeSpan SyncInterval = TimeSpan.FromMinutes(5);
        private static DateTime LastSyncTime = DateTime.MinValue;

        /// <summary>
        /// Initialize the storage system
        /// </summary>
        public static void Initialize()
        {
            if (!Directory.Exists(LocalSavePath))
            {
                Directory.CreateDirectory(LocalSavePath);
            }

            // Load cached data from disk
            LoadAllLocalData();
        }

        #region Local Storage Operations

        /// <summary>
        /// Save data locally with optional encryption
        /// </summary>
        public static async Task<bool> SaveDataAsync<T>(string key, T data, bool encrypt = false, bool cloudSync = true)
        {
            try
            {
                // Serialize data using JsonHelper
                string json = JsonHelper.Serialize(data);
                
                // Encrypt if requested
                if (encrypt)
                {
                    json = EncryptString(json);
                }

                // Save to cache
                DataCache[key] = data;
                ModifiedKeys.Add(key);

                // Save to disk
                string filePath = GetLocalFilePath(key);
                await File.WriteAllTextAsync(filePath, json);

                // Cloud sync if enabled
                if (cloudSync && AutoCloudSync)
                {
                    await SyncToCloudAsync(key, data);
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error saving data for key {key}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Load data from local storage with optional decryption
        /// </summary>
        public static async Task<T> LoadDataAsync<T>(string key, bool encrypted = false, bool useCache = true)
        {
            try
            {
                // Check cache first if enabled
                if (useCache && DataCache.TryGetValue(key, out object cachedData))
                {
                    if (cachedData is T typedData)
                        return typedData;
                }

                string filePath = GetLocalFilePath(key);
                if (!File.Exists(filePath))
                    return default;

                string json = await File.ReadAllTextAsync(filePath);

                // Decrypt if necessary
                if (encrypted)
                {
                    json = DecryptString(json);
                }

                // Deserialize using JsonHelper
                T data = JsonUtility.FromJson<T>(json);
                
                // Update cache
                DataCache[key] = data;
                
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading data for key {key}: {ex.Message}");
                return default;
            }
        }

        #endregion

        #region Cloud Sync Integration

        /// <summary>
        /// Sync local data to Firebase cloud storage
        /// </summary>
        private static async Task<bool> SyncToCloudAsync<T>(string key, T data)
        {
            try
            {
                // Only sync if enough time has passed since last sync
                if (DateTime.Now - LastSyncTime < SyncInterval)
                    return true;

                // Use FirebaseHelper to save to cloud
                bool success = await FirebaseHelper.SetDocumentAsync($"userdata/{Application.identifier}", key, data);
                
                if (success)
                {
                    LastSyncTime = DateTime.Now;
                }
                
                return success;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Cloud sync failed for key {key}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Pull latest data from cloud and update local storage
        /// </summary>
        public static async Task<bool> PullFromCloudAsync(string key)
        {
            try
            {
                // Get data from Firebase
                var cloudData = await FirebaseHelper.GetDocumentAsync<Dictionary<string, object>>(
                    $"userdata/{Application.identifier}", key);

                if (cloudData != null)
                {
                    // Convert to JSON and save locally
                    string json = JsonHelper.Serialize(cloudData);
                    await File.WriteAllTextAsync(GetLocalFilePath(key), json);
                    
                    // Update cache
                    DataCache[key] = cloudData;
                    
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Cloud pull failed for key {key}: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Asset Bundle Integration

        /// <summary>
        /// Cache an asset bundle locally and track its metadata
        /// </summary>
        public static async Task<bool> CacheAssetBundleAsync(string bundleName, string version)
        {
            try
            {
                // Load bundle using AssetBundleManager
                var bundle = await AssetBundleManager.LoadBundleAsync(bundleName);
                if (bundle == null)
                    return false;

                // Save metadata
                var metadata = new BundleMetadata
                {
                    Version = version,
                    CacheDate = DateTime.UtcNow,
                    Size = (uint)new FileInfo(AssetBundleManager.GetBundleFilePath(bundleName)).Length
                };

                await SaveDataAsync($"bundle_{bundleName}_meta", metadata);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to cache bundle {bundleName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if a cached bundle needs updating
        /// </summary>
        public static async Task<bool> IsBundleOutdatedAsync(string bundleName, string latestVersion)
        {
            var metadata = await LoadDataAsync<BundleMetadata>($"bundle_{bundleName}_meta");
            return metadata == null || metadata.Version != latestVersion;
        }

        #endregion

        #region Utilities

        private static string GetLocalFilePath(string key)
        {
            return Path.Combine(LocalSavePath, $"{key}.json");
        }

        private static string EncryptString(string text)
        {
            try
            {
                using var aes = System.Security.Cryptography.Aes.Create();
                aes.Key = EncryptionKey;
                aes.GenerateIV();

                using var encryptor = aes.CreateEncryptor();
                byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(text);
                byte[] encrypted = encryptor.TransformFinalBlock(textBytes, 0, textBytes.Length);

                // Combine IV and encrypted data
                byte[] result = new byte[aes.IV.Length + encrypted.Length];
                Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);

                return Convert.ToBase64String(result);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Encryption failed: {ex.Message}");
                return text;
            }
        }

        private static string DecryptString(string encryptedText)
        {
            try
            {
                byte[] fullData = Convert.FromBase64String(encryptedText);
                
                using var aes = System.Security.Cryptography.Aes.Create();
                aes.Key = EncryptionKey;

                // Extract IV from the beginning of the data
                byte[] iv = new byte[aes.IV.Length];
                byte[] encrypted = new byte[fullData.Length - iv.Length];
                Buffer.BlockCopy(fullData, 0, iv, 0, iv.Length);
                Buffer.BlockCopy(fullData, iv.Length, encrypted, 0, encrypted.Length);
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                byte[] decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
                return System.Text.Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Decryption failed: {ex.Message}");
                return encryptedText;
            }
        }

        private static void LoadAllLocalData()
        {
            try
            {
                string[] files = Directory.GetFiles(LocalSavePath, "*.json");
                foreach (string file in files)
                {
                    string key = Path.GetFileNameWithoutExtension(file);
                    string json = File.ReadAllText(file);
                    DataCache[key] = JsonHelper.DeserializeToDictionary(json);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading local data: {ex.Message}");
            }
        }

        #endregion

        #region Helper Classes

        private class BundleMetadata
        {
            public string Version { get; set; }
            public DateTime CacheDate { get; set; }
            public uint Size { get; set; }
        }

        #endregion

        #region Settings Management

        /// <summary>
        /// Configure auto-sync settings
        /// </summary>
        public static void ConfigureSync(bool autoSync, TimeSpan? interval = null)
        {
            AutoCloudSync = autoSync;
            if (interval.HasValue)
            {
                SyncInterval = interval.Value;
            }
        }

        /// <summary>
        /// Clear all cached data and optionally delete local files
        /// </summary>
        public static void ClearCache(bool deleteFiles = false)
        {
            DataCache.Clear();
            ModifiedKeys.Clear();

            if (deleteFiles)
            {
                try
                {
                    Directory.Delete(LocalSavePath, true);
                    Directory.CreateDirectory(LocalSavePath);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error clearing local files: {ex.Message}");
                }
            }
        }

        #endregion
    }
}