using System;

public struct TemperamentWeights
{
    public float attack;
    public float pressure;
    public float disengage;
    public float defend;
    public float evade;
    public float heal;
    public float flank;
    public float special;
    public float bait;
}
public class CombatTemperamentDB
{
    public static TemperamentWeights Get(Enums.CombatTemperament t)
    {
        switch (t)
        {
            case Enums.CombatTemperament.SuperDefensive:
                return new TemperamentWeights { attack=.6f, pressure=.8f, disengage=1.4f, defend=1.6f, evade=1.3f, heal=1.8f, flank=.9f, special=.8f, bait=1.2f };
            case Enums.CombatTemperament.Defensive:
                return new TemperamentWeights { attack=.9f, pressure=1.0f, disengage=1.15f, defend=1.25f, evade=1.1f, heal=1.2f, flank=1.0f, special=.95f, bait=1.1f };
            case Enums.CombatTemperament.Normal:
                return new TemperamentWeights { attack=1.0f, pressure=1.0f, disengage=1.0f, defend=1.0f, evade=1.0f, heal=1.0f, flank=1.0f, special=1.0f, bait=1.0f };
            case Enums.CombatTemperament.Aggressive:
                return new TemperamentWeights { attack=1.3f, pressure=1.25f, disengage=.85f, defend=.9f, evade=.95f, heal=.7f, flank=1.2f, special=1.2f, bait=.9f };
            case Enums.CombatTemperament.SuperAgressive:
                return new TemperamentWeights { attack=1.7f, pressure=1.5f, disengage=.6f, defend=.7f, evade=.9f, heal=.4f, flank=1.3f, special=1.4f, bait=.8f };
            default:
                throw new ArgumentOutOfRangeException(nameof(t), t, null);
        }
    }
}
