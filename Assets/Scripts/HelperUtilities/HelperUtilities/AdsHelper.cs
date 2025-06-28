using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityHelperSDK.Data;
#if UNITY_ADS
using UnityEngine.Advertisements;
#endif
#if ADMOB
using GoogleMobileAds;
using GoogleMobileAds.Api;
#endif
#if APPLOVIN
using AppLovinMax;
#endif

namespace UnityHelperSDK.HelperUtilities{


    /// <summary>
    /// AdsHelper: Singleton manager for multiple ad networks (Unity Ads, AdMob, AppLovin)
    /// Provides a unified API to load and show rewarded, interstitial, and banner ads.
    /// </summary>
    public class AdsHelper : MonoBehaviour
    {
        public static AdsHelper Instance { get; private set; }

        [Header("Ad Network Selection")]
        public AdNetwork primaryNetwork = AdNetwork.UnityAds;
        public bool enableAdTracking = true;
        public float interstitialCooldown = 30f; // Seconds between interstitial ads
        public float rewardedCooldown = 30f; // Seconds between rewarded ads
        public int maxDailyInterstitials = 10; // Max interstitials per day
        public int maxDailyRewarded = 5; // Max rewarded ads per day

        [Header("Unity Ads Settings")]
        public string unityGameId = "YOUR_UNITY_GAME_ID";
        public bool testMode = true;

        [Header("AdMob Settings")]
        public string admobAppId = "YOUR_ADMOB_APP_ID";
        public string admobInterstitialId = "YOUR_ADMOB_INTERSTITIAL_ID";
        public string admobRewardedId = "YOUR_ADMOB_REWARDED_ID";
        public string admobBannerId = "YOUR_ADMOB_BANNER_ID";

        [Header("AppLovin Settings")]
        public string appLovinSdkKey = "YOUR_APPLOVIN_SDK_KEY";
        public string appLovinInterstitialId = "YOUR_APPLOVIN_INTERSTITIAL_ID";
        public string appLovinRewardedId = "YOUR_APPLOVIN_REWARDED_ID";
        public string appLovinBannerId = "YOUR_APPLOVIN_BANNER_ID";

        public enum AdNetwork { UnityAds, AdMob, AppLovin }

        public enum BannerPosition
        {
            TOP_LEFT, TOP_CENTER, TOP_RIGHT,
            BOTTOM_LEFT, BOTTOM_CENTER, BOTTOM_RIGHT,
            CENTER
        }

        // Events
        public event Action OnInterstitialLoaded;
        public event Action<string> OnInterstitialFailed;
        public event Action OnRewardedLoaded;
        public event Action<string> OnRewardedFailed;
        public event Action OnRewardedCompleted;
        public event Action OnBannerLoaded;
        public event Action<string> OnBannerFailed;

        // Internal references
    #if ADMOB
        private InterstitialAd _admobInterstitial;
        private RewardedAd _admobRewarded;
        private BannerView _admobBanner;
    #endif

        // Ad tracking
        private const string LAST_INTERSTITIAL_KEY = "last_interstitial_time";
        private const string LAST_REWARDED_KEY = "last_rewarded_time";
        private const string DAILY_INTERSTITIAL_COUNT = "daily_interstitial_count";
        private const string DAILY_REWARDED_COUNT = "daily_rewarded_count";
        private const string LAST_COUNT_RESET_DATE = "last_count_reset_date";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeAds();
            ResetDailyCountsIfNeeded();
        }

        /// <summary>
        /// Initialize all configured ad networks
        /// </summary>
        public void InitializeAds()
        {
    #if UNITY_ADS
            Advertisement.Initialize(unityGameId, testMode);
    #endif

    #if ADMOB
            MobileAds.Initialize(status => {
                Debug.Log("AdMob Initialized");
                LoadInterstitial();
                LoadRewarded();
            });
    #endif

    #if APPLOVIN
            MaxSdkCallbacks.OnSdkInitializedEvent += (MaxSdkBase.SdkConfiguration sdkConfiguration) =>
            {
                Debug.Log("AppLovin Initialized");
                InitializeAppLovinAds();
            };
            MaxSdk.SetSdkKey(appLovinSdkKey);
            MaxSdk.InitializeSdk();
    #endif
        }

