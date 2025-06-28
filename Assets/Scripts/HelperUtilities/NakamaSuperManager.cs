// NakamaSuperManager.cs
// A bulletproof, reusable Swiss-knife manager for Nakama in Unity C#
// Depends on: PrefsHelper, JsonHelper, EventHelper, SimpleStateMachine, FirestoreHelper

using GooglePlayGames;
using Nakama;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor.PackageManager;
using GooglePlayGames.BasicApi;
using UnityEngine;
using UnityHelperSDK.Data;
using UnityHelperSDK.DesignPatterns;
using UnityHelperSDK.Events;
using static System.Collections.Specialized.BitVector32;

public enum NakamaConnectionState { Disconnected, Connecting, Connected, Error }

public class NakamaSuperManager : MonoBehaviour
{
    public static NakamaSuperManager Instance { get; private set; }

    [Header("Nakama Settings")]
    public string scheme = "http";
    public string serverKey = "defaultkey";
    public string host = "127.0.0.1";
    public int port = 7350;
    public const string defaultLeaderboardId = "defaultLeaderboardId" ;
    public bool useSSL = false;

    private IClient _client;
    private ISession _session;
    private ISocket _socket;

    private readonly StateMachine<NakamaSuperManager, NakamaConnectionState> _stateMachine;
    private const GamePrefs NakamaSessionKey = GamePrefs.PlayerName;

    private NakamaSuperManager()
    {
        _stateMachine = new StateMachine<NakamaSuperManager, NakamaConnectionState>(this)
            .DefineState(NakamaConnectionState.Disconnected)
                .EndState()
            .DefineState(NakamaConnectionState.Connecting)
                .OnEnter(ctx => EventHelper.Trigger(new OnNakamaConnecting()))
                .EndState()
            .DefineState(NakamaConnectionState.Connected)
                .OnEnter(ctx => EventHelper.Trigger(new OnNakamaConnected()))
                .EndState()
            .DefineState(NakamaConnectionState.Error)
                .OnEnter(ctx => EventHelper.Trigger(new OnNakamaError()))
                .EndState();
        _stateMachine.SetInitialState(NakamaConnectionState.Disconnected);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _client = new Nakama.Client(scheme,host,port,serverKey);
    }

    private void Start()
    {
        var saved = PrefsHelper.Get<string, GamePrefs>(NakamaSessionKey, null);
        if (!string.IsNullOrEmpty(saved))
        {
            try { _session = JsonHelper.Deserialize<Session>(saved); }
            catch { }
            if (_session != null && !_session.IsExpired)
            {
                ConnectSocket();
                return;
            }
        }
    }

    #region Authentication
    public async Task AuthenticateDeviceAsync(string deviceId)
    {
        _stateMachine.TransitionTo(NakamaConnectionState.Connecting);
        try
        {
            _session = await _client.AuthenticateDeviceAsync(deviceId);
            SaveSession();
            _stateMachine.TransitionTo(NakamaConnectionState.Connected);
            ConnectSocket();
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            _stateMachine.TransitionTo(NakamaConnectionState.Error);
        }
    }

    public async Task AuthenticateEmailAsync(string email, string password)
    {
        _stateMachine.TransitionTo(NakamaConnectionState.Connecting);
        try
        {
            _session = await _client.AuthenticateEmailAsync(email, password, create: true);
            SaveSession();
            _stateMachine.TransitionTo(NakamaConnectionState.Connected);
            ConnectSocket();
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            _stateMachine.TransitionTo(NakamaConnectionState.Error);
        }
    }

    /// <summary>
    /// Authenticate using Google Play Games Services.
    /// Requires Google Play Games plugin setup.
    /// </summary>
#if UNITY_ANDROID
    public async Task AuthenticateWithGooglePlayGamesAsync()
    {
        _stateMachine.TransitionTo(NakamaConnectionState.Connecting);
        try
        {
            // Authenticate local Google Play user
            //var tcs = new TaskCompletionSource<bool>();
            PlayGamesPlatform.Instance.Authenticate(success =>
            {
                if (success == SignInStatus.Success)
                {
                    Debug.Log("GPG authentication succeeded");
                }
                else
                {
                    throw new Exception("Google Play Games authentication failed");
                }
            });

            // Retrieve auth code or ID token
            string serverAuthCode = String.Empty;
            PlayGamesPlatform.Instance.RequestServerSideAccess(false, authCode =>
            {
                if (!String.IsNullOrEmpty(authCode)) {
                    serverAuthCode = authCode;
                } else {
                    serverAuthCode = null;
                }
            });
            // Exchange code for a Firebase custom token or directly use as OAuth
            _session = await _client.AuthenticateCustomAsync(serverAuthCode);
            SaveSession();
            _stateMachine.TransitionTo(NakamaConnectionState.Connected);
            ConnectSocket();
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            _stateMachine.TransitionTo(NakamaConnectionState.Error);
        }
    }
#endif

    /// <summary>
    /// Authenticate using Firebase custom token.
    /// </summary>
    public async Task AuthenticateWithFirebaseCustomTokenAsync(string customToken)
    {
        _stateMachine.TransitionTo(NakamaConnectionState.Connecting);
        try
        {
            _session = await _client.AuthenticateCustomAsync(customToken);
            SaveSession();
            _stateMachine.TransitionTo(NakamaConnectionState.Connected);
            ConnectSocket();
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            _stateMachine.TransitionTo(NakamaConnectionState.Error);
        }
    }
    private void SaveSession()
    {
        var json = JsonHelper.Serialize(_session);
        PrefsHelper.Set<GamePrefs>(NakamaSessionKey, json);
    }
    #endregion

