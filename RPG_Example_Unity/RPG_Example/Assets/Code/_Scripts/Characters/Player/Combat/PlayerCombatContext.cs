public class PlayerCombatContext : ICombatContext
{
    private readonly PlayerContext _context;
    private readonly IWeaponUser _weapons;

    public PlayerCombatContext(PlayerContext ctx)
    {
        _context = ctx;
        _weapons = new PlayerWeaponUser(ctx.Combat);
    }

    public CharacterAnimation Animation => _context.Animation;
    public IStamina Stamina => _context.Stamina;
    public IWeaponUser Weapons => _weapons;
}
