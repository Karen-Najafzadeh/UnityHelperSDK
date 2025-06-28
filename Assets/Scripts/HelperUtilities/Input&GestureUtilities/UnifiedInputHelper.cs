using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// A comprehensive input helper that combines both high-level input management and complex gesture recognition.
/// 
/// Features:
/// - Cross-platform input handling (Touch, Mouse, Keyboard)
/// - Basic gestures (Tap, Double Tap, Simple Swipes)
/// - Complex gesture recognition:
///   - Custom tap/combo sequences
///   - Advanced swipe patterns
///   - Shape recognition using $1 Unistroke Recognizer
/// - Input System integration
/// - Custom key bindings
/// - Input action management
/// </summary>
public class UnifiedInputHelper : MonoBehaviour
{
    public static UnifiedInputHelper Instance { get; private set; }

    // Configuration
    private const float DefaultBufferTime = 1f;
    private const float SwipeThreshold = 50f;
    private const float TapThreshold = 0.2f;
    private const float DoubleTapThreshold = 0.3f;

    // Input state tracking
    private readonly Dictionary<string, InputAction> _actions = new Dictionary<string, InputAction>();
    private readonly Dictionary<string, KeyBinding> _keyBindings = new Dictionary<string, KeyBinding>();
    private readonly List<PointerEvent> _buffer = new List<PointerEvent>();
    private readonly List<GestureDefinition> _gestures = new List<GestureDefinition>();
    
    private Vector2 _touchStartPos;
    private float _touchStartTime;
    private float _lastTapTime;

    [Tooltip("How long (in seconds) to keep input history for gesture detection.")]
    public float bufferTime = DefaultBufferTime;

