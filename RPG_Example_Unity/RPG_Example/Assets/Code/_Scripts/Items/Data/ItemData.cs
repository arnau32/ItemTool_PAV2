using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Serialization;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif



[CreateAssetMenu(fileName = "ItemData", menuName = "Items/ItemData")]
public class ItemData : ScriptableObject
{
    [field: SerializeField] public string uniqueID { get; private set; }
    public Enums.ItemType itemType;

    [Header("General Information")]
    [FormerlySerializedAs("itemName")]
    public string itemNameID;
    public LocalizedString localizedDisplayName;
    public Sprite icon;
    [TextArea] public string description;

    [SerializeReference]
    public List<ItemDescriptionBlock> descriptionBlocks;
    public Enums.ItemRarity itemRarity;
    public float value;
    public float weight;
    public int maxStack = 1;

    public Dimensions SlotDimension;

    private void OnValidate()
    {
        #if UNITY_EDITOR
            uniqueID = this.itemNameID;
            UnityEditor.EditorUtility.SetDirty(this);
        #endif
    }


#if UNITY_EDITOR
    [ContextMenu("Add Text Block")]
    public void AddTextBlock()
    {
        descriptionBlocks.Add(new TextDescriptionBlock());
        EditorUtility.SetDirty(this);
    }

    [ContextMenu("Add Stat Block")]
    public void AddStatBlock()
    {
        descriptionBlocks.Add(new StatDescriptionBlock());
        EditorUtility.SetDirty(this);
    }
#endif

}

[Serializable]
public struct Dimensions
{
    public int Height;
    public int Width;
}

