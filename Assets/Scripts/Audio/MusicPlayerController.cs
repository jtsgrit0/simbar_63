using UnityEngine;
using UnityEngine.UI;

public class MusicPlayerController : MonoBehaviour
{
    public Text songTitleText;
    public Slider progressSlider;
    public Slider volumeSlider;
    public Button playPauseButton;
    public Button nextButton;
    public Button prevButton;
    public Text playPauseLabel;

    public AudioClip[] playlist;
    public int currentTrackIndex = 0;
    public bool isPlaying = false;

    private void Start()
    {
        if (playPauseButton != null)
        {
            playPauseButton.onClick.AddListener(TogglePlayPause);
            AddClickSound(playPauseButton);
        }
        if (nextButton != null)
        {
            nextButton.onClick.AddListener(NextTrack);
            AddClickSound(nextButton);
        }
        if (prevButton != null)
        {
            prevButton.onClick.AddListener(PrevTrack);
            AddClickSound(prevButton);
        }
        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value = AudioManager.Instance != null ? AudioManager.Instance.masterVolume : 0.8f;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        UpdateUI();
    }

    private void Update()
    {
        if (AudioManager.Instance == null) return;

        if (progressSlider != null && AudioManager.Instance.GetCurrentBGM() != null)
        {
            float length = AudioManager.Instance.GetBGMLength();
            if (length > 0f)
            {
                progressSlider.value = AudioManager.Instance.GetBGMTime() / length;
            }
        }
    }

    public void TogglePlayPause()
    {
        if (AudioManager.Instance == null) return;

        if (isPlaying)
        {
            AudioManager.Instance.PauseBGM();
            isPlaying = false;
        }
        else
        {
            if (AudioManager.Instance.GetCurrentBGM() == null && playlist.Length > 0)
            {
                PlayTrack(currentTrackIndex);
            }
            else
            {
                AudioManager.Instance.UnpauseBGM();
                isPlaying = true;
            }
        }
        UpdateUI();
    }

    public void NextTrack()
    {
        if (playlist.Length == 0) return;
        currentTrackIndex = (currentTrackIndex + 1) % playlist.Length;
        PlayTrack(currentTrackIndex);
    }

    public void PrevTrack()
    {
        if (playlist.Length == 0) return;
        currentTrackIndex = (currentTrackIndex - 1 + playlist.Length) % playlist.Length;
        PlayTrack(currentTrackIndex);
    }

    private void PlayTrack(int index)
    {
        if (AudioManager.Instance == null || index < 0 || index >= playlist.Length) return;
        AudioManager.Instance.PlayBGM(playlist[index]);
        isPlaying = true;
        UpdateUI();
    }

    public void SetVolume(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(value);
        }
    }

    private void UpdateUI()
    {
        if (songTitleText != null && playlist.Length > 0)
        {
            songTitleText.text = isPlaying ? playlist[currentTrackIndex].name : "Paused";
        }
        if (playPauseLabel != null)
        {
            playPauseLabel.text = isPlaying ? "||" : ">";
        }
    }

    private void AddClickSound(Button button)
    {
        AudioClip clickSound = Resources.Load<AudioClip>("UI/click-a");
        if (clickSound == null)
        {
            clickSound = Resources.Load<AudioClip>("UI/kenney_ui_click-a");
        }
        if (clickSound != null)
        {
            button.onClick.AddListener(() => AudioManager.Instance.PlaySFX(clickSound));
        }
    }
}