    #region Initialization

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        SamplePointer();
        PruneBuffer();
        DetectGestures();
    }

    #endregion

    #region Input System Integration

    /// <summary>Register a new input action with callback</summary>
    public void RegisterAction(string name, InputAction action, Action<InputAction.CallbackContext> callback)
    {
        if (_actions.ContainsKey(name))
        {
            _actions[name].Disable();
        }
        
        action.Enable();
        action.performed += callback;
        _actions[name] = action;
    }

    /// <summary>Unregister an input action</summary>
    public void UnregisterAction(string name)
    {
        if (_actions.TryGetValue(name, out var action))
        {
            action.Disable();
            _actions.Remove(name);
        }
    }

    #endregion

    #region Simple Gesture Detection

    /// <summary>Check for basic swipe gesture and return direction</summary>
    public SwipeDirection DetectBasicSwipe()
    {
        if (!Input.touchSupported) return SwipeDirection.None;
        
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            
            switch (touch.phase)
            {
                case UnityEngine.TouchPhase.Began:
                    _touchStartPos = touch.position;
                    _touchStartTime = Time.time;
                    break;
                    
                case UnityEngine.TouchPhase.Ended:
                    Vector2 delta = touch.position - _touchStartPos;
                    
                    if (delta.magnitude > SwipeThreshold)
                    {
                        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                        
                        if (angle > -45f && angle <= 45f) return SwipeDirection.Right;
                        if (angle > 45f && angle <= 135f) return SwipeDirection.Up;
                        if (angle > 135f || angle <= -135f) return SwipeDirection.Left;
                        if (angle > -135f && angle <= -45f) return SwipeDirection.Down;
                    }
                    break;
            }
        }
        
        return SwipeDirection.None;
    }

    /// <summary>Detect single tap gesture</summary>
    public bool DetectTap(out Vector2 position)
    {
        position = Vector2.zero;
        
        if (!Input.touchSupported) return false;
        
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            
            if (touch.phase == UnityEngine.TouchPhase.Ended)
            {
                float duration = Time.time - _touchStartTime;
                if (duration < TapThreshold)
                {
                    position = touch.position;
                    return true;
                }
            }
        }
        
        return false;
    }

    /// <summary>Detect double tap gesture</summary>
    public bool DetectDoubleTap(out Vector2 position)
    {
        position = Vector2.zero;
        
        if (DetectTap(out position))
        {
            float timeSinceLastTap = Time.time - _lastTapTime;
            if (timeSinceLastTap < DoubleTapThreshold)
            {
                _lastTapTime = 0f;
                return true;
            }
            _lastTapTime = Time.time;
        }
        
        return false;
    }

    #endregion

    #region Complex Gesture Recognition

    /// <summary>Register a new complex gesture definition.</summary>
    public void RegisterGesture(GestureDefinition gesture)
    {
        _gestures.Add(gesture);
    }

    /// <summary>Unregister an existing gesture definition.</summary>
    public void UnregisterGesture(string name)
    {
        _gestures.RemoveAll(g => g.Name == name);
    }

    private void SamplePointer()
    {
        Vector2 pos;
        bool isDown = false, isUp = false;
#if UNITY_EDITOR || UNITY_STANDALONE
        if (Input.GetMouseButtonDown(0)) isDown = true;
        if (Input.GetMouseButtonUp(0)) isUp = true;
        pos = Input.mousePosition;
#else
        if (Input.touchCount > 0)
        {
            var t = Input.GetTouch(0);
            pos = t.position;
            if (t.phase == TouchPhase.Began) isDown = true;
            if (t.phase == TouchPhase.Ended) isUp = true;
        }
        else return;
#endif
        _buffer.Add(new PointerEvent { Position = pos, Time = Time.time, IsDown = isDown, IsUp = isUp });
    }

    private void PruneBuffer()
    {
        float cutoff = Time.time - bufferTime;
        _buffer.RemoveAll(e => e.Time < cutoff);
    }

    private void DetectGestures()
    {
        foreach (var g in _gestures)
        {
            if (g.Type == GestureType.Combo && ComboMatch(g)) g.OnDetected?.Invoke();
            if (g.Type == GestureType.Swipe && SwipeMatch(g)) g.OnDetected?.Invoke();
            if (g.Type == GestureType.Shape && ShapeMatch(g)) g.OnDetected?.Invoke();
        }
    }

    private bool ComboMatch(GestureDefinition g)
    {
        var steps = g.ComboSteps;
        int idx = 0;
        foreach (var e in _buffer)
        {
            var step = steps[idx];
            if (((step.EventType == ButtonEvent.Down && e.IsDown) || (step.EventType == ButtonEvent.Up && e.IsUp))
                && Input.GetKey(step.Key))
            {
                idx++;
                if (idx >= steps.Count) return true;
            }
        }
        return false;
    }

    private bool SwipeMatch(GestureDefinition g)
    {
        var down = _buffer.Where(e => e.IsDown).FirstOrDefault();
        var up = _buffer.Where(e => e.IsUp).LastOrDefault();
        if (down == null || up == null) return false;
        float dt = up.Time - down.Time;
        if (dt > g.TimeWindow) return false;
        var delta = up.Position - down.Position;
        if (delta.magnitude < g.DeadZone) return false;
        float angle = Vector2.Angle(delta, g.SwipeDirection);
        return angle < g.AngleThreshold;
    }

    private bool ShapeMatch(GestureDefinition g)
    {
        var strokes = _buffer.Where(e => !float.IsNaN(e.Position.x)).ToList();
        if (!strokes.Any() || strokes.Last().IsDown || !_buffer.Any(e => e.IsUp)) return false;
        var startIdx = _buffer.FindLastIndex(e => e.IsDown);
        var endIdx = _buffer.FindLastIndex(e => e.IsUp);
        if (startIdx < 0 || endIdx <= startIdx) return false;
        var points = _buffer.GetRange(startIdx, endIdx - startIdx + 1).Select(e => e.Position).ToList();
        var score = DollarOneRecognizer.Recognize(points, g.TemplatePoints, out float dist);
        return score < g.MatchThreshold;
    }

    #endregion

    #region Key Bindings

    /// <summary>Register a new key binding</summary>
    public void RegisterKeyBinding(string actionName, KeyBinding binding)
    {
        _keyBindings[actionName] = binding;
    }

    /// <summary>Check if a key binding is active</summary>
    public bool IsBindingActive(string actionName)
    {
        if (_keyBindings.TryGetValue(actionName, out var binding))
        {
            return binding.IsActive();
        }
        return false;
    }

    #endregion

    #region Data Types

    private class PointerEvent
    {
        public Vector2 Position;
        public float Time;
        public bool IsDown;
        public bool IsUp;
    }

    public enum GestureType { Combo, Swipe, Shape }
    public enum ButtonEvent { Down, Up }
    public enum SwipeDirection { None, Up, Down, Left, Right }

    public class InputComboStep
    {
        public KeyCode Key;
        public ButtonEvent EventType;
    }

    public class KeyBinding
    {
        public KeyCode PrimaryKey { get; set; }
        public KeyCode[] ModifierKeys { get; set; }
        
        public bool IsActive()
        {
            if (!Input.GetKey(PrimaryKey)) return false;
            
            if (ModifierKeys != null)
            {
                foreach (var modifier in ModifierKeys)
                {
                    if (!Input.GetKey(modifier)) return false;
                }
            }
            
            return true;
        }
    }

    public class GestureDefinition
    {
        public string Name;
        public GestureType Type;
        public float TimeWindow;
        public float DeadZone;
        // For swipe
        public Vector2 SwipeDirection;
        public float AngleThreshold = 45f;
        // For combos
        public List<InputComboStep> ComboSteps;
        // For shapes
        public Vector2[] TemplatePoints;
        public float MatchThreshold = 0.3f;
        public Action OnDetected;
    }

    #endregion

    #region $1 Unistroke Recognizer

    private static class DollarOneRecognizer
    {
        private const int NumPoints = 64;
        private const float SquareSize = 250f;

        public static float Recognize(List<Vector2> points, Vector2[] template, out float distance)
        {
            var pts = Resample(points, NumPoints);
            var radians = IndicativeAngle(pts);
            pts = RotateBy(pts, -radians);
            pts = ScaleToSquare(pts, SquareSize);
            pts = TranslateToOrigin(pts);
            var tmpl = new List<Vector2>(template);
            tmpl = Resample(tmpl, NumPoints);
            tmpl = RotateBy(tmpl, -IndicativeAngle(tmpl));
            tmpl = ScaleToSquare(tmpl, SquareSize);
            tmpl = TranslateToOrigin(tmpl);
            distance = PathDistance(pts, tmpl);
            return distance / (float)Math.Sqrt(2 * SquareSize * SquareSize);
        }

        private static List<Vector2> Resample(List<Vector2> pts, int n)
        {
            float I = PathLength(pts) / (n - 1);
            float D = 0f;
            List<Vector2> newPts = new List<Vector2> { pts[0] };
            for (int i = 1; i < pts.Count; i++)
            {
                float d = Vector2.Distance(pts[i - 1], pts[i]);
                if ((D + d) >= I)
                {
                    var qx = pts[i - 1].x + ((I - D) / d) * (pts[i].x - pts[i - 1].x);
                    var qy = pts[i - 1].y + ((I - D) / d) * (pts[i].y - pts[i - 1].y);
                    Vector2 q = new Vector2(qx, qy);
                    newPts.Add(q);
                    pts.Insert(i, q);
                    D = 0f;
                }
                else D += d;
            }
            if (newPts.Count == n - 1) newPts.Add(pts[pts.Count - 1]);
            return newPts;
        }

        private static float IndicativeAngle(List<Vector2> pts)
        {
            var c = Centroid(pts);
            return Mathf.Atan2(c.y - pts[0].y, c.x - pts[0].x);
        }

        private static List<Vector2> RotateBy(List<Vector2> pts, float radians)
        {
            var c = Centroid(pts);
            List<Vector2> newPts = new List<Vector2>();
            for (int i = 0; i < pts.Count; i++)
            {
                float x = (pts[i].x - c.x) * Mathf.Cos(radians) - (pts[i].y - c.y) * Mathf.Sin(radians) + c.x;
                float y = (pts[i].x - c.x) * Mathf.Sin(radians) + (pts[i].y - c.y) * Mathf.Cos(radians) + c.y;
                newPts.Add(new Vector2(x, y));
            }
            return newPts;
        }

        private static List<Vector2> ScaleToSquare(List<Vector2> pts, float size)
        {
            var min = pts.Aggregate(pts[0], (acc, p) => new Vector2(Mathf.Min(acc.x, p.x), Mathf.Min(acc.y, p.y)));
            var max = pts.Aggregate(pts[0], (acc, p) => new Vector2(Mathf.Max(acc.x, p.x), Mathf.Max(acc.y, p.y)));
            var scale = new Vector2(max.x - min.x, max.y - min.y);
            List<Vector2> newPts = new List<Vector2>();
            foreach (var p in pts) newPts.Add(new Vector2((p.x - min.x) / scale.x * size, (p.y - min.y) / scale.y * size));
            return newPts;
        }

        private static List<Vector2> TranslateToOrigin(List<Vector2> pts)
        {
            var c = Centroid(pts);
            return pts.Select(p => p - c).ToList();
        }

        private static float PathDistance(List<Vector2> a, List<Vector2> b)
        {
            float d = 0f;
            for (int i = 0; i < a.Count; i++) d += Vector2.Distance(a[i], b[i]);
            return d / a.Count;
        }

        private static float PathLength(List<Vector2> pts)
        {
            float d = 0f;
            for (int i = 1; i < pts.Count; i++) d += Vector2.Distance(pts[i - 1], pts[i]);
            return d;
        }

        private static Vector2 Centroid(List<Vector2> pts)
        {
            float x = pts.Average(p => p.x);
            float y = pts.Average(p => p.y);
            return new Vector2(x, y);
        }
    }

    #endregion
}
