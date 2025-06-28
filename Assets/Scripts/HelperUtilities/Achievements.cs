// Refactored Quest System using SimpleStateMachine<TContext, TStateEnum>
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityHelperSDK.Data;
using UnityHelperSDK.DesignPatterns;
using UnityHelperSDK.Events;

// Centralized enum for quest states
public enum QuestStateEnum { NotStarted, InProgress, Completed }

/// <summary>
/// ScriptableObject definition for a Quest and its Objectives.
/// </summary>
[CreateAssetMenu(menuName = "Quests/QuestDefinition", fileName = "New QuestDefinition")]
public class QuestDefinitionSO : ScriptableObject
{
    public string id;
    public string title;
    [TextArea] public string description;
    public bool autoStart = true;
    public List<ObjectiveDefinitionSO> objectives;
}

/// <summary>
/// ScriptableObject for a single objective within a quest.
/// </summary>
[Serializable]
public class ObjectiveDefinitionSO
{
    public string id;
    [TextArea] public string description;
    public string triggerEvent;
    public int targetCount;
}

/// <summary>
/// Manager that loads QuestDefinitionSOs and tracks quest progression.
/// </summary>
public class QuestManager : MonoBehaviour
{
    [Header("Quest Definitions (ScriptableObjects)")]
    public List<QuestDefinitionSO> questDefinitions;

    private Dictionary<string, Quest> _activeQuests = new Dictionary<string, Quest>();

    private void Awake()
    {
        InitializeQuests();
        foreach (var quest in _activeQuests.Values)
        {
            quest.LoadProgress();
            if (quest.Definition.autoStart && quest.CurrentState == QuestStateEnum.NotStarted)
                quest.Start();
        }
    }

    private void OnEnable()
    {
        EventHelper.Subscribe<OnObjectiveProgress>(HandleProgress);
    }

    private void OnDisable()
    {
        EventHelper.Unsubscribe<OnObjectiveProgress>(HandleProgress);
    }

    private void InitializeQuests()
    {
        _activeQuests.Clear();
        foreach (var def in questDefinitions)
        {
            var quest = new Quest(def);
            _activeQuests[def.id] = quest;
        }
    }

    private void HandleProgress(OnObjectiveProgress evt)
    {
        if (_activeQuests.TryGetValue(evt.questId, out var quest))
        {
            quest.UpdateObjectiveProgress(evt.objectiveId, evt.progress);
        }
    }

    public Quest GetQuest(string questId) => _activeQuests.TryGetValue(questId, out var q) ? q : null;
}

/// <summary>
/// Runtime quest instance with state machine and objectives.
/// </summary>
public class Quest
{
    public QuestDefinitionSO Definition { get; }
    public QuestStateEnum CurrentState => _stateMachine.CurrentState;
    public bool IsCompleted => CurrentState == QuestStateEnum.Completed;

    private readonly StateMachine<Quest, QuestStateEnum> _stateMachine;
    private readonly Dictionary<string, Objective> _objectives = new Dictionary<string, Objective>();
    private const GamePrefs ProgressKey = GamePrefs.PurchaseHistoryComplexData;

    public Quest(QuestDefinitionSO def)
    {
        Definition = def;

        // Setup the state machine using fluent API
        _stateMachine = new StateMachine<Quest, QuestStateEnum>(this)
            .DefineState(QuestStateEnum.NotStarted)
                .OnEnter(ctx => Debug.Log($"Quest '{ctx.Definition.id}' initialized."))
                .EndState()
            .DefineState(QuestStateEnum.InProgress)
                .OnEnter(ctx => EventHelper.Trigger(new OnQuestStarted { questId = ctx.Definition.id, timestamp = DateTime.Now }))
                .OnExit(ctx => ctx.SaveProgress())
                .EndState()
            .DefineState(QuestStateEnum.Completed)
                .OnEnter(ctx =>
                {
                    ctx.SaveProgress();
                    EventHelper.Trigger(new OnQuestCompleted { questId = ctx.Definition.id, timestamp = DateTime.Now });
                })
                .EndState();

        // Start at NotStarted
        _stateMachine.SetInitialState(QuestStateEnum.NotStarted);

        // Initialize objectives
        foreach (var objDef in def.objectives)
        {
            var obj = new Objective(this, objDef);
            _objectives[objDef.id] = obj;
        }
    }

