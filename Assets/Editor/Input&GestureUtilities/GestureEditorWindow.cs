using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityHelperSDK.Input;
using UnityHelperSDK.HelperUtilities;

public class GestureEditorWindow : EditorWindow
{
    private const string GESTURES_PATH = "Assets/Resources/Gestures";
    private Dictionary<string, GestureDefinitionAsset> _gestures = new Dictionary<string, GestureDefinitionAsset>();
    private Vector2 _scrollPosition;
    private bool _isDirty;
    private string _selectedGesture;
    private bool _isRecordingShape;
    private List<Vector2> _recordedPoints = new List<Vector2>();
    private GesturePreviewPanel _previewPanel;
    private Vector2 _lastMousePos;
    private string _searchQuery = "";
    private GestureType _typeFilter = GestureType.All;

    [MenuItem("Unity Helper SDK/Gesture Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<GestureEditorWindow>();
        window.titleContent = new GUIContent("Gesture Editor");
        window.minSize = new Vector2(800, 600);
    }

    private void OnEnable()
    {
        LoadGestureData();
    }

    private void OnGUI()
    {
        EditorGUIHelper.InitializeStyles();

        DrawToolbar();

        using (new EditorGUILayout.HorizontalScope())
        {
            // Left panel - Gesture list
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(250)))
            {
                DrawGestureList();
            }

            // Right panel - Gesture inspector
            using (new EditorGUILayout.VerticalScope())
            {
                DrawGestureInspector();
            }
        }

