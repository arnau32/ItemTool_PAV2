using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.UIElements;

public class ExtractionResumeScreen : MonoBehaviour
{
    #region Fields

    [SerializeField] private GameObject _firstButton;
    [SerializeField] private GameObject _hud;
    [SerializeField] private Transform _itemsContainer;
    [SerializeField] private GameObject _itemEntryPrefab;
    [SerializeField] private PlayerHealthSystem _playerHealth;

    [Header("Door Penalty")]
    [SerializeField] private GameObject _stolenDustRoot;
    [SerializeField] private TMP_Text _stolenDustText;
    [SerializeField] private LocalizedString _stolenDustLocalized;

    private PanelEventHandler[] _panelEventHandlers;
    private Coroutine _selectRoutine;
    private string _sceneID;
    private int    _loadingScreenId = -1;
    private int    _pendingPenalty;

    #endregion

    #region Public API

    public void Show(string sceneID, int loadingScreenId = -1)
    {
        _sceneID         = sceneID;
        _loadingScreenId = loadingScreenId;
        gameObject.SetActive(true);
    }

    // Call before Show() when extracting via door — sets the amount the NPC will steal on confirm.
    public void SetPenalty(int amount)
    {
        _pendingPenalty = amount;
    }

    public void OnClickReturnToBase()
    {
        if (_pendingPenalty > 0)
        {
            PlayerInventory.Instance?.ConsumeDustPenalty(_pendingPenalty);
            _pendingPenalty = 0;
        }

        PlayerInventory.Instance?.ExtractAuroraDustCollectables();

        gameObject.SetActive(false);

        var loader = GameServices.Get<ISceneLoader>();
        if (_loadingScreenId >= 0)
            loader.LoadScene(_sceneID, 3.5f, _loadingScreenId);
        else
            loader.LoadScene(_sceneID, 3.5f);
    }

    #endregion

    #region Unity Callbacks

    private void OnEnable()
    {
        if (_playerHealth != null)
            _playerHealth.SetInvulnerable(true);

        if (_hud != null)
            _hud.SetActive(false);

        GameServices.Get<InputService>().OnUIOpen();

        SetPanelEventHandlersEnabled(false);
        StartSelectNextFrame();
        PopulateRecoveredItems();
        RefreshStolenDustText();
        Application.focusChanged += OnApplicationFocus;
    }

    private void OnDisable()
    {
        Application.focusChanged -= OnApplicationFocus;

        // Do NOT clear invulnerability here — the scene is about to load and the
        // health system resets _isInvulnerable via SetupStats on the new scene.
        // Clearing it here leaves the player vulnerable during the 3.5s load window.

        SetPanelEventHandlersEnabled(true);
        StopSelectRoutine();
        ClearItemsContainer();
    }

    #endregion

    #region Recovered Items

    private void PopulateRecoveredItems()
    {
        if (_itemsContainer == null || _itemEntryPrefab == null) return;

        var inventory = PlayerInventory.Instance;
        if (inventory != null)
        {
            var dustKept = BuildDustKeptMap(inventory);

            foreach (var stack in inventory.ItemStacks)
            {
                if (stack.IsEmpty) continue;

                if (dustKept != null && dustKept.TryGetValue(stack, out int kept))
                {
                    if (kept > 0)
                        SpawnEntry(stack, kept);
                }
                else
                {
                    SpawnEntry(stack);
                }
            }
        }

        var equipSlots = FindObjectsByType<EquipmentSlotContainer>(FindObjectsSortMode.None);
        foreach (var slot in equipSlots)
        {
            if (slot.EquippedStack == null || slot.EquippedStack.IsEmpty) continue;
            SpawnEntry(slot.EquippedStack);
        }
    }

    // Mirrors the consumption order of ConsumeDustPenalty to show exact kept quantities per stack.
    private Dictionary<ItemStack, int> BuildDustKeptMap(PlayerInventory inventory)
    {
        if (_pendingPenalty <= 0) return null;

        var map = new Dictionary<ItemStack, int>();
        int remaining = _pendingPenalty;

        foreach (var stack in inventory.ItemStacks)
        {
            if (!(stack.data is CollectableItemData c) || !c.isAuroraDust) continue;
            int remove = Mathf.Min(stack.quantity, remaining);
            map[stack] = stack.quantity - remove;
            remaining -= remove;
        }

        return map;
    }

    private void SpawnEntry(ItemStack stack, int overrideQuantity = -1)
    {
        var go = Instantiate(_itemEntryPrefab, _itemsContainer);
        if (go.TryGetComponent<LostItemEntry>(out var entry))
            entry.SetData(stack, overrideQuantity);
    }

    private void ClearItemsContainer()
    {
        if (_itemsContainer == null) return;

        for (int i = _itemsContainer.childCount - 1; i >= 0; i--)
            Destroy(_itemsContainer.GetChild(i).gameObject);
    }

    #endregion

    #region Stolen Dust Text

    private async void RefreshStolenDustText()
    {
        if (_stolenDustRoot == null) return;

        if (_pendingPenalty <= 0)
        {
            _stolenDustRoot.SetActive(false);
            return;
        }

        _stolenDustRoot.SetActive(true);

        if (_stolenDustText != null)
        {
            string label = await _stolenDustLocalized.GetLocalizedStringAsync();
            _stolenDustText.text = $"{label} {_pendingPenalty}";
        }
    }

    #endregion

    #region UI Helpers

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            StartSelectNextFrame();
    }

    private void SetPanelEventHandlersEnabled(bool enabled)
    {
        if (enabled)
        {
            if (_panelEventHandlers == null) return;

            foreach (var handler in _panelEventHandlers)
                if (handler != null)
                    handler.enabled = true;

            _panelEventHandlers = null;
            return;
        }

        _panelEventHandlers = FindObjectsByType<PanelEventHandler>(FindObjectsSortMode.None);

        foreach (var handler in _panelEventHandlers)
            if (handler != null)
                handler.enabled = false;
    }

    private void StartSelectNextFrame()
    {
        StopSelectRoutine();
        _selectRoutine = StartCoroutine(SelectRoutine());
    }

    private void StopSelectRoutine()
    {
        if (_selectRoutine != null)
        {
            StopCoroutine(_selectRoutine);
            _selectRoutine = null;
        }
    }

    private IEnumerator SelectRoutine()
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        if (_firstButton == null || EventSystem.current == null) yield break;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(_firstButton);
    }

    #endregion
}
