using System.Collections.Generic;
using UnityEngine;

namespace UnityHelperSDK.Tutorial
{
    /// <summary>
    /// ScriptableObject for storing tutorial category data
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialCategory", menuName = "Tutorials/Tutorial Category", order = 0)]
    public class TutorialCategory : ScriptableObject
    {
        public string Id;
        public string Name;
        [TextArea]
        public string Description;
        public int SortOrder;
        public List<string> TutorialIds = new();

        public static TutorialCategory[] LoadAllCategories()
        {
            return Resources.LoadAll<TutorialCategory>("Tutorials");
        }
    }
}
