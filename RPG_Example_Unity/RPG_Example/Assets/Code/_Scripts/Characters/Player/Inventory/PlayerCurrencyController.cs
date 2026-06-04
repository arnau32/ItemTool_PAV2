using UnityEngine;
using UnityEngine.UIElements;

public class PlayerCurrencyController : MonoBehaviour
{
    [Header("Currency Values")]
    [SerializeField] private int gold;


    private Label auroraDustLabel;


    public static PlayerCurrencyController Instance;



    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }
    }

    private void Start()
    {
        PlayerInventory.Instance.OnAuraDustChanged+= UpdateUI;
        InitUI();
        UpdateUI();
    }

    private void InitUI()
    {
        var root = UIManager.Instance.tabViewPanel;

        auroraDustLabel = root.Q<Label>("AuroraDustLabel");

        if (auroraDustLabel == null)
        {
            Debug.LogError("Currency Labels not found!");
        }

    }

    #region Public Methods

    

    #endregion

    private void UpdateUI()
    {
        if (auroraDustLabel != null)
            auroraDustLabel.text = PlayerInventory.Instance.AuraDust.ToString();
    }

    #region Getters


    #endregion
}
