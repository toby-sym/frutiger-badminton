using UnityEngine;
using UnityEngine.Rendering;

namespace FrutigerBadminton
{
    /// <summary>Creates a short, texture-free ring and shard burst when a racket meets the shuttlecock.</summary>
    public sealed class AeroHitEffect : MonoBehaviour
    {
        const float Duration = .42f;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock propertyBlock;
        readonly Transform[] shards = new Transform[8];
        readonly Vector3[] shardDirections = new Vector3[8];
        readonly Quaternion[] shardRestRotations = new Quaternion[8];

        Transform ring;
        Transform flash;
        Renderer ringRenderer;
        Renderer flashRenderer;
        Renderer[] shardRenderers;
        Mesh ringMesh;
        Material ringMaterial;
        Material flashMaterial;
        Material shardMaterial;
        Color tint;
        float strength;
        float elapsed;

        void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        public static void Spawn(Vector3 position, Color color, float intensity)
        {
            var effectObject = new GameObject("Procedural racket impact");
            var effect = effectObject.AddComponent<AeroHitEffect>();
            effect.Initialize(position, color, intensity);
        }

        void Initialize(Vector3 position, Color color, float intensity)
        {
            transform.position = position;
            tint = color;
            strength = Mathf.Clamp(intensity, .6f, 1.8f);

            Camera camera = Camera.main;
            Vector3 facing = camera ? camera.transform.position - position : Vector3.forward;
            if (facing.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);

            ringMesh = BuildRingMesh();
            ringMaterial = CreateGlowMaterial(color, 2.8f);
            flashMaterial = CreateGlowMaterial(Color.Lerp(color, Color.white, .65f), 4.2f);
            shardMaterial = CreateGlowMaterial(Color.Lerp(color, Color.white, .34f), 2.1f);

            var ringObject = new GameObject("Aqua shock ring");
            ring = ringObject.transform;
            ring.SetParent(transform, false);
            ringObject.AddComponent<MeshFilter>().sharedMesh = ringMesh;
            ringRenderer = ringObject.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = ringMaterial;
            ringRenderer.shadowCastingMode = ShadowCastingMode.Off;
            ringRenderer.receiveShadows = false;

            flash = CreateOrb("White hot center", Vector3.zero, Vector3.one * .12f, out flashRenderer);
            flashRenderer.sharedMaterial = flashMaterial;
            shardRenderers = new Renderer[shards.Length];
            for (int i = 0; i < shards.Length; i++)
            {
                float angle = i / (float)shards.Length * Mathf.PI * 2f;
                shardDirections[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), .12f * (i % 2 == 0 ? 1f : -1f)).normalized;
                shards[i] = CreateOrb("Glass spark " + i, Vector3.zero, new Vector3(.045f, .11f, .045f), out shardRenderers[i]);
                shards[i].localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                shardRestRotations[i] = shards[i].localRotation;
                shardRenderers[i].sharedMaterial = shardMaterial;
            }
        }

        Transform CreateOrb(string objectName, Vector3 localPosition, Vector3 scale, out Renderer renderer)
        {
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = objectName;
            Destroy(orb.GetComponent<Collider>());
            orb.transform.SetParent(transform, false);
            orb.transform.localPosition = localPosition;
            orb.transform.localScale = scale;
            renderer = orb.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return orb.transform;
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / Duration);
            float fade = 1f - progress;
            float expansion = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, progress * 1.45f));
            float retract = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.68f, 1f, progress)) * .82f;

            ring.localScale = Vector3.one * Mathf.Lerp(.12f, .95f * strength, expansion) * retract;
            ring.localRotation = Quaternion.Euler(0f, 0f, progress * 36f);
            SetGlow(ringRenderer, tint, fade, 2.8f);

            float flashIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / .055f));
            float flashOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.08f, .31f, elapsed));
            flash.localScale = Vector3.one * Mathf.Lerp(.08f, .4f * strength, flashIn) * flashOut;
            SetGlow(flashRenderer, Color.Lerp(tint, Color.white, .65f), flashOut, 4.2f);

            for (int i = 0; i < shards.Length; i++)
            {
                float distance = Mathf.SmoothStep(0f, .72f * strength, progress);
                shards[i].localPosition = shardDirections[i] * distance;
                shards[i].localRotation = shardRestRotations[i] * Quaternion.Euler(progress * (120f + i * 11f), progress * 90f, 0f);
                shards[i].localScale = new Vector3(.045f, .11f, .045f) * (1f - progress);
                SetGlow(shardRenderers[i], Color.Lerp(tint, Color.white, .34f), fade, 2.1f);
            }

            if (elapsed >= Duration) Destroy(gameObject);
        }

        void SetGlow(Renderer renderer, Color color, float fade, float emission)
        {
            if (!renderer) return;
            propertyBlock.Clear();
            propertyBlock.SetColor(BaseColorId, color * fade);
            propertyBlock.SetColor(ColorId, color * fade);
            propertyBlock.SetColor(EmissionId, color * (emission * fade));
            renderer.SetPropertyBlock(propertyBlock);
        }

        static Material CreateGlowMaterial(Color color, float emission)
        {
            Shader shader = AeroRuntimeShaderLibrary.Resolve("Universal Render Pipeline/Lit");
            var material = new Material(shader) { name = "Procedural impact glow" };
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color); else material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .78f);
            if (material.HasProperty(EmissionId))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor(EmissionId, color * emission);
            }
            return material;
        }

        static Mesh BuildRingMesh()
        {
            const int segments = 40;
            const int sides = 6;
            var vertices = new Vector3[(segments + 1) * sides];
            var triangles = new int[segments * sides * 6];
            int tri = 0;
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments * Mathf.PI * 2f;
                for (int j = 0; j < sides; j++)
                {
                    float v = j / (float)sides * Mathf.PI * 2f;
                    float radius = .5f + Mathf.Cos(v) * .035f;
                    vertices[i * sides + j] = new Vector3(Mathf.Cos(u) * radius, Mathf.Sin(u) * radius, Mathf.Sin(v) * .035f);
                    if (i >= segments) continue;
                    int a = i * sides + j;
                    int b = i * sides + (j + 1) % sides;
                    int c = (i + 1) * sides + (j + 1) % sides;
                    int d = (i + 1) * sides + j;
                    triangles[tri++] = a; triangles[tri++] = c; triangles[tri++] = b;
                    triangles[tri++] = a; triangles[tri++] = d; triangles[tri++] = c;
                }
            }
            var mesh = new Mesh { name = "Generated shock ring" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            return mesh;
        }

        void OnDestroy()
        {
            if (ringMesh) Destroy(ringMesh);
            if (ringMaterial) Destroy(ringMaterial);
            if (flashMaterial) Destroy(flashMaterial);
            if (shardMaterial) Destroy(shardMaterial);
        }
    }
}
