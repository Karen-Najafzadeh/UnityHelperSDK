using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Firebase;
using Firebase.Extensions;
using Firebase.Firestore;
using System.Linq;
using UnityHelperSDK.HelperUtilities;

/// <summary>
/// A comprehensive Firebase helper class that integrates Firestore operations with JsonHelper and NetworkRequestManager.
/// Provides easy-to-use static methods for common Firestore operations, real-time updates, batch operations,
/// and data conversion utilities.
/// </summary>

namespace UnityHelperSDK.Data{

    public static class FirebaseHelper
    {
        private static FirebaseFirestore _firestoreInstance;
        private static bool _isInitialized;
        private static readonly Dictionary<string, ListenerRegistration> _activeListeners 
            = new Dictionary<string, ListenerRegistration>();

        /// <summary>
        /// Initialize Firebase and Firestore. Call this before using any other methods.
        /// </summary>
        public static async Task<bool> InitializeAsync()
        {
            if (_isInitialized) return true;

            try
            {
                // Initialize Firebase
                var app = await FirebaseApp.CheckAndFixDependenciesAsync();
                if (app != DependencyStatus.Available)
                {
                    Debug.LogError("Could not resolve Firebase dependencies");
                    return false;
                }

                // Get Firestore instance
                _firestoreInstance = FirebaseFirestore.DefaultInstance;
                _isInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Firebase initialization failed: {ex.Message}");
                return false;
            }
        }

        #region Document Operations

