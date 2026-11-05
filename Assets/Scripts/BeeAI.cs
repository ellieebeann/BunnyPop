public class BeeAI : EnemyAI
{
    public float moveSpeed = 24f;
    protected override float PatrolSpeed => moveSpeed;
    protected override bool Flying => true;
}
