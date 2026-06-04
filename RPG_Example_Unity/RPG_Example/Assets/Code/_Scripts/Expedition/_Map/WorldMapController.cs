using System;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.UI;

public class WorldMapController : MonoBehaviour, ISaveable
{
    public static WorldMapController Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private Map map;

    [Header("References")]
    [SerializeField] private Transform player;

    [Header("UI")]
    [SerializeField] private GameObject mapRoot;
    [SerializeField] private Image mapImage;
    [SerializeField] private RectTransform mapRect;
    [SerializeField] private RectTransform playerIcon;
    [SerializeField] private RectTransform poiContainer;
    [SerializeField] private MapPOIIcon poiIconPrefab;

    [Header("Shaders")]
    [SerializeField] private Material mapFogMaterial;
    [SerializeField] private Shader revealBrushShader;

    private MapRevealPainter revealPainter;

    private readonly Dictionary<string, MapPOIData> runtimePOIs = new();
    private readonly Dictionary<string, MapPOIIcon> spawnedPOIIcons = new();

    public bool isOpen;
    private bool hasLastRevealPosition;
    private Vector3 lastRevealPosition;

    private readonly HashSet<string> _savedDiscoveredIds = new();

    private SaveService _save;
    private QuestService _questService;

    public static event Action<MapPOIData> OnPOIPopupRequested;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[WorldMapController] More than one instance found. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        isOpen = false;
        Initialize();

