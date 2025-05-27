using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System;

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

        [MenuItem("Window/Tutorial Editor")]
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
            _selectedCategory = categoryId;
            _selectedTutorial = tutorialId;
            _inspectorContainer?.MarkDirtyRepaint();
        }

        private void DrawInspector()
        {
            if (string.IsNullOrEmpty(_selectedCategory) && string.IsNullOrEmpty(_selectedTutorial))
            {
                EditorGUILayout.HelpBox("Select a tutorial or category to edit", MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (!string.IsNullOrEmpty(_selectedCategory) && _categories.TryGetValue(_selectedCategory, out var category))
            {
                DrawCategoryInspector(category);
            }

            if (!string.IsNullOrEmpty(_selectedTutorial) && _tutorials.TryGetValue(_selectedTutorial, out var tutorial))
            {
                DrawTutorialInspector(tutorial);
            }

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

            SerializedObject serializedObject = new SerializedObject(category);
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("Name"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sortOrder"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tutorials in Category", EditorStyles.boldLabel);

            SerializedProperty tutorialIdsProperty = serializedObject.FindProperty("tutorialIds");
            if (tutorialIdsProperty.arraySize > 0)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < tutorialIdsProperty.arraySize; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    var tutorialId = tutorialIdsProperty.GetArrayElementAtIndex(i).stringValue;
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
                        // Remove from category
                        tutorialIdsProperty.DeleteArrayElementAtIndex(i);
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
                    // Delete the category asset
                    var path = Path.Combine(TUTORIALS_PATH, $"{category.Id}.asset");
                    AssetDatabase.DeleteAsset(path);
                    
                    // Remove from dictionary
                    _categories.Remove(category.Id);
                    
                    // Clear selection
                    _selectedCategory = null;
                    _selectedTutorial = null;
                    
                    // Mark dirty and refresh
                    _isDirty = true;
                    _treeView?.Refresh(_categories, _tutorials);
                    return;
                }
            }

            if (EditorGUI.EndChangeCheck() || serializedObject.hasModifiedProperties)
            {
                serializedObject.ApplyModifiedProperties();
                _isDirty = true;
            }
        }

        private void DrawTutorialInspector(TutorialDefinition tutorial)
        {
            EditorGUILayout.LabelField("Tutorial Settings", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            SerializedObject serializedObject = new SerializedObject(tutorial);
            serializedObject.Update();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("title"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("requiredLevel"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("onlyShowOnce"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Dependencies", EditorStyles.boldLabel);
            SerializedProperty dependenciesProperty = serializedObject.FindProperty("dependencies");
            
            if (dependenciesProperty.arraySize > 0)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < dependenciesProperty.arraySize; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    var dependencyId = dependenciesProperty.GetArrayElementAtIndex(i).stringValue;
                    if (_tutorials.TryGetValue(dependencyId, out var dependentTutorial))
                    {
                        EditorGUILayout.LabelField($"{i + 1}. {dependentTutorial.Title}");
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"{i + 1}. [Missing Tutorial]");
                    }
                    
                    if (GUILayout.Button("Remove", GUILayout.Width(60)))
                    {
                        dependenciesProperty.DeleteArrayElementAtIndex(i);
                        _isDirty = true;
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Dependency"))
            {
                var menu = new GenericMenu();
                foreach (var kvp in _tutorials)
                {
                    if (kvp.Key != tutorial.Id && !tutorial.Dependencies.Contains(kvp.Key))
                    {
                        menu.AddItem(new GUIContent(kvp.Value.Title), false, () => {
                            dependenciesProperty.arraySize++;
                            dependenciesProperty.GetArrayElementAtIndex(dependenciesProperty.arraySize - 1).stringValue = kvp.Key;
                            serializedObject.ApplyModifiedProperties();
                            _isDirty = true;
                        });
                    }
                }
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("steps"));

            EditorGUILayout.Space();
            if (GUILayout.Button("Delete Tutorial"))
            {
                if (EditorUtility.DisplayDialog("Delete Tutorial", 
                    "Are you sure you want to delete this tutorial?", 
                    "Delete", "Cancel"))
                {
                    // Remove from any categories that reference it
                    foreach (var category in _categories.Values)
                    {
                        if (category.TutorialIds.Remove(tutorial.Id))
                        {
                            EditorUtility.SetDirty(category);
                        }
                    }

                    // Remove from any tutorials that depend on it
                    foreach (var otherTutorial in _tutorials.Values)
                    {
                        if (otherTutorial.Dependencies.Remove(tutorial.Id))
                        {
                            EditorUtility.SetDirty(otherTutorial);
                        }
                    }

                    // Delete the asset
                    var path = Path.Combine(TUTORIALS_PATH, $"{tutorial.Id}.asset");
                    AssetDatabase.DeleteAsset(path);
                    
                    // Remove from dictionary
                    _tutorials.Remove(tutorial.Id);
                    
                    // Clear selection
                    _selectedTutorial = null;
                    
                    // Mark dirty and refresh
                    _isDirty = true;
                    _treeView?.Refresh(_categories, _tutorials);
                    return;
                }
            }

            if (EditorGUI.EndChangeCheck() || serializedObject.hasModifiedProperties)
            {
                serializedObject.ApplyModifiedProperties();
                _isDirty = true;
            }
        }

        private void LoadTutorialData()
        {
            try
            {
                _categories = new Dictionary<string, TutorialCategory>();
                _tutorials = new Dictionary<string, TutorialDefinition>();

                var tutorialJson = JsonHelper.DeserializeFromFile(Path.Combine(Application.dataPath, "Resources/Tutorials/tutorial_definitions.json"));
                var categoryJson = JsonHelper.DeserializeFromFile(Path.Combine(Application.dataPath, "Resources/Tutorials/tutorial_categories.json"));

                // Convert runtime data to editor format
                if (tutorialJson != null)
                {
                    var tutorialData = JsonHelper.Deserialize<Dictionary<string, TutorialRepository.TutorialData>>(JsonHelper.Serialize(tutorialJson));
                    foreach (var kvp in tutorialData)
                    {
                        var tutorial = TutorialDefinition.FromRuntimeData(kvp.Value);
                        var path = Path.Combine(TUTORIALS_PATH, $"{tutorial.Id}.asset");
                        var existingTutorial = AssetDatabase.LoadAssetAtPath<TutorialDefinition>(path);
                        if (existingTutorial == null)
                        {
                            AssetDatabase.CreateAsset(tutorial, path);
                        }
                        else
                        {
                            EditorUtility.CopySerializedIfDifferent(tutorial, existingTutorial);
                        }
                        _tutorials[tutorial.Id] = tutorial;
                    }
                }

                if (categoryJson != null)
                {
                    var categoryData = JsonHelper.Deserialize<Dictionary<string, TutorialRepository.TutorialCategoryData>>(JsonHelper.Serialize(categoryJson));
                    foreach (var kvp in categoryData)
                    {
                        var category = TutorialCategory.FromRuntimeData(kvp.Value);
                        var path = Path.Combine(TUTORIALS_PATH, $"{category.Id}.asset");
                        var existingCategory = AssetDatabase.LoadAssetAtPath<TutorialCategory>(path);
                        if (existingCategory == null)
                        {
                            AssetDatabase.CreateAsset(category, path);
                        }
                        else
                        {
                            EditorUtility.CopySerializedIfDifferent(category, existingCategory);
                        }
                        _categories[category.Id] = category;
                    }
                }

                AssetDatabase.SaveAssets();
                _isDirty = false;
                _treeView?.Refresh(_categories, _tutorials);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading tutorial data: {ex.Message}");
            }
        }

        private void SaveTutorialData()
        {
            if (!_isDirty)
                return;

            // Ensure directory exists
            if (!Directory.Exists(TUTORIALS_PATH))
            {
                Directory.CreateDirectory(TUTORIALS_PATH);
            }

            // Save categories
            foreach (var category in _categories.Values)
            {
                string assetPath = $"{TUTORIALS_PATH}/Category_{category.Id}.asset";
                AssetDatabase.CreateAsset(category, assetPath);
            }

            // Save tutorials
            foreach (var tutorial in _tutorials.Values)
            {
                string assetPath = $"{TUTORIALS_PATH}/Tutorial_{tutorial.Id}.asset";
                AssetDatabase.CreateAsset(tutorial, assetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            _isDirty = false;
            _treeView.Refresh(_categories, _tutorials);
            Debug.Log("Tutorial data saved successfully!");
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
            var category = CreateInstance<TutorialCategory>();
            category.Initialize(newId, $"New Category {_categories.Count + 1}", "New category description", _categories.Count);

            // Save the asset
            var path = Path.Combine(TUTORIALS_PATH, $"{category.Id}.asset");
            AssetDatabase.CreateAsset(category, path);
            _categories[category.Id] = category;
            
            // Mark dirty and refresh
            _isDirty = true;
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
            var tutorial = CreateInstance<TutorialDefinition>();
            tutorial.Initialize(newId, selectedCategoryId, $"New Tutorial {_tutorials.Count + 1}", "Tutorial description");

            // Save the asset
            var path = Path.Combine(TUTORIALS_PATH, $"{tutorial.Id}.asset");
            AssetDatabase.CreateAsset(tutorial, path);
            _tutorials[tutorial.Id] = tutorial;

            // Add to category
            if (_categories.TryGetValue(selectedCategoryId, out var category))
            {
                category.TutorialIds.Add(tutorial.Id);
                EditorUtility.SetDirty(category);
            }

            // Mark dirty and refresh
            _isDirty = true;
            _selectedTutorial = tutorial.Id;
            _treeView?.Refresh(_categories, _tutorials);
        }
    }
}
