using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityHelperSDK.Events
{
    /// <summary>
    /// Priority levels for event handlers
    /// </summary>
    public enum EventPriority
    {
        Critical = 0,
        High = 1,
        Normal = 2,
        Low = 3,
        Background = 4
    }

    /// <summary>
    /// Base class for event handler wrappers
    /// </summary>
    public abstract class EventHandlerWrapperBase
    {
        public EventPriority Priority { get; }
        public bool IsCoroutine { get; }
        public MonoBehaviour CoroutineRunner { get; }
        public object Tag { get; }
        public abstract Type EventType { get; }
        public abstract Delegate Handler { get; }

        protected EventHandlerWrapperBase(EventPriority priority, bool isCoroutine, MonoBehaviour coroutineRunner, object tag)
        {
            Priority = priority;
            IsCoroutine = isCoroutine;
            CoroutineRunner = coroutineRunner;
            Tag = tag;
        }
    }

    /// <summary>
    /// Wrapper for event handlers that includes priority and metadata
    /// </summary>
    public class EventHandlerWrapper<T> : EventHandlerWrapperBase where T : struct
    {
        private readonly Action<T> _handler;
        public override Type EventType => typeof(T);
        public override Delegate Handler => _handler;
        
        public EventHandlerWrapper(Action<T> handler, EventPriority priority = EventPriority.Normal, 
            bool isCoroutine = false, MonoBehaviour coroutineRunner = null, object tag = null)
            : base(priority, isCoroutine, coroutineRunner, tag)
        {
            _handler = handler;
        }
    }

    /// <summary>
    /// Interface for events that can be chained together
    /// </summary>
    public interface IChainedEvent
    {
        IEnumerator ProcessEvent();
        bool IsComplete { get; }
        bool HasError { get; }
        Exception Error { get; }
        IChainedEvent NextEvent { get; set; }
    }

    /// <summary>
    /// Base class for UI events that need to be processed in order
    /// </summary>
    public abstract class UIEvent : IChainedEvent
    {
        public MonoBehaviour CoroutineRunner { get; protected set; }
        public IChainedEvent NextEvent { get; set; }
        public bool IsComplete { get; protected set; }
        public bool HasError { get; protected set; }
        public Exception Error { get; protected set; }

        protected UIEvent(MonoBehaviour coroutineRunner = null)
        {
            CoroutineRunner = coroutineRunner;
        }

        public abstract IEnumerator ProcessEvent();

        protected void SetError(Exception ex)
        {
            HasError = true;
            Error = ex;
            IsComplete = true;
        }

        protected void Complete()
        {
            IsComplete = true;
        }
    }

    /// <summary>
    /// Event completion tracker for coroutine-based events
    /// </summary>
    public class EventCompletionTracker
    {
        private readonly List<IChainedEvent> _pendingEvents = new List<IChainedEvent>();
        private readonly HashSet<IChainedEvent> _completedEvents = new HashSet<IChainedEvent>();
        
        public bool IsComplete => _pendingEvents.Count == 0;
        public bool HasErrors => _completedEvents.Any(e => e.HasError);
        
        public void AddEvent(IChainedEvent evt)
        {
            _pendingEvents.Add(evt);
        }
        
        public void Update()
        {
            _pendingEvents.RemoveAll(evt =>
            {
                if (evt.IsComplete)
                {
                    _completedEvents.Add(evt);
                    return true;
                }
                return false;
            });
        }
        
        public void Clear()
        {
            _pendingEvents.Clear();
            _completedEvents.Clear();
        }
        
        public IEnumerable<Exception> GetErrors()
        {
            return _completedEvents.Where(e => e.HasError).Select(e => e.Error);
        }
    }

    /// <summary>
    /// Interface for objects that want automatic event cleanup
    /// </summary>
    public interface IEventHandler
    {
        void OnEventCleanup();
    }
}
