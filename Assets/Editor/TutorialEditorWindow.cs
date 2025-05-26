using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

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
    private SerializedObject _serializedObject;
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
        _treeView.OnSelectionChanged += OnTutorialSelected;
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

        category.Name = EditorGUILayout.TextField("Name", category.Name);
        category.Description = EditorGUILayout.TextField("Description", category.Description);
        category.SortOrder = EditorGUILayout.IntField("Sort Order", category.SortOrder);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tutorials in Category", EditorStyles.boldLabel);

        if (category.TutorialIds != null && category.TutorialIds.Any())
        {
            for (int i = 0; i < category.TutorialIds.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Tutorial {i + 1}: {category.TutorialIds[i]}");
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    category.TutorialIds.RemoveAt(i);
                    _isDirty = true;
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No tutorials in this category", MessageType.Info);
        }

        if (EditorGUI.EndChangeCheck())
        {
            _isDirty = true;
        }
    }

    private void DrawTutorialInspector(TutorialDefinition tutorial)
    {
        EditorGUILayout.LabelField("Tutorial Settings", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();

        tutorial.Category = EditorGUILayout.TextField("Category", tutorial.Category);
        tutorial.RequiredLevel = EditorGUILayout.IntField("Required Level", tutorial.RequiredLevel);
        tutorial.OnlyShowOnce = EditorGUILayout.Toggle("Only Show Once", tutorial.OnlyShowOnce);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Steps", EditorStyles.boldLabel);

        if (tutorial.Steps != null && tutorial.Steps.Any())
        {
            for (int i = 0; i < tutorial.Steps.Count; i++)
            {
                DrawTutorialStep(tutorial.Steps[i], i);
            }
        }

        if (GUILayout.Button("Add Step"))
        {
            if (tutorial.Steps == null)
                tutorial.Steps = new List<TutorialStep>();
                
            tutorial.Steps.Add(new TutorialStep
            {
                Id = $"step_{tutorial.Steps.Count + 1}",
                DialogueKey = "tutorial.new.step",
                RequiredConditions = new List<string>(),
                CompletionCondition = ""
            });
            _isDirty = true;
        }

        if (EditorGUI.EndChangeCheck())
        {
            _isDirty = true;
        }
    }

    private void DrawTutorialStep(TutorialStep step, int index)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        EditorGUILayout.LabelField($"Step {index + 1}", EditorStyles.boldLabel);
        
        step.Id = EditorGUILayout.TextField("ID", step.Id);
        step.DialogueKey = EditorGUILayout.TextField("Dialogue Key", step.DialogueKey);
        step.TargetObjectPath = EditorGUILayout.TextField("Target Object Path", step.TargetObjectPath);
        step.CompletionCondition = EditorGUILayout.TextField("Completion Condition", step.CompletionCondition);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Required Conditions");
        if (GUILayout.Button("+", GUILayout.Width(20)))
        {
            if (step.RequiredConditions == null)
                step.RequiredConditions = new List<string>();
            step.RequiredConditions.Add("");
            _isDirty = true;
        }
        EditorGUILayout.EndHorizontal();

        if (step.RequiredConditions != null)
        {
            for (int i = 0; i < step.RequiredConditions.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                step.RequiredConditions[i] = EditorGUILayout.TextField(step.RequiredConditions[i]);
                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    step.RequiredConditions.RemoveAt(i);
                    _isDirty = true;
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        if (GUILayout.Button("Remove Step"))
        {
            _tutorials[_selectedTutorial].Steps.RemoveAt(index);
            _isDirty = true;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
    }

    private void LoadTutorialData()
    {
        var categoriesPath = Path.Combine(TUTORIALS_PATH, "tutorial_categories.json");
        var tutorialsPath = Path.Combine(TUTORIALS_PATH, "tutorial_definitions.json");

        _categories = JsonUtility.FromJson<Dictionary<string, TutorialCategory>>(
            File.ReadAllText(categoriesPath));
        _tutorials = JsonUtility.FromJson<Dictionary<string, TutorialDefinition>>(
            File.ReadAllText(tutorialsPath));

        _isDirty = false;
        _treeView?.Refresh(_categories, _tutorials);
    }

    private void SaveTutorialData()
    {
        if (!_isDirty) return;

        var categoriesPath = Path.Combine(TUTORIALS_PATH, "tutorial_categories.json");
        var tutorialsPath = Path.Combine(TUTORIALS_PATH, "tutorial_definitions.json");

        File.WriteAllText(categoriesPath, JsonUtility.ToJson(_categories, true));
        File.WriteAllText(tutorialsPath, JsonUtility.ToJson(_tutorials, true));

        _isDirty = false;
        AssetDatabase.Refresh();
    }

    private void AddNewCategory()
    {
        var newId = $"category_{_categories.Count + 1}";
        _categories[newId] = new TutorialCategory
        {
            Id = newId,
            Name = "New Category",
            Description = "Description",
            TutorialIds = new List<string>(),
            SortOrder = _categories.Count + 1
        };
        _isDirty = true;
        _treeView?.Refresh(_categories, _tutorials);
    }

    private void AddNewTutorial()
    {
        var newId = $"tutorial_{_tutorials.Count + 1}";
        _tutorials[newId] = new TutorialDefinition
        {
            Id = newId,
            Category = _selectedCategory ?? _categories.Keys.FirstOrDefault(),
            RequiredLevel = 1,
            OnlyShowOnce = true,
            Steps = new List<TutorialStep>(),
            Dependencies = new List<string>(),
            CustomData = new Dictionary<string, object>()
        };
        _isDirty = true;
        _treeView?.Refresh(_categories, _tutorials);
    }
}

[System.Serializable]
public class TutorialCategory
{
    public string Id;
    public string Name;
    public string Description;
    public List<string> TutorialIds;
    public int SortOrder;
}

[System.Serializable]
public class TutorialDefinition
{
    public string Id;
    public string Category;
    public int RequiredLevel;
    public bool OnlyShowOnce;
    public List<TutorialStep> Steps;
    public List<string> Dependencies;
    public Dictionary<string, object> CustomData;
}

[System.Serializable]
public class TutorialStep
{
    public string Id;
    public string DialogueKey;
    public string TargetObjectPath;
    public List<string> RequiredConditions;
    public string CompletionCondition;
    public Dictionary<string, object> CustomData;
}
