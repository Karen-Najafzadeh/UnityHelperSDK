using UnityEngine;

    public interface ICondition
    {
        bool IsMet();
    }

    [CreateAssetMenu(menuName = "Unity Helper SDK/Tutorial System/Condition/Base")]
    public class ConditionSO : ScriptableObject, ICondition
    {
        public virtual bool IsMet() => true;
    }

