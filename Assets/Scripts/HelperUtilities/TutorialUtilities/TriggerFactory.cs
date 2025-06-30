using System;
using UnityEngine;
using UnityHelperSDK.Events;
using UnityHelperSDK.TutorialUtilities;
using System.Linq;
using System.Collections.Generic;

public static class TriggerFactory
{
    public static ITrigger Create(TriggerDefinitionSO def, string tutorialID, string stepID)
    {
        // Composite trigger support
        if (def is CompositeTriggerDefinitionSO composite)
        {
            var subTriggers = composite.SubTriggers.Select(sub => Create(sub, tutorialID, stepID)).ToArray();
            return new CompositeTrigger(subTriggers, composite.Logic);
        }

        switch (def.Type)
        {
            case TriggerType.SceneLoaded:
                var sceneName = def.Parameters.FirstOrDefault(p => p.Key == "sceneName")?.StringValue;
                return new EventTrigger<OnSceneLoadedEvent>(
                    evt => evt.SceneName == sceneName
                );

            case TriggerType.ButtonPressed:
                var buttonName = def.Parameters.FirstOrDefault(p => p.Key == "buttonName")?.StringValue;
                return new EventTrigger<OnUIButtonPressed>(
                    evt => evt.ButtonName == buttonName
                );

                // Add other cases as needed, e.g. PlayerMoved, PlayerHealthChanged, etc.
        }

        // Return null or throw an exception if no case matches
        return null;
    }
}
