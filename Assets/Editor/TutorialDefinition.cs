using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityHelperSDK.Editor
{
    public class TutorialDefinition : ScriptableObject
    {
        [SerializeField]
        private string id;
        [SerializeField]
        private string title;
        [SerializeField]
        private string description;
        [SerializeField]
        private string[] dependencies;
        [SerializeField]
        private bool onlyShowOnce = true;
        [SerializeField]
        private int requiredLevel;

        public string Id => id;
        public string Title => title;
        public string Description => description;
        public string[] Dependencies => dependencies;
        public bool OnlyShowOnce => onlyShowOnce;
        public int RequiredLevel => requiredLevel;

        public void Initialize(string newId, string newTitle = "", string newDescription = "", int reqLevel = 1, bool showOnce = true)
        {
            id = newId;
            title = newTitle;
            description = newDescription;
            requiredLevel = reqLevel;
            onlyShowOnce = showOnce;
            dependencies = new string[0];
        }
    }

    public class TutorialCategory : ScriptableObject
    {
        [SerializeField]
        private string id;
        [SerializeField]
        private string displayName;  // Changed from name to avoid conflict
        [SerializeField]
        private string description;
        [SerializeField]
        private int sortOrder;
        [SerializeField]
        private List<string> tutorialIds;

        public string Id => id;
        public string Name => displayName;  // Property still called Name for consistency
        public string Description => description;
        public int SortOrder => sortOrder;
        public List<string> TutorialIds => tutorialIds;

        public void Initialize(string newId, string newName = "", string newDescription = "", int order = 0)
        {
            id = newId;
            displayName = newName;
            description = newDescription;
            sortOrder = order;
            tutorialIds = new List<string>();
        }
    }
}
