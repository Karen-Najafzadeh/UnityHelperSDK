using Firebase.Auth;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using System;
using System.Net;
using System.Threading.Tasks;
using UnityEngine;
using UnityHelperSDK.Data;

/// <summary>
/// Google Play Games Service authentication helper
/// Handles GPGS sign-in, Firebase authentication, and session management
/// Integrates with PrefsHelper for token persistence
/// </summary>
public static class GPGSAuthHelper
{
    // Authentication states
    //public enum AuthState { SignedOut, Authenticating, SignedIn, Error }

    // Events
    //public static event Action<AuthState> OnAuthStateChanged;
    //public static event Action<string> OnUserAuthenticated;
    //public static event Action<string> OnAuthError;

    // Configuration
    private static string GPGS_TOKEN_KEY = "GPGS_AuthToken";
    private static string FIREBASE_USER_KEY = "FirebaseUserID";
    private const int TOKEN_REFRESH_THRESHOLD = 300; // 5 minutes before expiration

    // Current state
    //public static AuthState CurrentState { get; private set; } = AuthState.SignedOut;
    public static string UserId { get; private set; }
    public static string DisplayName { get; private set; }

    #region Authentication Flow

    /// <summary>
    /// Main authentication method
    /// </summary>
    public static async Task<bool> Authenticate(bool silentOnly = false)
    {
        //UpdateAuthState(AuthState.Authenticating);

        try
        {
            return await SignInWithGPGS();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Authentication failed: {ex.Message}");
            //UpdateAuthState(AuthState.Error, ex.Message);
            return false;
        }
    }


    private static async Task<bool> SignInWithGPGS()
    {

        bool success = false;
        //var authTask = new TaskCompletionSource<SignInStatus>();
        PlayGamesPlatform.Instance.Authenticate( async (result) =>
        {
            //authTask.TrySetResult(result);
            if(result == SignInStatus.Success)
            {
                // Successful sign-in
                //authTask.TrySetResult(result);
                success =  await HandleAuthResult(result);

            }
            else
            {
                // Handle error or cancellation
                Debug.LogError($"GPGS sign-in failed: {result}");
                success = false;
                //authTask.TrySetResult(result);
            }
        });

        //SignInStatus result = await authTask.Task;
        return success;
    }

    private static async Task<bool> HandleAuthResult(SignInStatus result)
    {
        bool success = false;
        if (result != SignInStatus.Success)
        {
            //UpdateAuthState(AuthState.Error, $"GPGS sign-in failed: {result}");
            return false;
        }

        // Get server auth code
        PlayGamesPlatform.Instance.RequestServerSideAccess(false, async authCode =>
        {
            if (string.IsNullOrEmpty(authCode))
            {
                //UpdateAuthState(AuthState.Error, "Failed to get server auth code");
                success = false;
            }
            else
            {
                success = await SignInWithFirebase(authCode);
            }
        });


        // Authenticate with Firebase
        return success;
    }

    private static async Task<bool> SignInWithFirebase(string authCode)
    {
        // Initialize Firebase if needed
        if (!await FirebaseHelper.InitializeAsync())
        {
            //UpdateAuthState(AuthState.Error, "Firebase initialization failed");
            return false;
        }

        try
        {
            // Create Firebase credential
            Credential credential = PlayGamesAuthProvider.GetCredential(authCode);

            // Sign in to Firebase
            FirebaseUser user = await FirebaseAuth.DefaultInstance
                .SignInWithCredentialAsync(credential);

            // Handle successful authentication
            UserId = user.UserId;
            DisplayName = user.DisplayName;

            // Store tokens
            PrefsHelper.Set(AuthPrefs.GPGSToken, authCode);
            PrefsHelper.Set(AuthPrefs.FirebaseUserID, user.UserId);

            //UpdateAuthState(AuthState.SignedIn);
            //OnUserAuthenticated?.Invoke(user.UserId);

            Debug.Log($"Authenticated: {user.UserId}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Firebase authentication failed: {ex.Message}");
            //UpdateAuthState(AuthState.Error, ex.Message);
            return false;
        }
    }

    #endregion

    #region Bulk Operations

    public static async Task<bool> OverrideDataFromFirestore()
    {
        bool success = false;
        try
        {
            Type[] allEnumPrefs = EnumScanner.GetAllEnumsInNamespace(); // Ensure enums are scanned
            foreach (Type type in allEnumPrefs)
            {
                await PrefsHelper.SyncFromCloud<GamePrefs>();
            }
            success = true;

        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to sync data from Firestore: {ex.Message}");
            // Handle error
            success = false;
        }
        return success;
    }

}

// Extend authentication preferences
public enum AuthPrefs
{
    [Type("string")] GPGSToken,
    [Type("string")] FirebaseUserID,
    [Type("string")] LastTokenRefresh
}
#endregion