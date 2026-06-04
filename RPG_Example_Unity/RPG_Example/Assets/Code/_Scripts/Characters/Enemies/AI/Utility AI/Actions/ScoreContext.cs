public struct ScoreContext
{
    public EnemyContext enemyContext;
    public float distance;
    public float sqrDistance;
    public float angleDegree;
    public float selfHP;
    public float targetHP;
    public float selfStamine;
    public bool  hasLOS;
    public bool  spaceFree;
    public float targetWindup;
    public float targetRecovery;
    public float advantage;
    public TemperamentWeights style;
    public float timeNow;
    public float attackIntent01;
    public bool  hasReadyAttack;

    public bool attackTokenAvailable;
}