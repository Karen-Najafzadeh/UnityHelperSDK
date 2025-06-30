using System;
using System.Collections.Generic;
using UnityEngine;
using UnityHelperSDK.TutorialUtilities;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    TutorialDefinitionSO _currentTutorial;
    int _currentStepIndex;
    List<ITrigger> _activeTriggers = new List<ITrigger>();

    public event Action<StepDefinitionSO> OnStepEntered;
    public event Action OnTutorialCompleted;

    void Awake() => Instance = this;

    bool AreConditionsMet(UnityHelperSDK.TutorialUtilities.ConditionSO[] conditions)
    {
        if (conditions == null) return true;
        foreach (var cond in conditions)
            if (cond != null && !(cond is ICondition ic && ic.IsMet())) return false;
        return true;
    }

    public void StartTutorial(TutorialDefinitionSO tutorialSO)
    {
        if (!AreConditionsMet(tutorialSO.StartConditions)) return;
        _currentTutorial = tutorialSO;
        _currentStepIndex = -1;
        AdvanceStep();  // will initialize initial trigger or first step immediately
    }

    public void AdvanceStep()
    {
        // Tear down old triggers
        foreach (var trig in _activeTriggers) trig.TearDown();
        _activeTriggers.Clear();

        _currentStepIndex++;
        if (_currentStepIndex >= _currentTutorial.Steps.Length)
        {
            if (AreConditionsMet(_currentTutorial.CompleteConditions))
                OnTutorialCompleted?.Invoke();
            return;
        }

        // Fire OnStepEntered so UI can bind
        var stepSO = _currentTutorial.Steps[_currentStepIndex];
        if (!AreConditionsMet(stepSO.StartConditions)) return;
        OnStepEntered?.Invoke(stepSO);

        // Create and init all completion triggers for this step
        foreach (var trigDef in stepSO.CompletionTriggers)
        {
            var trig = TriggerFactory.Create(trigDef, _currentTutorial.TutorialID, stepSO.StepID);
            trig.Fired += () =>
            {
                if (AreConditionsMet(stepSO.CompleteConditions))
                    AdvanceStep();
            };
            trig.Initialize();
            _activeTriggers.Add(trig);
        }
    }
}
