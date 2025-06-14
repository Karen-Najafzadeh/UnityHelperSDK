using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using UnityHelperSDK.HelperUtilities;

/// <summary>
/// A specialized helper for displaying damage numbers and other floating text in world space.
/// Integrates with ObjectPoolHelper for efficient popup management and supports various
/// animation styles and color coding.
/// </summary>

namespace UnityHelperSDK.HelperUtilities
{
    /// <summary>
    /// Helper class for displaying damage popups in the game world.
    /// </summary>
    public static class DamagePopupHelper
    {
        // Pool key for damage popup prefabs
        private const string POOL_KEY = "DamagePopup";
        
        // Animation settings
        private static readonly float PopupDuration = 1f;
        private static readonly float PopupFloatDistance = 1f;
        private static readonly float PopupSpreadRadius = 0.5f;
        private static readonly float CriticalScale = 1.5f;
        
        // Color settings
        private static readonly Color CriticalColor = new Color(1f, 0.2f, 0.2f);
        private static readonly Color NormalColor = Color.white;
        private static readonly Color HealColor = new Color(0.2f, 1f, 0.2f);
        private static readonly Color ElementalColors = new Color(0.2f, 0.6f, 1f);
            
        /// <summary>
        /// Initialize the damage popup system
        /// </summary>
        public static async Task Initialize()
        {
            var prefab = CreateDamagePopupPrefab();
            await ObjectPoolHelper.InitializePoolAsync(POOL_KEY, prefab, new ObjectPoolHelper.PoolSettings
            {
                InitialSize = 20,
                MaxSize = 100,
                ExpandBy = 10,
                AutoExpand = true
            });
        }

        /// <summary>
        /// Show a damage number at the specified world position
        /// </summary>
        public static async void ShowDamage(Vector3 worldPos, float amount, bool isCritical = false, bool isHeal = false)
        {
            var popup = ObjectPoolHelper.Get(POOL_KEY, worldPos, Quaternion.identity);
            if (popup == null) return;

            var text = popup.GetComponent<TMP_Text>();
            
            // Set text and color
            text.text = isHeal ? $"+{amount:F0}" : $"-{amount:F0}";
            text.color = isHeal ? HealColor : (isCritical ? CriticalColor : NormalColor);
            
            // Add random offset to prevent stacking
            Vector3 randomOffset = Random.insideUnitCircle * PopupSpreadRadius;
            popup.transform.position += randomOffset;

            // Animate
            var sequence = DOTween.Sequence();
            
            // Scale punch effect
            sequence.Append(popup.transform.DOPunchScale(Vector3.one * 0.5f, 0.2f));
            
            // Float up
            sequence.Join(popup.transform.DOMoveY(
                popup.transform.position.y + PopupFloatDistance, 
                PopupDuration
            ).SetEase(Ease.OutCubic));
            
            // Fade out
            sequence.Join(text.DOFade(0f, PopupDuration * 0.5f)
                .SetDelay(PopupDuration * 0.5f));

            // Return to pool after animation
            await Task.Delay((int)(PopupDuration * 1000));
            popup.GetComponent<PoolableObject>().ReturnToPool();
        }

        /// <summary>
        /// Show damage numbers in a combo (multiple hits)
        /// </summary>
        public static void ShowCombo(Vector3 worldPos, float[] damages, bool isCritical = false)
        {
            for (int i = 0; i < damages.Length; i++)
            {
                // Delay each number slightly
                float delay = i * 0.1f;
                DOVirtual.DelayedCall(delay, () => ShowDamage(worldPos, damages[i], isCritical));
            }
        }

        private static GameObject CreateDamagePopupPrefab()
        {
            var go = new GameObject("DamagePopup");
            
            // Add TextMeshPro component
            var tmp = go.AddComponent<TMP_Text>();
            tmp.font = Resources.Load<TMP_FontAsset>("Fonts/Default");
            tmp.fontSize = 8;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            
            // Make it face the camera
            go.AddComponent<Billboard>();
            
            // Add poolable component
            var poolable = go.AddComponent<PoolableObject>();
            poolable.PoolKey = POOL_KEY;

            return go;
        }
    }

    /// <summary>
    /// Makes an object always face the camera
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        private Camera _mainCamera;
        
        void Start()
        {
            _mainCamera = Camera.main;
        }
        
        void LateUpdate()
        {
            if (_mainCamera != null)
            {
                transform.forward = _mainCamera.transform.forward;
            }
        }
    }
}