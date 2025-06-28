namespace UnityHelperSDK.Tutorial
{
    /// <summary>
    /// Types of conditions that can be used in tutorials
    /// </summary>
    public enum TutorialConditionType
    {
        /// <summary>
        /// Condition for starting a tutorial
        /// </summary>
        Start,

        /// <summary>
        /// Condition for completing a tutorial step
        /// </summary>
        Step,

        /// <summary>
        /// Custom condition that can be defined by the user
        /// </summary>
        Custom
    }
}
