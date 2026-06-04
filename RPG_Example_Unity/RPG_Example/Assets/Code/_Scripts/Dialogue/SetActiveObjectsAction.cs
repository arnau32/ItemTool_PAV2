using UnityEngine;

/// <summary>
/// Dialogue action: activates or deactivates scene GameObjects registered in SceneObjectRegistry.
/// Use to reposition NPCs, swap scene states, etc. after a dialogue node fires.
/// </summary>
[CreateAssetMenu(fileName = "Set Active Objects", menuName = "Dialogue/Actions/Set Active Objects")]
public class SetActiveObjectsAction : DialogueAction
{
    [System.Serializable]
    public struct Entry
    {
        [Tooltip("Must match the id registered in SceneObjectRegistry.")]
        public string id;
        public bool active;
    }

    [SerializeField] private Entry[] _entries;

    public override void Execute()
    {
        var registry = SceneObjectRegistry.Instance;

        if (registry == null)
        {
            Debug.LogWarning("[SetActiveObjectsAction] No SceneObjectRegistry found in scene.");
            return;
        }

        for (int i = 0; i < _entries.Length; i++)
        {
            var entry = _entries[i];

            if (string.IsNullOrEmpty(entry.id))
            {
                Debug.LogWarning($"[SetActiveObjectsAction] Entry [{i}] has no id — skipping.");
                continue;
            }

            if (!registry.TryGet(entry.id, out var obj))
            {
                Debug.LogWarning($"[SetActiveObjectsAction] Id '{entry.id}' not found in SceneObjectRegistry.");
                continue;
            }

            obj.SetActive(entry.active);
        }
    }
}
