using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO.Compression;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Newtonsoft.Json.Converters;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_EDITOR
using UnityEditor;
#endif


// using Firebase;
// using Firebase.Extensions;
// using Firebase.Firestore;

/// <summary>
/// A comprehensive JSON utility class for Unity and .NET projects,
/// featuring serialization, deserialization, file I/O, schema validation,
/// diff/merge, HTTP requests, Firestore integration, and more.
/// </summary>

namespace UnityHelperSDK.Data{


public static class JsonHelper
    {
        //--------------------------------------------------------------------------------
        // Core Serialize / Deserialize
        //--------------------------------------------------------------------------------

        /// <summary>Serialize any object to JSON string.</summary>
        public static string Serialize(object data, bool prettyPrint = false)
        {
            var settings = CreateSettings(prettyPrint);
            return JsonConvert.SerializeObject(data, settings);
        }

        /// <summary>Deserialize JSON string to Dictionary&lt;string, object&gt;.</summary>
        public static Dictionary<string, object> DeserializeToDictionary(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON string is null or empty.", nameof(json));
            var token = JToken.Parse(json);
            if (token.Type != JTokenType.Object)
                throw new InvalidOperationException("JSON root is not an object.");
            return ParseJObject((JObject)token);
        }

        private static Dictionary<string, object> ParseJObject(JObject jo)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in jo.Properties())
                dict[prop.Name] = ParseToken(prop.Value);
            return dict;
        }

        private static object ParseToken(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.Object:  return ParseJObject((JObject)token);
                case JTokenType.Array:   return token.Select(ParseToken).ToList();
                case JTokenType.Integer: return token.Value<long>();
                case JTokenType.Float:   return token.Value<double>();
                case JTokenType.String:  return token.Value<string>();
                case JTokenType.Boolean: return token.Value<bool>();
                case JTokenType.Null:    return null;
                case JTokenType.Date:    return token.Value<DateTime>();
                case JTokenType.Bytes:   return token.Value<byte[]>();
                default:                 return token.ToString();
            }
        }

        /// <summary>Attempt to deserialize without throwing.</summary>
        public static bool TryDeserialize(string json, out Dictionary<string, object> result, out string error)
        {
            result = null; error = null;
            try { result = DeserializeToDictionary(json); return true; }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        //--------------------------------------------------------------------------------
        // File I/O
        //--------------------------------------------------------------------------------

        /// <summary>Serialize object and save to file.</summary>
        public static void SerializeToFile(object data, string filePath, bool prettyPrint = false)
        {
            var json = Serialize(data, prettyPrint);
            var dir = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(filePath, json);
        }

        /// <summary>Deserialize JSON from file to dictionary.</summary>
        public static Dictionary<string, object> DeserializeFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"JSON file not found: {filePath}");
            return DeserializeToDictionary(File.ReadAllText(filePath));
        }

        /// <summary>Async file write.</summary>
        public static async Task SerializeToFileAsync(object data, string filePath, bool prettyPrint = false, CancellationToken ct = default)
        {
            var json = Serialize(data, prettyPrint);
            var dir = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(filePath, json, ct).ConfigureAwait(false);
        }

        /// <summary>Async file read.</summary>
        public static async Task<Dictionary<string, object>> DeserializeFromFileAsync(string filePath, CancellationToken ct = default)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"JSON file not found: {filePath}");
            var json = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
            return DeserializeToDictionary(json);
        }

        //--------------------------------------------------------------------------------
        // Formatting & Minification
        //--------------------------------------------------------------------------------

        public static string PrettyPrint(string json) => JToken.Parse(json).ToString(Formatting.Indented);
        public static string Minify(string json)     => JToken.Parse(json).ToString(Formatting.None);

        /// <summary>Deserialize JSON string to type T.</summary>
        public static T Deserialize<T>(string json)
        {
            if (string.IsNullOrEmpty(json))
                return default;
            return JsonConvert.DeserializeObject<T>(json);
        }

        //--------------------------------------------------------------------------------
        // Schema Validation
        //--------------------------------------------------------------------------------

        // public static void ValidateSchema(string json, string schemaJson)
        // {
        //     var schema = JSchema.Parse(schemaJson);
        //     var token  = JToken.Parse(json);
        //     if (!token.IsValid(schema, out IList<string> errors))
        //         throw new JsonSchemaException("Schema validation failed: " + string.Join("; ", errors));
        // }

        //--------------------------------------------------------------------------------
        // Merge & Diff
        //--------------------------------------------------------------------------------

        public static void MergeJson(JObject dest, JObject src)
        {
            dest.Merge(src, new JsonMergeSettings {
                MergeArrayHandling = MergeArrayHandling.Concat
            });
        }

        public static List<string> DiffJson(JToken a, JToken b)
        {
            var diffs = new List<string>();
            void Recurse(JToken x, JToken y, string path)
            {
                if (!JToken.DeepEquals(x, y))
                {
                    if (x.Type == JTokenType.Object && y.Type == JTokenType.Object)
                    {
                        var props = new HashSet<string>(((JObject)x).Properties().Select(p => p.Name)
                            .Concat(((JObject)y).Properties().Select(p => p.Name)));
                        foreach (var prop in props)
                            Recurse(x[prop], y[prop], $"{path}/{prop}");
                    }
                    else diffs.Add(path);
                }
            }
            Recurse(a, b, string.Empty);
            return diffs;
        }

        //--------------------------------------------------------------------------------
        // Path-based Access
        //--------------------------------------------------------------------------------

        public static object GetByPath(Dictionary<string, object> dict, string path)
        {
            var segs = path.TrimStart('/').Split(new[]{'.','/'}, StringSplitOptions.RemoveEmptyEntries);
            object cur = dict;
            foreach (var seg in segs)
            {
                if (cur is Dictionary<string, object> d && d.TryGetValue(seg, out var nxt)) cur = nxt;
                else if (cur is List<object> l && int.TryParse(seg, out var idx) && idx < l.Count) cur = l[idx];
                else throw new KeyNotFoundException($"Path not found: {path}");
            }
            return cur;
        }

        public static void SetByPath(Dictionary<string, object> dict, string path, object value)
        {
            var segs = path.TrimStart('/').Split(new[]{'.','/'}, StringSplitOptions.RemoveEmptyEntries);
            object cur = dict;
            for (int i = 0; i < segs.Length - 1; i++)
            {
                var seg = segs[i];
                if (cur is Dictionary<string, object> d)
                {
                    if (!d.TryGetValue(seg, out var nxt) || nxt == null)
                    {
                        nxt = new Dictionary<string, object>(); d[seg] = nxt;
                    }
                    cur = nxt;
                }
                else throw new InvalidOperationException($"Cannot traverse path segment '{seg}'");
            }
            if (cur is Dictionary<string, object> final) final[segs.Last()] = value;
            else throw new InvalidOperationException($"Cannot set path on non-object at '{segs.Last()}'");
        }

        //--------------------------------------------------------------------------------
        // Cleanup & Conversion
        //--------------------------------------------------------------------------------

        public static void RemoveNulls(object obj)
        {
            switch (obj)
            {
                case Dictionary<string, object> d:
                    foreach (var key in d.Keys.ToList())
                    {
                        var v = d[key];
                        if (v == null) d.Remove(key);
                        else { RemoveNulls(v); if (v is IDictionary<string,object> dd && dd.Count==0) d.Remove(key); if (v is IList<object> ll && ll.Count==0) d.Remove(key); }
                    }
                    break;
                case List<object> l:
                    for (int i = l.Count -1; i>=0; i--)
                    {
                        var v = l[i];
                        if (v == null) l.RemoveAt(i);
                        else { RemoveNulls(v); if (v is IDictionary<string,object> dd2 && dd2.Count==0) l.RemoveAt(i); if (v is IList<object> ll2 && ll2.Count==0) l.RemoveAt(i); }
                    }
                    break;
            }
        }

        public static T ToObject<T>(Dictionary<string, object> dict)
        {
            var json = JsonConvert.SerializeObject(dict);
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static T DeepClone<T>(T obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static Dictionary<string, object> Flatten(Dictionary<string, object> source, string parentKey = "", char separator = '.')
        {
            var res = new Dictionary<string, object>();
            foreach (var kvp in source)
            {
                var key = string.IsNullOrEmpty(parentKey) ? kvp.Key : parentKey + separator + kvp.Key;
                if (kvp.Value is Dictionary<string, object> nd)
                    foreach (var n in Flatten(nd, key, separator)) res[n.Key] = n.Value;
                else res[key] = kvp.Value;
            }
            return res;
        }

        public static bool ContainsPath(Dictionary<string, object> dict, string path)
        {
            try { GetByPath(dict, path); return true; } catch { return false; }
        }

        //--------------------------------------------------------------------------------
        // Unity Editor & PlayerPrefs Helpers
        //--------------------------------------------------------------------------------

        #if UNITY_EDITOR
        /// <summary>Create a ScriptableObject from JSON (Editor Only).</summary>
        public static void CreateScriptableObjectFromJson<T>(string json, string assetPath) where T : ScriptableObject
        {
            var obj = ScriptableObject.CreateInstance<T>();
            JsonUtility.FromJsonOverwrite(json, obj);
            AssetDatabase.CreateAsset(obj, assetPath);
            AssetDatabase.SaveAssets();
        }
        #endif

        /// <summary>Save object as JSON to PlayerPrefs.</summary>
        public static void SaveToPrefs(string key, object obj)
        {
            PlayerPrefs.SetString(key, Serialize(obj));
            PlayerPrefs.Save();
        }

        /// <summary>Load JSON from PlayerPrefs to dictionary.</summary>
        public static Dictionary<string, object> LoadFromPrefs(string key)
        {
            return PlayerPrefs.HasKey(key)
                ? DeserializeToDictionary(PlayerPrefs.GetString(key))
                : null;
        }

        //--------------------------------------------------------------------------------
        // HTTP Requests (UnityWebRequest)
        //--------------------------------------------------------------------------------

        /// <summary>Send JSON payload via POST/PUT</summary>
        public static async Task<UnityWebRequestAsyncOperation> SendJsonAsync(
            string url,
            object payload,
            string method = UnityWebRequest.kHttpVerbPOST,
            Dictionary<string,string> headers = null)
        {
            string json = Serialize(payload);
            byte[] body = Encoding.UTF8.GetBytes(json);
            using var req = new UnityWebRequest(url, method)
            {
                uploadHandler = new UploadHandlerRaw(body),
                downloadHandler = new DownloadHandlerBuffer()
            };
            req.SetRequestHeader("Content-Type", "application/json");
            if (headers != null) foreach (var kvp in headers) req.SetRequestHeader(kvp.Key, kvp.Value);
            var op = req.SendWebRequest();
            #if UNITY_2020_1_OR_NEWER
            while (!op.isDone) await Task.Yield();
            #else
            await Task.Run(() => { while (!op.isDone) {} });
            #endif
            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogError($"SendJsonAsync Error: {req.error}\n{req.downloadHandler.text}");
            return op;
        }

        /// <summary>GET JSON and deserialize to T</summary>
        public static async Task<T> GetJsonAsync<T>(
            string url,
            Dictionary<string,string> headers = null)
        {
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Content-Type","application/json");
            if (headers != null) foreach (var kvp in headers) req.SetRequestHeader(kvp.Key, kvp.Value);
            var op = req.SendWebRequest();
            #if UNITY_2020_1_OR_NEWER
            while (!op.isDone) await Task.Yield();
            #else
            await Task.Run(() => { while (!op.isDone) {} });
            #endif
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"GetJsonAsync Error: {req.error}\n{req.downloadHandler.text}");
                return default;
            }
            return JsonConvert.DeserializeObject<T>(req.downloadHandler.text);
        }

        // //--------------------------------------------------------------------------------
        // // Firebase Firestore Integration
        // //--------------------------------------------------------------------------------

        // /// <summary>Initialize Firebase (call once on startup).</summary>
        // public static async Task InitializeFirebaseAsync()
        // {
        //     var status = await FirebaseApp.CheckAndFixDependenciesAsync().ConfigureAwait(false);
        //     if (status != DependencyStatus.Available)
        //         throw new Exception($"Firebase init failed: {status}");
        // }

        // /// <summary>Write any object to Firestore.</summary>
        // public static Task WriteToFirestoreAsync(string collection, string document, object data)
        // {
        //     var db = FirebaseFirestore.DefaultInstance;
        //     var json = Serialize(data);
        //     var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
        //     return db.Collection(collection).Document(document).SetAsync(dict);
        // }

        // /// <summary>Read a Firestore document as dictionary.</summary>
        // public static async Task<Dictionary<string, object>> ReadFromFirestoreAsync(string collection, string document)
        // {
        //     var db = FirebaseFirestore.DefaultInstance;
        //     var snap = await db.Collection(collection).Document(document).GetSnapshotAsync().ConfigureAwait(false);
        //     return snap.Exists ? snap.ToDictionary() : null;
        // }

        // /// <summary>Read entire Firestore collection.</summary>
        // public static async Task<List<Dictionary<string, object>>> ReadCollectionAsync(string collection)
        // {
        //     var db = FirebaseFirestore.DefaultInstance;
        //     var snap = await db.Collection(collection).GetSnapshotAsync().ConfigureAwait(false);
        //     return snap.Documents.Select(d => d.ToDictionary()).ToList();
        // }

        // /// <summary>Update single field in Firestore document.</summary>
        // public static Task UpdateFirestoreFieldAsync(
        //     string collection, string document, string fieldPath, object value)
        // {
        //     var db = FirebaseFirestore.DefaultInstance;
        //     return db.Collection(collection).Document(document).UpdateAsync(fieldPath, value);
        // }

        // //--------------------------------------------------------------------------------
        // // JSON Patch (RFC 6902) support - requires Marvin.JsonPatch
        // //--------------------------------------------------------------------------------
        // public static void ApplyJsonPatch<T>(ref T target, string patchJson)
        // {
        //     var patchDoc = JsonConvert.DeserializeObject<Marvin.JsonPatch.JsonPatchDocument<T>>(patchJson);
        //     patchDoc.ApplyTo(target);
        // }

        //--------------------------------------------------------------------------------
        // Versioning / Migration Hooks
        //--------------------------------------------------------------------------------
        private static readonly Dictionary<int, Func<Dictionary<string, object>, Dictionary<string, object>>> _migrations =
            new Dictionary<int, Func<Dictionary<string, object>, Dictionary<string, object>>>();

        public static void RegisterMigration(int version, Func<Dictionary<string, object>, Dictionary<string, object>> migrate)
            => _migrations[version] = migrate;

        public static Dictionary<string, object> Migrate(Dictionary<string, object> dict, int fromVersion, int toVersion)
        {
            for (int v = fromVersion; v < toVersion; v++)
                if (_migrations.TryGetValue(v, out var fn)) dict = fn(dict);
            return dict;
        }

        //--------------------------------------------------------------------------------
        // Custom Converter Registration
        //--------------------------------------------------------------------------------
        private static readonly List<JsonConverter> _customConverters = new List<JsonConverter>();
        public static void RegisterConverter(JsonConverter converter) => _customConverters.Add(converter);

        private static JsonSerializerSettings CreateSettings(bool prettyPrint)
            => new JsonSerializerSettings
            {
                Formatting = prettyPrint ? Formatting.Indented : Formatting.None,
                Converters = _customConverters.Concat(new JsonConverter[]{ new StringEnumConverter() }).ToList(),
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Include
            };

        //--------------------------------------------------------------------------------
        // Compression Helpers (GZIP)
        //--------------------------------------------------------------------------------
        public static byte[] CompressJson(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, CompressionMode.Compress)) gz.Write(bytes, 0, bytes.Length);
            return ms.ToArray();
        }

        public static string DecompressJson(byte[] compressed)
        {
            using var ms = new MemoryStream(compressed);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var sr = new StreamReader(gz);
            return sr.ReadToEnd();
        }

        //--------------------------------------------------------------------------------
        // Telemetry Hooks
        //--------------------------------------------------------------------------------
        public static event Action<string> OnOperationStart;
        public static event Action<string, int> OnOperationComplete;

        public static string SerializeWithEvents(object data, bool prettyPrint = false)
        {
            OnOperationStart?.Invoke(nameof(SerializeWithEvents));
            var json = Serialize(data, prettyPrint);
            OnOperationComplete?.Invoke(nameof(SerializeWithEvents), json.Length);
            return json;
        }
    }
}
