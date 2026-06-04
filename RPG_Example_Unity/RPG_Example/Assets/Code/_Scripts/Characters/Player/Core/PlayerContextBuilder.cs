using UnityEngine;

public class PlayerContextBuilder : CharacterContextBuilder<PlayerContextBuilder>
{
    public PlayerInputs Inputs { get; private set; }
    public StaminaSystem Stamina { get; private set; }
    public PlayerAnimation Animation { get; private set; }
    public ComboController ComboController { get; private set; }
    public PlayerMovement Movement { get; private set; }
    public EquipmentHandler EquipmentHandler { get; private set; }
    public PlayerLockOnSystem LockOnSystem { get; private set; }
    public PlayerLifeController PlayerLifeController { get; private set; }
    public PlayerCombatController CombatController { get; private set; }
    public PlayerHUD Hud { get; private set; }
    public PlayerCombat Combat { get; private set; }
    public AbilityScoreSystem AbilityScoreSystem { get; private set; }
    public PlayerFeedbacksController FeedbacksController { get; private set; }
    public PlayerAudio Audio { get; private set; }
    public PlayerPlayableController PlayableController { get; private set; }

    public PlayerContextBuilder(GameObject owner, CharacterStats stats, CharacterHealthSystem health) : base(owner, stats, health) { }

    public PlayerContextBuilder WithInputs(PlayerInputs inputs)
    {
        Inputs = inputs;
        return this;
    }

    public PlayerContextBuilder WithStamina(StaminaSystem stamina)
    {
        Stamina = stamina;
        return this;
    }

    public PlayerContextBuilder WithAnimation(PlayerAnimation animation)
    {
        Animation = animation;
        return this;
    }

    public PlayerContextBuilder WithComboController(ComboController comboController)
    {
        ComboController = comboController;
        return this;
    }

    public PlayerContextBuilder WithMovement(PlayerMovement movement)
    {
        Movement = movement;
        return this;
    }

    public PlayerContextBuilder WithEquipmentHandler(EquipmentHandler equipHandler)
    {
        EquipmentHandler = equipHandler;
        return this;
    }

    public PlayerContextBuilder WithLockOn(PlayerLockOnSystem lockOnSystem)
    {
        LockOnSystem = lockOnSystem;
        return this;
    }

    public PlayerContextBuilder WithLifeController(PlayerLifeController lifeController)
    {
        PlayerLifeController = lifeController;
        return this;
    }

    public PlayerContextBuilder WithCombatController(PlayerCombatController combatController)
    {
        CombatController = combatController;
        return this;
    }

    public PlayerContextBuilder WithHUD(PlayerHUD hud)
    {
        Hud = hud;
        return this;
    }

    public PlayerContextBuilder WithCombat(PlayerCombat combat)
    {
        Combat = combat;
        return this;
    }

    public PlayerContextBuilder WithAbilityScore(AbilityScoreSystem abilityScore)
    {
        AbilityScoreSystem = abilityScore;
        return this;
    }

    public PlayerContextBuilder WithFeedbacks(PlayerFeedbacksController feedbacks)
    {
        FeedbacksController = feedbacks;
        return this;
    }

    public PlayerContextBuilder WithAudio(PlayerAudio audio)
    {
        Audio = audio;
        return this;
    }

    public PlayerContextBuilder WithPlayableController(PlayerPlayableController playable)
    {
        PlayableController = playable;
        return this;
    }

    public PlayerContext Build() => new PlayerContext(this);
}