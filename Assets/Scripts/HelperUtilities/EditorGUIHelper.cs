using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;


namespace UnityHelperSDK.HelperUtilities{


#if UNITY_EDITOR

/// <summary>
/// A comprehensive helper for Unity Editor GUI operations, providing easy-to-use methods
/// for creating consistent and professional-looking custom inspectors and editor windows.
/// 
/// Features:
/// - Styled section headers and foldouts
/// - Common property layouts
/// - Drag and drop areas
/// - Search fields
/// - Custom field styles
/// - Undo/Redo support
/// - Responsive layouts
/// - Property validation
/// </summary>
public static class EditorGUIHelper
{
    // Style cache
    private static Dictionary<string, GUIStyle> _styles;
    private static bool _stylesInitialized;
    
    // Layout settings
    private static readonly float DefaultSpacing = 2f;
    private static readonly float SectionSpacing = 10f;
    private static readonly float IndentWidth = 15f;
    
    // Colors
    private static readonly Color HeaderColor = new Color(0.6f, 0.6f, 0.6f);
    private static readonly Color WarningColor = new Color(1f, 0.7f, 0.3f);
    private static readonly Color ErrorColor = new Color(1f, 0.3f, 0.3f);
    
    #region Initialization
    
    /// <summary>
    /// Initialize styles. Called automatically when needed.
    /// </summary>
    public static void InitializeStyles()
    {
        if (_stylesInitialized) return;
        
        _styles = new Dictionary<string, GUIStyle>();
        
        // Header style
        var headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            margin = new RectOffset(0, 0, 10, 5)
        };
        _styles["Header"] = headerStyle;
        
        // Section style
        var sectionStyle = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(10, 10, 10, 10),
            margin = new RectOffset(0, 0, 5, 5)
        };
        _styles["Section"] = sectionStyle;
        
        // Custom foldout style
        var foldoutStyle = new GUIStyle(EditorStyles.foldout)
        {
            fontStyle = FontStyle.Bold
        };
        _styles["Foldout"] = foldoutStyle;
        