        // Auto-save on changes
        if (_isDirty)
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUILayout.LabelField("Unsaved changes", EditorStyles.boldLabel);
                if (GUILayout.Button("Save", EditorStyles.toolbarButton))
                {
                    SaveGestureData();
                }
            }
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("New Gesture", EditorStyles.toolbarButton))
            {
                CreateNewGesture();
            }

            if (GUILayout.Button("Save All", EditorStyles.toolbarButton))
            {
                SaveGestureData();
            }

            GUILayout.FlexibleSpace();

            if (_isRecordingShape)
            {
                if (GUILayout.Button("Stop Recording", EditorStyles.toolbarButton))
                {
                    StopRecordingShape();
                }
            }
            else if (_selectedGesture != null && _gestures.TryGetValue(_selectedGesture, out var gesture) && gesture.Type == GestureType.Shape)
            {
                if (GUILayout.Button("Record Shape", EditorStyles.toolbarButton))
                {
                    StartRecordingShape();
                }
            }
        }
    }

    private void DrawGestureList()
    {
        EditorGUIHelper.BeginSection("Gestures");

        // Search and filter
        using (new EditorGUILayout.HorizontalScope())
        {
            _searchQuery = EditorGUILayout.TextField(_searchQuery, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("×", EditorStyles.toolbarButton, GUILayout.Width(24)) && !string.IsNullOrEmpty(_searchQuery))
            {
                _searchQuery = "";
                GUI.FocusControl(null);
            }
        }

        // Type filter
        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (GestureType type in System.Enum.GetValues(typeof(GestureType)))
            {
                bool isSelected = (_typeFilter & type) != 0;
                bool newSelected = GUILayout.Toggle(isSelected, type.ToString(), EditorStyles.toolbarButton);
                
                if (newSelected != isSelected)
                {
                    if (newSelected)
                        _typeFilter |= type;
                    else
                        _typeFilter &= ~type;
                }
            }
        }

        EditorGUILayout.Space();

        // Gesture list
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        var filteredGestures = _gestures.Values
            .Where(g => string.IsNullOrEmpty(_searchQuery) || 
                       g.DisplayName.IndexOf(_searchQuery, System.StringComparison.OrdinalIgnoreCase) >= 0)
            .Where(g => (_typeFilter & g.Type) != 0)
            .OrderBy(g => g.DisplayName);

        foreach (var gesture in filteredGestures)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool isSelected = _selectedGesture == gesture.Id;
                bool newSelected = EditorGUILayout.ToggleLeft(gesture.DisplayName, isSelected);
                
                GUILayout.Label(gesture.Type.ToString(), EditorStyles.miniLabel);
                
                if (newSelected != isSelected)
                {
                    _selectedGesture = newSelected ? gesture.Id : null;
                    GUI.FocusControl(null);
                }
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUIHelper.EndSection();
    }

    private void DrawGestureInspector()
    {
        if (string.IsNullOrEmpty(_selectedGesture) || !_gestures.TryGetValue(_selectedGesture, out var gesture))
        {
            EditorGUILayout.HelpBox("Select a gesture to edit", MessageType.Info);
            return;
        }

        var serializedObject = new SerializedObject(gesture);
        serializedObject.Update();

        EditorGUIHelper.BeginSection($"Edit Gesture: {gesture.DisplayName}");

        EditorGUI.BeginChangeCheck();

        // Basic properties
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("Display Name"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("type"), new GUIContent("Gesture Type"));

        EditorGUILayout.Space();

        // Type-specific properties
        switch (gesture.Type)
        {
            case GestureType.Swipe:
                DrawSwipeProperties(serializedObject);
                break;

            case GestureType.Combo:
                DrawComboProperties(serializedObject);
                break;

            case GestureType.Shape:
                DrawShapeProperties(serializedObject);
                break;
        }

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            _isDirty = true;
        }

        EditorGUIHelper.EndSection();

        // Preview area for shape gestures
        if (gesture.Type == GestureType.Shape)
        {
            EditorGUIHelper.BeginSection("Shape Preview");
            
            var rect = GUILayoutUtility.GetRect(200, 200);
            if (Event.current.type == EventType.Repaint)
            {
                GUI.Box(rect, "");
                DrawShapePreview(rect, gesture.TemplatePoints);
            }

            if (_isRecordingShape)
            {
                HandleShapeRecording(rect);
            }

            EditorGUIHelper.EndSection();
        }

        // Delete button
        EditorGUILayout.Space();
        if (GUILayout.Button("Delete Gesture"))
        {
            if (EditorUtility.DisplayDialog("Delete Gesture", 
                "Are you sure you want to delete this gesture?", 
                "Delete", "Cancel"))
            {
                DeleteGesture(gesture);
            }
        }
    }

    private void DrawSwipeProperties(SerializedObject serializedObject)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty("timeWindow"), new GUIContent("Time Window (seconds)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("deadZone"), new GUIContent("Dead Zone"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("swipeDirection"), new GUIContent("Direction"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("angleThreshold"), new GUIContent("Angle Threshold"));
    }

    private void DrawComboProperties(SerializedObject serializedObject)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty("timeWindow"), new GUIContent("Time Window (seconds)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("comboSteps"), new GUIContent("Combo Steps"), true);
    }

    private void DrawShapeProperties(SerializedObject serializedObject)
    {
        EditorGUILayout.PropertyField(serializedObject.FindProperty("timeWindow"), new GUIContent("Time Window (seconds)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("matchThreshold"), new GUIContent("Match Threshold"));
        
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Click 'Record Shape' to draw a new shape template", MessageType.Info);
    }

    private void DrawShapePreview(Rect rect, IEnumerable<Vector2> points)
    {
        if (points == null || !points.Any()) return;

        var normalizedPoints = NormalizePoints(points.ToList(), rect);
        
        Handles.BeginGUI();
        Handles.color = Color.blue;
        
        for (int i = 1; i < normalizedPoints.Count; i++)
        {
            Handles.DrawLine(normalizedPoints[i - 1], normalizedPoints[i]);
        }
        
        Handles.EndGUI();
    }

    private List<Vector2> NormalizePoints(List<Vector2> points, Rect bounds)
    {
        if (points == null || points.Count == 0) return new List<Vector2>();

        var minX = points.Min(p => p.x);
        var maxX = points.Max(p => p.x);
        var minY = points.Min(p => p.y);
        var maxY = points.Max(p => p.y);

        var width = maxX - minX;
        var height = maxY - minY;
        var scale = Mathf.Min(bounds.width / width, bounds.height / height) * 0.8f;

        return points.Select(p => new Vector2(
            bounds.x + bounds.width * 0.1f + (p.x - minX) * scale,
            bounds.y + bounds.height * 0.1f + (p.y - minY) * scale
        )).ToList();
    }

    private void HandleShapeRecording(Rect rect)
    {
        var e = Event.current;
        if (!rect.Contains(e.mousePosition)) return;

        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0)
                {
                    _recordedPoints.Clear();
                    _recordedPoints.Add(e.mousePosition);
                    _lastMousePos = e.mousePosition;
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (e.button == 0 && Vector2.Distance(_lastMousePos, e.mousePosition) > 5f)
                {
                    _recordedPoints.Add(e.mousePosition);
                    _lastMousePos = e.mousePosition;
                    Repaint();
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (e.button == 0)
                {
                    StopRecordingShape();
                    e.Use();
                }
                break;

            case EventType.Repaint:
                if (_recordedPoints.Count > 0)
                {
                    DrawShapePreview(rect, _recordedPoints);
                }
                break;
        }
    }

    private void CreateNewGesture()
    {
        string id = System.Guid.NewGuid().ToString("N");
        string name = "New Gesture";
        int counter = 1;
        
        while (_gestures.Values.Any(g => g.DisplayName == name))
        {
            name = $"New Gesture {counter++}";
        }

        var gesture = CreateInstance<GestureDefinitionAsset>();
        gesture.Initialize(id, name);

        if (!Directory.Exists(GESTURES_PATH))
        {
            Directory.CreateDirectory(GESTURES_PATH);
        }

        string assetPath = $"{GESTURES_PATH}/{id}.asset";
        AssetDatabase.CreateAsset(gesture, assetPath);
        AssetDatabase.SaveAssets();

        _gestures[id] = gesture;
        _selectedGesture = id;
        _isDirty = true;
    }

    private void DeleteGesture(GestureDefinitionAsset gesture)
    {
        string assetPath = AssetDatabase.GetAssetPath(gesture);
        AssetDatabase.DeleteAsset(assetPath);
        _gestures.Remove(gesture.Id);
        
        if (_selectedGesture == gesture.Id)
        {
            _selectedGesture = null;
        }
        
        _isDirty = true;
    }

    private void LoadGestureData()
    {
        _gestures.Clear();

        if (!Directory.Exists(GESTURES_PATH))
        {
            Directory.CreateDirectory(GESTURES_PATH);
        }

        var guids = AssetDatabase.FindAssets("t:GestureDefinitionAsset", new[] { GESTURES_PATH });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var gesture = AssetDatabase.LoadAssetAtPath<GestureDefinitionAsset>(path);
            if (gesture != null)
            {
                _gestures[gesture.Id] = gesture;
            }
        }
    }

    private void SaveGestureData()
    {
        AssetDatabase.SaveAssets();
        _isDirty = false;
    }

    private void StartRecordingShape()
    {
        _isRecordingShape = true;
        _recordedPoints.Clear();
    }

    private void StopRecordingShape()
    {
        _isRecordingShape = false;
        if (_recordedPoints.Count > 0 && _gestures.TryGetValue(_selectedGesture, out var gesture))
        {
            var serializedObject = new SerializedObject(gesture);
            var templatePointsProp = serializedObject.FindProperty("templatePoints");
            templatePointsProp.ClearArray();
            
            foreach (var point in _recordedPoints)
            {
                var index = templatePointsProp.arraySize++;
                var elementProp = templatePointsProp.GetArrayElementAtIndex(index);
                elementProp.vector2Value = point;
            }
            
            serializedObject.ApplyModifiedProperties();
            _isDirty = true;
        }
        _recordedPoints.Clear();
    }

    private void OnDestroy()
    {
        if (_isDirty)
        {
            if (EditorUtility.DisplayDialog("Save Changes", 
                "Do you want to save your changes?", 
                "Save", "Don't Save"))
            {
                SaveGestureData();
            }
        }
    }
}

