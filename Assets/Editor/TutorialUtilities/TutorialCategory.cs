using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityHelperSDK.Tutorial;


namespace UnityHelperSDK.Editor{


    /// <summary>
    /// Serializable class for storing tutorial category data
    /// </summary>
    [Serializable]
public class TutorialCategory
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public int SortOrder { get; set; }
    public List<string> TutorialIds { get; set; } = new();

    public TutorialCategory() { }

    public void Initialize(string newId, string newName = "", string newDescription = "", int order = 0)
    {
        Id = newId;
        Name = newName;
        Description = newDescription;
        SortOrder = order;
    }

    public UnityHelperSDK.Tutorial.TutorialRepository.TutorialCategoryData ToRuntimeData()
    {
        return new UnityHelperSDK.Tutorial.TutorialRepository.TutorialCategoryData
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Order = SortOrder
        };
    }

    public static TutorialCategory FromRuntimeData(UnityHelperSDK.Tutorial.TutorialRepository.TutorialCategoryData data)
    {
        return new TutorialCategory
        {
            Id = data.Id,
            Name = data.Name,
            Description = data.Description,
            SortOrder = data.Order
        };
    }
}

}