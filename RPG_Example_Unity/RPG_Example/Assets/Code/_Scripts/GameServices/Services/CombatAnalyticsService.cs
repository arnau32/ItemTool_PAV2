using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CombatAnalyticsService : MonoBehaviour, IGameServices
{
    #region Fields

    private bool _ending;
    private bool _sessionActive;

    private string _sessionId;
    private string _sessionLabel;
    private int _runNumber;

    private string _sceneName;
    private string _buildVersion;
    private string _startUtcIso;
    private string _endUtcIso;
    private float _startTimeRealtime;
    private float _endTimeRealtime;
    private string _endReason;

    private PlayerBlock _player = new();

    private readonly Dictionary<int, EnemyAgg> _enemyByTypeId = new(64);
    private readonly Dictionary<(int, int), AIAgg> _aiByTypeAndAction = new(256);

    private readonly Dictionary<int, EncounterRecord> _activeEncounters = new(8);
    private readonly List<EncounterSerializable> _completedEncounters = new(32);
    private readonly Dictionary<int, int> _encounterAttemptsByTypeId = new(16);

    private readonly Dictionary<int, MechanicAgg> _mechanicsByNameId = new(16);
    private string _lastAttackMechanicName;

    private readonly Dictionary<int, StateAgg> _statesByNameId = new(16);
    private int _currentStateNameId;
    private float _currentStateEnterRealtime;

    private ExtractionRunData _extractionRun;

    private AttackData _lastAttack;

    private bool _staminaAtZero;
    private float _staminaZeroStartRealtime;

    private bool _lockOnActive;
    private float _lockOnStartRealtime;

    private float _lastDamageTaken;
    private int _lastDamageHitTypeId;
    private bool _lastDamageWasInDodgeFrames;

    private bool _hasSignificantActivity;

    private const string RUN_COUNTER_KEY = "combat_analytics_run_counter";

    private readonly System.Text.StringBuilder _sb = new(256);

    // Weapon tracking
    private readonly Dictionary<string, WeaponAgg> _weaponById = new(8);
    private string _currentWeaponId;

    // Scene time tracking
    private SceneSegment _currentSceneSegment;
    private readonly List<SceneSegmentSerializable> _completedSegments = new(16);
    private readonly Dictionary<string, int> _sceneVisitCounts = new(8);

    // Loot events
    private readonly List<LootEventSerializable> _lootEvents = new(64);

    private string _lastKillerEnemyType;

    private const string COMBAT_SCENE_NAME = "Map";

    // Run progression
    private static int _runsThisSession = 0;
    private string _firstMechanicUsed;
    private float _firstEncounterTimeS;
    private float _firstKillTimeS;

    // Combo tracking
    private int _currentComboLength;
    private int _totalCombos;
    private float _totalComboLength;
    private int _maxComboLength;

    #endregion

    #region Properties

    public bool HasActiveSession => _sessionActive;
    public string CurrentSessionLabel => _sessionLabel;

    #endregion

    #region Unity Callbacks

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneEntered(scene.name);
    }

    private void OnApplicationQuit()
    {
        if (_sessionActive) EndSession("app_quit");
    }

    #endregion

    #region Session

    public void TryStartSessionIfNone()
    {
        if (_sessionActive) return;
        StartSession();
    }

    private void StartSession()
    {
        _sessionActive = true;
        _ending = false;

        _sessionId = Guid.NewGuid().ToString("N");

        _sceneName = SceneManager.GetActiveScene().name;
        _buildVersion = AnalyticsConfig.Load()?.FullBuildVersion ?? Application.version;

        _startUtcIso = DateTime.UtcNow.ToString("o");
        _endUtcIso = "";
        _endReason = "";

        _startTimeRealtime = Time.realtimeSinceStartup;
        _endTimeRealtime = 0f;

        _runNumber = Mathf.Max(0, PlayerPrefs.GetInt(RUN_COUNTER_KEY, 0)) + 1;
        PlayerPrefs.SetInt(RUN_COUNTER_KEY, _runNumber);
        PlayerPrefs.Save();

        _sb.Clear();
        _sb.Append("Run ").Append(_runNumber.ToString("000"))
            .Append(" | ").Append(_sceneName)
            .Append(" | ").Append(DateTime.Now.ToString("HH:mm:ss"));
        _sessionLabel = _sb.ToString();

        _player = new PlayerBlock();
        _enemyByTypeId.Clear();
        _aiByTypeAndAction.Clear();

        _activeEncounters.Clear();
        _completedEncounters.Clear();
        _encounterAttemptsByTypeId.Clear();
        _mechanicsByNameId.Clear();
        _lastAttackMechanicName = null;
        _statesByNameId.Clear();
        _currentStateNameId = 0;
        _currentStateEnterRealtime = 0f;
        _extractionRun = null;

        _lastAttack = null;
        _staminaAtZero = false;
        _staminaZeroStartRealtime = 0f;
        _lockOnActive = false;
        _lockOnStartRealtime = 0f;
        _lastDamageTaken = 0f;
        _lastDamageHitTypeId = 0;
        _lastDamageWasInDodgeFrames = false;
        _hasSignificantActivity = false;

        _weaponById.Clear();
        _currentWeaponId = null;

        _currentSceneSegment = null;
        _completedSegments.Clear();
        _sceneVisitCounts.Clear();

        _lootEvents.Clear();

        _lastKillerEnemyType = null;

        _runsThisSession += 1;
        _firstMechanicUsed = null;
        _firstEncounterTimeS = -1f;
        _firstKillTimeS = -1f;

        _currentComboLength = 0;
        _totalCombos = 0;
        _totalComboLength = 0f;
        _maxComboLength = 0;

        StringIdPool.Clear();

        // Open the first scene segment manually — sceneLoaded already fired before the session started
        SceneEntered(_sceneName);
    }

    public void EndSession(string reason)
    {
        if (!_sessionActive || _ending) return;

        if (!_hasSignificantActivity)
        {
            _sessionActive = false;
            return;
        }

        _ending = true;
        _sessionActive = false;

        FlushPendingAttackIfAny();
        FlushStaminaZeroIfAny();
        FlushLockOnIfAny();
        FlushCurrentStateIfAny();
        FlushActiveEncounters(playerDied: reason == "player_death");

        if (_currentWeaponId != null && _weaponById.TryGetValue(_currentWeaponId, out var currWeapon))
            currWeapon.OnUnequip(Time.realtimeSinceStartup);
        FlushCurrentSceneSegment();

        _endReason = reason ?? "unknown";
        _endUtcIso = DateTime.UtcNow.ToString("o");
        _endTimeRealtime = Time.realtimeSinceStartup;

        _player.deathLastDamage = _lastDamageTaken;
        _player.deathLastHitTypeId = _lastDamageHitTypeId;
        _player.deathLastWasInDodgeFrames = _lastDamageWasInDodgeFrames ? 1 : 0;

        var payload = BuildSessionPayload();
        var json = JsonUtility.ToJson(payload, true);

        if (GameServices.TryGet<CombatAnalyticsUploader>(out var uploader))
            StartCoroutine(uploader.UploadJson(json));
        else
            Debug.LogWarning("[CombatAnalytics] CombatAnalyticsUploader not registered — cannot upload to Sheets.");

        _ending = false;
    }

    private CombatAnalyticsSessions BuildSessionPayload()
    {
        float duration = Mathf.Max(0f, _endTimeRealtime - _startTimeRealtime);

        if (_extractionRun != null)
        {
            _extractionRun.encountersAttempted = _completedEncounters.Count;
            _extractionRun.encountersCompleted = 0;
            foreach (var enc in _completedEncounters)
                if (enc.playerDied == 0)
                    _extractionRun.encountersCompleted++;
        }

        if (_currentComboLength > 0)
        {
            _totalCombos += 1;
            _totalComboLength += _currentComboLength;
            if (_currentComboLength > _maxComboLength) _maxComboLength = _currentComboLength;
        }
        _player.comboLengthAvg = _totalCombos > 0 ? _totalComboLength / _totalCombos : 0f;
        _player.longestCombo = _maxComboLength;
        _player.explorationTimeSeconds = Mathf.Max(0f, duration - _player.combatTimeSeconds);

        var session = new CombatAnalyticsSessions
        {
            sessionLabel    = _sessionLabel,
            sessionId       = _sessionId,
            runNumber       = _runNumber,
            buildVersion    = _buildVersion,
            sceneName       = _sceneName,
            startUtcIso     = _startUtcIso,
            endUtcIso       = _endUtcIso,
            durationSeconds = duration,
            endReason       = _endReason,
            player          = _player.ToSerializable(),
            enemies         = new List<EnemyAggSerializable>(_enemyByTypeId.Count),
            ai              = new List<AIAggSerializable>(_aiByTypeAndAction.Count),
            encounters      = new List<EncounterSerializable>(_completedEncounters),
            mechanics       = new List<MechanicSerializable>(_mechanicsByNameId.Count),
            stateMachine    = new List<StateSerializable>(_statesByNameId.Count),
            extractionRun   = _extractionRun?.ToSerializable(
                _sessionId, duration,
                _player.damageTaken, _player.healsUsed, _player.damageDone),
            weapons                     = BuildWeaponsList(),
            sceneSegments               = new List<SceneSegmentSerializable>(_completedSegments),
            lootEvents                  = new List<LootEventSerializable>(_lootEvents),
            runsThisSession             = _runsThisSession,
            timeToFirstEncounterSeconds = _firstEncounterTimeS,
            timeToFirstKillSeconds      = _firstKillTimeS,
            progression = new ProgressionSerializable
            {
                firstMechanicUsed     = _firstMechanicUsed ?? "",
                newMechanicDiscovered = _firstMechanicUsed != null ? 1 : 0
            }
        };

        session.FillFromAggs(_enemyByTypeId, _aiByTypeAndAction.Values,
            _mechanicsByNameId.Values, _statesByNameId.Values);
        return session;
    }

    private List<WeaponStatsSerializable> BuildWeaponsList()
    {
        var list = new List<WeaponStatsSerializable>(_weaponById.Count);
        foreach (var kv in _weaponById)
            list.Add(kv.Value.ToSerializable(equippedAtDeath: kv.Key == _currentWeaponId && _endReason == "player_death"));
        return list;
    }

    #endregion

    #region Public Player API

    public void PlayerAttackStarted(AttackData attack, Enums.AttackInputs? inputType = null)
    {
        if (!_sessionActive) TryStartSessionIfNone();
        _hasSignificantActivity = true;

        FlushPendingAttackIfAny();

        _lastAttack = attack;
        _player.attacksStarted += 1;
        _player.staminaSpentAttacks += Mathf.Max(0f, attack.staminaCost);

        string atkLabel = !string.IsNullOrEmpty(attack.attackName)
            ? attack.attackName
            : (inputType.HasValue ? inputType.Value.ToString() : null);
        if (atkLabel != null)
        {
            int atkLabelId = StringIdPool.GetId(atkLabel);
            _player.attackNameCounts.Upsert(atkLabelId, 1);
            if (_currentWeaponId != null)
                _player.attackWeaponMap.Set(atkLabelId, _currentWeaponId);
        }

        if (inputType.HasValue)
        {
            _lastAttackMechanicName = inputType.Value switch
            {
                Enums.AttackInputs.HeavyInput  => MechanicNames.HeavyAttack,
                Enums.AttackInputs.Skill       => MechanicNames.WeaponSkill,
                Enums.AttackInputs.Run_Attack  => MechanicNames.HeavyAttack,
                _                              => MechanicNames.LightAttack
            };

            float t = Time.realtimeSinceStartup - _startTimeRealtime;
            MechanicRecordUse(_lastAttackMechanicName, attack.staminaCost, t);

            bool isHeavy = inputType.Value != Enums.AttackInputs.LightInput;
            foreach (var enc in _activeEncounters.Values)
            {
                if (isHeavy) enc.heavyAttacksUsed += 1;
                else enc.lightAttacksUsed += 1;
            }
        }
        else
        {
            _lastAttackMechanicName = null;
        }
    }

    public void PlayerAttackResolved(AttackData attack, bool hit)
    {
        if (!_sessionActive) TryStartSessionIfNone();
        if (attack == null) return;

        _player.attacksResolved += 1;
        if (hit) _player.hits += 1;
        else _player.misses += 1;

        if (hit) _currentComboLength += 1;

        if (_currentWeaponId != null && _weaponById.TryGetValue(_currentWeaponId, out var wa))
        {
            wa.attacksStarted += 1;
            if (hit) wa.hits += 1; else wa.misses += 1;
        }

        if (_lastAttack == attack)
        {
            if (_lastAttackMechanicName != null)
                MechanicRecordOutcome(_lastAttackMechanicName, hit);

            _lastAttack = null;
            _lastAttackMechanicName = null;
        }
    }

    public void PlayerDealtDamage(float damage, Component target)
    {
        if (!_sessionActive) TryStartSessionIfNone();
        _player.damageDone += damage;

        if (_currentWeaponId != null && _weaponById.TryGetValue(_currentWeaponId, out var wa))
            wa.damageDone += damage;
    }

    public void PlayerTookDamage(float damage, Enums.HitType hitType, bool wasInDodgeFrames)
    {
        if (!_sessionActive) TryStartSessionIfNone();
        _hasSignificantActivity = true;

        _player.damageTaken += damage;

        if (_currentComboLength > 0)
        {
            _totalCombos += 1;
            _totalComboLength += _currentComboLength;
            if (_currentComboLength > _maxComboLength) _maxComboLength = _currentComboLength;
            _currentComboLength = 0;
        }

        if (wasInDodgeFrames)
        {
            _player.damageTakenWhileDodging += damage;
            _player.hitsTakenWhileDodging += 1;

            int dodgeNameId = StringIdPool.GetId(MechanicNames.Dodge);
            if (_mechanicsByNameId.TryGetValue(dodgeNameId, out var dodgeAgg))
            {
                dodgeAgg.failCount += 1;
                dodgeAgg.successCount = Mathf.Max(0, dodgeAgg.successCount - 1);
            }
        }

        int hitTypeId = EnumStringCache.HitTypeId(hitType);
        _player.hitTypeTakenCounts.Upsert(hitTypeId, 1);

        _lastDamageTaken = damage;
        _lastDamageHitTypeId = hitTypeId;
        _lastDamageWasInDodgeFrames = wasInDodgeFrames;
    }

    public void PlayerDodged(bool lockedOn, Vector2 cardinal, float staminaCost)
    {
        if (!_sessionActive) TryStartSessionIfNone();
        _hasSignificantActivity = true;

        _player.dodges += 1;
        _player.staminaSpentDodges += Mathf.Max(0f, staminaCost);
        if (lockedOn) _player.lockedDodges += 1;
        _player.dodgeDirCounts.Upsert(CardinalToId(cardinal), 1);

        float t = Time.realtimeSinceStartup - _startTimeRealtime;
        MechanicRecordUse(MechanicNames.Dodge, staminaCost, t);
        MechanicRecordOutcome(MechanicNames.Dodge, true);

        foreach (var enc in _activeEncounters.Values) enc.dodgesUsed += 1;
    }

    public void PlayerStaminaChanged(float current, float max)
    {
        if (!_sessionActive) TryStartSessionIfNone();

        bool nowEmpty = current <= 0f;

        if (!_staminaAtZero && nowEmpty)
        {
            _staminaAtZero = true;
            _staminaZeroStartRealtime = Time.realtimeSinceStartup;
            _player.staminaEmptyCount += 1;
            foreach (var enc in _activeEncounters.Values) enc.staminaEmptyCount += 1;
        }
        else if (_staminaAtZero && !nowEmpty)
        {
            FlushStaminaZeroIfAny();
        }
    }

    public void PlayerHealed(float delta01)
    {
        if (!_sessionActive) TryStartSessionIfNone();

        _player.healsUsed += 1;
        _player.healedRatioTotal += delta01;

        float t = Time.realtimeSinceStartup - _startTimeRealtime;
        MechanicRecordUse(MechanicNames.Heal, 0f, t);
        MechanicRecordOutcome(MechanicNames.Heal, true);
    }

    public void PlayerLockOnToggled(bool active)
    {
        if (!_sessionActive) TryStartSessionIfNone();

        if (active)
        {
            if (!_lockOnActive)
            {
                _lockOnActive = true;
                _lockOnStartRealtime = Time.realtimeSinceStartup;
                _player.lockOnToggles += 1;

                float t = Time.realtimeSinceStartup - _startTimeRealtime;
                MechanicRecordUse(MechanicNames.LockOn, 0f, t);
                MechanicRecordOutcome(MechanicNames.LockOn, true);
            }
        }
        else
        {
            FlushLockOnIfAny();
        }
    }

    public void WeaponEquipped(string weaponId, string weaponType)
    {
        if (!_sessionActive) TryStartSessionIfNone();
        float now = Time.realtimeSinceStartup;

        if (_currentWeaponId != null && _weaponById.TryGetValue(_currentWeaponId, out var prev))
            prev.OnUnequip(now);

        _currentWeaponId = weaponId;

        if (!_weaponById.TryGetValue(weaponId, out var agg))
        {
            agg = new WeaponAgg { weaponId = weaponId, weaponType = weaponType };
            _weaponById[weaponId] = agg;
        }
        agg.OnEquip(now);
    }

    #endregion

    #region Public Enemy API

    public void EnemySpawned(EnemyBase enemyBase)
    {
        if (!_sessionActive) TryStartSessionIfNone();
        if (enemyBase == null || !IsInCombatScene()) return;

        GetOrCreateEnemyAgg(enemyBase).spawned += 1;
    }

    public void EnemyKilled(EnemyBase enemyBase)
    {
        if (!_sessionActive) TryStartSessionIfNone();
        if (enemyBase == null || !IsInCombatScene()) return;

        GetOrCreateEnemyAgg(enemyBase).killed += 1;

        if (_firstKillTimeS < 0f)
            _firstKillTimeS = Time.realtimeSinceStartup - _startTimeRealtime;
    }

    public void EnemyDealtDamageToPlayer(EnemyBase enemyBase, float damage, Enums.HitType hitType)
    {
        if (!_sessionActive) TryStartSessionIfNone();
        if (enemyBase == null || !IsInCombatScene()) return;

        _lastKillerEnemyType = enemyBase.Definition?.displayName ?? "";

        var agg = GetOrCreateEnemyAgg(enemyBase);
        agg.damageAttemptedToPlayer += damage;
        agg.hitTypeDealtCounts.Upsert(EnumStringCache.HitTypeId(hitType), 1);

        if (_activeEncounters.TryGetValue(enemyBase.GetInstanceID(), out var enc))
        {
            enc.hitsReceived += 1;
            enc.damageTaken += damage;
        }
    }

    public void EnemyTookDamageFromPlayer(EnemyBase enemyBase, float damage, Enums.HitType hitType)
    {
        if (!_sessionActive) TryStartSessionIfNone();
        if (enemyBase == null || !IsInCombatScene()) return;

        var agg = GetOrCreateEnemyAgg(enemyBase);
        agg.damageTakenFromPlayer += damage;
        agg.hitTypeTakenCounts.Upsert(EnumStringCache.HitTypeId(hitType), 1);
    }

    public void AIActionChosen(EnemyBase enemyBase, string actionName, Enums.ActionCategory category, float score, bool switched)
    {
        if (!_sessionActive) TryStartSessionIfNone();
        if (enemyBase == null) return;

        int enemyTypeId  = StringIdPool.GetId(enemyBase.Definition.displayName);
        int actionNameId = StringIdPool.GetId(actionName);
        var key = (enemyTypeId, actionNameId);

        if (!_aiByTypeAndAction.TryGetValue(key, out var agg))
        {
            agg = new AIAgg
            {
                enemyTypeId  = enemyTypeId,
                actionNameId = actionNameId,
                categoryId   = EnumStringCache.ActionCategoryId(category)
            };
            _aiByTypeAndAction[key] = agg;
        }

        agg.selectedCount += 1;
        if (switched) agg.switchedCount += 1;
        agg.scoreSum += score;
    }

    #endregion

    #region Public Encounter API

    public void EncounterStarted(EnemyBase enemy, Vector3 position)
    {
        if (!_sessionActive) TryStartSessionIfNone();
        if (enemy == null || !IsInCombatScene()) return;
        _hasSignificantActivity = true;

        if (_firstEncounterTimeS < 0f)
            _firstEncounterTimeS = Time.realtimeSinceStartup - _startTimeRealtime;

        int instanceId = enemy.GetInstanceID();
        if (_activeEncounters.ContainsKey(instanceId)) return;

        int enemyTypeId = StringIdPool.GetId(enemy.Definition.displayName);

        if (!_encounterAttemptsByTypeId.TryGetValue(enemyTypeId, out int attempts))
            attempts = 0;
        _encounterAttemptsByTypeId[enemyTypeId] = ++attempts;

        _activeEncounters[instanceId] = new EncounterRecord
        {
            instanceId    = instanceId,
            enemyTypeId   = enemyTypeId,
            attemptNumber = attempts,
            startRealtime = Time.realtimeSinceStartup,
            positionX     = position.x,
            positionY     = position.y,
            positionZ     = position.z,
            scene         = _currentSceneSegment?.sceneName ?? ""
        };
    }

    public void EncounterEnded(EnemyBase enemy, bool playerDied, Vector3 playerPosition = default)
    {
        if (enemy == null) return;

        int instanceId = enemy.GetInstanceID();
        if (!_activeEncounters.TryGetValue(instanceId, out var record)) return;
        _activeEncounters.Remove(instanceId);

        FinalizeEncounterRecord(record, playerDied, playerPosition);
    }

    #endregion

    #region Public Mechanic API

    public void ParryAttempted(bool success, float staminaCost = 0f)
    {
        if (!_sessionActive) TryStartSessionIfNone();

        float t = Time.realtimeSinceStartup - _startTimeRealtime;
        MechanicRecordUse(MechanicNames.Parry, staminaCost, t);
        MechanicRecordOutcome(MechanicNames.Parry, success);

        foreach (var enc in _activeEncounters.Values) enc.parriesUsed += 1;
    }

    #endregion

    #region Public State Machine API

    public void PlayerStateEntered(string stateName)
    {
        if (!_sessionActive) TryStartSessionIfNone();
        if (string.IsNullOrEmpty(stateName)) return;

        int newNameId = StringIdPool.GetId(stateName);

        if (_currentStateNameId != 0 && _statesByNameId.TryGetValue(_currentStateNameId, out var fromAgg))
            fromAgg.nextStateCounts.Upsert(newNameId, 1);

        FlushCurrentStateIfAny();

        if (!_statesByNameId.TryGetValue(newNameId, out var agg))
        {
            agg = new StateAgg { nameId = newNameId };
            _statesByNameId[newNameId] = agg;
        }

        agg.enterCount += 1;

        _currentStateNameId = newNameId;
        _currentStateEnterRealtime = Time.realtimeSinceStartup;
    }

    public void InvalidTransitionAttempted(string currentState, string attemptedState)
    {
        if (!_sessionActive) TryStartSessionIfNone();
        if (string.IsNullOrEmpty(currentState)) return;

        int nameId = StringIdPool.GetId(currentState);
        if (!_statesByNameId.TryGetValue(nameId, out var agg))
        {
            agg = new StateAgg { nameId = nameId };
            _statesByNameId[nameId] = agg;
        }

        agg.invalidTransitionAttempts += 1;
    }

    #endregion

    #region Public Extraction API

    public void ExtractionRunEnded(bool extracted, float totalLootValue, float lootValueExtracted,
        string deathZone, string killerEnemyType, Vector3 deathPosition)
    {
        if (!_sessionActive) TryStartSessionIfNone();

        _extractionRun ??= new ExtractionRunData();
        _extractionRun.extracted          = extracted;
        _extractionRun.totalLootValue     = totalLootValue;
        _extractionRun.lootValueExtracted = lootValueExtracted;
        _extractionRun.deathPositionX     = deathPosition.x;
        _extractionRun.deathPositionY     = deathPosition.y;
        _extractionRun.deathPositionZ     = deathPosition.z;

        if (!extracted)
        {
            _extractionRun.deathEnemyType = !string.IsNullOrEmpty(killerEnemyType)
                ? killerEnemyType
                : (_lastKillerEnemyType ?? "");
            _extractionRun.deathZone = !string.IsNullOrEmpty(deathZone)
                ? deathZone
                : (MapPOITrigger.CurrentPOI.Length > 0 ? MapPOITrigger.CurrentPOI : "");
        }
        else
        {
            _extractionRun.deathEnemyType = "";
            _extractionRun.deathZone      = "";
        }
    }

    #endregion

    #region Public Scene API

    public void SceneEntered(string sceneName)
    {
        if (!_sessionActive) return;
        float now     = Time.realtimeSinceStartup;
        float nowSess = now - _startTimeRealtime;

        if (_currentSceneSegment != null)
        {
            _completedSegments.Add(new SceneSegmentSerializable
            {
                sceneName        = _currentSceneSegment.sceneName,
                entryTimeSeconds = _currentSceneSegment.entryTimeSeconds,
                durationSeconds  = Mathf.Max(0f, now - _currentSceneSegment.entryRealtime),
                visitIndex       = _currentSceneSegment.visitIndex
            });
        }

        _sceneVisitCounts.TryGetValue(sceneName, out int visits);
        _sceneVisitCounts[sceneName] = ++visits;

        _currentSceneSegment = new SceneSegment
        {
            sceneName        = sceneName,
            entryTimeSeconds = nowSess,
            entryRealtime    = now,
            visitIndex       = visits
        };
    }

    #endregion

    #region Public Loot API

    public void LootSpawned(string itemId, string itemType, float itemValue, int quantity, Vector3 position, string itemRarity = "")
    {
        if (!_sessionActive) return;
        RecordLootEvent(itemId, itemType, itemValue, quantity, "spawned", position, itemRarity);
    }

    public void LootPickedUp(string itemId, string itemType, float itemValue, int quantity, Vector3 position, string itemRarity = "")
    {
        if (!_sessionActive) return;
        RecordLootEvent(itemId, itemType, itemValue, quantity, "picked_up", position, itemRarity);
    }

    public void LootIgnored(string itemId, string itemType, float itemValue, int quantity, Vector3 position, string itemRarity = "")
    {
        if (!_sessionActive) return;
        RecordLootEvent(itemId, itemType, itemValue, quantity, "ignored", position, itemRarity);
    }

    private void RecordLootEvent(string itemId, string itemType, float itemValue, int quantity, string action, Vector3 pos, string itemRarity = "")
    {
        _lootEvents.Add(new LootEventSerializable
        {
            itemId      = itemId,
            itemType    = itemType,
            itemRarity  = itemRarity ?? "",
            itemValue   = itemValue,
            quantity    = quantity,
            action      = action,
            posX        = pos.x,
            posY        = pos.y,
            posZ        = pos.z,
            timeSeconds = Time.realtimeSinceStartup - _startTimeRealtime
        });
    }

    #endregion

    #region Helpers

    private bool IsInCombatScene()
        => _currentSceneSegment?.sceneName == COMBAT_SCENE_NAME;

    private EnemyAgg GetOrCreateEnemyAgg(EnemyBase enemyBase)
    {
        int typeId = StringIdPool.GetId(enemyBase.Definition.displayName);
        if (!_enemyByTypeId.TryGetValue(typeId, out var agg))
        {
            agg = new EnemyAgg { enemyTypeId = typeId };
            _enemyByTypeId[typeId] = agg;
        }

        return agg;
    }

    private void FinalizeEncounterRecord(EncounterRecord record, bool playerDied, Vector3 deathPosition = default)
    {
        float duration = Mathf.Max(0f, Time.realtimeSinceStartup - record.startRealtime);

        _player.combatTimeSeconds += duration;

        float px = record.positionX, py = record.positionY, pz = record.positionZ;
        if (playerDied && deathPosition != default)
        {
            px = deathPosition.x;
            py = deathPosition.y;
            pz = deathPosition.z;
        }

        _completedEncounters.Add(new EncounterSerializable
        {
            encounterId      = record.instanceId.ToString(),
            enemyType        = StringIdPool.GetString(record.enemyTypeId),
            attemptNumber    = record.attemptNumber,
            completed        = playerDied ? 0 : 1,
            playerDied       = playerDied ? 1 : 0,
            durationSeconds  = duration,
            hitsReceived     = record.hitsReceived,
            damageTaken      = record.damageTaken,
            dodgesUsed       = record.dodgesUsed,
            parriesUsed      = record.parriesUsed,
            heavyAttacksUsed = record.heavyAttacksUsed,
            lightAttacksUsed = record.lightAttacksUsed,
            staminaEmptyCount = record.staminaEmptyCount,
            positionX        = px,
            positionY        = py,
            positionZ        = pz,
            scene            = record.scene
        });

        if (_extractionRun != null)
        {
            _extractionRun.encountersAttempted += 1;
            if (!playerDied) _extractionRun.encountersCompleted += 1;
        }
    }

    private void FlushPendingAttackIfAny()
    {
        if (_lastAttack == null) return;
        _lastAttack = null;
        _lastAttackMechanicName = null;
    }

    private void FlushStaminaZeroIfAny()
    {
        if (!_staminaAtZero) return;
        _player.staminaTimeAtZeroSeconds += Mathf.Max(0f, Time.realtimeSinceStartup - _staminaZeroStartRealtime);
        _staminaAtZero = false;
        _staminaZeroStartRealtime = 0f;
    }

    private void FlushLockOnIfAny()
    {
        if (!_lockOnActive) return;
        _player.lockOnTimeSeconds += Mathf.Max(0f, Time.realtimeSinceStartup - _lockOnStartRealtime);
        _lockOnActive = false;
        _lockOnStartRealtime = 0f;
    }

    private void FlushCurrentStateIfAny()
    {
        if (_currentStateNameId == 0) return;
        float duration = Mathf.Max(0f, Time.realtimeSinceStartup - _currentStateEnterRealtime);
        if (_statesByNameId.TryGetValue(_currentStateNameId, out var agg))
            agg.totalTimeSeconds += duration;
        _currentStateNameId = 0;
        _currentStateEnterRealtime = 0f;
    }

    private void FlushCurrentSceneSegment()
    {
        if (_currentSceneSegment == null) return;
        _completedSegments.Add(new SceneSegmentSerializable
        {
            sceneName        = _currentSceneSegment.sceneName,
            entryTimeSeconds = _currentSceneSegment.entryTimeSeconds,
            durationSeconds  = Mathf.Max(0f, Time.realtimeSinceStartup - _currentSceneSegment.entryRealtime),
            visitIndex       = _currentSceneSegment.visitIndex
        });
        _currentSceneSegment = null;
    }

    private void FlushActiveEncounters(bool playerDied)
    {
        foreach (var record in _activeEncounters.Values)
            FinalizeEncounterRecord(record, playerDied);
        _activeEncounters.Clear();
    }

    private void MechanicRecordUse(string mechanicName, float staminaCost, float sessionTimeS)
    {
        bool isAttackMechanic = mechanicName == MechanicNames.LightAttack
                             || mechanicName == MechanicNames.HeavyAttack
                             || mechanicName == MechanicNames.WeaponSkill;
        if (!isAttackMechanic && _firstMechanicUsed == null)
            _firstMechanicUsed = mechanicName;

        int nameId = StringIdPool.GetId(mechanicName);
        if (!_mechanicsByNameId.TryGetValue(nameId, out var agg))
        {
            agg = new MechanicAgg { nameId = nameId, firstUseTimeS = sessionTimeS };
            _mechanicsByNameId[nameId] = agg;
        }

        agg.useCount += 1;
        agg.staminaCost += Mathf.Max(0f, staminaCost);
    }

    private void MechanicRecordOutcome(string mechanicName, bool success)
    {
        int nameId = StringIdPool.GetId(mechanicName);
        if (!_mechanicsByNameId.TryGetValue(nameId, out var agg)) return;
        if (success) agg.successCount += 1;
        else agg.failCount += 1;
    }

    private static int CardinalToId(Vector2 cardinal)
    {
        const float T = 0.5f;
        if (Mathf.Abs(cardinal.x) > T)
            return cardinal.x > 0 ? StringIdPool.GetId("Right") : StringIdPool.GetId("Left");
        if (Mathf.Abs(cardinal.y) > T)
            return cardinal.y > 0 ? StringIdPool.GetId("Forward") : StringIdPool.GetId("Back");
        return StringIdPool.GetId("Neutral");
    }

    #endregion

    #region Serialization Types

    [Serializable]
    public class CombatAnalyticsSessions
    {
        public string sessionLabel;
        public string sessionId;
        public int    runNumber;
        public string buildVersion;
        public string sceneName;
        public string startUtcIso;
        public string endUtcIso;
        public float  durationSeconds;
        public string endReason;

        public PlayerBlockSerializable       player;
        public List<EnemyAggSerializable>    enemies;
        public List<AIAggSerializable>       ai;
        public List<EncounterSerializable>   encounters;
        public List<MechanicSerializable>    mechanics;
        public List<StateSerializable>       stateMachine;
        public ExtractionRunSerializable     extractionRun;

        public List<WeaponStatsSerializable>   weapons;
        public List<SceneSegmentSerializable>  sceneSegments;
        public List<LootEventSerializable>     lootEvents;
        public int                             runsThisSession;
        public float                           timeToFirstEncounterSeconds;
        public float                           timeToFirstKillSeconds;
        public ProgressionSerializable         progression;

        public CombatAnalyticsSessions FillFromAggs(
            Dictionary<int, EnemyAgg> enemyDict,
            IEnumerable<AIAgg> aiAggs,
            IEnumerable<MechanicAgg> mechanicAggs,
            IEnumerable<StateAgg> stateAggs)
        {
            foreach (var a in enemyDict.Values) enemies.Add(a.ToSerializable());
            foreach (var a in aiAggs)           ai.Add(a.ToSerializable());
            foreach (var a in mechanicAggs)     mechanics.Add(a.ToSerializable());
            foreach (var a in stateAggs)        stateMachine.Add(a.ToSerializable());
            return this;
        }
    }

    [Serializable]
    public class PlayerBlockSerializable
    {
        public int   attacksStarted;
        public int   attacksResolved;
        public int   hits;
        public int   misses;
        public float damageDone;
        public float damageTaken;
        public float damageTakenWhileDodging;
        public int   hitsTakenWhileDodging;
        public int   dodges;
        public int   lockedDodges;
        public float staminaSpentAttacks;
        public float staminaSpentDodges;
        public int   healsUsed;
        public float healedRatioTotal;
        public int   staminaEmptyCount;
        public float staminaTimeAtZeroSeconds;
        public int   lockOnToggles;
        public float lockOnTimeSeconds;
        public float deathLastDamage;
        public string deathLastHitType;
        public int   deathLastWasInDodgeFrames;
        public List<NamedIntWeapon> attackNameCounts;
        public List<NamedInt>       dodgeDirCounts;
        public List<NamedInt>       hitTypeTakenCounts;
        public float combatTimeSeconds;
        public float explorationTimeSeconds;
        public float comboLengthAvg;
        public int   longestCombo;
    }

    [Serializable]
    public class EnemyAggSerializable
    {
        public string enemyType;
        public int    spawned;
        public int    killed;
        public float  damageAttemptedToPlayer;
        public float  damageTakenFromPlayer;
        public List<NamedInt> hitTypeDealtCounts;
        public List<NamedInt> hitTypeTakenCounts;
    }

    [Serializable]
    public class AIAggSerializable
    {
        public string enemyType;
        public string actionName;
        public string category;
        public int    selectedCount;
        public int    switchedCount;
        public float  scoreSum;
    }

    [Serializable]
    public class EncounterSerializable
    {
        public string encounterId;
        public string enemyType;
        public int    attemptNumber;
        public int    completed;
        public int    playerDied;
        public float  durationSeconds;
        public int    hitsReceived;
        public float  damageTaken;
        public int    dodgesUsed;
        public int    parriesUsed;
        public int    heavyAttacksUsed;
        public int    lightAttacksUsed;
        public int    staminaEmptyCount;
        public float  positionX;
        public float  positionY;
        public float  positionZ;
        public string scene;
    }

    [Serializable]
    public class MechanicSerializable
    {
        public string mechanicName;
        public int    useCount;
        public int    successCount;
        public int    failCount;
        public float  firstUseTimeS;
        public float  staminaCost;
    }

    [Serializable]
    public class StateSerializable
    {
        public string stateName;
        public int    enterCount;
        public float  totalTimeSeconds;
        public float  avgTimeSeconds;
        public int    invalidTransitionAttempts;
        public string mostCommonNextState;
    }

    [Serializable]
    public class ExtractionRunSerializable
    {
        public string runId;
        public int    extracted;
        public float  durationSeconds;
        public float  totalLootValue;
        public float  lootValueExtracted;
        public string deathZone;
        public string deathEnemyType;
        public float  deathPositionX;
        public float  deathPositionY;
        public float  deathPositionZ;
        public int    encountersCompleted;
        public int    encountersAttempted;
        public float  totalDamageTaken;
        public float  totalDamageDone;
        public int    healsUsed;
    }

    [Serializable]
    public class WeaponStatsSerializable
    {
        public string weaponId;
        public string weaponType;
        public float  timeEquippedSeconds;
        public int    attacksStarted;
        public int    hits;
        public int    misses;
        public float  damageDone;
        public int    equippedAtDeath;
        public int    swapCount;
    }

    [Serializable]
    public class SceneSegmentSerializable
    {
        public string sceneName;
        public float  entryTimeSeconds;
        public float  durationSeconds;
        public int    visitIndex;
    }

    [Serializable]
    public class LootEventSerializable
    {
        public string itemId;
        public string itemType;
        public string itemRarity;
        public float  itemValue;
        public int    quantity;
        public string action;
        public float  posX, posY, posZ;
        public float  timeSeconds;
    }

    [Serializable]
    public class ProgressionSerializable
    {
        public string firstMechanicUsed;
        public int    newMechanicDiscovered;
    }

    [Serializable]
    public class NamedInt
    {
        public string name;
        public int    value;
    }

    [Serializable]
    public class NamedIntWeapon
    {
        public string name;
        public int    value;
        public string weaponId;
    }

    #endregion

    #region Inner Aggregation Types

    public class NamedIntMap
    {
        private readonly Dictionary<int, int> _dict = new(32);

        public void Upsert(int nameId, int add)
        {
            _dict[nameId] = _dict.TryGetValue(nameId, out var v) ? v + add : add;
        }

        public List<NamedInt> ToList()
        {
            var list = new List<NamedInt>(_dict.Count);
            foreach (var kv in _dict)
                list.Add(new NamedInt { name = StringIdPool.GetString(kv.Key), value = kv.Value });
            return list;
        }

        public List<NamedIntWeapon> ToListWithWeapons(Dictionary<int, string> weaponMap)
        {
            var list = new List<NamedIntWeapon>(_dict.Count);
            foreach (var kv in _dict)
            {
                weaponMap.TryGetValue(kv.Key, out var wid);
                list.Add(new NamedIntWeapon
                {
                    name     = StringIdPool.GetString(kv.Key),
                    value    = kv.Value,
                    weaponId = wid ?? ""
                });
            }
            return list;
        }
    }

    public class NamedStringMap
    {
        private readonly Dictionary<int, string> _dict = new(32);
        public void Set(int nameId, string value) { _dict[nameId] = value; }
        public Dictionary<int, string> Raw => _dict;
    }

    private class PlayerBlock
    {
        public int   attacksStarted;
        public int   attacksResolved;
        public int   hits;
        public int   misses;
        public float damageDone;
        public float damageTaken;
        public float damageTakenWhileDodging;
        public int   hitsTakenWhileDodging;
        public int   dodges;
        public int   lockedDodges;
        public float staminaSpentAttacks;
        public float staminaSpentDodges;
        public int   healsUsed;
        public float healedRatioTotal;
        public int   staminaEmptyCount;
        public float staminaTimeAtZeroSeconds;
        public int   lockOnToggles;
        public float lockOnTimeSeconds;
        public float deathLastDamage;
        public int   deathLastHitTypeId;
        public int   deathLastWasInDodgeFrames;
        public float combatTimeSeconds;
        public float explorationTimeSeconds;
        public float comboLengthAvg;
        public int   longestCombo;
        public NamedIntMap    attackNameCounts   = new();
        public NamedStringMap attackWeaponMap    = new();
        public NamedIntMap    dodgeDirCounts     = new();
        public NamedIntMap    hitTypeTakenCounts = new();

        public PlayerBlockSerializable ToSerializable() => new PlayerBlockSerializable
        {
            attacksStarted         = attacksStarted,
            attacksResolved        = attacksResolved,
            hits                   = hits,
            misses                 = misses,
            damageDone             = damageDone,
            damageTaken            = damageTaken,
            damageTakenWhileDodging = damageTakenWhileDodging,
            hitsTakenWhileDodging  = hitsTakenWhileDodging,
            dodges                 = dodges,
            lockedDodges           = lockedDodges,
            staminaSpentAttacks    = staminaSpentAttacks,
            staminaSpentDodges     = staminaSpentDodges,
            healsUsed              = healsUsed,
            healedRatioTotal       = healedRatioTotal,
            staminaEmptyCount      = staminaEmptyCount,
            staminaTimeAtZeroSeconds = staminaTimeAtZeroSeconds,
            lockOnToggles          = lockOnToggles,
            lockOnTimeSeconds      = lockOnTimeSeconds,
            deathLastDamage        = deathLastDamage,
            deathLastHitType       = StringIdPool.GetString(deathLastHitTypeId),
            deathLastWasInDodgeFrames = deathLastWasInDodgeFrames,
            attackNameCounts       = attackNameCounts.ToListWithWeapons(attackWeaponMap.Raw),
            dodgeDirCounts         = dodgeDirCounts.ToList(),
            hitTypeTakenCounts     = hitTypeTakenCounts.ToList(),
            combatTimeSeconds      = combatTimeSeconds,
            explorationTimeSeconds = explorationTimeSeconds,
            comboLengthAvg         = comboLengthAvg,
            longestCombo           = longestCombo
        };
    }

    public class EnemyAgg
    {
        public int   enemyTypeId;
        public int   spawned;
        public int   killed;
        public float damageAttemptedToPlayer;
        public float damageTakenFromPlayer;
        public NamedIntMap hitTypeDealtCounts = new();
        public NamedIntMap hitTypeTakenCounts = new();

        public EnemyAggSerializable ToSerializable() => new EnemyAggSerializable
        {
            enemyType               = StringIdPool.GetString(enemyTypeId),
            spawned                 = spawned,
            killed                  = killed,
            damageAttemptedToPlayer = damageAttemptedToPlayer,
            damageTakenFromPlayer   = damageTakenFromPlayer,
            hitTypeDealtCounts      = hitTypeDealtCounts.ToList(),
            hitTypeTakenCounts      = hitTypeTakenCounts.ToList()
        };
    }

    public class AIAgg
    {
        public int   enemyTypeId;
        public int   actionNameId;
        public int   categoryId;
        public int   selectedCount;
        public int   switchedCount;
        public float scoreSum;

        public AIAggSerializable ToSerializable() => new AIAggSerializable
        {
            enemyType     = StringIdPool.GetString(enemyTypeId),
            actionName    = StringIdPool.GetString(actionNameId),
            category      = StringIdPool.GetString(categoryId),
            selectedCount = selectedCount,
            switchedCount = switchedCount,
            scoreSum      = scoreSum
        };
    }

    private class EncounterRecord
    {
        public int    instanceId;
        public int    enemyTypeId;
        public int    attemptNumber;
        public float  startRealtime;
        public int    hitsReceived;
        public float  damageTaken;
        public int    dodgesUsed;
        public int    parriesUsed;
        public int    heavyAttacksUsed;
        public int    lightAttacksUsed;
        public int    staminaEmptyCount;
        public float  positionX;
        public float  positionY;
        public float  positionZ;
        public string scene;
    }

    public class MechanicAgg
    {
        public int   nameId;
        public int   useCount;
        public int   successCount;
        public int   failCount;
        public float firstUseTimeS;
        public float staminaCost;

        public MechanicSerializable ToSerializable() => new MechanicSerializable
        {
            mechanicName  = StringIdPool.GetString(nameId),
            useCount      = useCount,
            successCount  = successCount,
            failCount     = failCount,
            firstUseTimeS = firstUseTimeS,
            staminaCost   = staminaCost
        };
    }

    public class StateAgg
    {
        public int   nameId;
        public int   enterCount;
        public float totalTimeSeconds;
        public int   invalidTransitionAttempts;
        public NamedIntMap nextStateCounts = new();

        public StateSerializable ToSerializable()
        {
            string mostCommon = "";
            int maxCount = 0;
            foreach (var n in nextStateCounts.ToList())
            {
                if (n.value > maxCount)
                {
                    maxCount = n.value;
                    mostCommon = n.name;
                }
            }

            return new StateSerializable
            {
                stateName                = StringIdPool.GetString(nameId),
                enterCount               = enterCount,
                totalTimeSeconds         = totalTimeSeconds,
                avgTimeSeconds           = enterCount > 0 ? totalTimeSeconds / enterCount : 0f,
                invalidTransitionAttempts = invalidTransitionAttempts,
                mostCommonNextState      = mostCommon
            };
        }
    }

    private class WeaponAgg
    {
        public string weaponId;
        public string weaponType;
        public float  equipStartRealtime = -1f;
        public float  timeEquippedSeconds;
        public int    attacksStarted;
        public int    hits;
        public int    misses;
        public float  damageDone;
        public int    swapCount;

        public void OnEquip(float now)
        {
            equipStartRealtime = now;
            swapCount += 1;
        }

        public void OnUnequip(float now)
        {
            if (equipStartRealtime >= 0f)
            {
                timeEquippedSeconds += Mathf.Max(0f, now - equipStartRealtime);
                equipStartRealtime = -1f;
            }
        }

        public WeaponStatsSerializable ToSerializable(bool equippedAtDeath) => new WeaponStatsSerializable
        {
            weaponId            = weaponId,
            weaponType          = weaponType,
            timeEquippedSeconds = timeEquippedSeconds,
            attacksStarted      = attacksStarted,
            hits                = hits,
            misses              = misses,
            damageDone          = damageDone,
            equippedAtDeath     = equippedAtDeath ? 1 : 0,
            swapCount           = Mathf.Max(0, swapCount - 1)
        };
    }

    private class SceneSegment
    {
        public string sceneName;
        public float  entryTimeSeconds;
        public float  entryRealtime;
        public int    visitIndex;
    }

    private class ExtractionRunData
    {
        public bool   extracted;
        public float  totalLootValue;
        public float  lootValueExtracted;
        public string deathZone;
        public string deathEnemyType;
        public float  deathPositionX;
        public float  deathPositionY;
        public float  deathPositionZ;
        public int    encountersCompleted;
        public int    encountersAttempted;

        public ExtractionRunSerializable ToSerializable(string runId, float duration, float totalDamageTaken, int healsUsed, float totalDamageDone)
            => new ExtractionRunSerializable
            {
                runId               = runId,
                extracted           = extracted ? 1 : 0,
                durationSeconds     = duration,
                totalLootValue      = totalLootValue,
                lootValueExtracted  = lootValueExtracted,
                deathZone           = deathZone,
                deathEnemyType      = deathEnemyType,
                deathPositionX      = deathPositionX,
                deathPositionY      = deathPositionY,
                deathPositionZ      = deathPositionZ,
                encountersCompleted = encountersCompleted,
                encountersAttempted = encountersAttempted,
                totalDamageTaken    = totalDamageTaken,
                totalDamageDone     = totalDamageDone,
                healsUsed           = healsUsed
            };
    }

    #endregion

    #region String Pools

    static class StringIdPool
    {
        private static readonly Dictionary<string, int> _toId   = new(256);
        private static readonly List<string>            _fromId = new(256) { "" };

        public static int GetId(string s)
        {
            s ??= "";
            if (_toId.TryGetValue(s, out var id)) return id;
            id = _fromId.Count;
            _fromId.Add(s);
            _toId[s] = id;
            return id;
        }

        public static string GetString(int id)
            => (uint)id < (uint)_fromId.Count ? _fromId[id] : "";

        public static void Clear()
        {
            _toId.Clear();
            _fromId.Clear();
            _fromId.Add("");
        }
    }

    static class EnumStringCache
    {
        private static readonly Dictionary<Enums.HitType, int>        _hitType        = Build<Enums.HitType>();
        private static readonly Dictionary<Enums.ActionCategory, int>  _actionCategory = Build<Enums.ActionCategory>();

        public static int HitTypeId(Enums.HitType v)
            => _hitType.TryGetValue(v, out var id) ? id : 0;

        public static int ActionCategoryId(Enums.ActionCategory v)
            => _actionCategory.TryGetValue(v, out var id) ? id : 0;

        private static Dictionary<TEnum, int> Build<TEnum>() where TEnum : struct, Enum
        {
            var map    = new Dictionary<TEnum, int>(64);
            var values = (TEnum[])Enum.GetValues(typeof(TEnum));
            foreach (var v in values)
                map[v] = StringIdPool.GetId(v.ToString());
            return map;
        }
    }

    #endregion
}
