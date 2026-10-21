using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameLoader : MonoBehaviour
{
    public string mainSceneName = "SampleScene";

    private void Awake()
    {
        if (GameManager.Instance == null)
        {
            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
        }
        if (AudioManager.Instance == null)
        {
            var go = new GameObject("AudioManager");
            go.AddComponent<AudioManager>();
        }
        if (UIManager.Instance == null)
        {
            var go = new GameObject("UIManager");
            go.AddComponent<UIManager>();
        }
    }

    private void Start()
    {
        SceneManager.LoadScene(mainSceneName, LoadSceneMode.Single);
    }
}
