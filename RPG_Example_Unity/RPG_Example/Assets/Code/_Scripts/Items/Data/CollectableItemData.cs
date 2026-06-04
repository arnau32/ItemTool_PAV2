using UnityEngine;

[CreateAssetMenu(fileName = "ColectableItemData", menuName = "Items/Collectable")]
public class CollectableItemData : ItemData
{
    public int collectionID;

    [Tooltip("If true, this collectable converts to permanent AuroraDust currency on extraction.")]
    public bool isAuroraDust;
}