    #if APPLOVIN
        private void InitializeAppLovinAds()
        {
            // Initialize AppLovin Interstitial
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += (_, _) => OnInterstitialLoaded?.Invoke();
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += (_, error) => OnInterstitialFailed?.Invoke(error.Message);
            MaxSdk.LoadInterstitial(appLovinInterstitialId);

            // Initialize AppLovin Rewarded
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += (_, _) => OnRewardedLoaded?.Invoke();
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += (_, error) => OnRewardedFailed?.Invoke(error.Message);
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += (_, _, _) => OnRewardedCompleted?.Invoke();
            MaxSdk.LoadRewardedAd(appLovinRewardedId);
        }
    #endif

        #region Ad Tracking & Limits

        private void ResetDailyCountsIfNeeded()
        {
            var lastResetDate = PrefsHelper.Get<DateTime, GamePrefs>(GamePrefs.AdsDateTimeComplexData, DateTime.MinValue);
            if (lastResetDate.Date != DateTime.Today)
            {
                PrefsHelper.Set(GamePrefs.InterstitialCount, 0); // Reset interstitial count
                PrefsHelper.Set(GamePrefs.RewardedAdCount, 0); // Reset rewarded count
                PrefsHelper.SetJson(GamePrefs.AdsDateTimeComplexData, DateTime.Today);
            }
        }

        private bool CanShowInterstitial()
        {
            if (!enableAdTracking) return true;

            var lastShowTime = PrefsHelper.Get<DateTime, GamePrefs>(GamePrefs.AdsDateTimeComplexData, DateTime.MinValue);
            var dailyCount = PrefsHelper.Get<int, GamePrefs>(GamePrefs.InterstitialCount, 0);

            if (dailyCount >= maxDailyInterstitials)
            {
                Debug.Log("Daily interstitial limit reached");
                return false;
            }

            if ((DateTime.Now - lastShowTime).TotalSeconds < interstitialCooldown)
            {
                Debug.Log("Interstitial cooldown still active");
                return false;
            }

            return true;
        }

        private bool CanShowRewarded()
        {
            if (!enableAdTracking) return true;

            var lastShowTime = PrefsHelper.Get<DateTime, GamePrefs>(GamePrefs.AdsDateTimeComplexData, DateTime.MinValue);
            var dailyCount = PrefsHelper.Get<int, GamePrefs>(GamePrefs.RewardedAdCount, 0);

            if (dailyCount >= maxDailyRewarded)
            {
                Debug.Log("Daily rewarded limit reached");
                return false;
            }

            if ((DateTime.Now - lastShowTime).TotalSeconds < rewardedCooldown)
            {
                Debug.Log("Rewarded cooldown still active");
                return false;
            }

            return true;
        }

        private void TrackInterstitialShown()
        {
            if (!enableAdTracking) return;

            var count = PrefsHelper.Get<int, GamePrefs>(GamePrefs.InterstitialCount, 0);
            PrefsHelper.Set(GamePrefs.InterstitialCount, count + 1);
            PrefsHelper.SetJson(GamePrefs.AdsDateTimeComplexData, DateTime.Now);
        }

        private void TrackRewardedShown()
        {
            if (!enableAdTracking) return;

            var count = PrefsHelper.Get<int, GamePrefs>(GamePrefs.RewardedAdCount, 0);
            PrefsHelper.Set(GamePrefs.RewardedAdCount, count + 1);
            PrefsHelper.SetJson(GamePrefs.AdsDateTimeComplexData, DateTime.Now);
        }

        #endregion

        #region Interstitial Ads

        /// <summary>
        /// Load an interstitial ad
        /// </summary>
        public void LoadInterstitial()
        {
            switch (primaryNetwork)
            {
                case AdNetwork.UnityAds:
    #if UNITY_ADS
                    if (!Advertisement.IsReady())
                    {
                        OnInterstitialLoaded?.Invoke();
                    }
    #endif
                    break;

                case AdNetwork.AdMob:
    #if ADMOB
                    _admobInterstitial?.Destroy();
                    _admobInterstitial = new InterstitialAd(admobInterstitialId);
                    _admobInterstitial.OnAdLoaded += (_, __) => OnInterstitialLoaded?.Invoke();
                    _admobInterstitial.OnAdFailedToLoad += (_, e) => OnInterstitialFailed?.Invoke(e.LoadAdError.GetMessage());
                    _admobInterstitial.LoadAd(new AdRequest.Builder().Build());
    #endif
                    break;

                case AdNetwork.AppLovin:
    #if APPLOVIN
                    MaxSdk.LoadInterstitial(appLovinInterstitialId);
    #endif
                    break;
            }
        }

