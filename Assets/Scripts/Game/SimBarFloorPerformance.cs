using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Small pooled paper effects for the live floor. The caller owns mission state,
/// elapsed time and pause handling; this component has no automatic Update.
/// All positions passed to this helper are local to the venue root.
/// </summary>
public sealed class SimBarFloorPerformance : MonoBehaviour
{
    public const int ParticleLimit = 80;
    public const int LitterLimit = 40;
    public const float PaperFloorHeight = 0.086f;
    private const int PaletteCount = 5;

    private struct Paper
    {
        public bool active;
        public int kind, palette;
        public Vector3 position, velocity, angles, spin;
        public float age, phase;
    }

    private sealed class Batch
    {
        public Mesh mesh;
        public readonly List<Vector3> vertices = new List<Vector3>(768);
        public readonly List<Vector3> normals = new List<Vector3>(768);
        public readonly List<int> triangles = new List<int>(1536);
    }

    private readonly Paper[] particles = new Paper[ParticleLimit];
    private readonly Batch[] batches = new Batch[PaletteCount];
    private readonly Material[] materials = new Material[PaletteCount];
    private readonly int[] lastBurstSerial = { int.MinValue, int.MinValue };
    private readonly Vector3[] cleaners = new Vector3[2];
    private static readonly Vector3[] CleaningWaypoints =
    {
        new Vector3(2.2f, 0f, -3.9f), new Vector3(2.2f, 0f, .75f),
        new Vector3(8.45f, 0f, .75f), new Vector3(8.35f, 0f, -3.8f),
        new Vector3(4.2f, 0f, -4.8f), new Vector3(7.9f, 0f, -4.8f)
    };
    private static readonly Color[] Colors =
    {
        new Color(.15f,.90f,.94f), new Color(1f,.18f,.62f),
        new Color(1f,.74f,.14f), new Color(.66f,.46f,.98f),
        new Color(.97f,.96f,.90f)
    };

    private int nextParticle, visibleLitter, tissueShare, cleanerCount;
    private bool initialized, cleanupActive, dirty;
    private float cleanupProgress, effectClock;

    public int ActiveParticleCount { get; private set; }
    public int VisibleLitterCount { get { return visibleLitter; } }

    /// <summary>Safe to call more than once. Creates only five shared mesh batches.</summary>
    public static SimBarFloorPerformance Attach(Transform venueRoot)
    {
        if (venueRoot == null) return null;
        Transform existing = venueRoot.Find("Performance Paper Effects");
        if (existing != null)
        {
            var found = existing.GetComponent<SimBarFloorPerformance>();
            if (found != null) return found;
        }
        var root = new GameObject("Performance Paper Effects");
        root.transform.SetParent(venueRoot, false);
        var result = root.AddComponent<SimBarFloorPerformance>();
        result.Initialize();
        return result;
    }

