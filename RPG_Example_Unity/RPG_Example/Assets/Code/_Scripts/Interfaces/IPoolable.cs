// Interface used for objects that are going to use pools
public interface IPoolable
{
    void OnSpawnFromPool();
    void OnReturnToPool();
    void ReturnToPool();
}
