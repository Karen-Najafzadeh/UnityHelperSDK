using UnityEngine;
using System;
using System.Reflection;
using UnityHelperSDK.Events;

[CreateAssetMenu(menuName = "Tutorial/Requirements/Event (Generic)")]
public class EventRequirementSO : StepRequirementSO
{
    public string eventTypeName;
    public EventPriority priority = EventPriority.Normal;
    public bool isCoroutine = false;

    private Delegate _handlerDelegate;
    private Type _eventType;

    public override void Initialize()
    {
        base.Initialize();

        _eventType = Type.GetType(eventTypeName);
        if (_eventType == null || !_eventType.IsValueType)
        {
            Debug.LogError($"Cannot find struct '{eventTypeName}'");
            return;
        }

        var actionType = typeof(Action<>).MakeGenericType(_eventType);
        var methodInfo = GetType().GetMethod("OnEventTriggered", BindingFlags.Instance | BindingFlags.NonPublic)
                         .MakeGenericMethod(_eventType);
        _handlerDelegate = Delegate.CreateDelegate(actionType, this, methodInfo);

        var subscribeMethod = typeof(EventHelper)
            .GetMethod(nameof(EventHelper.Subscribe), BindingFlags.Static | BindingFlags.Public)
            .MakeGenericMethod(_eventType);
        subscribeMethod.Invoke(null, new object[]{ _handlerDelegate, priority, isCoroutine, null, null });
    }

    public override void Cleanup()
    {
        if (_handlerDelegate != null && _eventType != null)
        {
            var unsub = typeof(EventHelper)
                .GetMethod(nameof(EventHelper.Unsubscribe), BindingFlags.Static | BindingFlags.Public)
                .MakeGenericMethod(_eventType);
            unsub.Invoke(null, new object[]{ _handlerDelegate });
        }
        base.Cleanup();
    }

    private void OnEventTriggered<T>(T evt) where T : struct
    {
        RequirementMet();
    }
}
