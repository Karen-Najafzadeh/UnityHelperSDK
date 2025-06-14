using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityHelperSDK.Tutorial;


namespace UnityHelperSDK.Editor{


    /// <summary>
    /// ScriptableObject wrapper for runtime TutorialCategoryData
    /// </summary>
    public class TutorialCategory : ScriptableObject
    {
        [SerializeField]
        private string id;
        [SerializeField]
        private string displayName;
        [SerializeField]
        private string description;
        [SerializeField]
        private int sortOrder;
        [SerializeField]
        private List<string> tutorialIds = new List<string>();

        // Public properties
        public string Id => id;
        public string Name => displayName;
        public string Description => description;
        public int SortOrder => sortOrder;
        public List<string> TutorialIds => tutorialIds;

        public void Initialize(string newId, string newName = "", string newDescription = "", int order = 0)
        {
            id = newId;
            displayName = newName;
            description = newDescription;
            sortOrder = order;
        }

        /// <summary>
        /// Convert this ScriptableObject to runtime TutorialCategoryData
        /// </summary>
        public TutorialRepository.TutorialCategoryData ToRuntimeData()
        {
            return new TutorialRepository.TutorialCategoryData
            {
                Id = id,
                Name = displayName,
                Description = description,
                Order = sortOrder
            };
        }

        /// <summary>
        /// Create a new TutorialCategory from runtime TutorialCategoryData
        /// </summary>
        public static TutorialCategory FromRuntimeData(TutorialRepository.TutorialCategoryData data)
        {
            var category = CreateInstance<TutorialCategory>();
            category.id = data.Id;
            category.displayName = data.Name;
            category.description = data.Description;
            category.sortOrder = data.Order;
            return category;
        }
    }
}