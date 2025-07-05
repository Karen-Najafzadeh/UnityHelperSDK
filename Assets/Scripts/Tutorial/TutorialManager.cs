using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityHelperSDK.Events;
public struct TutorialTriggerEvent
{
    public string key;
}

public class TutorialManager : MonoBehaviour
{
    public AssetReferenceT<TutorialDefinitionSO>[] definitions;

    private StepController stepController;

    private void Awake()
    {
        stepController = GetComponent<StepController>();
        foreach (var defRef in definitions)
        {
            defRef.LoadAssetAsync().Completed += handle =>
            {
                var def = handle.Result;
                EventHelper.Subscribe<TutorialTriggerEvent>(evt =>
                {
                    if (evt.key == def.triggerKey)
                        StartTutorial(def);
                });
            };
        }
    }

    private void StartTutorial(TutorialDefinitionSO def)
    {
        if (def.steps != null && def.steps.Count > 0)
            stepController.StartStep(def.steps[0]);
    }
}
