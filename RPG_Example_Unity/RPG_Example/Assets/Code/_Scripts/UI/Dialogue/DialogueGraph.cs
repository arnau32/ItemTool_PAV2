using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "DialogueGraph", menuName = "Dialogue/Graph")]
public class DialogueGraph : ScriptableObject
{
    [Header("Character")] public Sprite characterIllustration;
    public string characterName;

    [Header("Graph")] public string startNodeId;
    public List<DialogueNode> nodes = new();

#if UNITY_EDITOR
    // Node IDs pinned to the Info tab for quick access.
    [HideInInspector] public List<string> pinnedNodeIds = new();
#endif

    #region Runtime API

    public DialogueNode GetNode(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        foreach (var dialogueNode in nodes)
        {
            if (dialogueNode != null && dialogueNode.id == id) return dialogueNode;
        }

        return null;
    }

    public DialogueNode GetStartNode() => GetNode(startNodeId);

    #endregion

    #region Editor API

#if UNITY_EDITOR

    /// <summary>
    /// Creates a new DialogueNode subasset, registers Undo, and adds it to the list.
    /// Called exclusively by DialogueGraphEditor.
    /// </summary>
    public DialogueNode Editor_CreateNode(DialogueNodeType type)
    {
        var node = CreateInstance<DialogueNode>();
        node.id = System.Guid.NewGuid().ToString("N")[..8];
        node.nodeType = type;
        node.name = GetNextAvailableNodeName();

        Undo.RecordObject(this, "Add Dialogue Node");
        nodes.Add(node);

        AssetDatabase.AddObjectToAsset(node, this);
        Undo.RegisterCreatedObjectUndo(node, "Add Dialogue Node");

        // First node becomes the start node automatically.
        if (nodes.Count == 1)
            startNodeId = node.id;

        EditorUtility.SetDirty(node);
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(this));

        return node;
    }

    /// <summary>
    /// Deletes a node subasset, fixes dangling references, and registers Undo.
    /// Called exclusively by DialogueGraphEditor.
    /// </summary>
    public void Editor_DeleteNode(DialogueNode node)
    {
        if (node == null) return;

        Undo.RecordObject(this, "Delete Dialogue Node");

        // 1. Destroy all subassets owned by this node (actions, conditions, options).
        DestroyNodeSubassets(node);

        // 2. Quitar el nodo de la lista
        nodes.Remove(node);

        // 3. Si era start node, asignar otro
        if (startNodeId == node.id)
            startNodeId = nodes.Count > 0 && nodes[0] != null ? nodes[0].id : string.Empty;

        // 4. Limpiar referencias colgantes en otros nodos
        foreach (var n in nodes)
        {
            if (n == null) continue;

            if (n.nextNodeId == node.id)
            {
                Undo.RecordObject(n, "Clear Deleted Node Reference");
                n.nextNodeId = string.Empty;
            }

            if (n.options != null)
            {
                foreach (var opt in n.options)
                {
                    if (opt == null) continue;

                    if (opt.nextNodeId == node.id)
                    {
                        Undo.RecordObject(opt, "Clear Deleted Node Reference");
                        opt.nextNodeId = string.Empty;
                    }
                }
            }

            EditorUtility.SetDirty(n);
        }

        // 5. Destruir el subasset del nodo
        Undo.DestroyObjectImmediate(node);

        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(this));
    }

    public void Editor_SetStartNode(DialogueNode node)
    {
        if (node == null) return;

        Undo.RecordObject(this, "Set Start Dialogue Node");
        startNodeId = node.id;
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }

    // ── Private editor helpers ────────────────────────────────────────────────

    /// <summary>
    /// Clears references owned by a node before destroying it.
    /// conditions and actions on options are external ScriptableObject assets —
    /// we only null the references, never destroy the assets themselves.
    /// DialogueOption subassets are destroyed because they live inside this .asset file.
    /// </summary>
    private void DestroyNodeSubassets(DialogueNode node)
    {
        if (node == null) return;

        // onEnterActions — external SOs, just clear the list.
        node.onEnterActions?.Clear();

        // Options are subassets — destroy each one.
        // Their conditions/actions lists hold external SO references — just clear them.
        if (node.options != null)
        {
            foreach (var option in node.options)
            {
                if (option == null) continue;

                option.conditions?.Clear();
                option.actions?.Clear();

                if (AssetDatabase.IsSubAsset(option))
                    Undo.DestroyObjectImmediate(option);
            }

            node.options.Clear();
        }

        EditorUtility.SetDirty(node);
    }

    private string GetNextAvailableNodeName()
    {
        string mainAssetName = name;
        int index = 0;

        while (true)
        {
            string candidate = $"{mainAssetName}_Nodo_{index}";
            bool exists = false;

            foreach (var n in nodes)
            {
                if (n != null && n.name == candidate)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists) return candidate;

            index++;
        }
    }

#endif

    #endregion
}