        /// <summary>
        /// Create or update a document with automatic JSON serialization.
        /// </summary>
        public static async Task<bool> SetDocumentAsync<T>(string collection, string documentId, T data)
        {
            if (!await EnsureInitialized()) return false;

            try
            {
                // Convert to Dictionary using JsonHelper
                string json = JsonHelper.Serialize(data);
                Dictionary<string, object> dict = JsonHelper.DeserializeToDictionary(json);
                
                await _firestoreInstance
                    .Collection(collection)
                    .Document(documentId)
                    .SetAsync(dict);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error setting document: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Retrieve a document and deserialize to type T.
        /// </summary>
        public static async Task<T> GetDocumentAsync<T>(string collection, string documentId)
        {
            if (!await EnsureInitialized()) return default;

            try
            {
                DocumentSnapshot snapshot = await _firestoreInstance
                    .Collection(collection)
                    .Document(documentId)
                    .GetSnapshotAsync();

                if (!snapshot.Exists) return default;

                // Convert Dictionary to JSON then to type T
                string json = JsonHelper.Serialize(snapshot.ToDictionary());
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error getting document: {ex.Message}");
                return default;
            }
        }

        /// <summary>
        /// Update specific fields in a document.
        /// </summary>
        public static async Task<bool> UpdateDocumentAsync(string collection, string documentId, 
            Dictionary<string, object> updates)
        {
            if (!await EnsureInitialized()) return false;

            try
            {
                await _firestoreInstance
                    .Collection(collection)
                    .Document(documentId)
                    .UpdateAsync(updates);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error updating document: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Delete a document from Firestore.
        /// </summary>
        public static async Task<bool> DeleteDocumentAsync(string collection, string documentId)
        {
            if (!await EnsureInitialized()) return false;

            try
            {
                await _firestoreInstance
                    .Collection(collection)
                    .Document(documentId)
                    .DeleteAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error deleting document: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Query Operations

        /// <summary>
        /// Query documents with conditions and return as List<T>.
        /// </summary>
        public static async Task<List<T>> QueryDocumentsAsync<T>(string collection, 
            List<QueryCondition> conditions = null, int limit = 0)
        {
            if (!await EnsureInitialized()) return new List<T>();

            try
            {
                Query query = _firestoreInstance.Collection(collection);

                // Apply conditions if any
                if (conditions != null)
                {
                    foreach (var condition in conditions)
                    {
                        query = condition.ApplyToQuery(query);
                    }
                }

                // Apply limit if specified
                if (limit > 0) query = query.Limit(limit);

                // Execute query
                QuerySnapshot querySnapshot = await query.GetSnapshotAsync();
                
                // Convert results
                List<T> results = new List<T>();
                foreach (DocumentSnapshot doc in querySnapshot.Documents)
                {
                    string json = JsonHelper.Serialize(doc.ToDictionary());
                    T item = JsonUtility.FromJson<T>(json);
                    results.Add(item);
                }

                return results;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error querying documents: {ex.Message}");
                return new List<T>();
            }
        }

        #endregion

        #region Real-time Listeners

        /// <summary>
        /// Set up a real-time listener for a document.
        /// </summary>
        public static void ListenToDocument<T>(string collection, string documentId, 
            Action<T> onUpdate, Action<Exception> onError = null)
        {
            if (!_isInitialized)
            {
                Debug.LogError("Firebase not initialized");
                return;
            }

            string listenerKey = $"{collection}/{documentId}";
            
            // Remove existing listener if any
            RemoveListener(listenerKey);

            try
            {
                var listener = _firestoreInstance
                    .Collection(collection)
                    .Document(documentId)
                    .Listen(snapshot =>
                    {
                        if (snapshot.Exists)
                        {
                            string json = JsonHelper.Serialize(snapshot.ToDictionary());
                            T data = JsonUtility.FromJson<T>(json);
                            onUpdate?.Invoke(data);
                        }
                    });

                _activeListeners[listenerKey] = listener;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error setting up listener: {ex.Message}");
                onError?.Invoke(ex);
            }
        }

        /// <summary>
        /// Remove a specific real-time listener.
        /// </summary>
        public static void RemoveListener(string listenerKey)
        {
            if (_activeListeners.TryGetValue(listenerKey, out var listener))
            {
                listener.Stop();
                _activeListeners.Remove(listenerKey);
            }
        }

        /// <summary>
        /// Remove all active real-time listeners.
        /// </summary>
        public static void RemoveAllListeners()
        {
            foreach (var listener in _activeListeners.Values)
            {
                listener.Stop();
            }
            _activeListeners.Clear();
        }

        #endregion

        #region Batch Operations

        /// <summary>
        /// Perform multiple write operations in a batch.
        /// </summary>
        public static async Task<bool> ExecuteBatchAsync(List<BatchOperation> operations)
        {
            if (!await EnsureInitialized()) return false;

            try
            {
                WriteBatch batch = _firestoreInstance.StartBatch();

                foreach (var operation in operations)
                {
                    DocumentReference docRef = _firestoreInstance
                        .Collection(operation.Collection)
                        .Document(operation.DocumentId);

                    switch (operation.Type)
                    {
                        case BatchOperationType.Set:
                            batch.Set(docRef, operation.Data);
                            break;
                        case BatchOperationType.Update:
                            batch.Update(docRef, operation.Data);
                            break;
                        case BatchOperationType.Delete:
                            batch.Delete(docRef);
                            break;
                    }
                }

                await batch.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error executing batch: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Helper Types and Methods

        /// <summary>
        /// Query condition for filtering Firestore queries.
        /// </summary>
        public class QueryCondition
        {
            public string Field { get; set; }
            public QueryOperator Operator { get; set; }
            public object Value { get; set; }

            public Query ApplyToQuery(Query query)
            {
                return Operator switch
                {
                    QueryOperator.EqualTo => query.WhereEqualTo(Field, Value),
                    QueryOperator.LessThan => query.WhereLessThan(Field, Value),
                    QueryOperator.LessThanOrEqualTo => query.WhereLessThanOrEqualTo(Field, Value),
                    QueryOperator.GreaterThan => query.WhereGreaterThan(Field, Value),
                    QueryOperator.GreaterThanOrEqualTo => query.WhereGreaterThanOrEqualTo(Field, Value),
                    QueryOperator.ArrayContains => query.WhereArrayContains(Field, Value),
                    _ => query
                };
            }
        }

        public enum QueryOperator
        {
            EqualTo,
            LessThan,
            LessThanOrEqualTo,
            GreaterThan,
            GreaterThanOrEqualTo,
            ArrayContains
        }

        public class BatchOperation
        {
            public string Collection { get; set; }
            public string DocumentId { get; set; }
            public Dictionary<string, object> Data { get; set; }
            public BatchOperationType Type { get; set; }
        }

        public enum BatchOperationType
        {
            Set,
            Update,
            Delete
        }

        private static async Task<bool> EnsureInitialized()
        {
            if (!_isInitialized)
            {
                return await InitializeAsync();
            }
            return true;
        }

        #endregion

        #region Integration with NetworkRequestManager

        /// <summary>
        /// Upload a file to Firebase Storage and store its metadata in Firestore.
        /// </summary>
        public static async Task<bool> UploadFileWithMetadataAsync(
            string fileUrl, 
            string destinationPath,
            string collection,
            string documentId,
            Dictionary<string, object> additionalMetadata = null)
        {
            try
            {
                // Download file using NetworkRequestManager
                byte[] fileData = await NetworkRequestManager.GetBytesAsync(fileUrl);
                
                // Upload to Firebase Storage (assuming you have Storage initialized)
                // Note: You'll need to add Firebase Storage implementation here
                
                // Store metadata in Firestore
                Dictionary<string, object> metadata = new Dictionary<string, object>
                {
                    { "path", destinationPath },
                    { "uploadedAt", DateTime.UtcNow },
                    { "size", fileData.Length }
                };

                if (additionalMetadata != null)
                {
                    foreach (var kvp in additionalMetadata)
                    {
                        metadata[kvp.Key] = kvp.Value;
                    }
                }

                return await SetDocumentAsync(collection, documentId, metadata);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error uploading file with metadata: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Synchronize remote JSON data with Firestore document.
        /// </summary>
        public static async Task<bool> SyncRemoteJsonWithFirestoreAsync(
            string jsonUrl,
            string collection,
            string documentId)
        {
            try
            {
                // Fetch JSON using NetworkRequestManager
                string jsonData = await NetworkRequestManager.GetStringAsync(jsonUrl);
                
                // Parse JSON using JsonHelper
                Dictionary<string, object> data = JsonHelper.DeserializeToDictionary(jsonData);
                
                // Update Firestore
                return await SetDocumentAsync(collection, documentId, data);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error syncing remote JSON with Firestore: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}