        /// <summary>
        /// Show an interstitial ad if available and conditions are met
        /// </summary>
        public void ShowInterstitial()
        {
            if (!CanShowInterstitial()) return;

            switch (primaryNetwork)
            {
                case AdNetwork.UnityAds:
    #if UNITY_ADS
                    if (Advertisement.IsReady())
                    {
                        Advertisement.Show();
                        TrackInterstitialShown();
                    }
    #endif
                    break;

                case AdNetwork.AdMob:
    #if ADMOB
                    if (_admobInterstitial != null && _admobInterstitial.IsLoaded())
                    {
                        _admobInterstitial.Show();
                        TrackInterstitialShown();
                    }
    #endif
                    break;

                case AdNetwork.AppLovin:
    #if APPLOVIN
                    if (MaxSdk.IsInterstitialReady(appLovinInterstitialId))
                    {
                        MaxSdk.ShowInterstitial(appLovinInterstitialId);
                        TrackInterstitialShown();
                    }
    #endif
                    break;
            }
        }

        #endregion

        #region Rewarded Ads

        /// <summary>
        /// Load a rewarded ad
        /// </summary>
        public void LoadRewarded()
        {
            switch (primaryNetwork)
            {
                case AdNetwork.UnityAds:
    #if UNITY_ADS
                    if (!Advertisement.IsReady("rewardedVideo"))
                    {
                        OnRewardedLoaded?.Invoke();
                    }
    #endif
                    break;

                case AdNetwork.AdMob:
    #if ADMOB
                    _admobRewarded?.Destroy();
                    _admobRewarded = new RewardedAd(admobRewardedId);
                    _admobRewarded.OnAdLoaded += (_, __) => OnRewardedLoaded?.Invoke();
                    _admobRewarded.OnAdFailedToLoad += (_, e) => OnRewardedFailed?.Invoke(e.LoadAdError.GetMessage());
                    _admobRewarded.OnUserEarnedReward += (_, __) => OnRewardedCompleted?.Invoke();
                    _admobRewarded.LoadAd(new AdRequest.Builder().Build());
    #endif
                    break;

                case AdNetwork.AppLovin:
    #if APPLOVIN
                    MaxSdk.LoadRewardedAd(appLovinRewardedId);
    #endif
                    break;
            }
        }

        /// <summary>
        /// Show a rewarded ad if available and conditions are met
        /// </summary>
        public void ShowRewarded()
        {
            if (!CanShowRewarded()) return;

            switch (primaryNetwork)
            {
                case AdNetwork.UnityAds:
    #if UNITY_ADS
                    if (Advertisement.IsReady("rewardedVideo"))
                    {
                        var options = new ShowOptions { resultCallback = result => {
                            if (result == ShowResult.Finished)
                            {
                                OnRewardedCompleted?.Invoke();
                                TrackRewardedShown();
                            }
                        }};
                        Advertisement.Show("rewardedVideo", options);
                    }
    #endif
                    break;

                case AdNetwork.AdMob:
    #if ADMOB
                    if (_admobRewarded != null && _admobRewarded.IsLoaded())
                    {
                        _admobRewarded.Show();
                        TrackRewardedShown();
                    }
    #endif
                    break;

                case AdNetwork.AppLovin:
    #if APPLOVIN
                    if (MaxSdk.IsRewardedAdReady(appLovinRewardedId))
                    {
                        MaxSdk.ShowRewardedAd(appLovinRewardedId);
                        TrackRewardedShown();
                    }
    #endif
                    break;
            }
        }

        #endregion

        #region Banner Ads

        /// <summary>
        /// Load and show a banner ad
        /// </summary>
        public void LoadBanner(BannerPosition position = BannerPosition.BOTTOM_CENTER)
        {
            switch (primaryNetwork)
            {
                case AdNetwork.UnityAds:
    #if UNITY_ADS
                    Advertisement.Banner.SetPosition((BannerPosition)position);
                    Advertisement.Banner.Load("banner", new BannerLoadOptions
                    {
                        loadCallback = () => OnBannerLoaded?.Invoke(),
                        errorCallback = msg => OnBannerFailed?.Invoke(msg)
                    });
                    Advertisement.Banner.Show("banner");
    #endif
                    break;

                case AdNetwork.AdMob:
    #if ADMOB
                    _admobBanner?.Destroy();
                    _admobBanner = new BannerView(admobBannerId, AdSize.Banner, (GoogleMobileAds.Api.AdPosition)position);
                    _admobBanner.OnAdLoaded += (_, __) => OnBannerLoaded?.Invoke();
                    _admobBanner.OnAdFailedToLoad += (_, e) => OnBannerFailed?.Invoke(e.LoadAdError.GetMessage());
                    _admobBanner.LoadAd(new AdRequest.Builder().Build());
    #endif
                    break;

                case AdNetwork.AppLovin:
    #if APPLOVIN
                    MaxSdk.CreateBanner(appLovinBannerId, (MaxSdkBase.BannerPosition)position);
                    MaxSdk.SetBannerExtraParameter(appLovinBannerId, "adaptive_banner", "true");
                    MaxSdkCallbacks.Banner.OnAdLoadedEvent += (_, _) => OnBannerLoaded?.Invoke();
                    MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += (_, error) => OnBannerFailed?.Invoke(error.Message);
                    MaxSdk.ShowBanner(appLovinBannerId);
    #endif
                    break;
            }
        }

