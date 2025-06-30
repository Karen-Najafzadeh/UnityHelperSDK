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
        private List<TutorialDefinitionSO> _tutorialAssets;
        private Vector2 _scrollPosition;
        private bool _isDirty;
        private string _selectedTutorial;
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
            root.Clear();
            // Only show a flat list for now
            _inspectorContainer = new IMGUIContainer(DrawInspector);
            root.Add(_inspectorContainer);
            // Toolbar
            var toolbar = new Toolbar();
            var saveButton = new ToolbarButton(() => SaveTutorialData()) { text = "Save" };
            toolbar.Add(saveButton);
            var refreshButton = new ToolbarButton(() => LoadTutorialData()) { text = "Refresh" };
            toolbar.Add(refreshButton);
            var addTutorialButton = new ToolbarButton(() => AddNewTutorial()) { text = "Add Tutorial" };
            toolbar.Add(addTutorialButton);
            root.Insert(0, toolbar);
        }        
        private void OnTutorialSelected(string tutorialId)
        {
            Debug.Log($"[TutorialEditorWindow] OnTutorialSelected - Tutorial: {tutorialId}");
            
            // Clear previous selection first
            _selectedTutorial = null;

            // Update selection based on what was clicked
            if (!string.IsNullOrEmpty(tutorialId) && _tutorialAssets.Any(t => t.TutorialID == tutorialId))
            {
                // A tutorial was selected - verify it exists
                _selectedTutorial = tutorialId;
                Debug.Log($"[TutorialEditorWindow] Selected tutorial {tutorialId}");
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
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // If a tutorial is selected
            if (!string.IsNullOrEmpty(_selectedTutorial))
            {
                var tutorial = _tutorialAssets.FirstOrDefault(t => t.TutorialID == _selectedTutorial);
                if (tutorial != null)
                {
                    // Draw the tutorial inspector
                    DrawTutorialInspector(tutorial);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Select a tutorial to edit", MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
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

        private void DrawTutorialInspector(TutorialDefinitionSO tutorial)
        {
            // If using TutorialDefinitionSO ScriptableObject:
            var so = new SerializedObject(tutorial);
            so.Update();

            EditorGUILayout.LabelField("Tutorial Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("TutorialID"), new GUIContent("ID"));
            EditorGUILayout.PropertyField(so.FindProperty("InitialTrigger"), new GUIContent("Initial Trigger"));
            EditorGUILayout.PropertyField(so.FindProperty("Steps"), new GUIContent("Steps"), true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Start Conditions", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("StartConditions"), new GUIContent("Start Conditions"), true);
            if (GUILayout.Button("Create New Start Condition"))
            {
                var newCond = ScriptableObject.CreateInstance<UnityHelperSDK.TutorialUtilities.ConditionSO>();
                string path = EditorUtility.SaveFilePanelInProject("Create ConditionSO", "NewStartCondition", "asset", "");
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.CreateAsset(newCond, path);
                    AssetDatabase.SaveAssets();
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Complete Conditions", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("CompleteConditions"), new GUIContent("Complete Conditions"), true);
            if (GUILayout.Button("Create New Complete Condition"))
            {
                var newCond = ScriptableObject.CreateInstance<UnityHelperSDK.TutorialUtilities.ConditionSO>();
                string path = EditorUtility.SaveFilePanelInProject("Create ConditionSO", "NewCompleteCondition", "asset", "");
                if (!string.IsNullOrEmpty(path))
                {
                    AssetDatabase.CreateAsset(newCond, path);
                    AssetDatabase.SaveAssets();
                }
            }
            so.ApplyModifiedProperties();
        }          
        private void LoadTutorialData()
        {
            Debug.Log("[TutorialEditorWindow] Loading ScriptableObject tutorial data...");
            _tutorialAssets = new List<TutorialDefinitionSO>();
            string[] guids = AssetDatabase.FindAssets("t:TutorialDefinitionSO", new[] { TUTORIALS_PATH });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<TutorialDefinitionSO>(path);
                if (asset != null)
                    _tutorialAssets.Add(asset);
            }
            _selectedTutorial = null;
            _isDirty = false;
            // Optionally, refresh tree view if you want to show a list of tutorials
            // _treeView?.Refresh(_tutorialAssets);
            Repaint();
        }

        private void SaveTutorialData()
        {
            AssetDatabase.SaveAssets();
            _isDirty = false;
            // _treeView?.Refresh(_tutorialAssets);
            Debug.Log("[TutorialEditorWindow] ScriptableObject tutorial data saved!");
            AssetDatabase.Refresh();
            Repaint();
        }

        private void AddNewTutorial()
        {
            // Count existing TutorialDefinitionSO assets
            var guids = AssetDatabase.FindAssets("t:TutorialDefinitionSO", new[] { TUTORIALS_PATH });
            int nextNumber = guids.Length + 1;
            string newId = $"tutorial_{nextNumber}";
            string assetPath = $"{TUTORIALS_PATH}/{newId}.asset";
            var tutorial = ScriptableObject.CreateInstance<TutorialDefinitionSO>();
            tutorial.TutorialID = newId;
            AssetDatabase.CreateAsset(tutorial, assetPath);
            AssetDatabase.SaveAssets();
            _tutorialAssets.Add(tutorial);
            _isDirty = false;
            _selectedTutorial = tutorial.TutorialID;
            // _treeView?.Refresh(_tutorialAssets);
        }
    }
}
