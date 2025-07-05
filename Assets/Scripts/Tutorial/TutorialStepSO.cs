using UnityEngine;

[CreateAssetMenu(menuName = "Tutorial/Step")]
public class TutorialStepSO : ScriptableObject
{
    public string stepID;
    public string message;
    public GameObject highlightTarget;
    public float timeDelay;
    public StepRequirementSO requirement;
    public TutorialStepSO nextOnSuccess;
    public TutorialStepSO nextOnFailure;
}