    public void Start()
    {
        if (_stateMachine.CurrentState == QuestStateEnum.NotStarted)
            _stateMachine.TransitionTo(QuestStateEnum.InProgress);
    }

    public void UpdateObjectiveProgress(string objectiveId, int amount)
    {
        if (_stateMachine.CurrentState != QuestStateEnum.InProgress)
            return;

        if (_objectives.TryGetValue(objectiveId, out var obj))
        {
            obj.AddProgress(amount);
            if (obj.IsCompleted && AllObjectivesCompleted())
                _stateMachine.TransitionTo(QuestStateEnum.Completed);
        }
    }

    public void SaveProgress()
    {
        var data = new QuestSaveData(Definition.id, CurrentState.ToString(), _objectives);
        var json = JsonHelper.Serialize(data);
        PrefsHelper.Set<GamePrefs>(ProgressKey, json);
    }

    public void LoadProgress()
    {
        var json = PrefsHelper.Get<string, GamePrefs>(ProgressKey, null);
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var data = JsonHelper.Deserialize<QuestSaveData>(json);
            if (data.questId != Definition.id) return;

            if (Enum.TryParse<QuestStateEnum>(data.stateName, out var state))
                _stateMachine.SetInitialState(state);

            foreach (var od in data.objectives)
                if (_objectives.TryGetValue(od.id, out var obj))
                    obj.Progress = od.progress;
        }
        catch
        {
            Debug.LogWarning($"Failed to load progress for quest '{Definition.id}'");
        }
    }

    private bool AllObjectivesCompleted()
    {
        foreach (var o in _objectives.Values)
            if (!o.IsCompleted) return false;
        return true;
    }
}

/// <summary>
/// Represents a quest objective at runtime.
/// </summary>
public class Objective
{
    public string Id => Definition.id;
    public int Progress { get; internal set; }
    public bool IsCompleted => Progress >= Definition.targetCount;
    public ObjectiveDefinitionSO Definition { get; }
    private readonly Quest _quest;

    public Objective(Quest quest, ObjectiveDefinitionSO def)
    {
        _quest = quest;
        Definition = def;
        Progress = 0;
        EventHelper.Subscribe<OnObjectiveProgress>(OnProgressEvent);
    }

    private void OnProgressEvent(OnObjectiveProgress evt)
    {
        if (evt.questId == _quest.Definition.id && evt.objectiveId == Definition.id)
        {
            AddProgress(evt.progress);
        }
    }

    public void AddProgress(int amount)
    {
        Progress = Mathf.Min(Progress + amount, Definition.targetCount);
        EventHelper.Trigger(new OnObjectiveCompleted { questId = _quest.Definition.id, objectiveId = Definition.id, timestamp = DateTime.Now });
    }
}

/// <summary>
/// Data classes for saving/loading quest state.
/// </summary>
[Serializable]
public class QuestSaveData
{
    public string questId;
    public string stateName;
    public List<ObjectiveSaveData> objectives;

    public QuestSaveData(string questId, string stateName, Dictionary<string, Objective> objs)
    {
        this.questId = questId;
        this.stateName = stateName;
        objectives = new List<ObjectiveSaveData>();
        foreach (var o in objs.Values)
            objectives.Add(new ObjectiveSaveData(o.Id, o.Progress));
    }
}

[Serializable]
public struct ObjectiveSaveData
{
    public string id;
    public int progress;

    public ObjectiveSaveData(string id, int progress)
    {
        this.id = id;
        this.progress = progress;
    }
}

/// <summary>
/// Events for quest system.
/// </summary>
public struct OnObjectiveProgress { public string questId; public string objectiveId; public int progress; public DateTime timestamp; }
public struct OnObjectiveCompleted { public string questId; public string objectiveId; public DateTime timestamp; }
public struct OnQuestStarted { public string questId; public DateTime timestamp; }
public struct OnQuestCompleted { public string questId; public DateTime timestamp; }
