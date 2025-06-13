using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityHelperSDK;

namespace UnityHelperSDK.Events
{
    /// <summary>
    /// Component that can be added to any GameObject to manage event subscriptions
    /// </summary>
    public class EventHandlerComponent : MonoBehaviour
    {
        private readonly List<(Type type, Delegate handler)> _handlers = new List<(Type, Delegate)>();

        public void AddHandler<T>(Action<T> handler) where T : struct
        {
            _handlers.Add((typeof(T), handler));
            EventHelper.Subscribe(handler);
        }

        private void OnDestroy()
        {
            foreach (var (type, handler) in _handlers)
            {
                var method = typeof(EventHelper).GetMethod(nameof(EventHelper.Unsubscribe));
                var genericMethod = method.MakeGenericMethod(type);
                genericMethod.Invoke(null, new object[] { handler });
            }
            _handlers.Clear();

            // If the GameObject has an IEventHandler component, notify it
            var eventHandlers = GetComponents<IEventHandler>();
            foreach (var handler in eventHandlers)
            {
                handler.OnEventCleanup();
            }
        }
    }

    /// <summary>
    /// A thread-safe, type-safe event management system for Unity applications.
    /// Provides centralized event handling with automatic cleanup and lifecycle management.
    /// </summary>
    public static class EventHelper
    {
        private static readonly object _lock = new object();
        private static readonly Dictionary<Type, List<EventHandlerWrapperBase>> _eventHandlers = 
            new Dictionary<Type, List<EventHandlerWrapperBase>>();
        private static readonly Dictionary<object, List<(Type type, EventHandlerWrapperBase handler)>> _scopedHandlers = 
            new Dictionary<object, List<(Type, EventHandlerWrapperBase)>>();
        private static readonly Queue<IChainedEvent> _chainedEvents = new Queue<IChainedEvent>();
        private static readonly EventCompletionTracker _completionTracker = new EventCompletionTracker();
        
        #region Event Registration

        /// <summary>
        /// Subscribe to an event with automatic lifetime management and priority
        /// </summary>
        public static void Subscribe<T>(Action<T> handler, EventPriority priority = EventPriority.Normal, 
            bool isCoroutine = false, MonoBehaviour coroutineRunner = null, object tag = null) where T : struct
        {
            lock (_lock)
            {
                var type = typeof(T);
                if (!_eventHandlers.TryGetValue(type, out var handlers))
                {
                    handlers = new List<EventHandlerWrapperBase>();
                    _eventHandlers[type] = handlers;
                }

                // Clean up any dead references
                handlers.RemoveAll(h => h == null || h.Handler == null || h.Handler.Target == null);

                // Create wrapper with priority and metadata
                var wrapper = new EventHandlerWrapper<T>(handler, priority, isCoroutine, coroutineRunner, tag);
                
                // Insert maintaining priority order
                int insertIndex = handlers.FindIndex(h => h.Priority > priority);
                
                if (insertIndex == -1)
                    handlers.Add(wrapper);
                else
                    handlers.Insert(insertIndex, wrapper);
            }
        }

        /// <summary>
        /// Subscribe to an event with a specific scope and priority
        /// </summary>
        public static void SubscribeScoped<T>(MonoBehaviour scope, Action<T> handler, 
            EventPriority priority = EventPriority.Normal, bool isCoroutine = false, object tag = null) where T : struct
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            lock (_lock)
            {
                var type = typeof(T);
                if (!_scopedHandlers.TryGetValue(scope, out var handlers))
                {
                    handlers = new List<(Type, EventHandlerWrapperBase)>();
                    _scopedHandlers[scope] = handlers;
                }

                EventHandlerWrapperBase wrapper = new EventHandlerWrapper<T>(handler, priority, isCoroutine, scope, tag);
                handlers.Add((type, wrapper));
                Subscribe(handler, priority, isCoroutine, scope, tag);
            }
        }

        /// <summary>
        /// Trigger an event with optional parameters
        /// </summary>
        public static void Trigger<T>(T eventData) where T : struct
        {
            List<EventHandlerWrapperBase> handlers;
            lock (_lock)
            {
                var type = typeof(T);
                if (!_eventHandlers.TryGetValue(type, out handlers))
                    return;

                // Make a copy to avoid modification during iteration
                handlers = new List<EventHandlerWrapperBase>(handlers);
            }

            // Execute handlers in priority order
            foreach (var wrapper in handlers)
            {
                if (wrapper == null || wrapper.Handler == null) continue;
                
                if (wrapper.Handler is Action<T> handler)
                {
                    if (wrapper.IsCoroutine && wrapper.CoroutineRunner != null)
                    {
                        wrapper.CoroutineRunner.StartCoroutine(ExecuteCoroutineHandler(handler, eventData));
                    }
                    else
                    {
                        try
                        {
                            handler(eventData);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Error executing event handler: {ex}");
                        }
                    }
                }
            }
        }

        private static IEnumerator ExecuteCoroutineHandler<T>(Action<T> handler, T eventData) where T : struct
        {
            try
            {
                handler(eventData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error executing coroutine event handler: {ex}");
            }
            yield break;
        }

        /// <summary>
        /// Unsubscribe from an event
        /// </summary>
        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            lock (_lock)
            {
                var type = typeof(T);
                if (!_eventHandlers.TryGetValue(type, out var handlers))
                    return;

                handlers.RemoveAll(h => 
                {
                    if (h?.Handler is Action<T> actionHandler)
                    {
                        return actionHandler.Equals(handler);
                    }
                    return false;
                });
            }
        }

        /// <summary>
        /// Clean up all event handlers for a scope
        /// </summary>
        public static void UnsubscribeScope(object scope)
        {
            lock (_lock)
            {
                if (_scopedHandlers.TryGetValue(scope, out var handlers))
                {
                    foreach (var (_, handler) in handlers)
                    {
                        if (handler?.Handler is Delegate del)
                        {
                            var method = typeof(EventHelper).GetMethod(nameof(Unsubscribe));
                            var genericMethod = method.MakeGenericMethod(handler.EventType);
                            genericMethod.Invoke(null, new object[] { del });
                        }
                    }
                    _scopedHandlers.Remove(scope);
                }
            }
        }

        /// <summary>
        /// Trigger a chained event sequence
        /// </summary>
        public static void TriggerChainedEvent(IChainedEvent firstEvent)
        {
            lock (_lock)
            {
                _chainedEvents.Enqueue(firstEvent);
                _completionTracker.AddEvent(firstEvent);
            }
        }

        /// <summary>
        /// Process events - should be called from a MonoBehaviour's Update method
        /// </summary>
        public static void ProcessEvents()
        {
            // Process chained events
            while (_chainedEvents.Count > 0)
            {
                var evt = _chainedEvents.Peek();
                if (!evt.IsComplete)
                {
                    if (evt is UIEvent uiEvent && uiEvent.CoroutineRunner != null)
                    {
                        uiEvent.CoroutineRunner.StartCoroutine(evt.ProcessEvent());
                    }
                    break;
                }

                _chainedEvents.Dequeue();
                if (evt.NextEvent != null)
                {
                    _chainedEvents.Enqueue(evt.NextEvent);
                    _completionTracker.AddEvent(evt.NextEvent);
                }
            }

            // Update completion tracker
            _completionTracker.Update();
        }
    }
}
#endregion