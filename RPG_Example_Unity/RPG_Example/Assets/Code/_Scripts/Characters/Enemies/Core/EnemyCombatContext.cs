using System;

public class EnemyCombatContext : ICombatContext
{
    private readonly EnemyContext _ctx;
    private readonly IWeaponUser _weapons;

    public EnemyCombatContext(EnemyContext ctx, EnemyCombat enemyCombat)
    {
        _ctx = ctx;
        _weapons = new EnemyWeaponUser(enemyCombat);
    }

    public CharacterAnimation Animation => _ctx.Animation;
    public Action<string, float> PlayTargetAnimation => _ctx.Animation.PlayTargetAnimation;
    public IStamina Stamina => null;
    public IWeaponUser Weapons => _weapons;
}
