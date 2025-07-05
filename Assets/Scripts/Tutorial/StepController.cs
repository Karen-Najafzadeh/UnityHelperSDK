using UnityEngine;
using System.Collections;

public class StepController : MonoBehaviour
{
    private TutorialStepSO current;

    public void StartStep(TutorialStepSO step)
    {
        current = step;
        if (step.timeDelay > 0)
            StartCoroutine(DelayedStart(step));
        else
            BeginStep(step);
    }

    private IEnumerator DelayedStart(TutorialStepSO step)
    {
        yield return new WaitForSeconds(step.timeDelay);
        BeginStep(step);
    }

    private void BeginStep(TutorialStepSO step)
    {
        ShowUI(step.message, step.highlightTarget);
        step.requirement.Initialize();
        step.requirement.OnRequirementMet += OnRequirementMet;
    }

    private void OnRequirementMet()
    {
        current.requirement.OnRequirementMet -= OnRequirementMet;
        current.requirement.Cleanup();

        var next = current.nextOnSuccess;
        // failure logic could set next = current.nextOnFailure

        if (next != null)
            StartStep(next);
        else
            TutorialComplete();
    }

    private void TutorialComplete()
    {
        Debug.Log("Tutorial sequence complete.");
    }

    private void ShowUI(string message, GameObject target)
    {
        // your UI display logic here
    }
}
