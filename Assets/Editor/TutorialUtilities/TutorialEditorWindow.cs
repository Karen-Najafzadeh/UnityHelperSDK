using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System;
using UnityHelperSDK.Tutorial;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace UnityHelperSDK.Editor
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
                    _categories.Remove(category.Id);
                    _selectedCategory = null;
                    _selectedTutorial = null;
                    _isDirty = true;
                    _treeView?.Refresh(_categories, _tutorials);
                    return;
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                // Update category fields if changed
                if (newName != category.Name || newDescription != category.Description || newSortOrder != category.SortOrder)
                {
                    category.Initialize(category.Id, newName, newDescription, newSortOrder);
                    _isDirty = true;
                }
            }
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
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                              var condition = startConditions[i];
            condition.EventId = EditorGUILayout.TextField("Event ID", condition.EventId);
            condition.ConditionType = (UnityHelperSDK.Tutorial.TutorialConditionType)EditorGUILayout.EnumPopup("Condition Type", condition.ConditionType);
                    
                    EditorGUILayout.LabelField("Parameters");
                    EditorGUI.indentLevel++;
                    
                    if (condition.Parameters == null)
                        condition.Parameters = new string[0];
                        
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
                EditorGUI.indentLevel--;
            }

            if (GUILayout.Button("Add Start Condition"))
            {
                startConditions.Add(new TutorialConditionData 
                { 
                    EventId = "", 
                    ConditionType = UnityHelperSDK.Tutorial.TutorialConditionType.Start,
                    Parameters = new string[0]
                });
                _isDirty = true;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tutorial Steps", EditorStyles.boldLabel);

            var steps = tutorial.Steps;
            if (steps.Count > 0)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < steps.Count; i++)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    
                    var step = steps[i];
                    step.Id = EditorGUILayout.TextField("Step ID", step.Id);
                    step.DialogueKey = EditorGUILayout.TextField("Dialogue Key", step.DialogueKey);
                    step.TargetObject = EditorGUILayout.ObjectField("Target Object", step.TargetObject, typeof(GameObject), true) as GameObject;

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Step Conditions", EditorStyles.boldLabel);
                    
                    if (step.Conditions == null)
                        step.Conditions = new List<TutorialConditionData>();

                    for (int c = 0; c < step.Conditions.Count; c++)
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        var condition = step.Conditions[c];
                        condition.EventId = EditorGUILayout.TextField("Event ID", condition.EventId);
                        condition.ConditionType = (UnityHelperSDK.Tutorial.TutorialConditionType)EditorGUILayout.EnumPopup("Condition Type", condition.ConditionType);
                        
                        EditorGUILayout.LabelField("Parameters");
                        EditorGUI.indentLevel++;
                        
                        if (condition.Parameters == null)
                            condition.Parameters = new string[0];
                            
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
                            step.Conditions.RemoveAt(c);
                            _isDirty = true;
                            break;
                        }
                        
                        EditorGUILayout.EndVertical();
                    }

                    if (GUILayout.Button("Add Step Condition"))
                    {                step.Conditions.Add(new TutorialConditionData 
                { 
                    EventId = "", 
                    ConditionType = UnityHelperSDK.Tutorial.TutorialConditionType.Step,
                    Parameters = new string[0]
                });
                        _isDirty = true;
                    }

                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("Completion Condition", EditorStyles.boldLabel);
                    
                    if (step.CompletionCondition == null)
                    {
                        if (GUILayout.Button("Add Completion Condition"))
                        {                    step.CompletionCondition = new TutorialConditionData 
                    { 
                        EventId = "", 
                        ConditionType = UnityHelperSDK.Tutorial.TutorialConditionType.Step,
                        Parameters = new string[0]
                    };
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
                        
                        if (completion.Parameters == null)
                            completion.Parameters = new string[0];
                            
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
                EditorGUI.indentLevel--;
            }

            if (GUILayout.Button("Add Step"))
            {
                steps.Add(new TutorialStepData 
                { 
                    Id = System.Guid.NewGuid().ToString(),
                    DialogueKey = "",
                    Conditions = new List<TutorialConditionData>()
                });
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
                if (EditorUtility.DisplayDialog("Delete Tutorial", 
                    "Are you sure you want to delete this tutorial?", 
                    "Delete", "Cancel"))
                {
                    if (_categories.TryGetValue(tutorial.CategoryId, out var category))
                    {
                        category.TutorialIds.Remove(tutorial.Id);
                    }
                    _tutorials.Remove(tutorial.Id);
                    _selectedCategory = null;
                    _selectedTutorial = null;
                    _isDirty = true;
                    _treeView?.Refresh(_categories, _tutorials);
                    return;
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                if (newTitle != tutorial.Title || 
                    newDescription != tutorial.Description || 
                    newRequiredLevel != tutorial.RequiredLevel || 
                    newOnlyShowOnce != tutorial.OnlyShowOnce)
                {
                    tutorial.Initialize(
                        tutorial.Id,
                        tutorial.CategoryId,
                        newTitle,
                        newDescription,
                        newRequiredLevel,
                        newOnlyShowOnce
                    );
                    _isDirty = true;
                }
            }
        }          
        private void LoadTutorialData()
        {
            Debug.Log("[TutorialEditorWindow] Entering LoadTutorialData()");

            try
            {
                Debug.Log("[TutorialEditorWindow] Loading tutorial data...");

                // Initialize empty collections
                _categories = new Dictionary<string, TutorialCategory>();
                _tutorials = new Dictionary<string, TutorialDefinition>();

                // Ensure the tutorials directory exists
                if (!Directory.Exists(TUTORIALS_PATH))
                {
                    Debug.Log($"[TutorialEditorWindow] Creating tutorials folder at: {TUTORIALS_PATH}");
                    Directory.CreateDirectory(TUTORIALS_PATH);
                }
                else
                {
                    Debug.Log($"[TutorialEditorWindow] Tutorials folder exists at: {TUTORIALS_PATH}");
                }

                // Load categories
                var categoriesPath = Path.Combine(TUTORIALS_PATH, "categories.json");
                Debug.Log($"[TutorialEditorWindow] Looking for categories at: {categoriesPath}");
                if (File.Exists(categoriesPath))
                {
                    Debug.Log($"[TutorialEditorWindow] Reading categories from: {categoriesPath}");
                    string jsonContent = File.ReadAllText(categoriesPath);
                    Debug.Log($"[TutorialEditorWindow] Categories JSON content: {jsonContent}");

                    var settings = new JsonSerializerSettings
                    {
                        ContractResolver = new DefaultContractResolver(),
                        Formatting = Formatting.Indented
                    };

                    var categories = JsonConvert.DeserializeObject<List<TutorialCategory>>(jsonContent);

                    if (categories != null && categories.Count > 0)
                    {
                        foreach (var category in categories)
                        {
                            Debug.Log($"[TutorialEditorWindow] Processing category - Id: {category.Id}, Name: {category.Name} , Description: {category.Description} , SortOrder: {category.SortOrder} tutorials: {category.TutorialIds.Count}");
                        }

                        _categories = categories.ToDictionary(c => c.Id);
                        Debug.Log($"[TutorialEditorWindow] Loaded {_categories.Count} categories successfully");
                    }
                    else
                    {
                        Debug.LogWarning("[TutorialEditorWindow] No categories found in JSON file or deserialization returned null.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[TutorialEditorWindow] Categories file does not exist at: {categoriesPath}");
                }

                // Load tutorials
                var tutorialsPath = Path.Combine(TUTORIALS_PATH, "tutorials.json");
                Debug.Log($"[TutorialEditorWindow] Looking for tutorials at: {tutorialsPath}");
                if (File.Exists(tutorialsPath))
                {
                    Debug.Log($"[TutorialEditorWindow] Reading tutorials from: {tutorialsPath}");
                    string jsonContent = File.ReadAllText(tutorialsPath);
                    Debug.Log($"[TutorialEditorWindow] Tutorials JSON content: {jsonContent}");

                    var settings = new JsonSerializerSettings
                    {
                        ContractResolver = new DefaultContractResolver(),
                        Formatting = Formatting.Indented
                    };
                    var tutorials = JsonConvert.DeserializeObject<List<TutorialDefinition>>(jsonContent);

                    if (tutorials != null && tutorials.Count > 0)
                    {
                        foreach (var tutorial in tutorials)
                        {
                            Debug.Log($"[TutorialEditorWindow] Processing tutorial - Id: {tutorial.Id}, CategoryId: {tutorial.CategoryId}, Title: {tutorial.Title}");

                            if (string.IsNullOrEmpty(tutorial.CategoryId))
                            {
                                Debug.LogError($"[TutorialEditorWindow] Tutorial '{tutorial.Id}' has an invalid category ID '{tutorial.CategoryId}'. This will be skipped.");
                                continue;
                            }

                            if (!_categories.ContainsKey(tutorial.CategoryId))
                            {
                                Debug.LogError($"[TutorialEditorWindow] Tutorial '{tutorial.Id}' references non-existent category '{tutorial.CategoryId}'. This will be skipped.");
                                continue;
                            }
                        }

                        _tutorials = tutorials.ToDictionary(t => t.Id);
                        Debug.Log($"[TutorialEditorWindow] Loaded {_tutorials.Count} tutorials successfully");
                    }
                    else
                    {
                        Debug.LogWarning("[TutorialEditorWindow] No tutorials found in JSON file or deserialization returned null.");
                    }
                }
                else
                {
                    Debug.LogWarning($"[TutorialEditorWindow] Tutorials file does not exist at: {tutorialsPath}");
                }

                // Clear selection when loading new data
                Debug.Log("[TutorialEditorWindow] Clearing selection after loading data.");
                _selectedCategory = null;
                _selectedTutorial = null;

                _isDirty = false;

                // Refresh the tree view with the loaded data
                if (_treeView != null)
                {
                    Debug.Log("[TutorialEditorWindow] Refreshing tree view.");
                    _treeView.Refresh(_categories, _tutorials);
                }

                Repaint();
                Debug.Log("[TutorialEditorWindow] Tutorial data loaded successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading tutorial data: {ex.Message}\n{ex.StackTrace}");
            }
            Debug.Log("[TutorialEditorWindow] Exiting LoadTutorialData()");
        }        
        
        private void SaveTutorialData()
        {
            if (!_isDirty)
                return;

            try
            {
                // Ensure directory exists
                if (!Directory.Exists(TUTORIALS_PATH))
                {
                    Directory.CreateDirectory(TUTORIALS_PATH);
                }

                // Save categories
                var categoriesPath = Path.Combine(TUTORIALS_PATH, "categories.json");
                var categoriesJson = UnityHelperSDK.Data.JsonHelper.Serialize(_categories.Values.ToList(), true);
                File.WriteAllText(categoriesPath, categoriesJson);
                
                // Save tutorials
                var tutorialsPath = Path.Combine(TUTORIALS_PATH, "tutorials.json");
                var tutorialsJson = UnityHelperSDK.Data.JsonHelper.Serialize(_tutorials.Values.ToList(), true);
                File.WriteAllText(tutorialsPath, tutorialsJson);
                
                _isDirty = false;
                _treeView?.Refresh(_categories, _tutorials);
                Debug.Log("[TutorialEditorWindow] Tutorial data saved successfully!");
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TutorialEditorWindow] Error saving tutorial data: {ex.Message}");
                EditorUtility.DisplayDialog("Error", "Failed to save tutorial data. Check the console for details.", "OK");
            }
            
            Repaint();
        }

        private void AddNewCategory()
        {
            // Generate a unique ID for the new category
            var newId = "category_" + (_categories.Count + 1);
            while (_categories.ContainsKey(newId))
            {
                newId = "category_" + (_categories.Count + 2);
            }

            // Create new category
            var category = new TutorialCategory();
            category.Initialize(newId, $"New Category {_categories.Count + 1}", "New category description", _categories.Count);

            _categories[category.Id] = category;
            
            // Mark dirty and save
            _isDirty = true;
            SaveTutorialData();
            
            _selectedCategory = category.Id;
            _selectedTutorial = null;
            _treeView?.Refresh(_categories, _tutorials);
        }

        private void AddNewTutorial()
        {
            // Make sure we have a selected category
            var selectedCategoryId = _selectedCategory ?? _categories.Keys.FirstOrDefault();
            if (string.IsNullOrEmpty(selectedCategoryId))
            {
                EditorUtility.DisplayDialog("Error", "Please select or create a category first.", "OK");
                return;
            }

            // Generate a unique ID for the new tutorial
            var newId = "tutorial_" + (_tutorials.Count + 1);
            while (_tutorials.ContainsKey(newId))
            {
                newId = "tutorial_" + (_tutorials.Count + 2);
            }

            // Create new tutorial
            var tutorial = new TutorialDefinition();
            tutorial.Initialize(newId, selectedCategoryId, $"New Tutorial {_tutorials.Count + 1}", "Tutorial description");

            _tutorials[tutorial.Id] = tutorial;

            // Add to category
            if (_categories.TryGetValue(selectedCategoryId, out var category))
            {
                category.TutorialIds.Add(tutorial.Id);
            }

            // Mark dirty and save
            _isDirty = true;
            SaveTutorialData();

            _selectedTutorial = tutorial.Id;
            _treeView?.Refresh(_categories, _tutorials);
        }
    }
}