public class GesturePreviewPanel : IMGUIContainer
{
    private List<Vector2> _points = new List<Vector2>();
    private bool _isRecording;
    private Vector2 _lastMousePos;

    public GesturePreviewPanel()
    {
        style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
        style.minHeight = 200;
        onGUIHandler = OnGUIHandler;
    }

    private void OnGUIHandler()
    {
        var rect = contentRect;
        GUI.Box(rect, "");

        if (_points.Count > 0)
        {
            Handles.BeginGUI();
            Handles.color = Color.blue;
            
            for (int i = 1; i < _points.Count; i++)
            {
                Handles.DrawLine(_points[i - 1], _points[i]);
            }
            
            Handles.EndGUI();
        }

        var e = Event.current;
        if (!rect.Contains(e.mousePosition)) return;

        switch (e.type)
        {
            case EventType.MouseDown:
                if (e.button == 0)
                {
                    _points.Clear();
                    _points.Add(e.mousePosition);
                    _isRecording = true;
                    _lastMousePos = e.mousePosition;
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (_isRecording && e.button == 0 && Vector2.Distance(_lastMousePos, e.mousePosition) > 5f)
                {
                    _points.Add(e.mousePosition);
                    _lastMousePos = e.mousePosition;
                    MarkDirtyRepaint();
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (e.button == 0)
                {
                    _isRecording = false;
                    e.Use();
                }
                break;
        }
    }

    public List<Vector2> GetPoints() => new List<Vector2>(_points);
    public void Clear() => _points.Clear();
}
