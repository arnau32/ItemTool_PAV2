using UnityEngine;

[RequireComponent(typeof(Collider))]
public class MapPOITrigger : MonoBehaviour
{
    [Header("POI")]
    [SerializeField] private MapPOIData poiData;

    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";

    [Header("Popup")]
    [Tooltip("Seconds before EveryEnter mode can show the popup again after the player re-enters.")]
    [Min(0f)] [SerializeField] private float reenterCooldown = 10f;

    public static string CurrentPOI { get; private set; } = "";

    private bool hasBeenDiscovered;
    private bool popupShownOnce;
    private bool isInside;
    private float _lastPopupTime = float.NegativeInfinity;

    private void Awake()
    {
        // Vector3 is a struct — always assign from the trigger's transform so
        // the SO doesn't need manual position setup in the Inspector.
        if (poiData != null)
            poiData.worldTransform = transform.position;
    }

    private void Start()
    {
        if (poiData == null)
        {
            Debug.LogWarning($"[MapPOITrigger] POI Data is null on {name}.");
            return;
        }

        if (poiData.showIconFromStart)
        {
            hasBeenDiscovered = true;
            WorldMapController.Instance?.RegisterDiscoveredPOI(poiData);
            return;
        }

        if (WorldMapController.Instance != null && WorldMapController.Instance.IsDiscoveredFromSave(poiData.id))
        {
            hasBeenDiscovered = true;
            WorldMapController.Instance.RegisterDiscoveredPOI(poiData);
        }
    }

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void OnValidate()
    {
        Collider col = GetComponent<Collider>();

        if (col != null)
            col.isTrigger = true;

        if (poiData != null)
            poiData.worldTransform = transform.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        if (isInside)
            return;

        isInside = true;

        if (poiData == null)
        {
            Debug.LogWarning($"[MapPOITrigger] POI Data is null on {name}.");
            return;
        }

        CurrentPOI = !string.IsNullOrEmpty(poiData.id) ? poiData.id : name;

        if (!hasBeenDiscovered)
        {
            hasBeenDiscovered = true;
            WorldMapController.Instance?.RegisterDiscoveredPOI(poiData);
        }

        HandlePopup();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        isInside = false;
        if (CurrentPOI == (!string.IsNullOrEmpty(poiData?.id) ? poiData.id : name))
            CurrentPOI = "";
    }

    private void HandlePopup()
    {
        switch (poiData.popupMode)
        {
            case MapPOIPopupMode.Never:
                return;

            case MapPOIPopupMode.Once:
                if (popupShownOnce)
                    return;

                popupShownOnce = true;
                WorldMapController.RequestPOIPopup(poiData);
                return;

            case MapPOIPopupMode.EveryEnter:
                if (Time.time - _lastPopupTime < reenterCooldown)
                    return;
                _lastPopupTime = Time.time;
                WorldMapController.RequestPOIPopup(poiData);
                return;
        }
    }
}