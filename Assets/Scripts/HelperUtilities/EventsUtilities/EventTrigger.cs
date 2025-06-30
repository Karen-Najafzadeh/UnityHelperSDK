// 2. Generic EventTrigger adapter implementing ITrigger
using System;
using UnityHelperSDK.Events;

public class EventTrigger<T> : ITrigger where T : struct
{
    readonly Func<T, bool> _predicate;
    Action<T> _handler;

    /// <summary>
    /// </summary>
    /// <param name="predicate">Return true when this trigger should fire.</param>
    public EventTrigger(Func<T, bool> predicate)
    {
        _predicate = predicate;
        _handler   = OnEvent;
    }

    public event Action Fired;

    public void Initialize()
    {
        // subscribe via your EventHelper
        EventHelper.Subscribe(_handler);
    }

    public void TearDown()
    {
        // unsubscribe
        EventHelper.Unsubscribe(_handler);
    }

    void OnEvent(T evt)
    {
        if (_predicate(evt))
            Fired?.Invoke();
    }
}