        _stylesInitialized = true;
    }
    
    #endregion
    
    #region Section Controls
    
    /// <summary>
    /// Begin a new section with a header
    /// </summary>
    public static void BeginSection(string title)
    {
        InitializeStyles();
        
        EditorGUILayout.Space(SectionSpacing);
        EditorGUILayout.BeginVertical(_styles["Section"]);
        
        var headerRect = EditorGUILayout.GetControlRect(false, 20f);
        EditorGUI.LabelField(headerRect, title, _styles["Header"]);
        
        EditorGUILayout.Space(DefaultSpacing);
    }
    
    /// <summary>
    /// End the current section
    /// </summary>
    public static void EndSection()
    {
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(DefaultSpacing);
    }
    
    /// <summary>
    /// Create a foldout section that can be expanded/collapsed
    /// </summary>
    public static bool FoldoutSection(string title, bool expanded)
    {
        InitializeStyles();
        
        EditorGUILayout.BeginVertical(_styles["Section"]);
        expanded = EditorGUILayout.Foldout(expanded, title, true, _styles["Foldout"]);
        
        if (expanded)
        {
            EditorGUILayout.Space(DefaultSpacing);
        }
        
        return expanded;
    }
    
    #endregion
    
    #region Property Fields
    
    /// <summary>
    /// Draw a property field with validation and undo support
    /// </summary>
    public static void PropertyField(SerializedProperty property, string label = null, string tooltip = null)
    {
        EditorGUI.BeginChangeCheck();
        
        if (string.IsNullOrEmpty(label))
        {
            EditorGUILayout.PropertyField(property, new GUIContent(property.displayName, tooltip));
        }
        else
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label, tooltip));
        }
        
        if (EditorGUI.EndChangeCheck())
        {
            property.serializedObject.ApplyModifiedProperties();
        }
    }
    
    /// <summary>
    /// Create an array property field with add/remove buttons
    /// </summary>
    public static void ArrayPropertyField(SerializedProperty arrayProperty, string label = null)
    {
        if (!arrayProperty.isArray) return;
        
        EditorGUILayout.BeginVertical();
        
        // Header with size field
        EditorGUILayout.BeginHorizontal();
        if (string.IsNullOrEmpty(label))
        {
            EditorGUILayout.PropertyField(arrayProperty);
        }
        else
        {
            EditorGUILayout.PropertyField(arrayProperty, new GUIContent(label));
        }
        EditorGUILayout.EndHorizontal();
        
        // Array elements
        if (arrayProperty.isExpanded)
        {
            EditorGUI.indentLevel++;
            
            for (int i = 0; i < arrayProperty.arraySize; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(arrayProperty.GetArrayElementAtIndex(i));
                
                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    arrayProperty.DeleteArrayElementAtIndex(i);
                    break;
                }
                EditorGUILayout.EndHorizontal();
            }
            
            // Add button
            if (GUILayout.Button("Add Element"))
            {
                arrayProperty.arraySize++;
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
    }
    
    #endregion
    
    #region Drag and Drop
    
    /// <summary>
    /// Create a drag and drop area for objects of type T
    /// </summary>
    public static T DragDropArea<T>(string label, float height = 50) where T : UnityEngine.Object
    {
        Event evt = Event.current;
        var dropArea = GUILayoutUtility.GetRect(0.0f, height, GUILayout.ExpandWidth(true));
        var style = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter
        };
        
        GUI.Box(dropArea, label, style);
        
        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(evt.mousePosition))
                    return null;
                
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    
                    foreach (var draggedObject in DragAndDrop.objectReferences)
                    {
                        if (draggedObject is T matchingObject)
                            return matchingObject;
                    }
                }
                break;
        }
        
        return null;
    }
    
    #endregion
    
    #region Validation and Messages
    
    /// <summary>
    /// Display a help box message
    /// </summary>
    public static void HelpBox(string message, MessageType type = MessageType.Info)
    {
        EditorGUILayout.HelpBox(message, type);
    }
    
    /// <summary>
    /// Display a validation error message
    /// </summary>
    public static void ErrorMessage(string message)
    {
        var oldColor = GUI.color;
        GUI.color = ErrorColor;
        EditorGUILayout.HelpBox(message, MessageType.Error);
        GUI.color = oldColor;
    }
    
    /// <summary>
    /// Display a warning message
    /// </summary>
    public static void WarningMessage(string message)
    {
        var oldColor = GUI.color;
        GUI.color = WarningColor;
        EditorGUILayout.HelpBox(message, MessageType.Warning);
        GUI.color = oldColor;
    }
    
    #endregion
    
    #region Search and Filtering
    
    /// <summary>
    /// Create a search field with filtering callback
    /// </summary>
    public static string SearchField(string searchText, Action<string> onSearch = null)
    {
        EditorGUILayout.BeginHorizontal();
        
        var newSearchText = EditorGUILayout.TextField("Search", searchText, EditorStyles.toolbarSearchField);
        
        if (newSearchText != searchText)
        {
            onSearch?.Invoke(newSearchText);
        }
        
        if (GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(20)) && !string.IsNullOrEmpty(searchText))
        {
            newSearchText = "";
            onSearch?.Invoke("");
            GUI.FocusControl(null);
        }
        
        EditorGUILayout.EndHorizontal();
        
        return newSearchText;
    }
    
    #endregion
    
    #region Layout Helpers
    
    /// <summary>
    /// Begin a horizontal group with specified spacing
    /// </summary>
    public static void BeginHorizontalGroup(float spacing)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(spacing);
    }

    /// <summary>
    /// Begin a horizontal group with default spacing
    /// </summary>
    public static void BeginHorizontalGroup()
    {
        BeginHorizontalGroup(DefaultSpacing);
    }
    
    /// <summary>
    /// End a horizontal group with specified spacing
    /// </summary>
    public static void EndHorizontalGroup(float spacing)
    {
        GUILayout.Space(spacing);
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// End a horizontal group with default spacing
    /// </summary>
    public static void EndHorizontalGroup()
    {
        EndHorizontalGroup(DefaultSpacing);
    }
    
    /// <summary>
    /// Create an indented section
    /// </summary>
    public static void Indent(Action drawContent)
    {
        EditorGUI.indentLevel++;
        drawContent?.Invoke();
        EditorGUI.indentLevel--;
    }
    
    #endregion
}

#endif
}