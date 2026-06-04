using UnityEngine;

public class TriggerPlayer : MonoBehaviour
{
    protected void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        OnPlayerTriggerEnter(other);
    }

    protected void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        OnPlayerTriggerExit(other);
    }

    protected virtual void OnPlayerTriggerEnter(Collider other) { }
    protected virtual void OnPlayerTriggerExit(Collider other) { }
}
