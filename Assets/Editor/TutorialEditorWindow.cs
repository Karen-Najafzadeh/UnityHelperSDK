using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityHelperSDK.Editor;
using System;

namespace UnityHelperSDK.Editor
{
    /// <summary>
    /// Editor window for managing tutorials and their categories.
    /// Provides a visual interface for editing tutorial configurations.
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
                    EditorGUILayout.PropertyField(tutorialIdsProperty.GetArrayElementAtIndex(i), new GUIContent($"Tutorial {i + 1}"));
                    if (GUILayout.Button("Remove", GUILayout.Width(60)))
                    {
                        tutorialIdsProperty.DeleteArrayElementAtIndex(i);
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
                    EditorGUILayout.PropertyField(dependenciesProperty.GetArrayElementAtIndex(i), new GUIContent($"Dependency {i + 1}"));
                    if (GUILayout.Button("-", GUILayout.Width(20)))
                    {
                        dependenciesProperty.DeleteArrayElementAtIndex(i);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }

            if (GUILayout.Button("Add Dependency"))
            {
                dependenciesProperty.arraySize++;
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

                var assetsPath = Path.Combine(Application.dataPath, "Resources/Tutorials");
                if (!Directory.Exists(assetsPath))
                {
                    Directory.CreateDirectory(assetsPath);
                }

                // Load all tutorial assets
                var guids = AssetDatabase.FindAssets("t:TutorialDefinition t:TutorialCategory", new[] { "Assets/Resources/Tutorials" });
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

                    if (asset is TutorialDefinition tutorial)
                    {
                        _tutorials[tutorial.Id] = tutorial;
                    }
                    else if (asset is TutorialCategory category)
                    {
                        _categories[category.Id] = category;
                    }
                }

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
            if (!_isDirty) return;

            try
            {
                foreach (var category in _categories.Values)
                {
                    if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(category)))
                    {
                        var path = Path.Combine(TUTORIALS_PATH, $"{category.Id}.asset");
                        AssetDatabase.CreateAsset(category, path);
                    }
                    EditorUtility.SetDirty(category);
                }

                foreach (var tutorial in _tutorials.Values)
                {
                    if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(tutorial)))
                    {
                        var path = Path.Combine(TUTORIALS_PATH, $"{tutorial.Id}.asset");
                        AssetDatabase.CreateAsset(tutorial, path);
                    }
                    EditorUtility.SetDirty(tutorial);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                _isDirty = false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error saving tutorial data: {ex.Message}");
            }
        }

        private void AddNewCategory()
        {
            var newId = $"category_{_categories.Count + 1}";
            var category = CreateInstance<TutorialCategory>();
            category.Initialize(newId, "New Category", "Description", _categories.Count + 1);

            var path = Path.Combine(TUTORIALS_PATH, $"{category.Id}.asset");
            AssetDatabase.CreateAsset(category, path);
            _categories[category.Id] = category;
            _isDirty = true;
            _treeView?.Refresh(_categories, _tutorials);
        }

        private void AddNewTutorial()
        {
            var newId = $"tutorial_{_tutorials.Count + 1}";
            var tutorial = CreateInstance<TutorialDefinition>();
            tutorial.Initialize(newId, "New Tutorial", "Description");

            var path = Path.Combine(TUTORIALS_PATH, $"{tutorial.Id}.asset");
            AssetDatabase.CreateAsset(tutorial, path);
            _tutorials[tutorial.Id] = tutorial;

            // Add to selected category
            if (!string.IsNullOrEmpty(_selectedCategory) && _categories.TryGetValue(_selectedCategory, out var category))
            {
                SerializedObject categoryObj = new SerializedObject(category);
                SerializedProperty tutorialIds = categoryObj.FindProperty("tutorialIds");
                tutorialIds.arraySize++;
                tutorialIds.GetArrayElementAtIndex(tutorialIds.arraySize - 1).stringValue = newId;
                categoryObj.ApplyModifiedProperties();
            }

            _isDirty = true;
            _treeView?.Refresh(_categories, _tutorials);
        }
    }
}