    private void Initialize()
    {
        if (initialized) return;
        initialized = true;
        // This existing resource keeps an unlit shader referenced in stripped WebGL builds.
        Material template = Resources.Load<Material>("SimBarArt/Graffiti/GraffitiUnlit");
        Shader shader = template != null ? template.shader : Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) { enabled = false; return; }
        for (int i = 0; i < PaletteCount; i++)
        {
            Material material = template != null ? new Material(template) : new Material(shader);
            material.name = "Mission paper " + i;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Colors[i]);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Colors[i]);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", Texture2D.whiteTexture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", Texture2D.whiteTexture);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            // The floor's spray-paint overlays use 3000; paper must remain visible above them.
            material.renderQueue = 3004;
            materials[i] = material;
            var node = new GameObject(i == 4 ? "Tissue ribbons and cleanup tools" : "Confetti color " + i,
                typeof(MeshFilter), typeof(MeshRenderer));
            node.transform.SetParent(transform, false);
            Mesh mesh = new Mesh { name = "Pooled mission paper " + i };
            mesh.MarkDynamic();
            mesh.bounds = new Bounds(new Vector3(5.5f, 2f, -.7f), new Vector3(18f, 16f, 18f));
            batches[i] = new Batch { mesh = mesh };
            node.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        dirty = true;
    }

    /// <summary>kind 0 = confetti, kind 1 = tissue. A repeated serial never repeats its burst.</summary>
    public void Burst(int kind, int serial)
    {
        if (kind < 0 || kind > 1 || serial <= lastBurstSerial[kind]) return;
        Initialize();
        lastBurstSerial[kind] = serial;
        int count = kind == 0 ? 56 : 24;
        for (int i = 0; i < count; i++)
        {
            int index = nextParticle++ % ParticleLimit;
            float a = Noise(serial * 179 + i * 13 + kind * 41);
            float b = Noise(serial * 53 + i * 97 + 5);
            float c = Noise(serial * 31 + i * 17 + 71);
            particles[index] = new Paper
            {
                active = true, kind = kind, palette = kind == 1 ? 4 : i % 4,
                position = new Vector3(i % 2 == 0 ? 2.7f : 8.1f, 2.0f + c * .45f, 2.55f),
                velocity = new Vector3((a - .5f) * 3.2f, 2.5f + b * 2.4f, -1.2f - c * 1.6f),
                angles = new Vector3(a * 360f, b * 360f, c * 360f),
                spin = new Vector3(80f + a * 160f, 60f + b * 90f, (c - .5f) * 300f),
                phase = a * 6.283185f
            };
        }
        dirty = true;
        Rebuild();
    }

    /// <summary>normalized = remaining litter ratio; counts are saved confetti/tissue performance counts.</summary>
    public void SetLitter(float normalized, int confettiCount, int tissueCount)
    {
        normalized = Finite(normalized) ? Mathf.Clamp01(normalized) : 0f;
        confettiCount = Mathf.Clamp(confettiCount, 0, 1000);
        tissueCount = Mathf.Clamp(tissueCount, 0, 1000);
        int total = confettiCount + tissueCount;
        int count = Mathf.CeilToInt(Mathf.Min(LitterLimit, total * 10) * normalized);
        int whiteShare = total > 0 ? Mathf.RoundToInt(1000f * tissueCount / total) : 0;
        if (count == visibleLitter && whiteShare == tissueShare) return;
        visibleLitter = count; tissueShare = whiteShare; dirty = true;
        Rebuild();
    }

    public void SetCleanup(bool active, float progress)
    {
        progress = Finite(progress) ? Mathf.Clamp01(progress) : 0f;
        if (cleanupActive == active && Mathf.Approximately(cleanupProgress, progress)) return;
        cleanupActive = active; cleanupProgress = progress; dirty = true;
        if (!active) cleanerCount = 0;
        Rebuild();
    }

    /// <summary>Give the current positions of assigned staff to place visible brooms in their hands.</summary>
    public void SetCleanerPositions(Vector3 first, Vector3 second, int count)
    {
        cleanerCount = cleanupActive ? Mathf.Clamp(count, 0, 2) : 0;
        cleaners[0] = first; cleaners[1] = second; dirty = true;
    }

    /// <summary>Public-aisle route points suitable for the existing navigation grid.</summary>
    public Vector3 GetCleanupTarget(int workerIndex)
    {
        int step = Mathf.Min(5, Mathf.FloorToInt(cleanupProgress * 6f));
        return CleaningWaypoints[(step + Mathf.Clamp(workerIndex, 0, 1) * 3) % CleaningWaypoints.Length];
    }

    /// <summary>Call only while the relevant simulation is running; no delta means no motion.</summary>
    public void TickEffects(float delta)
    {
        if (!Finite(delta) || delta <= 0f) { if (dirty) Rebuild(); return; }
        delta = Mathf.Min(delta, .2f); effectClock += delta;
        bool hadParticles = ActiveParticleCount > 0;
        ActiveParticleCount = 0;
        for (int i = 0; i < ParticleLimit; i++)
        {
            Paper paper = particles[i];
            if (!paper.active) continue;
            paper.age += delta;
            paper.velocity.y = Mathf.Max(paper.kind == 0 ? -1.45f : -2f,
                paper.velocity.y - delta * (paper.kind == 0 ? 1.8f : 2.7f));
            paper.velocity.x *= Mathf.Exp(-delta * .38f);
            paper.velocity.z *= Mathf.Exp(-delta * .24f);
            paper.position += paper.velocity * delta + Vector3.right * (Mathf.Sin(paper.age * 7f + paper.phase) * delta * .22f);
            paper.angles += paper.spin * delta;
            if (paper.position.y <= PaperFloorHeight || paper.age > 7.5f) paper.active = false;
            else ActiveParticleCount++;
            particles[i] = paper;
        }
        if (hadParticles || ActiveParticleCount > 0 || cleanupActive || dirty) Rebuild();
    }

    /// <summary>Call when a new night is created, not when resuming a saved one.</summary>
    public void ClearEffects()
    {
        for (int i = 0; i < ParticleLimit; i++) particles[i].active = false;
        nextParticle = visibleLitter = tissueShare = cleanerCount = ActiveParticleCount = 0;
        lastBurstSerial[0] = lastBurstSerial[1] = int.MinValue;
        effectClock = cleanupProgress = 0f; cleanupActive = false; dirty = true;
        Rebuild();
    }

    private void Rebuild()
    {
        if (!initialized) Initialize();
        if (batches[0] == null) return;
        for (int i = 0; i < PaletteCount; i++)
        { batches[i].vertices.Clear(); batches[i].normals.Clear(); batches[i].triangles.Clear(); }
        ActiveParticleCount = 0;
        for (int i = 0; i < ParticleLimit; i++)
        {
            Paper paper = particles[i]; if (!paper.active) continue;
            ActiveParticleCount++;
            Quaternion rotation = Quaternion.Euler(paper.angles);
            Vector3 u = rotation * Vector3.right, v = rotation * Vector3.up;
            if (paper.kind == 0) Quad(paper.palette, paper.position, u * .065f, v * .10f);
            else Ribbon(paper.position, u, v, .11f, .60f, paper.age * 4f + paper.phase);
        }
        for (int i = 0; i < visibleLitter; i++)
        {
            float x = 1.7f + Noise(i * 73 + 97) * 6.9f;
            float z = -4.7f + Noise(i * 199 + 41) * 6.0f;
            float angle = Noise(i * 31 + 6) * Mathf.PI * 2f;
            Vector3 u = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 v = new Vector3(-u.z, 0, u.x);
            Vector3 point = new Vector3(x, PaperFloorHeight + i * .00012f, z);
            if (tissueShare >= 1000 || Noise(i * 37 + 93) < tissueShare / 1000f) Ribbon(point, u, v, .13f, .48f, i * 2.3f);
            else Quad(i % 4, point, u * .075f, v * .11f);
        }
        if (cleanupActive)
            for (int i = 0; i < cleanerCount; i++) DrawBroom(cleaners[i], i);
        for (int i = 0; i < PaletteCount; i++)
        {
            Batch batch = batches[i]; batch.mesh.Clear(false);
            batch.mesh.SetVertices(batch.vertices); batch.mesh.SetNormals(batch.normals);
            batch.mesh.SetTriangles(batch.triangles, 0, false);
            batch.mesh.bounds = new Bounds(new Vector3(5.5f, 2f, -.7f), new Vector3(18f, 16f, 18f));
        }
        dirty = false;
    }

    private void Ribbon(Vector3 position, Vector3 u, Vector3 v, float width, float length, float phase)
    {
        // Three connected bends distinguish a tissue streamer from the colored paper squares.
        Vector3 previous = position - v * (length * .5f);
        for (int segment = 0; segment < 3; segment++)
        {
            Vector3 next = position + v * (length * ((segment + 1) / 3f - .5f))
                + u * (Mathf.Sin(phase + segment * 1.4f) * width * .7f);
            Quad(4, (previous + next) * .5f, u * (width * .5f), (next - previous) * .5f);
            previous = next;
        }
    }

    private void DrawBroom(Vector3 person, int index)
    {
        float sweep = Mathf.Sin(effectClock * 7f + index * 1.8f) * .22f;
        Vector3 head = person + new Vector3(.22f + sweep, .12f, -.26f);
        Vector3 hand = person + new Vector3(.13f, 1.0f, -.04f);
        Vector3 stem = hand - head;
        Vector3 cross = Vector3.Cross(stem, Vector3.forward).normalized * .022f;
        Quad(2, (head + hand) * .5f, cross, stem * .5f);
        Quad(4, head, Vector3.right * .22f, Vector3.forward * .10f);
        for (int bristle = 0; bristle < 5; bristle++)
            Quad(4, head + new Vector3((bristle - 2) * .08f, -.035f, -.04f), Vector3.right * .026f, Vector3.up * .065f);
    }

    private void Quad(int palette, Vector3 center, Vector3 u, Vector3 v)
    {
        Batch batch = batches[palette]; int offset = batch.vertices.Count;
        batch.vertices.Add(center - u - v); batch.vertices.Add(center + u - v);
        batch.vertices.Add(center + u + v); batch.vertices.Add(center - u + v);
        Vector3 normal = Vector3.Cross(u, v).normalized;
        for (int i = 0; i < 4; i++) batch.normals.Add(normal);
        batch.triangles.Add(offset); batch.triangles.Add(offset + 1); batch.triangles.Add(offset + 2);
        batch.triangles.Add(offset); batch.triangles.Add(offset + 2); batch.triangles.Add(offset + 3);
        batch.triangles.Add(offset + 2); batch.triangles.Add(offset + 1); batch.triangles.Add(offset);
        batch.triangles.Add(offset + 3); batch.triangles.Add(offset + 2); batch.triangles.Add(offset);
    }

    private static float Noise(int value)
    {
        unchecked
        {
            uint hash = (uint)value * 747796405u + 2891336453u;
            hash = ((hash >> (int)((hash >> 28) + 4)) ^ hash) * 277803737u;
            return ((hash >> 22) ^ hash) / (float)uint.MaxValue;
        }
    }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

    private void OnDestroy()
    {
        for (int i = 0; i < PaletteCount; i++)
        {
            if (batches[i] != null && batches[i].mesh != null) Destroy(batches[i].mesh);
            if (materials[i] != null) Destroy(materials[i]);
        }
    }
}
