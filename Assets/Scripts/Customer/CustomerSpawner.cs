using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

#if UNITY_EDITOR
using UnityEditor;
#endif

using Random = UnityEngine.Random;

public class CustomerSpawner : MonoBehaviour
{
    public Transform spawnPoint;
    public Transform exitPoint;
    public List<Table> tables = new List<Table>();
    public List<Customer> customerPrefabs = new List<Customer>();
    public float spawnInterval = 15f;
    public float spawnVariance = 5f;

    private float spawnTimer;
    private List<Customer> activeCustomers = new List<Customer>();

    private void Awake()
    {
        if (spawnPoint == null)
        {
            var point = new GameObject("SpawnPoint");
            point.transform.SetParent(transform);
            point.transform.position = new Vector3(-9f, 0f, 8f);
            spawnPoint = point.transform;
        }

        if (exitPoint == null)
        {
            var exit = new GameObject("ExitPoint");
            exit.transform.SetParent(transform);
            exit.transform.position = new Vector3(12f, 0f, 8f);
            exitPoint = exit.transform;
        }

        EnsureNavMeshSurface();
    }

    private void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.isOpen) return;

        if (tables.Count == 0)
        {
            tables.AddRange(FindObjectsByType<Table>(FindObjectsSortMode.None));
        }

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0f)
        {
            SpawnCustomer();
            spawnTimer = spawnInterval + Random.Range(-spawnVariance, spawnVariance);
        }
    }

    private void SpawnCustomer()
    {
        Table freeTable = tables.Find(t => t != null && !t.isOccupied);
        if (freeTable == null) return;

        Customer customer = CreateRuntimeCustomer();
        if (customer == null)
        {
            // Creation failed (for example no baked NavMesh) - leave the table free
            // rather than marking it occupied with no customer to release it.
            return;
        }

        freeTable.isOccupied = true;
        activeCustomers.Add(customer);
        customer.EnterPub(spawnPoint, freeTable);
        if (TryGetComponent<OrderManager>(out var orderManager))
        {
            orderManager.RegisterCustomer(customer);
        }
    }

    /// <summary>
    /// Releases a table and forgets the customer once they have finished their visit.
    /// </summary>
    public void ReleaseCustomer(Customer customer)
    {
        if (customer == null) return;

        activeCustomers.Remove(customer);
        if (customer.assignedTable != null)
        {
            customer.assignedTable.isOccupied = false;
        }
    }

    private Customer CreateRuntimeCustomer()
    {
                if (customerPrefabs.Count > 0)
                {
                    Customer selected = customerPrefabs[Random.Range(0, customerPrefabs.Count)];
                    if (selected != null)
                    {
                        return Instantiate(selected, spawnPoint.position, Quaternion.identity);
                    }
                }

                if (spawnPoint == null)
                {
                    return null;
                }

                if (!HasValidNavMeshAt(spawnPoint.position))
                {
                    EnsureNavMeshSurface();
                    if (!HasValidNavMeshAt(spawnPoint.position))
                    {
                        Debug.LogWarning("CustomerSpawner: no valid NavMesh is baked for this scene. Customer spawn skipped.");
                        return null;
                    }
                }

                GameObject customerGO = BuildFallbackCustomer();
                if (customerGO == null)
                {
                    return null;
                }

                if (!EnsureNavMeshAgent(customerGO))
                {
                    Destroy(customerGO);
                    return null;
                }

                ApplyWalkingAnimation(customerGO);

                var customer = customerGO.GetComponent<Customer>();
                if (customer == null)
                {
                    customer = customerGO.AddComponent<Customer>();
                }

                customer.customerName = "Guest " + Random.Range(1, 99);
                return customer;
            }

            private GameObject BuildFallbackCustomer()
            {
                GameObject customerGO = LoadWalkingCustomerPrefab();
                if (customerGO != null)
                {
                    customerGO = Instantiate(customerGO, spawnPoint.position, Quaternion.identity);
                    customerGO.name = "Customer_Walking";
                    return customerGO;
                }

                Debug.LogWarning("CustomerSpawner: no runtime customer model was found. Skipping spawn instead of creating a box placeholder.");
                return null;
            }

            private static bool EnsureNavMeshAgent(GameObject customerGO)
            {
                if (customerGO.GetComponent<NavMeshAgent>() != null)
                {
                    return true;
                }

                try
                {
                    customerGO.AddComponent<NavMeshAgent>();
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("CustomerSpawner: failed to create NavMeshAgent: " + e.Message);
                    return false;
                }
            }

            private static void ApplyWalkingAnimation(GameObject customerGO)
            {
                Animation animation = customerGO.GetComponentInChildren<Animation>();
                if (animation == null)
                {
                    animation = customerGO.AddComponent<Animation>();
                }

                AnimationClip playerClip = GetPlayerWalkingClip();
                if (playerClip != null)
                {
                    string clipName = playerClip.name;
                    playerClip.wrapMode = WrapMode.Loop;

                    if (animation.GetClip(clipName) == null)
                    {
                        animation.AddClip(playerClip, clipName);
                    }

                    animation.Play(clipName);
                    return;
                }

                string[] preferredNames = new[]
                {
                    "mixamo.com",
                    "Walking",
                    "Walk",
                    "Take 001",
                    "Run",
                    "Idle"
                };

                foreach (string clipName in preferredNames)
                {
                    AnimationClip clip = animation.GetClip(clipName);
                    if (clip != null)
                    {
                        clip.wrapMode = WrapMode.Loop;
                        animation.Play(clipName);
                        return;
                    }
                }

                AnimationClip walkingClip = TryLoadWalkingClip();
                if (walkingClip == null)
                {
                    walkingClip = CreateRuntimeWalkingClip("Walking");
                }

                if (walkingClip == null)
                {
                    return;
                }

                walkingClip.wrapMode = WrapMode.Loop;
                if (animation.GetClip("Walking") == null)
                {
                    animation.AddClip(walkingClip, "Walking");
                }

                animation.Play("Walking");
            }

            private static AnimationClip GetPlayerWalkingClip()
            {
                GameObject player = GameObject.Find("Player");
                if (player == null)
                {
                    return null;
                }

                Animation playerAnimation = player.GetComponentInChildren<Animation>();
                if (playerAnimation == null)
                {
                    return null;
                }

                foreach (AnimationState state in playerAnimation)
                {
                    if (state == null || state.clip == null)
                        continue;

                    if (state.clip.name.Contains("Walking") || state.clip.name.Contains("Walk") || state.clip.name.Contains("mixamo") || state.clip.name.Contains("Run"))
                    {
                        return state.clip;
                    }
                }

                return playerAnimation.clip != null ? playerAnimation.clip : null;
            }

            private static AnimationClip TryLoadWalkingClip()
            {
                var library = Resources.Load<SimBarArtLibrary>("SimBarArtLibrary");
                if (library != null && library.legacyWalkingClip != null)
                    return library.legacyWalkingClip;

                foreach (string candidate in new[]
                {
                    "Animations/Walking",
                    "Characters/Walking",
                    "Models/Walking",
                    "Characters/Customer",
                    "Models/Customer",
                    "Walking",
                    "Customer"
                })
                {
                    AnimationClip clip = Resources.Load<AnimationClip>(candidate);
                    if (clip != null)
                    {
                        return clip;
                    }
                }

                return null;
            }

            private static AnimationClip CreateRuntimeWalkingClip(string clipName)
            {
                AnimationClip clip = new AnimationClip();
                clip.name = clipName;
                clip.legacy = true;
                clip.frameRate = 30f;

                AnimationCurve bounceCurve = new AnimationCurve();
                AnimationCurve swingCurve = new AnimationCurve();

                for (int i = 0; i <= 24; i++)
                {
                    float t = i / 24f;
                    float phase = t * Mathf.PI * 2f;
                    float offsetY = Mathf.Sin(phase) * 0.08f;
                    float swingRot = Mathf.Sin(phase) * 10f;

                    bounceCurve.AddKey(t, offsetY);
                    swingCurve.AddKey(t, swingRot);
                }

                clip.SetCurve(string.Empty, typeof(Transform), "localPosition.y", bounceCurve);
                clip.SetCurve(string.Empty, typeof(Transform), "localEulerAngles.x", swingCurve);
                return clip;
            }

    private static GameObject LoadWalkingCustomerPrefab()
    {
        var library = Resources.Load<SimBarArtLibrary>("SimBarArtLibrary");
        if (library != null)
        {
            if (library.legacyWalkingModel != null) return library.legacyWalkingModel;
            var guest = library.Find("guest", 0);
            if (guest != null && guest.sourceModel != null) return guest.sourceModel;
        }

        string[] candidates =
        {
            "Characters/Walking",
            "Characters/Customer",
            "Characters/Casual",
            "Characters/Casual2",
            "Characters/Worker",
            "Models/Walking",
            "Models/Customer",
            "Models/Casual",
            "Models/Casual2",
            "Models/Worker",
            "Walking",
            "Customer",
            "Casual",
            "Casual2",
            "Worker"
        };

        foreach (var candidate in candidates)
        {
            var prefab = Resources.Load<GameObject>(candidate);
            if (prefab != null)
                return prefab;
        }

        foreach (var root in new[] { "Characters", "Models" })
        {
            var loadedObjects = Resources.LoadAll(root);
            foreach (var obj in loadedObjects)
            {
                if (obj is GameObject go)
                {
                    var name = go.name.ToLowerInvariant();
                    if (name.Contains("walking") || name.Contains("customer") || name.Contains("casual") || name.Contains("worker") || name.Contains("guest"))
                    {
                        return go;
                    }
                }
            }
        }

        return null;
    }

    private static bool HasValidNavMeshAt(Vector3 worldPosition)
    {
        var triangles = NavMesh.CalculateTriangulation();
        if (triangles.indices == null || triangles.indices.Length == 0)
        {
            return false;
        }

        return NavMesh.SamplePosition(worldPosition, out _, 2f, NavMesh.AllAreas);
    }

    private static void EnsureNavMeshSurface()
    {
        if (HasValidNavMeshAt(Vector3.zero))
        {
            return;
        }

        GameObject navRoot = GameObject.Find("RuntimeNavMeshRoot");
        if (navRoot == null)
        {
            navRoot = new GameObject("RuntimeNavMeshRoot");
        }

        GameObject floor = navRoot.transform.Find("NavMeshFloor")?.gameObject;
        if (floor == null)
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "NavMeshFloor";
            floor.transform.SetParent(navRoot.transform);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);
            floor.transform.localScale = new Vector3(24f, 1f, 18f);

            var box = floor.GetComponent<BoxCollider>();
            if (box == null)
            {
                floor.AddComponent<BoxCollider>();
            }

            var renderer = floor.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        var surface = floor.GetComponent<NavMeshSurface>();
        if (surface == null)
        {
            surface = floor.AddComponent<NavMeshSurface>();
        }

        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.defaultArea = 0;
        surface.overrideTileSize = false;
        surface.overrideVoxelSize = false;
        surface.BuildNavMesh();
    }

    public void OpenBusiness()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.isOpen = true;
        }
        spawnTimer = 2f;
    }

    public void CloseBusiness()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.isOpen = false;
        }
        foreach (var customer in activeCustomers)
        {
            if (customer != null && customer.currentState != Customer.CustomerState.Exited)
            {
                customer.Leave(false);
            }
        }
    }
}
