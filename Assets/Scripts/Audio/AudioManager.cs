using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float bgmVolume = 0.6f;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    private AudioSource bgmSource;
    private AudioSource sfxSource;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null)
            return;

        if (bgmSource.clip == clip)
        {
            bgmSource.volume = masterVolume * bgmVolume;
            if (!bgmSource.isPlaying)
                bgmSource.Play();
            return;
        }

        bgmSource.clip = clip;
        bgmSource.volume = masterVolume * bgmVolume;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        bgmSource.Stop();
    }

    public void PauseBGM()
    {
        bgmSource.Pause();
    }

    public void UnpauseBGM()
    {
        bgmSource.UnPause();
    }

    public bool IsBGMPlaying()
    {
        return bgmSource.isPlaying;
    }

    public AudioClip GetCurrentBGM()
    {
        return bgmSource.clip;
    }

    public float GetBGMTime()
    {
        return bgmSource.time;
    }

    public float GetBGMLength()
    {
        return bgmSource.clip != null ? bgmSource.clip.length : 0f;
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, masterVolume * sfxVolume);
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        bgmSource.volume = masterVolume * bgmVolume;
    }
}
