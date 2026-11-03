public class BeeAI : EnemyAI
{
    public float moveSpeed = 3.5f;
    protected override float PatrolSpeed => moveSpeed;
    protected override bool Flying => true;
}
