using UnityEngine;
using System.Collections;
using UnityHelperSDK.HelperUtilities;
using System.Threading;

[CreateAssetMenu(menuName = "Tutorial/Requirements/Timer")]
public class TimerRequirementSO : StepRequirementSO
{
    public float delaySeconds;

    public override void Initialize()
    {
        base.Initialize();
        TimeHelper.StartTimer("TimerRequirementSO", delaySeconds, onComplete: RequirementMet);
    }

    public override void Cleanup()
    {
        // optionally stop coroutine
        base.Cleanup();
    }
}
