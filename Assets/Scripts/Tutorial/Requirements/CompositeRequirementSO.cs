using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Tutorial/Requirements/Composite")]
public class CompositeRequirementSO : StepRequirementSO
{
    public enum Mode { AllMustPass, AnyCanPass }
    public Mode mode;
    public List<StepRequirementSO> subRequirements;

    private int passedCount;

    public override void Initialize()
    {
        base.Initialize();
        passedCount = 0;
        foreach (var req in subRequirements)
        {
            req.OnRequirementMet += OnSubMet;
            req.Initialize();
        }
    }

    private void OnSubMet()
    {
        passedCount++;
        if ((mode == Mode.AllMustPass && passedCount == subRequirements.Count) ||
            (mode == Mode.AnyCanPass && passedCount > 0))
        {
            RequirementMet();
        }
    }

    public override void Cleanup()
    {
        foreach (var req in subRequirements)
        {
            req.OnRequirementMet -= OnSubMet;
            req.Cleanup();
        }
        base.Cleanup();
    }
}
