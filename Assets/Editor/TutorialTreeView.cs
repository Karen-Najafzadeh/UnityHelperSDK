using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class TutorialTreeView : VisualElement
{
    public delegate void SelectionChangedHandler(string categoryId, string tutorialId);
    public event SelectionChangedHandler OnSelectionChanged;

    private TreeView _treeView;
    private Dictionary<string, TutorialCategory> _categories;
    private Dictionary<string, TutorialDefinition> _tutorials;

    public TutorialTreeView(Dictionary<string, TutorialCategory> categories, Dictionary<string, TutorialDefinition> tutorials)
    {
        _categories = categories;
        _tutorials = tutorials;
        
        style.flexGrow = 1;
        
        _treeView = new TreeView();
        _treeView.style.flexGrow = 1;
        _treeView.selectionType = SelectionType.Single;
        _treeView.itemsChosen += OnItemChosen;
        _treeView.selectionChanged += OnSelectionChanged;

        Add(_treeView);
        Refresh(categories, tutorials);
    }

    public void Refresh(Dictionary<string, TutorialCategory> categories, Dictionary<string, TutorialDefinition> tutorials)
    {
        _categories = categories;
        _tutorials = tutorials;

        var root = new TreeViewItem { id = 0, displayName = "Tutorials" };
        var items = new List<TreeViewItem> { root };
        var id = 1;

        foreach (var category in _categories.Values.OrderBy(c => c.SortOrder))
        {
            var categoryItem = new TreeViewItem { id = id++, displayName = category.Name };
            categoryItem.userData = new TreeItemData { CategoryId = category.Id };
            items.Add(categoryItem);

            if (category.TutorialIds != null)
            {
                foreach (var tutorialId in category.TutorialIds)
                {
                    if (_tutorials.TryGetValue(tutorialId, out var tutorial))
                    {
                        var tutorialItem = new TreeViewItem { id = id++, displayName = tutorial.Id };
                        tutorialItem.userData = new TreeItemData { CategoryId = category.Id, TutorialId = tutorial.Id };
                        items.Add(tutorialItem);
                    }
                }
            }
        }

        _treeView.SetRootItems(new[] { root });
        _treeView.SetItems(items);
    }

    private void OnItemChosen(IEnumerable<TreeViewItem> items)
    {
        var item = items.FirstOrDefault();
        if (item?.userData is TreeItemData data)
        {
            OnSelectionChanged?.Invoke(data.CategoryId, data.TutorialId);
        }
    }

    private class TreeItemData
    {
        public string CategoryId;
        public string TutorialId;
    }
}
