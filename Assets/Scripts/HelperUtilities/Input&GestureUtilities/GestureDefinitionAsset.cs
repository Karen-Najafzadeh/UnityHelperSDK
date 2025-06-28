using UnityEngine;
using System;
using System.Collections.Generic;
using UnityHelperSDK.Input;

[CreateAssetMenu(fileName = "New Gesture", menuName = "Input/Gesture Definition")]
public class GestureDefinitionAsset : ScriptableObject
{
    [SerializeField]
    private string id;
    [SerializeField]
    private string displayName;
    [SerializeField]
    private GestureType type;
    [SerializeField]
    private float timeWindow = 1f;
    [SerializeField]
    private float deadZone = 50f;
    [SerializeField]
    private Vector2 swipeDirection = Vector2.up;
    [SerializeField]
    private float angleThreshold = 45f;
    [SerializeField]
    private List<InputComboStep> comboSteps = new List<InputComboStep>();
    [SerializeField]
    private List<Vector2> templatePoints = new List<Vector2>();
    [SerializeField]
    private float matchThreshold = 0.3f;
    
    // Public properties
    public string Id => id;
    public string DisplayName => displayName;
    public GestureType Type => type;
    public float TimeWindow => timeWindow;
    public float DeadZone => deadZone;
    public Vector2 SwipeDirection => swipeDirection;
    public float AngleThreshold => angleThreshold;
    public IReadOnlyList<InputComboStep> ComboSteps => comboSteps;
    public IReadOnlyList<Vector2> TemplatePoints => templatePoints;
    public float MatchThreshold => matchThreshold;

    public void Initialize(string newId, string newName = "", GestureType gestureType = GestureType.Swipe)
    {
        id = newId;
        displayName = newName;
        type = gestureType;
    }

    public GestureDefinition ToRuntimeDefinition()
    {
        return new GestureDefinition
        {
            Name = id,
            Type = type,
            TimeWindow = timeWindow,
            DeadZone = deadZone,
            SwipeDirection = swipeDirection,
            AngleThreshold = angleThreshold,
            ComboSteps = new List<InputComboStep>(comboSteps),
            TemplatePoints = templatePoints.ToArray(),
            MatchThreshold = matchThreshold
        };
    }
}
