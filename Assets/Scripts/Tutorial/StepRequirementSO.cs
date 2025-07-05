using UnityEngine;
using System;

public abstract class StepRequirementSO : ScriptableObject
{
    public event Action OnRequirementMet;

    public virtual void Initialize() { }
    public virtual void Cleanup() { }
    public virtual bool IsMet() => false;

    protected void RequirementMet()
    {
        OnRequirementMet?.Invoke();
    }
}
