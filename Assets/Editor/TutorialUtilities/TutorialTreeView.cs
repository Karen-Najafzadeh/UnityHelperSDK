using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityHelperSDK.TutorialUtilities;

namespace UnityHelperSDK.Tutorial
{
    /// <summary>
    /// A custom TreeView for displaying ScriptableObject-based tutorials and their steps.
    /// </summary>
    public class TutorialTreeView : VisualElement
    {
        public delegate void TutorialSelectionChangedHandler(TutorialDefinitionSO tutorial, StepDefinitionSO step);
        public event TutorialSelectionChangedHandler OnTutorialSelectionChanged;

        private TreeView _treeView;
        private List<TutorialDefinitionSO> _tutorials;
        private const float ITEM_HEIGHT = 24f;
        private List<TreeViewItemData<TutorialItemData>> _items;

        public TutorialTreeView(List<TutorialDefinitionSO> tutorials)
        {
            _tutorials = tutorials ?? new List<TutorialDefinitionSO>();
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
                    item.style.color = itemData.isStep ? new Color(0.85f, 0.85f, 0.85f) : Color.white;
                    item.style.unityFontStyleAndWeight = itemData.isStep ? FontStyle.Normal : FontStyle.Bold;
                }
            };

            _treeView.selectionChanged += OnTreeSelectionChanged;
            Add(_treeView);
            Refresh(_tutorials);
        }

        public void Refresh(List<TutorialDefinitionSO> tutorials)
        {
            _tutorials = tutorials ?? new List<TutorialDefinitionSO>();
            var items = new List<TreeViewItemData<TutorialItemData>>();
            int id = 0;
            foreach (var tutorial in _tutorials)
            {
                var stepItems = new List<TreeViewItemData<TutorialItemData>>();
                if (tutorial.Steps != null)
                {
                    foreach (var step in tutorial.Steps)
                    {
                        if (step == null) continue;
                        stepItems.Add(new TreeViewItemData<TutorialItemData>(
                            id++,
                            new TutorialItemData
                            {
                                displayName = step.Title ?? step.StepID,
                                tutorial = tutorial,
                                step = step,
                                isStep = true
                            }
                        ));
                    }
                }
                items.Add(new TreeViewItemData<TutorialItemData>(
                    id++,
                    new TutorialItemData
                    {
                        displayName = tutorial.name + (!string.IsNullOrEmpty(tutorial.TutorialID) ? $" [{tutorial.TutorialID}]" : ""),
                        tutorial = tutorial,
                        step = null,
                        isStep = false
                    },
                    stepItems
                ));
            }
            _items = items;
            _treeView.SetRootItems(_items);
            _treeView.Rebuild();
        }

        private void OnTreeSelectionChanged(IEnumerable<object> items)
        {
            var selectedItem = items.FirstOrDefault() as TutorialItemData;
            if (selectedItem != null)
            {
                OnTutorialSelectionChanged?.Invoke(selectedItem.tutorial, selectedItem.step);
            }
        }

        private class TutorialItemData
        {
            public string displayName;
            public TutorialDefinitionSO tutorial;
            public StepDefinitionSO step;
            public bool isStep;
        }
    }
}