        if (GameServices.TryGet<SaveService>(out _save))
            _save.RegisterSaveable(this);
    }

    public bool IsOpen => isOpen;

    private void Update()
    {
        if (map == null || player == null)
            return;

        TryRevealAroundPlayer();

        if (isOpen)
        {
            UpdatePlayerIcon();
            UpdateVisiblePOIPositions();
        }
    }

    private void Start()
    {
        if (GameServices.TryGet<QuestService>(out _questService))
        {
            _questService.OnQuestStateChanged += HandleQuestStateChanged;
            SyncActiveQuestPOIs();
        }
    }

    private void OnDisable()
    {
        // OnDisable fires before OnDestroy and before sceneUnloaded.
        // Capturing here ensures the map is written to CurrentSave before
        // SaveService.OnSceneUnloaded calls WriteToDisk — even though we are
        // already unregistered by then. Same pattern as PlayerSaveHandler.
        if (_save == null) return;
        if (_save.IsDeathPenaltyApplied) return;

        CaptureToSave(_save.CurrentSave);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (_questService != null)
            _questService.OnQuestStateChanged -= HandleQuestStateChanged;

        revealPainter?.Dispose();

        _save?.UnregisterSaveable(this);
    }

    private void Initialize()
    {
        if (map == null)
        {
            Debug.LogError("[WorldMapController] Map is not assigned.");
            return;
        }

        if (mapImage == null)
        {
            Debug.LogError("[WorldMapController] Map Image is not assigned.");
            return;
        }

        if (mapFogMaterial == null)
        {
            Debug.LogError("[WorldMapController] Map Fog Material is not assigned.");
            return;
        }

        if (revealBrushShader == null)
        {
            Debug.LogError("[WorldMapController] Reveal Brush Shader is not assigned.");
            return;
        }

        mapImage.sprite = map.mapSprite;

        revealPainter = new MapRevealPainter(revealBrushShader, map.maskResolution);

        mapFogMaterial.SetTexture("_ExplorationMask", revealPainter.ExplorationMask);
        mapImage.material = mapFogMaterial;

        if (mapRoot != null)
            mapRoot.SetActive(false);
    }

    public void RegisterDiscoveredPOI(MapPOIData poi)
    {
        if (poi == null || string.IsNullOrWhiteSpace(poi.id))
        {
            Debug.LogWarning("[WorldMapController] Tried to register invalid POI.");
            return;
        }

        if (!runtimePOIs.ContainsKey(poi.id))
            runtimePOIs.Add(poi.id, poi);

        if (!spawnedPOIIcons.ContainsKey(poi.id))
        {
            if (poi.icon != null) CreatePOIIcon(poi);
        }
        else
        {
            spawnedPOIIcons[poi.id].gameObject.SetActive(true);
        }
    }

    public void RemovePOI(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return;

        runtimePOIs.Remove(id);

        if (spawnedPOIIcons.TryGetValue(id, out MapPOIIcon icon))
        {
            Destroy(icon.gameObject);
            spawnedPOIIcons.Remove(id);
        }
    }

    public void UpdatePOIIcon(string id, Sprite newIcon)
    {
        if (!runtimePOIs.TryGetValue(id, out MapPOIData poi))
            return;

        poi.icon = newIcon;

        if (spawnedPOIIcons.TryGetValue(id, out MapPOIIcon icon))
            icon.Setup(poi);
    }

    public static void RequestPOIPopup(MapPOIData poi)
    {
        if (poi == null)
            return;

        OnPOIPopupRequested?.Invoke(poi);
    }

    private void CreatePOIIcon(MapPOIData poi)
    {
        if (poiIconPrefab == null || poiContainer == null)
            return;

        MapPOIIcon icon = Instantiate(poiIconPrefab, poiContainer);
        icon.Setup(poi);

        if (map != null && mapRect != null && icon.RectTransform != null)
        {
            Vector2 position = MapCoordinateUtility.WorldToRectPosition(
                poi.GetWorldPosition(),
                map,
                mapRect
            );

            icon.RectTransform.anchoredPosition = position;
        }

        icon.gameObject.SetActive(true);

        spawnedPOIIcons.Add(poi.id, icon);
    }

    private void UpdateVisiblePOIPositions()
    {
        foreach (var pair in spawnedPOIIcons)
        {
            string id = pair.Key;
            MapPOIIcon icon = pair.Value;

            if (icon == null)
                continue;

            if (!runtimePOIs.TryGetValue(id, out MapPOIData poi))
                continue;

            Vector2 position = MapCoordinateUtility.WorldToRectPosition(
                poi.GetWorldPosition(),
                map,
                mapRect
            );

            icon.RectTransform.anchoredPosition = position;
        }
    }

    private void TryRevealAroundPlayer()
    {
        if (!hasLastRevealPosition)
        {
            RevealAroundPlayer();
            lastRevealPosition = player.position;
            hasLastRevealPosition = true;
            return;
        }

        float sqrDistance = (player.position - lastRevealPosition).sqrMagnitude;
        float requiredDistance = map.revealUpdateDistance * map.revealUpdateDistance;

        if (sqrDistance < requiredDistance)
            return;

        RevealAroundPlayer();
        lastRevealPosition = player.position;
    }

    private void RevealAroundPlayer()
    {
        Vector2 uv = MapCoordinateUtility.WorldToMapUV(player.position, map);

        float worldWidth = map.worldMax.x - map.worldMin.x;
        float radiusNormalized = map.revealRadiusWorld / worldWidth;

        revealPainter.Reveal(uv, radiusNormalized, map.revealSoftness);
    }

    private void UpdatePlayerIcon()
    {
        if (playerIcon == null || mapRect == null)
            return;

        Vector2 position = MapCoordinateUtility.WorldToRectPosition(player.position, map, mapRect);
        playerIcon.anchoredPosition = position;
    }

    public void ToggleMap()
    {
        SetMapOpen(!isOpen);
    }

    public void SetMapOpen(bool open)
    {
        isOpen = open;

        if (mapRoot != null)
            mapRoot.SetActive(open);

        if (open)
        {
            UpdatePlayerIcon();
            UpdateVisiblePOIPositions();
        }
    }

    public bool IsDiscoveredFromSave(string id) => _savedDiscoveredIds.Contains(id);

    // ── Quest POI management ──────────────────────────────────────────────────

    // Quest POIs are only meaningful in the expedition map (scene "Map" or any IsLevel scene).
    private bool IsExpeditionMap => map != null && SceneNames.IsLevel(map.name);

    private void HandleQuestStateChanged(Quest quest)
    {
        if (!IsExpeditionMap) return;

        switch (quest.state)
        {
            case Enums.QuestState.InProgress:
                RegisterQuestPOIs(quest.info);
                break;
            case Enums.QuestState.Finished:
                RemoveQuestPOIs(quest.info);
                break;
        }
    }

    private void SyncActiveQuestPOIs()
    {
        if (!IsExpeditionMap) return;

        foreach (var quest in _questService.GetAllQuests())
        {
            if (quest.state == Enums.QuestState.InProgress ||
                quest.state == Enums.QuestState.CanFinish)
                RegisterQuestPOIs(quest.info);
        }
    }

    private void RegisterQuestPOIs(QuestInfoSO info)
    {
        if (info.mapPOIs == null) return;
        for (int i = 0; i < info.mapPOIs.Length; i++)
        {
            if (info.mapPOIs[i] != null)
                RegisterDiscoveredPOI(info.mapPOIs[i]);
        }
    }

    private void RemoveQuestPOIs(QuestInfoSO info)
    {
        if (info.mapPOIs == null) return;
        for (int i = 0; i < info.mapPOIs.Length; i++)
        {
            if (info.mapPOIs[i] != null)
                RemovePOI(info.mapPOIs[i].id);
        }
    }

    // ── ISaveable ─────────────────────────────────────────────────────────────

    public void CaptureToSave(SaveData data)
    {
        if (map == null) return;

        var entry = data.map.GetOrCreate(map.name);
        entry.discoveredPOIIds.Clear();

        foreach (var kvp in runtimePOIs)
        {
            // Quest POIs are derived from quest state on every load — don't persist them.
            if (kvp.Value.type != MapPOIType.Quest)
                entry.discoveredPOIIds.Add(kvp.Key);
        }

        if (revealPainter != null)
        {
            byte[] pngBytes = revealPainter.GetMaskPNGBytes();
            entry.explorationMaskBase64 = Convert.ToBase64String(pngBytes);
        }
    }

    public void ApplyFromSave(SaveData data)
    {
        _savedDiscoveredIds.Clear();

        var entry = map != null ? data.map.Find(map.name) : null;

        if (entry != null)
        {
            for (int i = 0; i < entry.discoveredPOIIds.Count; i++)
                _savedDiscoveredIds.Add(entry.discoveredPOIIds[i]);
        }

        if (revealPainter == null) return;

        if (entry != null && !string.IsNullOrEmpty(entry.explorationMaskBase64))
        {
            byte[] pngBytes = Convert.FromBase64String(entry.explorationMaskBase64);
            revealPainter.LoadMaskPNGBytes(pngBytes);
        }
        else
        {
            revealPainter.ClearMask();
        }
    }
}