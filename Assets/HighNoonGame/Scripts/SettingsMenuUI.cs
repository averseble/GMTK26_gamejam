using UnityEngine;
using UnityEngine.UI;

public class SettingsMenuUI : MonoBehaviour
{
    [SerializeField] GameObject mainPanel;
    [SerializeField] GameObject settingsPanel;
    [SerializeField] Slider masterSlider;
    [SerializeField] Slider sfxSlider;
    [SerializeField] Slider musicSlider;

    void OnEnable()
    {
        SyncSlidersFromAudio();
    }

    public void Open()
    {
        if (mainPanel != null)
            mainPanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        SyncSlidersFromAudio();
        PlayClick();
    }

    public void Close()
    {
        OnExitSettings();
    }

    public void OnExitSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (mainPanel != null)
            mainPanel.SetActive(true);

        var audio = GetAudio();
        if (audio != null)
            audio.Save();

        PlayClick();
    }

    public void OnMasterChanged(float value)
    {
        var audio = GetAudio();
        if (audio != null)
            audio.MasterVolume = value;
    }

    public void OnSfxChanged(float value)
    {
        var audio = GetAudio();
        if (audio != null)
            audio.SfxVolume = value;
    }

    public void OnMusicChanged(float value)
    {
        var audio = GetAudio();
        if (audio != null)
            audio.MusicVolume = value;
    }

    void SyncSlidersFromAudio()
    {
        var audio = GetAudio();
        if (audio == null)
            return;

        if (masterSlider != null)
        {
            masterSlider.SetValueWithoutNotify(audio.MasterVolume);
            masterSlider.onValueChanged.RemoveListener(OnMasterChanged);
            masterSlider.onValueChanged.AddListener(OnMasterChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(audio.SfxVolume);
            sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        }

        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(audio.MusicVolume);
            musicSlider.onValueChanged.RemoveListener(OnMusicChanged);
            musicSlider.onValueChanged.AddListener(OnMusicChanged);
        }
    }

    static AudioManager GetAudio()
    {
        return GameRoot.Instance != null ? GameRoot.Instance.Audio : null;
    }

    static void PlayClick()
    {
        var audio = GetAudio();
        if (audio != null)
            audio.PlayUiClick();
    }
}
