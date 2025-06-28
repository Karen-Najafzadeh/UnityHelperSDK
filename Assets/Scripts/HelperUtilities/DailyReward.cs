using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityHelperSDK.Data;
using UnityHelperSDK.Events;

namespace UnityHelperSDK.Rewards
{
    [CreateAssetMenu(fileName = "DailyRewardDefinition", menuName = "Game/Rewards/Daily Reward Definition", order = 0)]
    public class DailyRewardDefinitionSO : ScriptableObject
    {
        public List<RewardDay> Days;
    }

    [Serializable]
    public class RewardDay
    {
        public int DayNumber;
        public List<RewardItem> Rewards;
    }

    [Serializable]
    public class RewardItem
    {
        public string RewardId;
        public int Quantity;
    }

    public class DailyRewardManager : MonoBehaviour
    {
        [SerializeField] private DailyRewardDefinitionSO rewardDefinition;

        public int CurrentStreak => PrefsHelper.Get<int, GamePrefs>(GamePrefs.DailyRewardStreak, 0);

        public bool CanClaimToday()
        {
            var lastClaim = DateTime.Parse(PrefsHelper.Get<string, GamePrefs>(GamePrefs.DailyRewardLastClaimed, DateTime.MinValue.ToString()));
            return DateTime.UtcNow.Date > lastClaim.Date;
        }

        public RewardDay GetTodayReward()
        {
            int index = Mathf.Min(CurrentStreak, rewardDefinition.Days.Count - 1);
            return rewardDefinition.Days[index];
        }

        public void ClaimTodayReward()
        {
            if (!CanClaimToday())
            {
                Debug.LogWarning("Daily reward already claimed today.");
                return;
            }

            var reward = GetTodayReward();
            PrefsHelper.Set<GamePrefs>(GamePrefs.DailyRewardLastClaimed, DateTime.UtcNow.ToString());
            PrefsHelper.Set<GamePrefs>(GamePrefs.DailyRewardStreak , CurrentStreak + 1);

            foreach (var item in reward.Rewards)
            {
                Debug.Log($"Granting reward: {item.RewardId} x{item.Quantity}");
                EventHelper.Trigger(new OnRewardGranted
                {
                    RewardId = item.RewardId,
                    Quantity = item.Quantity
                });
            }
        }
    }

    public struct OnRewardGranted
    {
        public string RewardId;
        public int Quantity;
    }
}
