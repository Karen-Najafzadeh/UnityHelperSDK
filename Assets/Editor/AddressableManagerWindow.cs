using UnityEditor;
using UnityEngine;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using System.Linq;
using System.Collections.Generic;

namespace UnityHelperSDK.Assets.Editor
{
    public class AddressableManagerWindow : EditorWindow
    {
        private Vector2 _scrollPosition;
        private string _searchFilter = "";
        private bool _showGroups = true;
        private Dictionary<string, bool> _groupFoldouts = new Dictionary<string, bool>();

        [MenuItem("Window/UnityHelperSDK/Addressable Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<AddressableManagerWindow>("Addressable Manager");
            window.Show();
        }

        private void OnGUI()
        {
            DrawToolbar();

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                DrawNoSettingsMessage();
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawAddressableGroups(settings);

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Search field
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField);

            GUILayout.FlexibleSpace();

            // Toggle groups view
            _showGroups = GUILayout.Toggle(_showGroups, "Show Groups", EditorStyles.toolbarButton);

            if (GUILayout.Button("Build", EditorStyles.toolbarButton))
            {
                BuildAddressables();
            }

            if (GUILayout.Button("Clean Build", EditorStyles.toolbarButton))
            {
                CleanBuildAddressables();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawNoSettingsMessage()
        {
            EditorGUILayout.HelpBox("Addressable Asset Settings not found. Click below to create them.", MessageType.Warning);
            if (GUILayout.Button("Create Addressable Asset Settings"))
            {
                AddressableAssetSettingsDefaultObject.GetSettings(true);
            }
        }

        private void DrawAddressableGroups(AddressableAssetSettings settings)
        {
            foreach (var group in settings.groups)
            {
                if (group == null) continue;

                // Skip if group doesn't match search filter
                if (!string.IsNullOrEmpty(_searchFilter) && 
                    !group.Name.ToLower().Contains(_searchFilter.ToLower()) &&
                    !group.entries.Any(e => e.address.ToLower().Contains(_searchFilter.ToLower())))
                {
                    continue;
                }

                if (!_groupFoldouts.ContainsKey(group.Name))
                    _groupFoldouts[group.Name] = true;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                _groupFoldouts[group.Name] = EditorGUILayout.Foldout(_groupFoldouts[group.Name], group.Name, true);

                if (_groupFoldouts[group.Name])
                {
                    EditorGUI.indentLevel++;
                    foreach (var entry in group.entries)
                    {
                        if (string.IsNullOrEmpty(_searchFilter) || 
                            entry.address.ToLower().Contains(_searchFilter.ToLower()))
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(entry.address);
                            if (GUILayout.Button("Select", GUILayout.Width(60)))
                            {
                                Selection.activeObject = entry.MainAsset;
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void BuildAddressables()
        {
            AddressableAssetSettings.BuildPlayerContent();
            Debug.Log("Addressables build completed");
        }

        private void CleanBuildAddressables()
        {
            AddressableAssetSettings.CleanPlayerContent();
            BuildAddressables();
            Debug.Log("Addressables clean build completed");
        }
    }
}
