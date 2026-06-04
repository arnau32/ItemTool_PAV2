using UnityEngine;

/// <summary>
/// Base class for all conditions that control whether a DialogueOption is visible.
///
/// Conditions are ScriptableObjects stored as subassets of a DialogueGraph,
/// created exclusively via DialogueGraphEditor.
///
/// If ALL conditions on an option evaluate to true → option is shown.
/// If ANY condition evaluates to false             → option is hidden.
///
/// Derive from this to add custom visibility rules (e.g. PlayerHasItem,
/// PlayerLevelRequirement, FactionStanding, etc.).
/// </summary>
public abstract class DialogueCondition : ScriptableObject
{
    /// <summary>Returns true if this condition is currently satisfied.</summary>
    public abstract bool Evaluate();
}




