# UnityHelperSDK Tutorial System Guide

## Overview

The UnityHelperSDK Tutorial System is a flexible, extensible, and ScriptableObject-driven framework for building in-game tutorials. It supports modular triggers, conditions, and step-based tutorials, all configurable via the Unity Editor. The system is designed for scalability and easy authoring, with robust editor tooling and runtime management.

---

## Key Concepts

### 1. ScriptableObject-Driven Architecture
- **TutorialDefinitionSO**: Represents a full tutorial, including its steps, triggers, and conditions.
- **StepDefinitionSO**: Represents a single step in a tutorial, with its own content, triggers, and conditions.
- **TriggerDefinitionSO**: Defines when a step or tutorial should advance, using parameters and types.
- **CompositeTriggerDefinitionSO**: Allows combining multiple triggers with AND/OR/NOT logic.
- **ConditionSO**: Base class for conditions that must be met to start/complete tutorials or steps. Extend for custom logic.

### 2. Triggers
- **ITrigger**: Interface for all triggers. Implementations include event-based triggers and composite triggers.
- **TriggerFactory**: Creates triggers from ScriptableObject definitions.
- **CompositeTrigger**: Combines multiple triggers using AND/OR/NOT logic.

### 3. Conditions
- **ICondition**: Interface for all conditions. Implementations check game state or other requirements.
- **PlayerScoreConditionSO**: Example condition that checks if the player has reached a required score.

### 4. Editor Tooling
- **TutorialEditorWindow**: Custom Unity Editor window for creating and editing tutorials, steps, triggers, and conditions.
- **TutorialTreeView**: UI component for browsing and selecting tutorials and steps.

---

## Getting Started

### 1. Creating a Tutorial
1. **Create a TutorialDefinitionSO**
   - Right-click in the Project window → `Create > Unity Helper SDK > Tutorial System > Tutorial Definition`.
   - Set the `TutorialID` and assign an `InitialTrigger` (optional).
   - Add `StepDefinitionSO` assets to the `Steps` array.
   - Optionally, assign `StartConditions` and `CompleteConditions`.

2. **Create Steps**
   - Right-click → `Create > Unity Helper SDK > Tutorial System > Step Definition`.
   - Set `StepID`, `Title`, `Body`, and `Icon`.
   - Add `TriggerDefinitionSO` assets to `CompletionTriggers`.
   - Optionally, assign `StartConditions` and `CompleteConditions`.

3. **Create Triggers**
   - Right-click → `Create > Unity Helper SDK > Tutorial System > Trigger Definition`.
   - Set the `Type` (e.g., `ButtonPressed`, `SceneLoaded`).
   - Add parameters as needed (e.g., `buttonName`, `sceneName`).

4. **Create Composite Triggers (Optional)**
   - Right-click → `Create > Unity Helper SDK > Tutorial System > Composite Trigger Definition`.
   - Set the `Logic` (And/Or/Not).
   - Add sub-triggers to the `SubTriggers` array.

5. **Create Conditions**
   - Right-click → `Create > Unity Helper SDK > Tutorial System > Condition > Base` or a custom condition.
   - For custom logic, inherit from `ConditionSO` and override `IsMet()`.

### 2. Using the Tutorial System at Runtime
- Add the `TutorialManager` component to a GameObject in your scene.
- Call `TutorialManager.Instance.StartTutorial(yourTutorialSO)` to begin a tutorial.
- Listen to `OnStepEntered` and `OnTutorialCompleted` events for UI updates.
- Use `TutorialUIController` as a reference for displaying step content.

---

## Example: Simple Button Press Tutorial

1. **Create a Trigger for Button Press**
   - Create a `TriggerDefinitionSO`.
   - Set `Type` to `ButtonPressed`.
   - Add a parameter: `Key = "buttonName"`, `StringValue = "StartButton"`.

2. **Create a Step**
   - Create a `StepDefinitionSO`.
   - Set `Title` to "Press the Start Button".
   - Add the trigger above to `CompletionTriggers`.

3. **Create a Tutorial**
   - Create a `TutorialDefinitionSO`.
   - Add the step above to `Steps`.

4. **Start the Tutorial**
   ```csharp
   // In your game logic
   public TutorialDefinitionSO myTutorial;
   void Start() {
       TutorialManager.Instance.StartTutorial(myTutorial);
   }
   ```

---

## Example: Composite Trigger (AND)

- Create two triggers: one for `ButtonPressed` ("StartButton"), one for `SceneLoaded` ("GameScene").
- Create a `CompositeTriggerDefinitionSO`:
  - Set `Logic` to `And`.
  - Add both triggers to `SubTriggers`.
- Assign this composite trigger to a step's `CompletionTriggers`.

---

## Example: Custom Condition

```csharp
using UnityEngine;
[CreateAssetMenu(menuName = "Unity Helper SDK/Tutorial System/Condition/Player Score")] 
public class PlayerScoreConditionSO : ConditionSO 
{
    public int RequiredScore;
    public override bool IsMet() {
        // Replace with your actual game manager logic
        return GameManager.Instance.Score >= RequiredScore;
    }
}
```
- Create a `PlayerScoreConditionSO` asset and set `RequiredScore`.
- Assign it to a step or tutorial's `StartConditions` or `CompleteConditions`.

