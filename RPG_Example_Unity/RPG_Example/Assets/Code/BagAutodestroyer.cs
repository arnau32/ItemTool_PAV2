using UnityEngine;

public class BagAutodestroyer : MonoBehaviour
{
    private Storage storage;

    private void Awake()
    {
        storage = GetComponent<Storage>();
    }
    public void Update()
    {
        if (storage.storageType == StorageType.Chest)
            return;


        if(storage.items.Count <= 0)
        {
            Destroy(gameObject);
        }
    }

    
}
