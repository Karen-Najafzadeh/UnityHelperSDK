using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.Events;

/// <summary>
/// A comprehensive dialogue system helper that handles conversations,
/// branching dialogue, and character interactions.
/// 
/// Features:
/// - Dialogue tree management
/// - Character-based conversations
/// - Text animation effects
/// - Branching dialogue support
/// - Event triggers
/// - Localization integration
/// - UI automation
/// - Response handling
/// </summary>
public static class DialogueHelper
{
    // Dialogue settings
    private static float _defaultCharacterDelay = 0.05f;
    private static float _defaultPunctuationDelay = 0.2f;
    private static bool _skipEnabled = true;
    
    // Active dialogue tracking
    private static DialogueNode _currentNode;
    private static bool _isDialogueActive;
    private static readonly Stack<DialogueNode> _dialogueHistory = new Stack<DialogueNode>();
    
    // Event handlers
    public static event Action<DialogueNode> OnDialogueNodeStart;
    public static event Action<DialogueNode> OnDialogueNodeComplete;
    public static event Action<string> OnCharacterSpeak;
    
    #region Dialogue Control
    
    /// <summary>
    /// Start a dialogue sequence from a root node
    /// </summary>
    public static async Task StartDialogue(DialogueNode rootNode, DialogueUIConfig uiConfig = null)
    {
        if (_isDialogueActive) return;
        
        _isDialogueActive = true;
        _currentNode = rootNode;
        _dialogueHistory.Clear();
        
        // Use UI Helper to show dialogue UI if needed
        if (uiConfig != null)
        {
            await UIHelper.ShowDialogueUI(uiConfig);
        }
        
        await ProcessNode(rootNode);
    }
    
    /// <summary>
    /// Process a single dialogue node
    /// </summary>
    private static async Task ProcessNode(DialogueNode node)
    {
        OnDialogueNodeStart?.Invoke(node);
        
        // Handle localization if available
        string text = LocalizationManager.GetLocalizedText(node.DialogueKey) ?? node.Text;
        
        // Display character name if present
        if (!string.IsNullOrEmpty(node.CharacterName))
        {
            await UIHelper.SetDialogueCharacterName(node.CharacterName);
            OnCharacterSpeak?.Invoke(node.CharacterName);
        }
        
        // Animate text display
        await AnimateText(text, node.TextComponent, node.CharacterDelay ?? _defaultCharacterDelay);
        
        // Show choices if any
        if (node.Choices != null && node.Choices.Count > 0)
        {
            var choice = await UIHelper.ShowDialogueChoices(node.Choices);
            _dialogueHistory.Push(node);
            
            if (node.Choices.TryGetValue(choice, out var nextNode))
            {
                await ProcessNode(nextNode);
            }
        }
        
        OnDialogueNodeComplete?.Invoke(node);
    }
    
    /// <summary>
    /// Animate text display character by character
    /// </summary>
    private static async Task AnimateText(string text, TMP_Text textComponent, float delay)
    {
        textComponent.text = "";
        
        for (int i = 0; i < text.Length; i++)
        {
            if (_skipEnabled && Input.GetKeyDown(KeyCode.Space))
            {
                textComponent.text = text;
                break;
            }
            
            textComponent.text += text[i];
            
            // Add extra delay for punctuation
            if (IsPunctuation(text[i]))
            {
                await Task.Delay(Mathf.RoundToInt(_defaultPunctuationDelay * 1000));
            }
            else
            {
                await Task.Delay(Mathf.RoundToInt(delay * 1000));
            }
        }
    }
    
    #endregion
    
    #region State Management
    
    /// <summary>
    /// Save current dialogue state
    /// </summary>
    public static DialogueState SaveState()
    {
        return new DialogueState
        {
            CurrentNodeId = _currentNode?.Id,
            History = new Stack<string>(_dialogueHistory.Select(n => n.Id))
        };
    }
    
    /// <summary>
    /// Restore a previously saved dialogue state
    /// </summary>
    public static void RestoreState(DialogueState state, Dictionary<string, DialogueNode> nodeMap)
    {
        if (state == null || nodeMap == null) return;
        
        _dialogueHistory.Clear();
        foreach (var nodeId in state.History)
        {
            if (nodeMap.TryGetValue(nodeId, out var node))
            {
                _dialogueHistory.Push(node);
            }
        }
        
        if (nodeMap.TryGetValue(state.CurrentNodeId, out var currentNode))
        {
            _currentNode = currentNode;
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    private static bool IsPunctuation(char c)
    {
        return c == '.' || c == ',' || c == '!' || c == '?' || c == ';';
    }
    
    #endregion
    
    #region Helper Classes
    
    /// <summary>
    /// Represents a node in the dialogue tree
    /// </summary>
    public class DialogueNode
    {
        public string Id { get; set; }
        public string CharacterName { get; set; }
        public string Text { get; set; }
        public string DialogueKey { get; set; }
        public float? CharacterDelay { get; set; }
        public Dictionary<string, DialogueNode> Choices { get; set; }
        public TMP_Text TextComponent { get; set; }
        public UnityEvent OnNodeEnter { get; set; }
        public UnityEvent OnNodeExit { get; set; }
    }
    
    /// <summary>
    /// Configuration for dialogue UI
    /// </summary>
    public class DialogueUIConfig
    {
        public TMP_Text NameText { get; set; }
        public TMP_Text DialogueText { get; set; }
        public RectTransform ChoicesContainer { get; set; }
        public GameObject ChoiceButtonPrefab { get; set; }
    }
    
    /// <summary>
    /// Stored dialogue state for save/load
    /// </summary>
    public class DialogueState
    {
        public string CurrentNodeId { get; set; }
        public Stack<string> History { get; set; }
    }
    
    #endregion
}
