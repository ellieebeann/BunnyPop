using TMPro;
using UnityEngine;

public sealed class GoalMarker : MonoBehaviour
{
    public RectTransform marker;
    public TMP_Text label;
    public Camera worldCamera;
    public RectTransform canvas;
    private Transform target;
    private string targetName;
    private float nextSearch;
    void LateUpdate()
    {
        var game = GameManager.instance;
        if (marker == null || canvas == null || game == null) return;
        bool visible = !game.HasEnded && !GameManager.IsPaused && !game.IsDying;
        marker.gameObject.SetActive(visible);
        if (!visible) return;
        if (Time.unscaledTime >= nextSearch || target == null || !target.gameObject.activeInHierarchy)
        {
            nextSearch = Time.unscaledTime + 0.5f;
            FindGoal(game);
        }
        if (target == null || worldCamera == null) { marker.gameObject.SetActive(false); return; }
        Vector3 point = worldCamera.WorldToScreenPoint(target.position + Vector3.up * 8f);
        bool behind = point.z < 0;
        Vector2 screen = point;
        if (behind) screen = new Vector2(Screen.width, Screen.height) * 0.5f - (screen - new Vector2(Screen.width, Screen.height) * 0.5f);
        float marginX = Screen.width * 0.09f, marginY = Screen.height * 0.18f;
        screen.x = Mathf.Clamp(screen.x, marginX, Screen.width - marginX);
        screen.y = Mathf.Clamp(screen.y, marginY, Screen.height - marginY);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out var local);
        marker.anchoredPosition = local;
        var player = worldCamera.GetComponent<CameraController>()?.player;
        int distance = Mathf.RoundToInt(Vector3.Distance(player != null ? player.position : worldCamera.transform.position, target.position));
        if (label != null) label.text = targetName + "  " + distance + "m" + (behind ? "  (behind)" : "");
    }
    void FindGoal(GameManager game)
    {
        target = null;
        if (game.CanRestore && game.altar != null) { target = game.altar.transform; targetName = "CAMP ALTAR"; return; }
        if (game.wandChest != null && !game.wandChest.IsOpened) { target = game.wandChest.transform; targetName = "WAND CHEST"; return; }
        float nearest = float.PositiveInfinity;
        Vector3 origin = worldCamera.GetComponent<CameraController>()?.player?.position ?? worldCamera.transform.position;
        if (game.seeds < game.allSeeds)
        {
            foreach (var seed in Object.FindObjectsByType<Seed>())
            {
                if (seed.carrotCharges > 0 || !seed.CanCollect) continue;
                float distance = (seed.transform.position - origin).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance; target = seed.transform; targetName = "MAGIC SEED";
            }
        }
        else if (game.requireAllEnemies && game.EnemiesRemaining > 0)
        {
            foreach (var enemy in Object.FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include))
            {
                if (enemy.IsDead) continue;
                Transform destination = enemy.transform;
                if (!enemy.gameObject.activeInHierarchy)
                {
                    foreach (var gate in Object.FindObjectsByType<ForestGate>())
                        if (gate.sentry != null && gate.sentry.gameObject == enemy.gameObject) destination = gate.transform;
                }
                float distance = (destination.position - origin).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance; target = destination; targetName = "ENEMY";
            }
        }
        else if (game.altar != null) { target = game.altar.transform; targetName = "CAMP ALTAR"; }
    }
}
