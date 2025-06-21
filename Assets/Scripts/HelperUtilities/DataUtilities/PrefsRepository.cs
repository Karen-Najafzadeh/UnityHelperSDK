using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityHelperSDK.Data{


    # region Enums
    /// <summary>
    /// Attribute to specify the type of a preference key
    /// </summary>
    public class TypeAttribute : Attribute
    {
        public string Type { get; }
        public TypeAttribute(string type) => Type = type;
    }


    /// <summary>
    /// Enum representing various game preferences with associated types.
    /// Each enum value corresponds to a specific preference key,
    /// and the Type attribute specifies the data type of that preference.
    /// This enum can be used to manage game settings, player data, and other preferences
    /// in a structured way.
    /// The Type attribute allows for easy serialization and deserialization of preferences,
    /// making it suitable for saving and loading game state.
    /// Feel free to extend this enum with additional preferences as needed.
    /// Or even create new enums for different categories of preferences.
    /// in this way you can have a more organized structure
    /// </summary>
    public enum GamePrefs
    {
        [Type("int")] Score,
        [Type("int")] InterstitialCount,
        [Type("int")] RewardedAdCount,
        [Type("string")] PlayerName,
        [Type("bool")] IsTutorialComplete,
        [Type("vector3")] LastPosition,
        [Type("color")] UIColor,
        [Type("string")] AdsDateTimeComplexData, // For JSON-serialized objects
        [Type("string")] PurchaseHistoryComplexData, // For JSON-serialized objects
        [Type("string")] DailyRewardLastClaimed,
        [Type("int")] DailyRewardStreak,
    }
    #endregion
}