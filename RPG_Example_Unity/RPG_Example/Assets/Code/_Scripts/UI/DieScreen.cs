using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class DieScreen : MonoBehaviour
{
    #region Fields

    [SerializeField] private GameObject _button;
    [SerializeField] private GameObject _hud;
    [SerializeField] private Transform _itemsContainer;
    [SerializeField] private GameObject _lostItemPrefab;
    [Tooltip("Parent GameObject that contains the lost-items title and list. Hidden in Easy mode.")]
    [SerializeField] private GameObject _lostItemsSection;
    [SerializeField] private GameObject _lostItemTitle;

    public string sceneToLoad = "Village";

    private PanelEventHandler[] _panelEventHandlers;
    private EquipmentSlotContainer[] _equipmentSlots;
    private Coroutine _selectRoutine;

    private static readonly WaitForEndOfFrame _waitForEndOfFrame = new();

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        _panelEventHandlers = FindObjectsByType<PanelEventHandler>(FindObjectsSortMode.None);
        _equipmentSlots = FindObjectsByType<EquipmentSlotContainer>(FindObjectsSortMode.None);
    }

    private void OnEnable()
    {
        if (_hud != null)
            _hud.SetActive(false);

        SetPanelEventHandlersEnabled(false);
        StartSelectNextFrame();

        bool easyMode = GameServices.TryGet<SaveService>(out var save) && save.CurrentDifficulty == 1;

        if (_lostItemsSection != null)
            _lostItemsSection.SetActive(!easyMode);
        
        _lostItemTitle.SetActive(!easyMode);

        if (!easyMode)
            PopulateLostItems();

        Application.focusChanged += OnApplicationFocus;
    }

    private void OnDisable()
    {
        Application.focusChanged -= OnApplicationFocus;
        SetPanelEventHandlersEnabled(true);
        StopSelectRoutine();
        ClearItemsContainer();
    }

    #endregion

    #region Public API

    public void OnClickReturnToBase()
    {
        GameServices.Get<ISceneLoader>().LoadScene(sceneToLoad, 3.5f, 1);
    }

    public void OnClickReturnMainMenu()
    {
        GameServices.Get<ISceneLoader>().LoadScene(SceneNames.Menu, 3.5f, Random.Range(0, 2));
    }

    #endregion

    #region Lost Items

    private void PopulateLostItems()
    {
        if (_itemsContainer == null || _lostItemPrefab == null) return;

        var inventory = PlayerInventory.Instance;
        if (inventory != null)
        {
            foreach (var stack in inventory.ItemStacks)
            {
                if (stack.IsEmpty) continue;
                SpawnEntry(stack);
            }
        }

        if (_equipmentSlots != null)
        {
            foreach (var slot in _equipmentSlots)
            {
                if (slot == null || slot.EquippedStack == null || slot.EquippedStack.IsEmpty) continue;
                SpawnEntry(slot.EquippedStack);
            }
        }
    }

    private void SpawnEntry(ItemStack stack)
    {
        var go = Instantiate(_lostItemPrefab, _itemsContainer);
        if (go.TryGetComponent<LostItemEntry>(out var entry))
            entry.SetData(stack);
    }

    private void ClearItemsContainer()
    {
        if (_itemsContainer == null) return;

        for (int i = _itemsContainer.childCount - 1; i >= 0; i--)
            Destroy(_itemsContainer.GetChild(i).gameObject);
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
        if (_panelEventHandlers == null) return;

        foreach (var handler in _panelEventHandlers)
            if (handler != null)
                handler.enabled = enabled;
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
        yield return _waitForEndOfFrame;

        if (_button == null || EventSystem.current == null) yield break;

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(_button);
    }

    #endregion
}
