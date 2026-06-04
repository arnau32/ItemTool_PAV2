using System;

public interface IHealth
{
    float CurrentHealth01 { get; }
    float MaxHealth { get; }
    bool IsAlive { get; }
    void Heal(float amount);
    
    event Action<float> OnHealthChanged;
    event Action OnDeath;
}
