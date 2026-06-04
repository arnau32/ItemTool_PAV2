using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// One selectable option inside a Choice node.
///
/// Standalone ScriptableObject asset — NOT a subasset of DialogueGraph.
///
/// WHY standalone instead of subasset:
///   Subassets embedded in a .asset file are only loaded when their parent asset
///   is loaded. When a DialogueGraph is loaded via Addressables or Resources,
///   Unity does NOT guarantee that embedded subassets are fully deserialized
///   before the graph is used at runtime — especially under IL2CPP where the
///   linker can strip types that only appear as subasset entries.
///   Standalone assets are always included in the build and always deserialized
///   independently when directly referenced from DialogueNode.options[].
///
/// Created and managed exclusively by DialogueGraphEditor.
/// Lives in the same folder as the DialogueGraph that owns it.
/// </summary>
[CreateAssetMenu(fileName = "DialogueOption", menuName = "Dialogue/Option")]
public class DialogueOption : ScriptableObject
{
    public LocalizedString localizedOptionText;
    public string nextNodeId;
    public List<DialogueCondition> conditions = new();
    public List<DialogueAction>    actions    = new();

    /// <summary>Returns true if all conditions pass, or the list is empty.</summary>
    public bool EvaluateConditions()
    {
        foreach (var t in conditions)
        {
            if (t != null && !t.Evaluate()) return false;
        }

        return true;
    }
}