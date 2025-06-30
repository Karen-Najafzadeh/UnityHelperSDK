using System;

public interface ITrigger
{
    void Initialize();    // Subscribe to EventHelper or other signals
    void TearDown();      // Unsubscribe / clean up
    event Action Fired;   // Invoked when the condition is met
}