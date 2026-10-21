using UnityEngine;

public class BarRuntimeController : MonoBehaviour
{
    private void Start()
    {
        EnsureRuntimeObjects();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.isOpen = true;
        }
    }

    private void EnsureRuntimeObjects()
    {
        if (FindFirstObjectByType<BarSceneBuilder>() == null)
        {
            var builder = new GameObject("BarSceneBuilder");
            builder.AddComponent<BarSceneBuilder>();
        }

        if (FindFirstObjectByType<CustomerSpawner>() == null)
        {
            var spawner = new GameObject("CustomerSpawner");
            spawner.AddComponent<CustomerSpawner>();
        }

        if (FindFirstObjectByType<BarUIBuilder>() == null)
        {
            var ui = new GameObject("BarUIBuilder");
            ui.AddComponent<BarUIBuilder>();
        }

        if (FindFirstObjectByType<CozyAmbientAudio>() == null)
        {
            var audio = new GameObject("CozyAmbientAudio");
            audio.AddComponent<CozyAmbientAudio>();
        }

        if (FindFirstObjectByType<PlayerController>() == null)
        {
            GameObject player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 1f, 3f);
            player.AddComponent<CharacterController>();
            player.AddComponent<PlayerController>();

            var controller = player.GetComponent<CharacterController>();
            if (controller != null)
            {
                controller.height = 1.8f;
                controller.radius = 0.35f;
                controller.center = new Vector3(0f, 0.9f, 0f);
            }
        }

        if (Camera.main == null)
        {
            var camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            camera.AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 8f, -10f);
            camera.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
        }
    }
}