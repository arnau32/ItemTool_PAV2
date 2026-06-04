using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [SerializeField] private int _index;

    public int Index => _index;
}