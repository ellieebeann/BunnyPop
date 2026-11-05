using UnityEngine;

public sealed class GameAudio : MonoBehaviour
{
    public AudioSource exploration, combat, ui;
    public AudioClip exploringMusic, combatMusic, buttonClick;
    [Range(0, 1)] public float musicVolume = 0.25f;
    private static GameAudio current;
    private EnemyAI[] enemies;
    private float nextSearch;
    public static float MasterVolume => PlayerPrefs.GetFloat("BunnyPop.Volume", 0.8f);
    public static bool IsMuted => PlayerPrefs.GetInt("BunnyPop.Muted", 0) != 0;
    void Awake()
    {
        current = this;
        ApplyVolume();
        if (exploration != null) exploration.volume = musicVolume;
        if (combat != null) combat.volume = 0;
        Setup(exploration, exploringMusic);
        Setup(combat, combatMusic);
        if (combat != null) combat.volume = 0;
    }
    static void Setup(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null) return;
        source.clip = clip; source.loop = true; source.spatialBlend = 0; source.Play();
    }
    void Update()
    {
        bool danger = false;
        if (Time.unscaledTime >= nextSearch)
        {
            nextSearch = Time.unscaledTime + 1f;
            enemies = Object.FindObjectsByType<EnemyAI>();
        }
        if (GameManager.instance != null && !GameManager.instance.HasEnded)
            foreach (var enemy in enemies)
                if (enemy != null && (enemy.CurrentState == EnemyAI.BehaviourState.Chase || enemy.CurrentState == EnemyAI.BehaviourState.Attack)) { danger = true; break; }
        float mix = danger ? musicVolume : 0;
        if (combat != null) combat.volume = Mathf.MoveTowards(combat.volume, mix, Time.unscaledDeltaTime * 0.15f);
        if (exploration != null) exploration.volume = Mathf.MoveTowards(exploration.volume, musicVolume - mix, Time.unscaledDeltaTime * 0.15f);
    }
    public void SetVolume(float volume)
    {
        PlayerPrefs.SetFloat("BunnyPop.Volume", Mathf.Clamp01(volume));
        ApplyVolume();
    }
    public void ToggleMute()
    {
        PlayerPrefs.SetInt("BunnyPop.Muted", IsMuted ? 0 : 1);
        ApplyVolume();
    }
    public void Click() { if (ui != null && buttonClick != null) ui.PlayOneShot(buttonClick, 0.6f); }
    static void ApplyVolume() => AudioListener.volume = IsMuted ? 0 : MasterVolume;
    public static void PlayUI(AudioClip clip)
    {
        if (clip == null) return;
        if (current != null && current.ui != null) current.ui.PlayOneShot(clip);
        else PlayWorld(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
    }
    public static void PlayWorld(AudioClip clip, Vector3 point, float volume = 1f)
    {
        if (clip == null) return;
        var source = new GameObject("One shot - " + clip.name).AddComponent<AudioSource>();
        source.transform.position = point;
        source.clip = clip; source.volume = volume; source.spatialBlend = 1;
        source.rolloffMode = AudioRolloffMode.Logarithmic; source.minDistance = 12; source.maxDistance = 160;
        source.Play();
        Destroy(source.gameObject, clip.length + 0.1f);
    }
    void OnDestroy() { if (current == this) current = null; }
}
