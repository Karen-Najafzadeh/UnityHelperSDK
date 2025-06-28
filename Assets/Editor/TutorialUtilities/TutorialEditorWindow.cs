using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System;
using UnityHelperSDK.Tutorial;
using UnityHelperSDK.HelperUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace UnityHelperSDK.Tutorial
{
    /// <summary>
    /// Editor window for managing tutorials and their categories,
    /// providing a bridge between runtime and editor data formats.
    /// </summary>
    public class TutorialEditorWindow : EditorWindow
    {
        private const string TUTORIALS_PATH = "Assets/Resources/Tutorials";
        private Dictionary<string, TutorialCategory> _categories;
        private Dictionary<string, TutorialDefinition> _tutorials;
        private Vector2 _scrollPosition;
        private bool _isDirty;
        private string _selectedCategory;
        private string _selectedTutorial;
        private TutorialTreeView _treeView;
        private IMGUIContainer _inspectorContainer;
        private Dictionary<int, bool> _stepFoldouts = new();
        private Dictionary<(int, int), bool> _conditionFoldouts = new();
        private int _expandedStep = -1;
        private Dictionary<int, int> _expandedCondition = new();

        [MenuItem("Unity Helper SDK/Tutorial Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<TutorialEditorWindow>();
            window.titleContent = new GUIContent("Tutorial Editor");
            window.minSize = new Vector2(800, 600);
        }

        private void OnEnable()
        {
            LoadTutorialData();
            CreateUI();
        }

        private void CreateUI()
        {
            var root = rootVisualElement;
            
            // Create a split view
            var splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
            root.Add(splitView);

            // Left side - Tree view
            _treeView = new TutorialTreeView(_categories, _tutorials);
            _treeView.OnTutorialSelectionChanged += OnTutorialSelected;
            splitView.Add(_treeView);

            // Right side - Inspector
            _inspectorContainer = new IMGUIContainer(DrawInspector);
            splitView.Add(_inspectorContainer);

            // Toolbar
            var toolbar = new Toolbar();
            
            var saveButton = new ToolbarButton(() => SaveTutorialData()) { text = "Save" };
            toolbar.Add(saveButton);
            
            var refreshButton = new ToolbarButton(() => LoadTutorialData()) { text = "Refresh" };
            toolbar.Add(refreshButton);

            var addCategoryButton = new ToolbarButton(() => AddNewCategory()) { text = "Add Category" };
            toolbar.Add(addCategoryButton);

            var addTutorialButton = new ToolbarButton(() => AddNewTutorial()) { text = "Add Tutorial" };
            toolbar.Add(addTutorialButton);

            root.Insert(0, toolbar);
        }        
        private void OnTutorialSelected(string categoryId, string tutorialId)
        {
            Debug.Log($"[TutorialEditorWindow] OnTutorialSelected - Category: {categoryId}, Tutorial: {tutorialId}");
            
            // Clear previous selection first
            _selectedCategory = null;
            _selectedTutorial = null;

            // Update selection based on what was clicked
            if (!string.IsNullOrEmpty(tutorialId) && _tutorials.ContainsKey(tutorialId))
            {
                // A tutorial was selected - verify it exists and get its category
                var tutorial = _tutorials[tutorialId];
                _selectedTutorial = tutorialId;
                _selectedCategory = tutorial.CategoryId; // Use the category from the tutorial itself
                Debug.Log($"[TutorialEditorWindow] Selected tutorial {tutorialId} in category {_selectedCategory}");
            }
            else if (!string.IsNullOrEmpty(categoryId) && _categories.ContainsKey(categoryId))
            {
                // A category was selected - verify it exists
                _selectedCategory = categoryId;
                Debug.Log($"[TutorialEditorWindow] Selected category {categoryId}");
            }
            else
            {
                Debug.Log("[TutorialEditorWindow] Nothing valid was selected");
            }

            // Force the inspector to repaint
            _inspectorContainer?.MarkDirtyRepaint();
            Repaint();
        }
        
        private void DrawInspector()
        {


            // If a tutorial is selected
            if (!string.IsNullOrEmpty(_selectedTutorial))
            {
                if (_tutorials.TryGetValue(_selectedTutorial, out var tutorial))
                {
                    // Show parent category information in a foldout
                    if (_categories.TryGetValue(_selectedCategory, out var category))
                    {
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField("Category:", GUILayout.Width(70));
                        EditorGUILayout.LabelField(category.Name, EditorStyles.boldLabel);
                    }
                    EditorGUILayout.Space();
                    }

                    // Draw the tutorial inspector
                    DrawTutorialInspector(tutorial);
                }
            }
            // If only a category is selected
            else if (!string.IsNullOrEmpty(_selectedCategory))
            {
                if (_categories.TryGetValue(_selectedCategory, out var category))
                {
                    DrawCategoryInspector(category);
                }
                }
                else if (string.IsNullOrEmpty(_selectedCategory) && string.IsNullOrEmpty(_selectedTutorial))
                {
                EditorGUILayout.HelpBox("Select a tutorial or category to edit", MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            EditorGUILayout.EndScrollView();

            if (_isDirty)
            {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Save Changes", GUILayout.Width(120)))
            {
                SaveTutorialData();
            }
            EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawCategoryInspector(TutorialCategory category)
        {
            EditorGUILayout.LabelField("Category Settings", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            // Direct field editing instead of using SerializedObject
            string newName = EditorGUILayout.TextField("Name", category.Name);
            string newDescription = EditorGUILayout.TextField("Description", category.Description);
            int newSortOrder = EditorGUILayout.IntField("Sort Order", category.SortOrder);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tutorials in Category", EditorStyles.boldLabel);

            var tutorialIds = category.TutorialIds;
            if (tutorialIds.Count > 0)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < tutorialIds.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    var tutorialId = tutorialIds[i];
                    if (_tutorials.TryGetValue(tutorialId, out var tutorial))
                    {
                        EditorGUILayout.LabelField($"{i + 1}. {tutorial.Title}");
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"{i + 1}. [Missing Tutorial]");
                    }
                    
                    if (GUILayout.Button("Remove", GUILayout.Width(60)))
                    {
                        tutorialIds.RemoveAt(i);
                        _isDirty = true;
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.HelpBox("No tutorials in this category", MessageType.Info);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Delete Category"))
            {
                if (EditorUtility.DisplayDialog("Delete Category", 
                    "Are you sure you want to delete this category? This will not delete the tutorials in it.", 
                    "Delete", "Cancel"))
                {
                    string assetPath = AssetDatabase.GetAssetPath(category);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        AssetDatabase.DeleteAsset(assetPath);
                    }
                    _categories.Remove(category.Id);
                    _selectedCategory = null;
                    _selectedTutorial = null;
                    _isDirty = false;
                    _treeView?.Refresh(_categories, _tutorials);
                    return;
                }
            }

            // if (EditorGUI.EndChangeCheck())
            // {
            //     // Update category fields if changed
            //     if (newName != category.Name || newDescription != category.Description || newSortOrder != category.SortOrder)
            //     {
            //         category.Initialize(category.Id, newName, newDescription, newSortOrder);
            //         _isDirty = true;
            //     }
            // }
        }

        private void DrawTutorialInspector(TutorialDefinition tutorial)
        {
            EditorGUILayout.LabelField("Tutorial Settings", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("ID", tutorial.Id);
            EditorGUI.EndDisabledGroup();

            string newTitle = EditorGUILayout.TextField("Title", tutorial.Title);
            string newDescription = EditorGUILayout.TextField("Description", tutorial.Description);
            int newRequiredLevel = EditorGUILayout.IntField("Required Level", tutorial.RequiredLevel);
            bool newOnlyShowOnce = EditorGUILayout.Toggle("Only Show Once", tutorial.OnlyShowOnce);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Dependencies", EditorStyles.boldLabel);
            
            var dependencies = tutorial.Dependencies;
            if (dependencies.Count > 0)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < dependencies.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (_tutorials.TryGetValue(dependencies[i], out var depTutorial))
                    {
                        EditorGUILayout.LabelField($"{i + 1}. {depTutorial.Title}");
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"{i + 1}. [Missing Tutorial]");
                    }
                    
                    if (GUILayout.Button("Remove", GUILayout.Width(60)))
                    {
                        dependencies.RemoveAt(i);
                        _isDirty = true;
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Start Conditions", EditorStyles.boldLabel);
            var startConditions = tutorial.StartConditions;
            if (startConditions.Count > 0)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < startConditions.Count; i++)
                {
                    if (!_conditionFoldouts.ContainsKey((-1, i))) _conditionFoldouts[(-1, i)] = false;
                    _conditionFoldouts[(-1, i)] = EditorGUIHelper.FoldoutSection($"Start Condition {i + 1}", _conditionFoldouts[(-1, i)]);
                    if (_conditionFoldouts[(-1, i)])
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        var condition = startConditions[i];
                        condition.EventId = EditorGUILayout.TextField("Event ID", condition.EventId);
                        condition.ConditionType = (UnityHelperSDK.Tutorial.TutorialConditionType)EditorGUILayout.EnumPopup("Condition Type", condition.ConditionType);
                        EditorGUILayout.LabelField("Parameters");
                        EditorGUI.indentLevel++;
                        if (condition.Parameters == null) condition.Parameters = new string[0];
                        for (int p = 0; p < condition.Parameters.Length; p++)
                        {
                            EditorGUILayout.BeginHorizontal();
                            condition.Parameters[p] = EditorGUILayout.TextField($"Parameter {p + 1}", condition.Parameters[p]);
                            if (GUILayout.Button("-", GUILayout.Width(20)))
                            {
                                var newParams = condition.Parameters.ToList();
                                newParams.RemoveAt(p);
                                condition.Parameters = newParams.ToArray();
                                _isDirty = true;
                                break;
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                        if (GUILayout.Button("Add Parameter"))
                        {
                            var newParams = condition.Parameters.ToList();
                            newParams.Add("");
                            condition.Parameters = newParams.ToArray();
                            _isDirty = true;
                        }
                        EditorGUI.indentLevel--;
                        if (GUILayout.Button("Remove Condition"))
                        {
                            startConditions.RemoveAt(i);
                            _isDirty = true;
                            break;
                        }
                        EditorGUILayout.EndVertical();
                    }
                }
                EditorGUI.indentLevel--;
            }
            if (GUILayout.Button("Add Start Condition"))
            {
                startConditions.Add(new TutorialConditionData { EventId = "", ConditionType = UnityHelperSDK.Tutorial.TutorialConditionType.Start, Parameters = new string[0] });
                _isDirty = true;
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tutorial Steps", EditorStyles.boldLabel);
            var steps = tutorial.Steps;
            if (steps.Count > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Collapse All Steps", GUILayout.Width(150)))
                {
                    _expandedStep = -1;
                    _expandedCondition.Clear();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel++;
                for (int i = 0; i < steps.Count; i++)
                {
                    bool expanded = _expandedStep == i;
                    var step = steps[i];
                    if (GUILayout.Button($"{(expanded ? "▼" : "►")} Step {i + 1}: {step.DialogueKey}", EditorStyles.foldout))
                    {
                        _expandedStep = expanded ? -1 : i;
                        _expandedCondition.Clear();
                    }
                    if (expanded)
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        step.Id = EditorGUILayout.TextField("Step ID", step.Id);
                        step.DialogueKey = EditorGUILayout.TextField("Dialogue Key", step.DialogueKey);
                        step.TargetObject = EditorGUILayout.ObjectField("Target Object", step.TargetObject, typeof(GameObject), true) as GameObject;
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Step Conditions", EditorStyles.boldLabel);
                        var conditions = step.Conditions ??= new List<TutorialConditionData>();
                        if (conditions.Count > 0)
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("Collapse All Conditions", GUILayout.Width(180)))
                                _expandedCondition[i] = -1;
                            EditorGUILayout.EndHorizontal();
                        }
                        for (int c = 0; c < conditions.Count; c++)
                        {
                            bool condExpanded = _expandedCondition.TryGetValue(i, out int idx) && idx == c;
                            if (GUILayout.Button($"{(condExpanded ? "▼" : "►")} Condition {c + 1}", EditorStyles.foldout))
                                _expandedCondition[i] = condExpanded ? -1 : c;
                            if (condExpanded)
                            {
                                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                                var condition = conditions[c];
                                condition.EventId = EditorGUILayout.TextField("Event ID", condition.EventId);
                                condition.ConditionType = (UnityHelperSDK.Tutorial.TutorialConditionType)EditorGUILayout.EnumPopup("Condition Type", condition.ConditionType);
                                EditorGUILayout.LabelField("Parameters");
                                EditorGUI.indentLevel++;
                                if (condition.Parameters == null) condition.Parameters = new string[0];
                                for (int p = 0; p < condition.Parameters.Length; p++)
                                {
                                    EditorGUILayout.BeginHorizontal();
                                    condition.Parameters[p] = EditorGUILayout.TextField($"Parameter {p + 1}", condition.Parameters[p]);
                                    if (GUILayout.Button("-", GUILayout.Width(20)))
                                    {
                                        var newParams = condition.Parameters.ToList();
                                        newParams.RemoveAt(p);
                                        condition.Parameters = newParams.ToArray();
                                        _isDirty = true;
                                        break;
                                    }
                                    EditorGUILayout.EndHorizontal();
                                }
                                if (GUILayout.Button("Add Parameter"))
                                {
                                    var newParams = condition.Parameters.ToList();
                                    newParams.Add("");
                                    condition.Parameters = newParams.ToArray();
                                    _isDirty = true;
                                }
                                EditorGUI.indentLevel--;
                                if (GUILayout.Button("Remove Condition"))
                                {
                                    conditions.RemoveAt(c);
                                    _isDirty = true;
                                    break;
                                }
                                EditorGUILayout.EndVertical();
                            }
                        }
                        if (GUILayout.Button("Add Step Condition"))
                        {
                            step.Conditions.Add(new TutorialConditionData { EventId = "", ConditionType = UnityHelperSDK.Tutorial.TutorialConditionType.Step, Parameters = new string[0] });
                            _isDirty = true;
                        }
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Completion Condition", EditorStyles.boldLabel);
                        if (step.CompletionCondition == null)
                        {
                            if (GUILayout.Button("Add Completion Condition"))
                            {
                                step.CompletionCondition = new TutorialConditionData { EventId = "", ConditionType = UnityHelperSDK.Tutorial.TutorialConditionType.Step, Parameters = new string[0] };
                                _isDirty = true;
                            }
                        }
                        else
                        {
                            var completion = step.CompletionCondition;
                            completion.EventId = EditorGUILayout.TextField("Event ID", completion.EventId);
                            completion.ConditionType = (UnityHelperSDK.Tutorial.TutorialConditionType)EditorGUILayout.EnumPopup("Condition Type", completion.ConditionType);
                            EditorGUILayout.LabelField("Parameters");
                            EditorGUI.indentLevel++;
                            if (completion.Parameters == null) completion.Parameters = new string[0];
                            for (int p = 0; p < completion.Parameters.Length; p++)
                            {
                                EditorGUILayout.BeginHorizontal();
                                completion.Parameters[p] = EditorGUILayout.TextField($"Parameter {p + 1}", completion.Parameters[p]);
                                if (GUILayout.Button("-", GUILayout.Width(20)))
                                {
                                    var newParams = completion.Parameters.ToList();
                                    newParams.RemoveAt(p);
                                    completion.Parameters = newParams.ToArray();
                                    _isDirty = true;
                                    break;
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                            if (GUILayout.Button("Add Parameter"))
                            {
                                var newParams = completion.Parameters.ToList();
                                newParams.Add("");
                                completion.Parameters = newParams.ToArray();
                                _isDirty = true;
                            }
                            EditorGUI.indentLevel--;
                            if (GUILayout.Button("Remove Completion Condition"))
                            {
                                step.CompletionCondition = null;
                                _isDirty = true;
                            }
                        }
                        if (GUILayout.Button("Remove Step"))
                        {
                            steps.RemoveAt(i);
                            _isDirty = true;
                            break;
                        }
                        EditorGUILayout.EndVertical();
                    }
                }
                EditorGUI.indentLevel--;
            }
            if (GUILayout.Button("Add Step"))
            {
                steps.Add(new TutorialStepData { Id = System.Guid.NewGuid().ToString(), DialogueKey = "", Conditions = new List<TutorialConditionData>() });
                _isDirty = true;
            }
            EditorGUILayout.Space();
            if (GUILayout.Button("Add Dependency"))
            {
                var dependencyMenu = new GenericMenu();
                foreach (var kvp in _tutorials)
                {
                    if (kvp.Key != tutorial.Id && !tutorial.Dependencies.Contains(kvp.Key))
                    {
                        dependencyMenu.AddItem(new GUIContent(kvp.Value.Title), false, () =>
                        {
                            tutorial.Dependencies.Add(kvp.Key);
                            _isDirty = true;
                        });
                    }
                }
                dependencyMenu.ShowAsContext();
            }
            if (GUILayout.Button("Delete Tutorial"))
            {
                if (EditorUtility.DisplayDialog("Delete Tutorial", "Are you sure you want to delete this tutorial?", "Delete", "Cancel"))
                {
                    if (_categories.TryGetValue(tutorial.CategoryId, out var category))
                    {
                        category.TutorialIds.Remove(tutorial.Id);
                        EditorUtility.SetDirty(category);
                    }
                    string assetPath = AssetDatabase.GetAssetPath(tutorial);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        AssetDatabase.DeleteAsset(assetPath);
                    }
                    _tutorials.Remove(tutorial.Id);
                    _selectedCategory = null;
                    _selectedTutorial = null;
                    _isDirty = false;
                    _treeView?.Refresh(_categories, _tutorials);
                    return;
                }
            }

            // if (EditorGUI.EndChangeCheck())
            // {
            //     if (newTitle != tutorial.Title || 
            //         newDescription != tutorial.Description || 
            //         newRequiredLevel != tutorial.RequiredLevel || 
            //         newOnlyShowOnce != tutorial.OnlyShowOnce)
            //     {
            //         tutorial.Initialize(
            //             tutorial.Id,
            //             tutorial.CategoryId,
            //             newTitle,
            //             newDescription,
            //             newRequiredLevel,
            //             newOnlyShowOnce
            //         );
            //         _isDirty = true;
            //     }
            // }
        }          
        private void LoadTutorialData()
        {
            Debug.Log("[TutorialEditorWindow] Loading ScriptableObject tutorial data...");
            _categories = new Dictionary<string, TutorialCategory>();
            _tutorials = new Dictionary<string, TutorialDefinition>();

            // Load all ScriptableObject assets from Resources/Tutorials
            foreach (var cat in TutorialCategory.LoadAllCategories())
            {
                if (!string.IsNullOrEmpty(cat.Id))
                    _categories[cat.Id] = cat;
            }
            foreach (var tut in TutorialDefinition.LoadAllDefinitions())
            {
                if (!string.IsNullOrEmpty(tut.Id))
                    _tutorials[tut.Id] = tut;
            }

            _selectedCategory = null;
            _selectedTutorial = null;
            _isDirty = false;
            _treeView?.Refresh(_categories, _tutorials);
            Repaint();
        }

        private void SaveTutorialData()
        {
            // No-op: ScriptableObjects are saved as assets, not as JSON.
            AssetDatabase.SaveAssets();
            _isDirty = false;
            _treeView?.Refresh(_categories, _tutorials);
            Debug.Log("[TutorialEditorWindow] ScriptableObject tutorial data saved!");
            AssetDatabase.Refresh();
            Repaint();
        }

        private void AddNewCategory()
        {
            var newId = "category_" + Guid.NewGuid().ToString("N");
            var assetPath = $"Assets/Resources/Tutorials/{newId}.asset";
            var category = ScriptableObject.CreateInstance<TutorialCategory>();
            category.Id = newId;
            category.Name = $"New Category {_categories.Count + 1}";
            category.Description = "New category description";
            category.SortOrder = _categories.Count;
            AssetDatabase.CreateAsset(category, assetPath);
            AssetDatabase.SaveAssets();
            _categories[category.Id] = category;
            _isDirty = false;
            _selectedCategory = category.Id;
            _selectedTutorial = null;
            _treeView?.Refresh(_categories, _tutorials);
        }

        private void AddNewTutorial()
        {
            var selectedCategoryId = _selectedCategory ?? _categories.Keys.FirstOrDefault();
            if (string.IsNullOrEmpty(selectedCategoryId))
            {
                EditorUtility.DisplayDialog("Error", "Please select or create a category first.", "OK");
                return;
            }
            var newId = "tutorial_" + Guid.NewGuid().ToString("N");
            var assetPath = $"Assets/Resources/Tutorials/{newId}.asset";
            var tutorial = ScriptableObject.CreateInstance<TutorialDefinition>();
            tutorial.Id = newId;
            tutorial.CategoryId = selectedCategoryId;
            tutorial.Title = $"New Tutorial {_tutorials.Count + 1}";
            tutorial.Description = "Tutorial description";
            AssetDatabase.CreateAsset(tutorial, assetPath);
            AssetDatabase.SaveAssets();
            _tutorials[tutorial.Id] = tutorial;
            if (_categories.TryGetValue(selectedCategoryId, out var category))
            {
                if (!category.TutorialIds.Contains(tutorial.Id))
                    category.TutorialIds.Add(tutorial.Id);
                EditorUtility.SetDirty(category);
            }
            _isDirty = false;
            _selectedTutorial = tutorial.Id;
            _treeView?.Refresh(_categories, _tutorials);
        }
    }
}
