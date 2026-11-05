using UnityEngine;

public sealed class AudioOptions : MonoBehaviour
{
    public GameAudio audioController;
    public UnityEngine.UI.Slider slider;
    public UnityEngine.UI.Image muteImage;
    public Sprite soundOn, soundOff;
    void OnEnable() { Refresh(); }
    public void SetVolume(float value) { audioController?.SetVolume(value); Refresh(); }
    public void ToggleMute() { audioController?.ToggleMute(); Refresh(); }
    void Refresh()
    {
        if (slider != null) slider.SetValueWithoutNotify(GameAudio.MasterVolume);
        if (muteImage != null) muteImage.sprite = GameAudio.IsMuted ? soundOff : soundOn;
    }
}
