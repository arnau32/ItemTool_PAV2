using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

[CreateAssetMenu(menuName = "FMODAudio/UI Sounds", fileName = "UISounds")]
public class UISounds : FMODEventLibrary
{
    [System.Serializable]
    public struct EquipSlotSound
    {
        public Enums.EquipSlot slot;
        public EventReference sound;
    }

    #region Fields

    [field: Header("Menus")]
    [field: SerializeField] public EventReference ButtonClick    { get; private set; }
    [field: SerializeField] public EventReference MenuNavigate  { get; private set; }
    [field: SerializeField] public EventReference SliderMove    { get; private set; }

    [field: Header("Map")]
    [field: SerializeField] public EventReference MapOpen { get; private set; }

    [field: Header("Inventory")]
    [field: SerializeField] public EventReference CursorMove    { get; private set; }
    [field: SerializeField] public EventReference ItemPickUp    { get; private set; }
    [field: SerializeField] public EventReference LootAllComplete { get; private set; }

    [field: Header("Equipment")]
    [SerializeField] private List<EquipSlotSound> _equipSounds = new();

    [field: Header("Skill Tree")]
    [field: SerializeField] public EventReference UpgradeComplete { get; private set; }

    [field: Header("Dialogue")]
    [field: SerializeField] public EventReference DialoguePageTurn { get; private set; }
    // Leave empty to silence typewriter letter sounds.
    [field: SerializeField] public EventReference DialogueLetter   { get; private set; }

    #endregion

    #region Public API

    public bool TryGetEquipSound(Enums.EquipSlot slot, out EventReference sound)
    {
        for (int i = 0; i < _equipSounds.Count; i++)
        {
            if (_equipSounds[i].slot != slot) continue;
            sound = _equipSounds[i].sound;
            return true;
        }

        sound = default;
        return false;
    }

    #endregion
}
