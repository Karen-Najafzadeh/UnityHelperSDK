using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Security;
using UnityEngine.Purchasing.Extension;
using UnityHelperSDK.Data;


namespace UnityHelperSDK.HelperUtilities{


    /// <summary>
    /// IAPHelper: Singleton manager for Unity In-App Purchases
    /// Handles initialization, purchase flow, receipt validation, and product catalog management.
    /// </summary>
    public class IAPHelper : MonoBehaviour, IDetailedStoreListener
    {
        public static IAPHelper Instance { get; private set; }

        private IStoreController _storeController;
        private IExtensionProvider _extensionProvider;

        [Serializable]
        public struct IAPProduct
        {
            public string id;
            public ProductType type;
        }

        [Tooltip("List your products and their types here.")]
        public List<IAPProduct> products = new List<IAPProduct>();

        // Purchase history tracking
        private const string PURCHASE_DATE_FORMAT = "yyyy-MM-dd'T'HH:mm:ss'Z'";
        private Dictionary<string, (DateTime? purchaseDate, bool isActive)> _purchaseCache = new Dictionary<string, (DateTime?, bool)>();

        // Events for purchase results
        public event Action<Product> OnPurchaseSucceeded;
        private event Action<Product, PurchaseFailureReason> _onPurchaseFailed;
        public event Action<Product, PurchaseFailureReason> OnPurchaseFailedEvent
        {
            add => _onPurchaseFailed += value;
            remove => _onPurchaseFailed -= value;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePurchasing();
        }

        /// <summary>
        /// Initializes the Unity IAP purchasing system.
        /// </summary>
        public void InitializePurchasing()
        {
            if (IsInitialized()) return;

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            foreach (var prod in products)
            {
                builder.AddProduct(prod.id, prod.type);
            }

            UnityPurchasing.Initialize(this, builder);
        }

        private bool IsInitialized()
        {
            return _storeController != null && _extensionProvider != null;
        }

        /// <summary>
        /// Initiates purchase of the specified product.
        /// </summary>
        public void BuyProduct(string productId)
        {
            if (!IsInitialized())
            {
                Debug.LogError("IAPHelper: BuyProduct failed. Not initialized.");
                return;
            }

            Product product = _storeController.products.WithID(productId);
            if (product != null && product.availableToPurchase)
            {
                Debug.Log($"IAPHelper: Purchasing product asynchronously: '{product.definition.id}'");
                _storeController.InitiatePurchase(product);
            }
            else
            {
                Debug.LogError($"IAPHelper: BuyProduct - Product not found or not available for purchase: {productId}");
            }
        }

        /// <summary>
        /// Get formatted price with proper currency symbol and localization
        /// </summary>
        public string GetFormattedPrice(string productId)
        {
            if (!IsInitialized())
                return string.Empty;

            Product product = _storeController.products.WithID(productId);
            if (product == null || !product.availableToPurchase)
                return string.Empty;

            return LocalizationManager.FormatCurrency(
                (decimal)product.metadata.localizedPrice,
                product.metadata.isoCurrencyCode);
        }

        /// <summary>
        /// Get complete product information including localized price and description.
        /// </summary>
        public (string title, string description, string price) GetProductInfo(string productId)
        {
            if (!IsInitialized())
                return (string.Empty, string.Empty, string.Empty);

            Product product = _storeController.products.WithID(productId);
            if (product == null || !product.availableToPurchase)
                return (string.Empty, string.Empty, string.Empty);

            return (
                product.metadata.localizedTitle,
                product.metadata.localizedDescription,
                GetFormattedPrice(productId)
            );
        }

        /// <summary>
        /// Check if a product is owned (for non-consumable and subscription products).
        /// </summary>
        public bool IsProductOwned(string productId)
        {
            if (!IsInitialized())
                return false;

            Product product = _storeController.products.WithID(productId);
            return product != null && product.hasReceipt;
        }

        #region IStoreListener Implementation

        public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            Debug.Log("IAPHelper: OnInitialized");
            _storeController = controller;
            _extensionProvider = extensions;
        }

