using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

public enum DialogueNodeType { Text, Choice, End, Random }

public class DialogueNode : ScriptableObject
{
    public string id;
    public DialogueNodeType nodeType;

    [Space(10)][Header("Text / End")]
    public LocalizedString localizedText;
    public string nextNodeId;
    public List<DialogueAction> onEnterActions = new();

    [Space(10)][Header("Random")]
    public List<LocalizedString> randomTexts = new();

    [Space(10)][Header("Choice")]
    public bool isRouter;

    public List<DialogueOption> options = new();
    
#if UNITY_EDITOR
    // Editor-only position in the graph canvas. Persisted so layout survives reloads.
    [HideInInspector] public Vector2 editorPosition;
#endif
}