using UnityEngine;
using UnityEngine.SceneManagement;
using System;

namespace UnityHelperSDK.Events
{

    /// <summary>
    /// Static class containing definitions for various game events.
    /// Each event is represented as a struct with relevant data fields.
    /// Events are categorized for better organization and management.
    /// Feel free to extend this class with additional events as needed.
    /// For more examples, refer to the TutorialEvents.cs file.
    /// </summary>
    
    #region Event Categories
    public static class EventCategories
    {
        public const string System = "System";
        public const string Game = "Game";
        public const string UI = "UI";
        public const string Player = "Player";
        public const string Tutorial = "Tutorial";
        public const string Audio = "Audio";
        public const string Scene = "Scene";

        // Add more categories as needed
    }
#endregion
#region Event Definitions
    // System Events

    // simple event with no data
    public struct OnGameInitialized { }

    // a more complex event with multiple data fields
    public struct OnPlayerHealthChanged
    {
        public float currentHealth;
        public float maxHealth;
        public float damage;
        public GameObject source;
    }

    // A complex event representing a multiplayer match result
    public struct OnMultiplayerMatchEnded
    {
        public string matchId;
        public DateTime endTime;
        public string[] playerIds;
        public int[] playerScores;
        public string winnerPlayerId;
        public bool wasAborted;
        public string[] disconnectedPlayerIds;
        public string mapName;
        public TimeSpan matchDuration;
        public string gameMode;
        public string[] teamIds;
        public int[] teamScores;
        public string[] achievementsUnlocked;
        public string replayFilePath;
    }

    // add as much events as you want! these above are just examples.
    // you can create your own events by creating a struct with the required data
}
#endregion