    #region Cloud Sync with Firestore

    /// <summary>
    /// Sync leaderboard metadata to Firestore. Each record's metadata is stored as a document.
    /// </summary>
    public async Task<bool> SyncLeaderboardMetadataToFirestoreAsync(string collection, string leaderboardId)
    {
        try
        {
            var records = await GetLeaderboardAsync(leaderboardId);
            var tasks = new List<Task<bool>>();
            foreach (var rec in records)
            {
                string docId = $"{leaderboardId}_{rec.Username}";
                tasks.Add(FirebaseHelper.SetDocumentAsync(collection, docId, rec.Metadata));
            }
            var results = await Task.WhenAll(tasks);
            return Array.TrueForAll(results, ok => ok);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error syncing leaderboard metadata to Firestore: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Retrieve leaderboard metadata from Firestore.
    /// </summary>
    public async Task<Dictionary<string, Dictionary<string, object>>> GetLeaderboardMetadataFromFirestoreAsync(string collection, string leaderboardId, List<string> userIds)
    {
        var result = new Dictionary<string, Dictionary<string, object>>();
        var tasks = new List<Task>();
        foreach (var userId in userIds)
        {
            string docId = $"{leaderboardId}_{userId}";
            tasks.Add(FirebaseHelper.GetDocumentAsync<Dictionary<string, object>>(collection, docId)
                .ContinueWith(t => {
                    if (t.Result != null)
                        result[userId] = t.Result;
                }));
        }
        await Task.WhenAll(tasks);
        return result;
    }
    #endregion

    #region Socket Management
    private void ConnectSocket()
    {
        _socket = _client.NewSocket();
        _socket.Connected += () => Debug.Log("Socket connected");
        _socket.Closed += () => Debug.Log("Socket closed");
        _ = _socket.ConnectAsync(_session);
    }

    public async Task DisconnectAsync()
    {
        if (_socket != null) await _socket.CloseAsync();
        _stateMachine.TransitionTo(NakamaConnectionState.Disconnected);
    }
    #endregion

    #region RPC & Storage
    public async Task<IApiAccount> GetAccountAsync() => await _client.GetAccountAsync(_session);

    public async Task<string> RPCAsync(string id, string payload)
    {
        var result = await _client.RpcAsync(_session, id, payload);
        return result.Payload;
    }

    public async Task WriteStorageAsync(string collection, string key, string jsonData)
    {
        WriteStorageObject storageObject = new WriteStorageObject
        {
            Collection = collection,
            Key = key,
            Value = jsonData
        };
        var record = new[] { storageObject };
        await _client.WriteStorageObjectsAsync(_session, record);
    }

    public async Task<IApiStorageObjects> ReadStorageAsync(string collection, string key)
    {
        var objectId = new StorageObjectId
        {
            Collection = collection,
            Key = key,
            UserId = _session.UserId  // Important: Specify user ownership
        };
        var list = await _client.ReadStorageObjectsAsync(_session, new[] { objectId });
        return list;
    }
    #endregion

    #region Leaderboard with Metadata
    public async Task<List<IApiLeaderboardRecord>> GetLeaderboardAsync(string leaderboardId = defaultLeaderboardId , int limit = 10)
    {
        var records = await _client.ListLeaderboardRecordsAsync(_session, leaderboardId, limit: limit);
        return records.Records.ToList(); // each record.Metadata contains key-value pairs
    }

    /// <summary>
    /// Record a score with optional metadata (e.g., player level, timestamp, custom data)
    /// </summary>
    public async Task RecordScoreAsync(string leaderboardId, long score, IDictionary<string, string> metadata = null)
    {
        string serializedMetaData = JsonHelper.Serialize(metadata);
        await _client.WriteLeaderboardRecordAsync(
            _session,
            leaderboardId,
            score,
            metadata: serializedMetaData
        );
    }

    public async Task<IApiLeaderboardRecord> GetUserLeaderboardRecordAsync(string leaderboardId)
    {
        try
        {
            // Request the leaderboard record for the current user
            var records = await _client.ListLeaderboardRecordsAsync(
                _session,
                leaderboardId,
                ownerIds: new[] { _session.UserId },
                limit: 1
            );

            // If the user has a record, return it
            if (records.OwnerRecords != null && records.OwnerRecords.Count() > 0)
            {
                var userRecord = records.OwnerRecords.FirstOrDefault();
                Debug.Log($"Rank: {userRecord.Rank}, Score: {userRecord.Score}, Metadata: {userRecord.Metadata}");
                return userRecord;
            }
            else
            {
                Debug.Log("User does not have a leaderboard record.");
                return null;
            }
        }
        catch (ApiResponseException ex)
        {
            Debug.LogError($"Failed to get leaderboard record: {ex.Message}");
            return null;
        }
    }

    #endregion

    private void Update() => _stateMachine.Update();
}

#region Events
public struct OnNakamaConnecting { }
public struct OnNakamaConnected { }
public struct OnNakamaError { }
#endregion
