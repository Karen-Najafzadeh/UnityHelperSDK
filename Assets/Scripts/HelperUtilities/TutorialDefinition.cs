using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TutorialDefinition
{
    public string Id;
    public string Title;
    public string Description;
    public string[] Dependencies;
    public bool OnlyShowOnce = true;
    public int RequiredLevel;
}

[Serializable]
public class TutorialCategory
{
    public string Id;
    public string Name;
    public string Description;
    public int SortOrder;
    public List<string> TutorialIds;
}