        void IStoreListener.OnInitializeFailed(InitializationFailureReason error)
        {
            OnInitializeFailed(error, "No additional information");
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.LogError($"IAPHelper: OnInitializeFailed: Error: {error}, Message: {message}");
        }

        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
        {
            Debug.Log($"IAPHelper: Processing purchase: {args.purchasedProduct.definition.id}");

            // Track the purchase
            CoroutineHelper.StartManagedCoroutine(
                $"track_purchase_{args.purchasedProduct.definition.id}",
                TrackPurchaseRoutine(args.purchasedProduct)
            );

            OnPurchaseSucceeded?.Invoke(args.purchasedProduct);
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            Debug.LogError($"Purchase failed - Product: '{product.definition.id}', Reason: {failureDescription.reason}, Message: {failureDescription.message}");
            _onPurchaseFailed?.Invoke(product, failureDescription.reason);
        }

        void IStoreListener.OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            Debug.LogError($"Purchase failed - Product: '{product.definition.id}', Reason: {failureReason}");
            _onPurchaseFailed?.Invoke(product, failureReason);
        }

        #endregion

        #region Purchase Tracking

        private IEnumerator TrackPurchaseRoutine(Product product)
        {
            yield return new WaitForSeconds(0);
            TrackPurchase(product);
        }

        private void TrackPurchase(Product product)
        {
            var purchaseInfo = new Dictionary<string, object>
            {
                ["productId"] = product.definition.id,
                ["purchaseDate"] = DateTime.UtcNow.ToString(PURCHASE_DATE_FORMAT),
                ["transactionId"] = product.transactionID,
                ["receipt"] = product.receipt
            };

            var history = PrefsHelper.GetJson<List<Dictionary<string, object>>, GamePrefs>(GamePrefs.PurchaseHistoryComplexData) ?? new List<Dictionary<string, object>>();
            history.Add(purchaseInfo);
            PrefsHelper.SetJson(GamePrefs.PurchaseHistoryComplexData, history);

            _purchaseCache[product.definition.id] = (DateTime.UtcNow, true);
        }

        /// <summary>
        /// Get purchase history for a product
        /// </summary>
        public List<(DateTime date, string transactionId)> GetPurchaseHistory(string productId)
        {
            var history = PrefsHelper.GetJson<List<Dictionary<string, object>>, GamePrefs>(GamePrefs.PurchaseHistoryComplexData);
            if (history == null) return new List<(DateTime, string)>();

            return history
                .Where(p => p["productId"].ToString() == productId)
                .Select(p => (
                    DateTime.ParseExact(p["purchaseDate"].ToString(), PURCHASE_DATE_FORMAT, null),
                    p["transactionId"].ToString()
                ))
                .ToList();
        }

        /// <summary>
        /// Get time since last purchase of a product
        /// </summary>
        public string GetTimeSinceLastPurchase(string productId)
        {
            var history = GetPurchaseHistory(productId);
            if (history.Count == 0) return string.Empty;

            var lastPurchase = history.OrderByDescending(h => h.date).First().date;
            return LocalizationManager.GetRelativeTime(lastPurchase);
        }

        /// <summary>
        /// Restores purchases on non-consumable products (iOS).
        /// </summary>
        public void RestorePurchases()
        {
            if (!IsInitialized())
            {
                Debug.LogError("IAPHelper: RestorePurchases failed. Not initialized.");
                return;
            }

            if (Application.platform == RuntimePlatform.IPhonePlayer ||
                Application.platform == RuntimePlatform.OSXPlayer)
            {
                Debug.Log("IAPHelper: RestorePurchases started ...");

                var apple = _extensionProvider.GetExtension<IAppleExtensions>();
                apple.RestoreTransactions((success, message) =>
                {
                    Debug.Log($"IAPHelper: RestorePurchases continuing: Success: {success}, Message: {message}");
                });
            }
            else
            {
                Debug.Log("IAPHelper: RestorePurchases - No need to restore on this platform.");
            }
        }

        #endregion    /// <summary>
        /// Get subscription info including renewal date if available.
        /// </summary>
        public (bool isSubscribed, DateTime? expiryDate) GetSubscriptionInfo(string productId)
        {
            if (!IsInitialized())
                return (false, null);

            Product product = _storeController.products.WithID(productId);
            if (product == null || !product.hasReceipt || product.definition.type != ProductType.Subscription)
                return (false, null);

            try
            {
                var subscriptionManager = new SubscriptionManager(product, null);
                var info = subscriptionManager.getSubscriptionInfo();

                if (info == null)
                    return (false, null);

                return (
                    info.isSubscribed() == UnityEngine.Purchasing.Result.True,
                    info.getExpireDate()
                );
            }
            catch (Exception ex)
            {
                Debug.LogError($"IAPHelper: Failed to get subscription info for {productId}: {ex}");
                return (false, null);
            }
        }
    }
}