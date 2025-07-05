//==================== TutorialDefinitionSO.cs ====================
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Tutorial/Definition")]
public class TutorialDefinitionSO : ScriptableObject
{
    public string tutorialID;
    public TutorialCategory category;
    public string triggerKey;
    public List<TutorialStepSO> steps;
}

public enum TutorialCategory { Gameplay, UI, Combat, Exploration, Other }

