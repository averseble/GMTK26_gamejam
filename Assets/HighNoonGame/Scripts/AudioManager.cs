using UnityEngine;

public class AudioManager : MonoBehaviour
{
    const string PrefMaster = "audio.master";
    const string PrefSfx = "audio.sfx";
    const string PrefMusic = "audio.music";

    [Header("Clips")]
    [SerializeField] AudioClip shotClip;
    [SerializeField] [Range(0f, 2f)] float shotVolume = 1f;
    [SerializeField] AudioClip characterHitClip;
    [SerializeField] [Range(0f, 2f)] float characterHitVolume = 1f;
    [SerializeField] AudioClip tileImpactClip;
    [SerializeField] [Range(0f, 2f)] float tileImpactVolume = 1f;
    [SerializeField] AudioClip uiClickClip;
    [SerializeField] [Range(0f, 2f)] float uiClickVolume = 1f;
    [SerializeField] AudioClip musicClip;
    [SerializeField] [Range(0f, 2f)] float musicClipVolume = 1f;

    [Header("Sources")]
    [SerializeField] int sfxPoolSize = 8;

    AudioSource _musicSource;
    AudioSource[] _sfxPool;
    int _sfxIndex;

    float _master = 1f;
    float _sfx = 1f;
    float _music = 1f;

    public float MasterVolume
    {
        get => _master;
        set
        {
            _master = Mathf.Clamp01(value);
            ApplyVolumes();
            PlayerPrefs.SetFloat(PrefMaster, _master);
        }
    }

    public float SfxVolume
    {
        get => _sfx;
        set
        {
            _sfx = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(PrefSfx, _sfx);
        }
    }

    public float MusicVolume
    {
        get => _music;
        set
        {
            _music = Mathf.Clamp01(value);
            ApplyVolumes();
            PlayerPrefs.SetFloat(PrefMusic, _music);
        }
    }

    void Awake()
    {
        _master = PlayerPrefs.GetFloat(PrefMaster, 1f);
        _sfx = PlayerPrefs.GetFloat(PrefSfx, 1f);
        _music = PlayerPrefs.GetFloat(PrefMusic, 1f);

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.loop = true;
        _musicSource.playOnAwake = false;

        _sfxPool = new AudioSource[Mathf.Max(1, sfxPoolSize)];
        for (int i = 0; i < _sfxPool.Length; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            _sfxPool[i] = src;
        }

        ApplyVolumes();

        if (musicClip != null)
        {
            _musicSource.clip = musicClip;
            _musicSource.Play();
        }
    }

    void ApplyVolumes()
    {
        AudioListener.volume = _master;
        if (_musicSource != null)
            _musicSource.volume = _music * musicClipVolume;
    }

    public void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || _sfxPool == null || _sfxPool.Length == 0)
            return;

        var src = _sfxPool[_sfxIndex];
        _sfxIndex = (_sfxIndex + 1) % _sfxPool.Length;
        src.PlayOneShot(clip, Mathf.Clamp01(_sfx * volumeScale));
    }

    public void PlayShot() => PlaySfx(shotClip, shotVolume);
    public void PlayCharacterHit() => PlaySfx(characterHitClip, characterHitVolume);
    public void PlayTileImpact() => PlaySfx(tileImpactClip, tileImpactVolume);
    public void PlayUiClick() => PlaySfx(uiClickClip, uiClickVolume);

    public void Save()
    {
        PlayerPrefs.Save();
    }
}
