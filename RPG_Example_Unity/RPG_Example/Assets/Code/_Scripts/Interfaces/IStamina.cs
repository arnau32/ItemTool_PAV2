using System;

public interface IStamina
{
    float MaxStamina { get; }
    float CurrentStamina { get; }

    bool TryConsumeStamina(float cost);
    void StartDrain(bool onlyInCombat = false, Func<bool> combatCheck = null);
    void StopDrain();
    bool HasStaminaToAction(float cost);

    event Action OnStaminaModifies;
}
