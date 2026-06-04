public class PlayerContext : CharacterContext
{
    public PlayerInputs Inputs { get; }
    public StaminaSystem Stamina { get; }
    public PlayerAnimation Animation { get; }
    public ComboController ComboController { get; }
    public PlayerMovement Movement { get; }
    public EquipmentHandler EquipmentHandler { get; }
    public PlayerLockOnSystem LockOnSystem { get; }
    public PlayerLifeController PlayerLifeController { get; }
    public PlayerCombatController CombatController { get; }
    public PlayerHUD Hud { get; }
    public PlayerCombat Combat { get; }
    public AbilityScoreSystem AbilityScoreSystem { get; }
    public PlayerFeedbacksController FeedbacksController { get; }
    public PlayerAudio Audio { get; }
    public FactionComponent FactionComponent { get; }
    public PlayerPlayableController PlayableController { get; }

    internal PlayerContext(PlayerContextBuilder b) : base(b.Owner, b.Stats, b.Health)
    {
        Inputs = b.Inputs;
        Stamina = b.Stamina;
        Animation = b.Animation;
        ComboController = b.ComboController;
        Movement = b.Movement;
        EquipmentHandler = b.EquipmentHandler;
        LockOnSystem = b.LockOnSystem;
        PlayerLifeController = b.PlayerLifeController;
        Hud = b.Hud;
        Combat = b.Combat;
        CombatController = b.CombatController;
        AbilityScoreSystem = b.AbilityScoreSystem;
        FeedbacksController = b.FeedbacksController;
        Audio = b.Audio;
        FactionComponent = b.FactionComponent;
        PlayableController = b.PlayableController;
    }
}