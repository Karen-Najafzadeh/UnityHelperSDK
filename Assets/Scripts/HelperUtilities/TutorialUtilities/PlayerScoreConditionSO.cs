using UnityEngine;

    [CreateAssetMenu(menuName = "Unity Helper SDK/Tutorial System/Condition/Player Score")]
    public class PlayerScoreConditionSO : ConditionSO
    {
        public int RequiredScore;
        public override bool IsMet()
        {
        // Replace with your actual game manager logic
        return true;
        }
    }