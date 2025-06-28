using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityHelperSDK.Events;

namespace UnityHelperSDK.DesignPatterns
{
    /// <summary>
    /// Generic observer pattern implementation that integrates with Unity's event system.
    /// Provides thread-safe event handling with priority support and automatic cleanup.
    /// </summary>
    /// <typeparam name="T">The type of data to be observed. Must be a struct to work with EventHelper.</typeparam>
    public class ObserverPattern<T> where T : struct
    {
        private readonly MonoBehaviour _coroutineRunner;
        private readonly object _tag;

        /// <summary>
        /// Creates a new observer pattern instance.
        /// </summary>
        /// <param name="coroutineRunner">Optional MonoBehaviour to run coroutine-based observers.</param>
        /// <param name="tag">Optional tag to identify this observer group.</param>
        public ObserverPattern(MonoBehaviour coroutineRunner = null, object tag = null)
        {
            _coroutineRunner = coroutineRunner;
            _tag = tag;
        }

        /// <summary>
        /// Subscribe to events with priority and coroutine support.
        /// </summary>
        /// <param name="observer">The observer callback.</param>
        /// <param name="priority">The priority level for this observer.</param>
        /// <param name="isCoroutine">Whether this observer should run as a coroutine.</param>
        /// <param name="gameObject">Optional GameObject to automatically manage observer lifetime.</param>
        public void Subscribe(Action<T> observer, EventPriority priority = EventPriority.Normal, 
            bool isCoroutine = false, GameObject gameObject = null)
        {
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            // If a GameObject is provided, use EventHandlerComponent for automatic cleanup
            if (gameObject != null)
            {
                var handlerComponent = gameObject.GetComponent<EventHandlerComponent>() 
                    ?? gameObject.AddComponent<EventHandlerComponent>();
                handlerComponent.AddHandler(observer);
            }
            else
            {
                EventHelper.Subscribe(observer, priority, isCoroutine, _coroutineRunner, _tag);
            }
        }

        /// <summary>
        /// Unsubscribe from events.
        /// </summary>
        /// <param name="observer">The observer to unsubscribe.</param>
        public void Unsubscribe(Action<T> observer)
        {
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            EventHelper.Unsubscribe(observer);
        }

        /// <summary>
        /// Notify all observers of an event.
        /// </summary>
        /// <param name="data">The event data to send to observers.</param>
        public void Notify(T data)
        {
            EventHelper.Trigger(data);
        }
    }

    /// <summary>
    /// Example usage:
    /// <code>
    /// public class HealthSystem : MonoBehaviour
    /// {
    ///     // Create an observer pattern instance with coroutine support
    ///     private ObserverPattern<int> _onHealthChanged;
    ///     private int _health;
    ///     
    ///     private void Awake()
    ///     {
    ///         // Pass this MonoBehaviour for coroutine support
    ///         _onHealthChanged = new ObserverPattern<int>(this);
    ///     }
    ///     
    ///     public void SetHealth(int value)
    ///     {
    ///         _health = value;
    ///         _onHealthChanged.Notify(_health);
    ///     }
    ///     
    ///     /// Subscribe to health changes with priority and automatic cleanup
    ///     public void AddHealthObserver(Action<int> observer, EventPriority priority = EventPriority.Normal, 
    ///         bool isCoroutine = false, GameObject target = null)
    ///     {
    ///         _onHealthChanged.Subscribe(observer, priority, isCoroutine, target);
    ///     }
    /// }
    /// 
    /// // Basic usage:
    /// healthSystem.AddHealthObserver(health => Debug.Log($"Health changed: {health}"));
    /// 
    /// // Priority and coroutine usage with automatic cleanup:
    /// healthSystem.AddHealthObserver(
    ///     observer: async health => {
    ///         await Task.Delay(100);
    ///         Debug.Log($"High priority health change: {health}");
    ///     },
    ///     priority: EventPriority.High,
    ///     isCoroutine: true,
    ///     target: this.gameObject
    /// );
    /// 
    /// // The notification:
    /// healthSystem.SetHealth(100);
    /// </code>
    /// </summary>
}