---

## Example: Editor Usage
- Open the editor: `Unity Helper SDK > Tutorial Editor`.
- Use the tree view to select and edit tutorials and steps.
- Add, remove, or reorder steps and triggers.
- Create new conditions and assign them directly from the editor.

---

## How Step Advancement and Conditions Work

### When Are Conditions Checked?
- **Tutorial Start**: Before a tutorial starts, all `StartConditions` on the `TutorialDefinitionSO` must be met.
- **Step Start**: Before a step is entered, all `StartConditions` on the `StepDefinitionSO` must be met.
- **Step Completion**: When any of a step's `CompletionTriggers` fires, all `CompleteConditions` on the `StepDefinitionSO` must be met before advancing to the next step.
- **Tutorial Completion**: After the last step, all `CompleteConditions` on the `TutorialDefinitionSO` must be met before the tutorial is considered complete.

### How Are Conditions Evaluated?
- Each condition is a `ConditionSO` (or subclass) with an `IsMet()` method.
- The system checks all conditions in the relevant array (`StartConditions` or `CompleteConditions`).
- If any condition returns `false`, the tutorial or step will not start/advance.

### How Does Step Advancement Work?
1. When a tutorial is started, it checks its `StartConditions`.
2. The first step is entered if its `StartConditions` are met.
3. Each step listens for its `CompletionTriggers`.
4. When a trigger fires, the system checks the step's `CompleteConditions`.
5. If all are met, the next step is entered (or the tutorial completes if it was the last step).

---

## Example: Step Advancement with Conditions

Suppose you want a step to advance only if the player has collected 3 coins **and** pressed a button.

1. **Create a Coin Condition**
```csharp
[CreateAssetMenu(menuName = "Unity Helper SDK/Tutorial System/Condition/Coins Collected")]
public class CoinsCollectedConditionSO : ConditionSO {
    public int RequiredCoins;
    public override bool IsMet() {
        return GameManager.Instance.Coins >= RequiredCoins;
    }
}
```
- Create a `CoinsCollectedConditionSO` asset and set `RequiredCoins = 3`.

2. **Create a Button Trigger**
- Create a `TriggerDefinitionSO` with `Type = ButtonPressed` and `buttonName = "ContinueButton"`.

3. **Assign to Step**
- In your `StepDefinitionSO`:
  - Add the button trigger to `CompletionTriggers`.
  - Add the coin condition to `CompleteConditions`.

**Result:**
- The step will only advance when the player presses the button **and** has collected at least 3 coins.

---

## Example: Step Will Not Advance Until All Conditions Are Met

If you have multiple conditions in `CompleteConditions`, **all** must return `true` for the step to advance, even if the trigger fires multiple times.

```csharp
// StepDefinitionSO setup:
// CompletionTriggers: [ButtonPressed ("NextButton")]
// CompleteConditions: [CoinsCollectedConditionSO (RequiredCoins=5), PlayerScoreConditionSO (RequiredScore=100)]
```
- The step will only advance when the player presses "NextButton" **and** has at least 5 coins **and** a score of 100 or more.

---

## Example: Step Advancement Flow

1. Player enters step. UI shows instructions.
2. Player presses the required button (trigger fires).
3. System checks all `CompleteConditions` for the step.
4. If all are met, `AdvanceStep()` is called and the next step is entered.
5. If not, the step remains active and waits for the next trigger event.

---

## Advanced: Composite Triggers and Conditions

- You can use `CompositeTriggerDefinitionSO` to require multiple triggers (e.g., both a button press and a scene load) before checking conditions.
- You can create custom `ConditionSO` subclasses for any game logic.

---

## Best Practices
- Use one ScriptableObject asset per tutorial, step, trigger, and condition for modularity.
- Organize assets in folders (e.g., `Assets/Tutorials/Steps`, `Assets/Tutorials/Triggers`).
- Extend `ConditionSO` and `TriggerDefinitionSO` for custom logic.
- Use composite triggers for complex requirements.
- Use the editor tooling for safe, visual editing.

---

## Extending the System
- **Add new trigger types**: Extend `TriggerFactory` and add new `TriggerType` enums.
- **Add new conditions**: Inherit from `ConditionSO` and override `IsMet()`.
- **Improve editor tooling**: Add custom inspectors or drawers for better UX.

---

## References
- See the provided source files for implementation details:
  - `TutorialDefinitionSO.cs`, `StepDefinitionSO.cs`, `TriggerDefinitionSO.cs`, `CompositeTriggerDefinitionSO.cs`, `ConditionSO.cs`, `PlayerScoreConditionSO.cs`, `TriggerFactory.cs`, `TutorialManager.cs`, `TutorialUIController.cs`, `TutorialEditorWindow.cs`, `TutorialTreeView.cs`

---

For further help or advanced usage, see the code comments and extend as needed for your project!