        /// <summary>
        /// Hide the banner ad
        /// </summary>
        public void HideBanner()
        {
            switch (primaryNetwork)
            {
                case AdNetwork.UnityAds:
    #if UNITY_ADS
                    Advertisement.Banner.Hide();
    #endif
                    break;

                case AdNetwork.AdMob:
    #if ADMOB
                    _admobBanner?.Hide();
    #endif
                    break;

                case AdNetwork.AppLovin:
    #if APPLOVIN
                    MaxSdk.HideBanner(appLovinBannerId);
    #endif
                    break;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get time remaining until next interstitial can be shown
        /// </summary>
        public float GetInterstitialCooldownRemaining()
        {
            if (!enableAdTracking) return 0f;

            var lastShowTime = PrefsHelper.Get<DateTime, GamePrefs>(GamePrefs.AdsDateTimeComplexData, DateTime.MinValue);
            var timeSince = (float)(DateTime.Now - lastShowTime).TotalSeconds;
            return Mathf.Max(0f, interstitialCooldown - timeSince);
        }

        /// <summary>
        /// Get time remaining until next rewarded ad can be shown
        /// </summary>
        public float GetRewardedCooldownRemaining()
        {
            if (!enableAdTracking) return 0f;

            var lastShowTime = PrefsHelper.Get<DateTime, GamePrefs>(GamePrefs.AdsDateTimeComplexData, DateTime.MinValue);
            var timeSince = (float)(DateTime.Now - lastShowTime).TotalSeconds;
            return Mathf.Max(0f, rewardedCooldown - timeSince);
        }

        /// <summary>
        /// Get remaining interstitial ads available today
        /// </summary>
        public int GetRemainingDailyInterstitials()
        {
            if (!enableAdTracking) return int.MaxValue;

            var count = PrefsHelper.Get<int, GamePrefs>(GamePrefs.InterstitialCount, 0);
            return Mathf.Max(0, maxDailyInterstitials - count);
        }

        /// <summary>
        /// Get remaining rewarded ads available today
        /// </summary>
        public int GetRemainingDailyRewarded()
        {
            if (!enableAdTracking) return int.MaxValue;

            var count = PrefsHelper.Get<int, GamePrefs>(GamePrefs.RewardedAdCount, 0);
            return Mathf.Max(0, maxDailyRewarded - count);
        }

        /// <summary>
        /// Check if interstitial ads are available
        /// </summary>
        public bool IsInterstitialAvailable()
        {
            if (!CanShowInterstitial()) return false;

            switch (primaryNetwork)
            {
                case AdNetwork.UnityAds:
    #if UNITY_ADS
                    return Advertisement.IsReady();
    #endif
                    break;

                case AdNetwork.AdMob:
    #if ADMOB
                    return _admobInterstitial != null && _admobInterstitial.IsLoaded();
    #endif
                    break;

                case AdNetwork.AppLovin:
    #if APPLOVIN
                    return MaxSdk.IsInterstitialReady(appLovinInterstitialId);
    #endif
                    break;
            }

            return false;
        }

        /// <summary>
        /// Check if rewarded ads are available
        /// </summary>
        public bool IsRewardedAvailable()
        {
            if (!CanShowRewarded()) return false;

            switch (primaryNetwork)
            {
                case AdNetwork.UnityAds:
    #if UNITY_ADS
                    return Advertisement.IsReady("rewardedVideo");
    #endif
                    break;

                case AdNetwork.AdMob:
    #if ADMOB
                    return _admobRewarded != null && _admobRewarded.IsLoaded();
    #endif
                    break;

                case AdNetwork.AppLovin:
    #if APPLOVIN
                    return MaxSdk.IsRewardedAdReady(appLovinRewardedId);
    #endif
                    break;
            }

            return false;
        }

        #endregion
    }
}