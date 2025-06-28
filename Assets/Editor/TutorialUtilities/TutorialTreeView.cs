using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityHelperSDK.Tutorial
{
    /// <summary>
    /// A custom TreeView implementation for displaying and managing tutorials.
    /// Supports both runtime and editor data formats.
    /// </summary>
    public class TutorialTreeView : VisualElement
    {
        public delegate void TutorialSelectionChangedHandler(string categoryId, string tutorialId);
        public event TutorialSelectionChangedHandler OnTutorialSelectionChanged;

        private TreeView _treeView;
        private Dictionary<string, TutorialCategory> _categories;
        private Dictionary<string, TutorialDefinition> _tutorials;
        private const float ITEM_HEIGHT = 24f;
        private List<TreeViewItemData<TutorialItemData>> _items;

        /// <summary>
        /// Initializes a new instance of the TutorialTreeView.
        /// </summary>
        /// <param name="categories">Dictionary of tutorial categories</param>
        /// <param name="tutorials">Dictionary of tutorial definitions</param>
        public TutorialTreeView(Dictionary<string, TutorialCategory> categories, Dictionary<string, TutorialDefinition> tutorials)
        {
            _categories = categories ?? new Dictionary<string, TutorialCategory>();
            _tutorials = tutorials ?? new Dictionary<string, TutorialDefinition>();
            
            style.flexGrow = 1;

            _treeView = new TreeView
            {
                fixedItemHeight = ITEM_HEIGHT,
                selectionType = SelectionType.Single,
                showBorder = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.All
            };

            _treeView.style.flexGrow = 1;
            _treeView.viewDataKey = "TutorialTreeView";
            
            // Setup makeItem and bindItem
            _treeView.makeItem = () => {
                var item = new Label();
                item.style.paddingLeft = 4;
                item.style.unityTextAlign = TextAnchor.MiddleLeft;
                item.style.height = ITEM_HEIGHT;
                return item;
            };

            _treeView.bindItem = (element, index) => 
            {
                var item = element as Label;
                var itemData = _treeView.GetItemDataForIndex<TutorialItemData>(index);
                if (item != null && itemData != null)
                {
                    item.text = itemData.displayName;
                    item.style.color = string.IsNullOrEmpty(itemData.tutorialId) ? 
                        new Color(0.7f, 0.7f, 0.7f) : // Category color
                        Color.white; // Tutorial color
                    item.style.unityFontStyleAndWeight = string.IsNullOrEmpty(itemData.tutorialId) ? 
                        FontStyle.Bold : FontStyle.Normal;
                }
            };
            
            _treeView.selectionChanged += OnTreeSelectionChanged;

            Add(_treeView);
            Refresh(categories, tutorials);
        }

        public void Refresh(Dictionary<string, TutorialCategory> categories, Dictionary<string, TutorialDefinition> tutorials)
        {
            _categories = categories ?? new Dictionary<string, TutorialCategory>();
            _tutorials = tutorials ?? new Dictionary<string, TutorialDefinition>();

            var items = new List<TreeViewItemData<TutorialItemData>>();
            int id = 0;

            // Create root item for "Tutorials"
            var root = new TreeViewItemData<TutorialItemData>(
                id++,
                new TutorialItemData { displayName = "Tutorials", categoryId = "", tutorialId = "" }
            );

            // Create category items
            var categoryItems = new List<TreeViewItemData<TutorialItemData>>();
            foreach (var category in _categories.Values.OrderBy(c => c.SortOrder))
            {
                var tutorialItems = new List<TreeViewItemData<TutorialItemData>>();
                
                // Add tutorials for this category
                if (category.TutorialIds != null)
                {
                    foreach (var tutorialId in category.TutorialIds)
                    {
                        if (_tutorials.TryGetValue(tutorialId, out var tutorial))
                        {
                            tutorialItems.Add(new TreeViewItemData<TutorialItemData>(
                                id++,
                                new TutorialItemData
                                {
                                    displayName = tutorial.Title ?? tutorial.Id,
                                    categoryId = category.Id,
                                    tutorialId = tutorial.Id
                                }
                            ));
                        }
                    }
                }

                // Create category item with its tutorials as children
                categoryItems.Add(new TreeViewItemData<TutorialItemData>(
                    id++,
                    new TutorialItemData 
                    { 
                        displayName = category.Name,
                        categoryId = category.Id,
                        tutorialId = ""
                    },
                    tutorialItems // Add tutorials as children
                ));
            }

            // Add categories under root
            items.Add(new TreeViewItemData<TutorialItemData>(root.id, root.data, categoryItems));
            _items = items;

            // Set the items source
            _treeView.SetRootItems(_items);
            _treeView.Rebuild();
        }        
        private void OnTreeSelectionChanged(IEnumerable<object> items)
        {
            var selectedItem = items.FirstOrDefault() as TutorialItemData;
            var itemData = selectedItem;
            if (itemData != null)
            {
                Debug.Log($"tree view selection: Selected item - displayName: {itemData.displayName}, categoryId: {itemData.categoryId}, tutorialId: {itemData.tutorialId}");
                // Invoke with both IDs, letting the handler determine what to do based on which one is empty
                OnTutorialSelectionChanged?.Invoke(itemData.categoryId, itemData.tutorialId);
            }
            else
            {
            Debug.Log("tree view selection: Selected item is not an int index");
            }
        }

        private class TutorialItemData
        {
            public string displayName;
            public string categoryId;
            public string tutorialId;
        }
    }
}
