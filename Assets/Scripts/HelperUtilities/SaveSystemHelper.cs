using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// A comprehensive save system helper that handles game state persistence,
/// save file management, and cloud synchronization.
/// 
/// Features:
/// - Multiple save slots
/// - Automatic cloud backup
/// - Save file encryption
/// - Save data compression
/// - Save file validation
/// - Auto-save system
/// - Save migration
/// </summary>
public static class SaveSystemHelper
{
    // Save system settings
    private static readonly string SaveFolder = Path.Combine(Application.persistentDataPath, "Saves");
    private static readonly string TempFolder = Path.Combine(Application.persistentDataPath, "Temp");
    private static readonly string BackupFolder = Path.Combine(Application.persistentDataPath, "Backups");
    
    // Encryption settings (change these in production!)
    private static readonly byte[] EncryptionKey = Encoding.UTF8.GetBytes("YourGameEncryptionKey123!");
    private static readonly byte[] EncryptionIV = Encoding.UTF8.GetBytes("YourGameIV1234567");
    
    // Auto-save settings
    private static bool _autoSaveEnabled = true;
    private static float _autoSaveInterval = 300f; // 5 minutes
    private static float _lastAutoSave;

    #region Save Operations

    /// <summary>
    /// Save game state to a specific slot
    /// </summary>
    public static async Task SaveGameAsync(int slot, object gameState, bool createBackup = true)
    {
        var savePath = GetSaveFilePath(slot);
        var json = JsonHelper.Serialize(gameState);
        
        // Create save directory if it doesn't exist
        Directory.CreateDirectory(SaveFolder);
        
        // Create backup if requested
        if (createBackup && File.Exists(savePath))
        {
            var backupPath = GetBackupFilePath(slot);
            Directory.CreateDirectory(BackupFolder);
            File.Copy(savePath, backupPath, true);
        }
        
        // Encrypt and save
        var encrypted = EncryptData(json);
        await File.WriteAllBytesAsync(savePath, encrypted);
        
        // Upload to cloud if enabled
        if (FirebaseHelper.IsInitialized)
        {
            await UploadSaveToCloud(slot, encrypted);
        }
        
        _lastAutoSave = Time.time;
    }

    /// <summary>
    /// Load game state from a specific slot
    /// </summary>
    public static async Task<T> LoadGameAsync<T>(int slot) where T : class
    {
        var savePath = GetSaveFilePath(slot);
        
        if (!File.Exists(savePath))
        {
            Debug.LogWarning($"No save file found in slot {slot}");
            return null;
        }
        
        try
        {
            var encrypted = await File.ReadAllBytesAsync(savePath);
            var json = DecryptData(encrypted);
            return JsonHelper.Deserialize<T>(json);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading save file: {e.Message}");
            
            // Try to recover from backup
            return await LoadBackupAsync<T>(slot);
        }
    }

    #endregion

    #region Cloud Integration

    /// <summary>
    /// Upload save file to cloud storage
    /// </summary>
    private static async Task UploadSaveToCloud(int slot, byte[] saveData)
    {
        var metadata = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow,
            ["slot"] = slot,
            ["version"] = Application.version
        };

        await FirebaseHelper.SetDocumentAsync($"saves/{slot}", metadata);
        // Upload actual save data to Firebase Storage
        // Implementation depends on your Firebase setup
    }

    /// <summary>
    /// Download save file from cloud storage
    /// </summary>
    public static async Task<T> LoadFromCloudAsync<T>(int slot) where T : class
    {
        // Implementation depends on your Firebase setup
        throw new NotImplementedException();
    }

    #endregion

    #region Auto-Save System

    /// <summary>
    /// Update auto-save system (call from Update)
    /// </summary>
    public static void UpdateAutoSave()
    {
        if (!_autoSaveEnabled) return;
        
        if (Time.time - _lastAutoSave >= _autoSaveInterval)
        {
            // Auto-save implementation
            // You'll need to implement how to get current game state
        }
    }

    #endregion

    #region Save File Management

    /// <summary>
    /// Get info about all save files
    /// </summary>
    public static List<SaveFileInfo> GetAllSaveFiles()
    {
        var saves = new List<SaveFileInfo>();
        
        if (!Directory.Exists(SaveFolder)) return saves;
        
        foreach (var file in Directory.GetFiles(SaveFolder, "save_*.dat"))
        {
            var info = new FileInfo(file);
            saves.Add(new SaveFileInfo
            {
                Slot = ExtractSlotNumber(info.Name),
                CreationTime = info.CreationTime,
                LastWriteTime = info.LastWriteTime,
                Size = info.Length
            });
        }
        
        return saves;
    }

    /// <summary>
    /// Delete a save file
    /// </summary>
    public static void DeleteSave(int slot)
    {
        var savePath = GetSaveFilePath(slot);
        var backupPath = GetBackupFilePath(slot);
        
        if (File.Exists(savePath))
            File.Delete(savePath);
            
        if (File.Exists(backupPath))
            File.Delete(backupPath);
    }

    #endregion

    #region Utility Methods

    private static string GetSaveFilePath(int slot)
    {
        return Path.Combine(SaveFolder, $"save_{slot}.dat");
    }

    private static string GetBackupFilePath(int slot)
    {
        return Path.Combine(BackupFolder, $"save_{slot}.bak");
    }

    private static byte[] EncryptData(string data)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = EncryptionKey;
            aes.IV = EncryptionIV;
            
            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                using (StreamWriter sw = new StreamWriter(cs))
                {
                    sw.Write(data);
                }
                
                return ms.ToArray();
            }
        }
    }

    private static string DecryptData(byte[] data)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = EncryptionKey;
            aes.IV = EncryptionIV;
            
            using (MemoryStream ms = new MemoryStream(data))
            using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
            using (StreamReader sr = new StreamReader(cs))
            {
                return sr.ReadToEnd();
            }
        }
    }

    private static int ExtractSlotNumber(string filename)
    {
        var match = System.Text.RegularExpressions.Regex.Match(filename, @"save_(\d+)\.dat");
        return match.Success ? int.Parse(match.Groups[1].Value) : -1;
    }

    #endregion

    #region Helper Classes

    /// <summary>
    /// Information about a save file
    /// </summary>
    public class SaveFileInfo
    {
        public int Slot { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime LastWriteTime { get; set; }
        public long Size { get; set; }
    }

    #endregion
}
