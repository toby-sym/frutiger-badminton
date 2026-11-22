using System.Collections.Generic;
using UnityEngine;

namespace FrutigerBadminton
{
    /// <summary>Builds the first playable environment entirely from generated meshes and primitives.</summary>
    public sealed class AeroCourtScene : MonoBehaviour
    {
        [SerializeField] Shader vertexColorShader;
        const float CourtWidth = 6.1f;
        const float CourtLength = 13.4f;
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<CloudDrift> cloudDrifts = new List<CloudDrift>();
        readonly List<LanternOrbit> lanternOrbits = new List<LanternOrbit>();
        readonly List<CloudDrift> bubbleDrifts = new List<CloudDrift>();
        readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
        Material playerShadowMaterial;
        Material playerGlazeMaterial;

        struct CloudDrift
        {
            public Transform root;
            public Vector3 origin;
            public float phase;
            public float speed;

            public CloudDrift(Transform root, Vector3 origin, float phase, float speed)
            {
                this.root = root;
                this.origin = origin;
                this.phase = phase;
                this.speed = speed;
            }
        }

        struct LanternOrbit
        {
            public Transform lantern;
            public float radius;
            public float baseHeight;
            public float angle;
            public float phase;
            public float speed;
            public float size;

            public LanternOrbit(Transform lantern, float radius, float baseHeight, float angle, float phase, float speed, float size)
            {
                this.lantern = lantern;
                this.radius = radius;
                this.baseHeight = baseHeight;
                this.angle = angle;
                this.phase = phase;
                this.speed = speed;
                this.size = size;
            }
        }

        void Awake()
        {
            BuildWorld();
        }

        void Update()
        {
            float time = Time.time;
            for (int i = 0; i < cloudDrifts.Count; i++)
            {
                CloudDrift drift = cloudDrifts[i];
                float wave = time * drift.speed + drift.phase;
                drift.root.position = drift.origin + new Vector3(Mathf.Sin(wave) * .38f, Mathf.Sin(wave * .72f) * .11f, Mathf.Cos(wave * .63f) * .2f);
                drift.root.rotation = Quaternion.Euler(0f, Mathf.Sin(wave * .48f) * 2.2f, 0f);
            }

            for (int i = 0; i < lanternOrbits.Count; i++)
            {
                LanternOrbit orbit = lanternOrbits[i];
                float angle = orbit.angle + time * orbit.speed;
                float bob = (Mathf.Sin(time * 1.35f + orbit.phase) - Mathf.Sin(orbit.phase)) * .13f;
                float twinkle = (Mathf.Sin(time * 2.1f + orbit.phase) - Mathf.Sin(orbit.phase)) * .035f;
                orbit.lantern.position = new Vector3(Mathf.Cos(angle) * orbit.radius, orbit.baseHeight + bob, Mathf.Sin(angle) * orbit.radius);
                orbit.lantern.localScale = Vector3.one * (orbit.size * (1f + twinkle));
            }

            for (int i = 0; i < bubbleDrifts.Count; i++)
            {
                CloudDrift bubble = bubbleDrifts[i];
                float phase = time * bubble.speed + bubble.phase;
                bubble.root.position = bubble.origin + new Vector3(Mathf.Sin(phase) * .28f, Mathf.Sin(phase * .7f) * .35f, 0f);
            }
        }

        void OnDestroy()
        {
            foreach (var material in ownedMaterials)
                if (material) Destroy(material);
            foreach (var mesh in ownedMeshes)
                if (mesh) Destroy(mesh);
            materialCache.Clear();
        }

        void BuildWorld()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.22f, .49f, .72f);
            RenderSettings.ambientEquatorColor = new Color(.52f, .76f, .86f);
            RenderSettings.ambientGroundColor = new Color(.08f, .20f, .31f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.43f, .76f, .9f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .0025f;

            var key = new GameObject("Aqua sunlight").AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, .91f, .76f);
            key.intensity = 1.2f;
            key.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
            key.shadows = LightShadows.Soft;

            var fill = new GameObject("Sky fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(.36f, .83f, 1f);
            fill.intensity = .3f;
            fill.transform.rotation = Quaternion.Euler(18f, 150f, 0f);

            CreateSkyDome();
            CreateHorizonLandscape();
            CreateFloatingIsland();
            CreateIslandFloorSheen();
            CreateCourt();
            CreatePavilion();
            CreateArenaScoreboard();
            CreateNet();
            CreateCloudBanks();
            CreateSpectatorOrbs();
            CreateCamera();
            CreateSolarHalo();
        }

        void CreateSkyDome()
        {
            var mesh = new Mesh { name = "Procedural gradient sky" };
            const int segments = 64;
            const int rings = 32;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            for (int y = 0; y <= rings; y++)
            {
                float t = y / (float)rings;
                float phi = (t * 2f - 1f) * Mathf.PI * .5f;
                for (int x = 0; x <= segments; x++)
                {
                    float theta = x / (float)segments * Mathf.PI * 2f;
                    float height = Mathf.Sin(phi);
                    vertices.Add(new Vector3(Mathf.Cos(theta) * Mathf.Cos(phi), height, Mathf.Sin(theta) * Mathf.Cos(phi)) * 180f);
                    float horizonBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1f, 0f, height));
                    Color color = height >= 0f
                        ? Color.Lerp(new Color(.62f, .91f, .98f), new Color(.035f, .28f, .64f), Mathf.Pow(height, .72f))
                        : Color.Lerp(new Color(.16f, .43f, .68f), new Color(.62f, .91f, .98f), horizonBlend);
                    colors.Add(color);
                }
            }
            for (int y = 0; y < rings; y++) for (int x = 0; x < segments; x++)
            {
                int a = y * (segments + 1) + x, b = a + segments + 1;
                triangles.Add(a); triangles.Add(b); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(b); triangles.Add(b + 1);
            }
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
            ownedMeshes.Add(mesh);
            var dome = new GameObject("Sky gradient dome");
            dome.transform.position = new Vector3(0f, -5f, 0f);
            dome.AddComponent<MeshFilter>().sharedMesh = mesh;
            dome.AddComponent<MeshRenderer>().sharedMaterial = MakeMaterial("Frutiger/Vertex Color", Color.white, 0f, .2f, vertexColorShader);
        }

        void CreateHorizonLandscape()
        {
            Shader shader = Resources.Load<Shader>("HorizonLandscape");
            if (!shader) return;
            var material = new Material(shader) { name = "Procedural horizon colours" };
            ownedMaterials.Add(material);

            for (int layer = 0; layer < 3; layer++)
            {
                const int segments = 128;
                float depth = 68f + layer * 11f;
                float topOffset = -4.2f + layer * 2.1f;
                var vertices = new List<Vector3>((segments + 1) * 3);
                var colors = new List<Color>((segments + 1) * 3);
                var triangles = new List<int>(segments * 12);
                Color ridge = Color.Lerp(new Color(.31f, .70f, .33f, .78f), new Color(.58f, .79f, .52f, .58f), layer / 2f);
                Color mid = Color.Lerp(new Color(.22f, .60f, .48f, .48f), new Color(.50f, .74f, .62f, .32f), layer / 2f);
                Color mist = new Color(.58f, .86f, .86f, 0f);
                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float x = Mathf.Lerp(-175f, 175f, t);
                    float edgeFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(t, 1f - t) * 8f));
                    Color fadedRidge = ridge; fadedRidge.a *= edgeFade;
                    Color fadedMid = mid; fadedMid.a *= edgeFade;
                    float crest = topOffset + Mathf.Sin(x * .042f + layer * 1.6f) * 2.4f
                        + Mathf.Sin(x * .095f + layer * 2.3f + .7f) * .75f
                        + Mathf.Sin(x * .16f - layer) * .2f;
                    vertices.Add(new Vector3(x, -13f, depth)); colors.Add(mist);
                    vertices.Add(new Vector3(x, Mathf.Lerp(-13f, crest, .6f), depth)); colors.Add(fadedMid);
                    vertices.Add(new Vector3(x, crest, depth)); colors.Add(fadedRidge);
                    if (i == segments) continue;
                    int first = i * 3;
                    for (int row = 0; row < 2; row++)
                    {
                        int a = first + row;
                        triangles.Add(a); triangles.Add(a + 3); triangles.Add(a + 1);
                        triangles.Add(a + 1); triangles.Add(a + 3); triangles.Add(a + 4);
                    }
                }
                var mesh = new Mesh { name = "Generated horizon ridge " + layer };
                mesh.SetVertices(vertices);
                mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                ownedMeshes.Add(mesh);
                var ridgeObject = new GameObject("Distant meadow ridge " + layer);
                ridgeObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = ridgeObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        void CreateFloatingIsland()
        {
            var island = new GameObject("Beveled floating court island");
            island.transform.position = new Vector3(0f, -1.1f, 0f);
            Mesh islandMesh = BuildFloatingIslandMesh();
            ownedMeshes.Add(islandMesh);
            island.AddComponent<MeshFilter>().sharedMesh = islandMesh;
            island.AddComponent<MeshRenderer>().sharedMaterials = new[]
            {
                MakeMaterial("Universal Render Pipeline/Lit", new Color(.025f, .23f, .48f), .9f, .025f),
                MakeMaterial("Universal Render Pipeline/Lit", new Color(.045f, .25f, .4f), .82f, .08f),
                MakeMaterial("Universal Render Pipeline/Lit", new Color(.2f, .8f, .96f), .5f, .68f),
                MakeMaterial("Universal Render Pipeline/Lit", new Color(.04f, .2f, .42f), .76f, .12f),
                MakeMaterial("Universal Render Pipeline/Lit", new Color(.025f, .1f, .27f), .68f, .08f)
            };
            for (int i = 0; i < 9; i++)
            {
                float x = -6.3f + i * 1.58f;
                float z = (i % 2 == 0 ? -8f : 8f);
                // Keep the angled supports below the island top instead of protruding through the court surround.
                var support = Cylinder("Crystal island support", new Vector3(x, -3.5f, z), new Vector3(.35f, 2.2f, .35f), new Color(.24f, .72f, .9f), .7f, .3f);
                support.transform.rotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? -1f : 1f) * 11f);
            }
        }

        void CreateIslandFloorSheen()
        {
            Shader shader = Resources.Load<Shader>("ProceduralCourtSheen");
            if (!shader) return;

            // Follow the chamfered island edge, just above its top and below the playing court.
            Vector2[] outline =
            {
                new Vector2(-.36f, -.5f), new Vector2(.36f, -.5f),
                new Vector2(.5f, -.36f), new Vector2(.5f, .36f),
                new Vector2(.36f, .5f), new Vector2(-.36f, .5f),
                new Vector2(-.5f, .36f), new Vector2(-.5f, -.36f)
            };
            var vertices = new Vector3[9];
            vertices[0] = new Vector3(0f, -.744f, 0f);
            for (int i = 0; i < outline.Length; i++)
                vertices[i + 1] = new Vector3(outline[i].x * 15.1f, -.744f, outline[i].y * 20.1f);
            var triangles = new int[outline.Length * 3];
            for (int i = 0; i < outline.Length; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % outline.Length + 1;
                triangles[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "Generated island floor glaze" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            ownedMeshes.Add(mesh);
            var material = new Material(shader) { name = "Blue island floor glaze" };
            material.SetColor("_BaseColor", new Color(.025f, .24f, .52f, 1f));
            material.SetColor("_GlintColor", new Color(.40f, .78f, .97f, 1f));
            material.SetFloat("_Opacity", .4f);
            material.SetFloat("_WaveScale", 5f);
            material.SetFloat("_FlowSpeed", .06f);
            material.SetFloat("_FresnelStrength", .35f);
            ownedMaterials.Add(material);
            var floor = new GameObject("Reflective island floor");
            floor.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = floor.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Mesh BuildFloatingIslandMesh()
        {
            const int materialCount = 5;
            var vertices = new List<Vector3>();
            var triangles = new List<int>[materialCount];
            for (int i = 0; i < triangles.Length; i++) triangles[i] = new List<int>();

            // The chamfered rings keep the court's large, level top while giving the island a clear crystal-mesa silhouette.
            float[] widths = { 15.1f, 15.5f, 15.5f, 15.5f, 15.1f, 15.1f, 14.7f, 13.8f, 11.8f, 8.6f };
            float[] lengths = { 20.1f, 20.5f, 20.5f, 20.5f, 20.1f, 20.1f, 19.7f, 18.8f, 16.7f, 13.8f };
            float[] heights = { .35f, .1f, -.28f, -.4f, -.52f, -.62f, -1.08f, -1.78f, -2.4f, -2.78f };
            float[,] corners =
            {
                { -.36f, -.5f }, { .36f, -.5f }, { .5f, -.36f }, { .5f, .36f },
                { .36f, .5f }, { -.36f, .5f }, { -.5f, .36f }, { -.5f, -.36f }
            };

            Vector3 Point(int ring, int corner) => new Vector3(
                corners[corner, 0] * widths[ring], heights[ring], corners[corner, 1] * lengths[ring]);

            Vector3 topCenter = new Vector3(0f, heights[0], 0f);
            for (int corner = 0; corner < 8; corner++)
                AddIslandTriangle(vertices, triangles[0], topCenter, Point(0, (corner + 1) % 8), Point(0, corner));

            int[] segmentMaterials = { 0, 1, 1, 1, 2, 2, 3, 4, 4 };
            for (int ring = 0; ring < heights.Length - 1; ring++)
            {
                List<int> materialTriangles = triangles[segmentMaterials[ring]];
                for (int corner = 0; corner < 8; corner++)
                {
                    int next = (corner + 1) % 8;
                    AddIslandQuad(vertices, materialTriangles,
                        Point(ring, corner), Point(ring, next), Point(ring + 1, next), Point(ring + 1, corner));
                }
            }

            Vector3 bottomCenter = new Vector3(0f, heights[heights.Length - 1], 0f);
            for (int corner = 0; corner < 8; corner++)
                AddIslandTriangle(vertices, triangles[4], bottomCenter, Point(heights.Length - 1, corner), Point(heights.Length - 1, (corner + 1) % 8));

            var mesh = new Mesh { name = "Generated beveled floating island" };
            mesh.SetVertices(vertices);
            mesh.subMeshCount = materialCount;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddIslandTriangle(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c)
        {
            int first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
        }

        static void AddIslandQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int first = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }

        void CreateCourt()
        {
            float top = -.72f;
            Cube("Iridescent badminton court", new Vector3(0f, top, 0f), new Vector3(CourtWidth, .12f, CourtLength), new Color(.015f, .22f, .56f), .92f, .035f);
            Cube("Far service box", new Vector3(0f, top + .068f, 3.05f), new Vector3(CourtWidth - .14f, .015f, 2.45f), new Color(.025f, .33f, .61f), .88f, .025f);
            Cube("Near service box", new Vector3(0f, top + .068f, -3.05f), new Vector3(CourtWidth - .14f, .015f, 2.45f), new Color(.02f, .28f, .56f), .9f, .025f);
            float lineY = top + .09f;
            Line("Left sideline", new Vector3(-CourtWidth / 2f, lineY, 0f), new Vector3(.075f, .015f, CourtLength), Color.white);
            Line("Right sideline", new Vector3(CourtWidth / 2f, lineY, 0f), new Vector3(.075f, .015f, CourtLength), Color.white);
            Line("Near baseline", new Vector3(0f, lineY, -CourtLength / 2f), new Vector3(CourtWidth, .015f, .075f), Color.white);
            Line("Far baseline", new Vector3(0f, lineY, CourtLength / 2f), new Vector3(CourtWidth, .015f, .075f), Color.white);
            Line("Short service line near", new Vector3(0f, lineY, -1.98f), new Vector3(CourtWidth, .015f, .075f), new Color(.91f, 1f, 1f));
            Line("Short service line far", new Vector3(0f, lineY, 1.98f), new Vector3(CourtWidth, .015f, .075f), new Color(.91f, 1f, 1f));
            Line("Center line near", new Vector3(0f, lineY, -3.7f), new Vector3(.075f, .015f, 3.45f), Color.white);
            Line("Center line far", new Vector3(0f, lineY, 3.7f), new Vector3(.075f, .015f, 3.45f), Color.white);
            Line("Long service line near", new Vector3(0f, lineY, -5.95f), new Vector3(CourtWidth, .015f, .075f), new Color(.74f, 1f, 1f));
            Line("Long service line far", new Vector3(0f, lineY, 5.95f), new Vector3(CourtWidth, .015f, .075f), new Color(.74f, 1f, 1f));
            CreateCourtSheen(top);
            CreatePlayer("Player", new Vector3(0f, top, -4.6f), new Color(.22f, .98f, .91f));
            CreatePlayer("Partner", new Vector3(-1.65f, top, -4.8f), new Color(.66f, .95f, .55f));
            CreatePlayer("Partner Two", new Vector3(1.65f, top, -4.2f), new Color(.66f, .75f, 1f));
            CreatePlayer("Rival", new Vector3(0f, top, 4.6f), new Color(1f, .52f, .62f));
            CreatePlayer("Rival Partner", new Vector3(1.65f, top, 4.8f), new Color(1f, .68f, .38f));
            CreatePlayer("Rival Wing", new Vector3(-1.65f, top, 4.2f), new Color(.89f, .54f, .95f));
        }

        void CreateCourtSheen(float courtTop)
        {
            Shader shader = Resources.Load<Shader>("ProceduralCourtSheen");
            if (!shader)
            {
                Debug.LogWarning("The procedural court sheen shader is missing; using the base court material.");
                return;
            }

            var overlay = new GameObject("Procedural moving court sheen");
            var mesh = BuildCourtSheenMesh(CourtWidth - .08f, CourtLength - .08f, courtTop + .085f);
            ownedMeshes.Add(mesh);
            overlay.AddComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(shader) { name = "Procedural aqua court glaze" };
            material.SetColor("_BaseColor", new Color(.025f, .27f, .58f, 1f));
            material.SetColor("_GlintColor", new Color(.55f, .92f, 1f, 1f));
            material.SetFloat("_Opacity", .62f);
            material.SetFloat("_WaveScale", 7f);
            material.SetFloat("_FlowSpeed", .1f);
            material.SetFloat("_FresnelStrength", .46f);
            ownedMaterials.Add(material);

            var renderer = overlay.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void CreatePavilion()
        {
            // An open-sided pavilion frames the view without putting geometry between the camera and play.
            Color frame = new Color(.20f, .42f, .56f);
            Color trim = new Color(.63f, .88f, .94f);
            const float back = 9.1f;
            const float left = -7.25f;
            const float height = 6.4f;
            for (int i = 0; i <= 8; i++)
            {
                float x = Mathf.Lerp(-7.25f, 7.25f, i / 8f);
                Cube("Pavilion rear mullion", new Vector3(x, 2.5f, back), new Vector3(.07f, height, .11f), frame, .8f, .025f);
            }
            for (int i = 0; i <= 6; i++)
            {
                float z = Mathf.Lerp(-9f, back, i / 6f);
                Cube("Pavilion side mullion", new Vector3(left, 2.5f, z), new Vector3(.11f, height, .07f), frame, .8f, .025f);
            }
            for (int i = 0; i < 3; i++)
            {
                float y = .25f + i * 2.25f;
                Cube("Pavilion rear rail", new Vector3(0f, y, back), new Vector3(14.5f, .055f, .09f), trim, .8f, .025f);
                Cube("Pavilion side rail", new Vector3(left, y, .05f), new Vector3(.09f, .055f, 18.1f), trim, .8f, .025f);
            }
            Cube("Pavilion rear crown", new Vector3(0f, 5.7f, back), new Vector3(14.6f, .18f, .22f), frame, .88f, .04f);
            Cube("Pavilion side crown", new Vector3(left, 5.7f, .05f), new Vector3(.22f, .18f, 18.2f), frame, .88f, .04f);

            // Shallow galleries and light strips add depth without crossing the camera's court view.
            Color gallery = new Color(.09f, .28f, .42f);
            Color light = new Color(.73f, .96f, 1f);
            Cube("Rear gallery ledge", new Vector3(0f, 2.3f, 8.7f), new Vector3(14.35f, .14f, .8f), gallery, .86f, .025f);
            Cube("Side gallery ledge", new Vector3(-6.85f, 2.3f, .05f), new Vector3(.8f, .14f, 18f), gallery, .86f, .025f);
            Cube("Rear gallery handrail", new Vector3(0f, 3.05f, 8.3f), new Vector3(14.25f, .055f, .055f), trim, .82f, .08f);
            Cube("Side gallery handrail", new Vector3(-6.45f, 3.05f, .05f), new Vector3(.055f, .055f, 17.9f), trim, .82f, .08f);
            for (int i = 0; i <= 8; i++)
            {
                float x = Mathf.Lerp(-7.1f, 7.1f, i / 8f);
                Cube("Rear gallery support", new Vector3(x, 2.69f, 8.3f), new Vector3(.045f, .76f, .045f), trim, .78f, .04f);
            }
            for (int i = 0; i <= 6; i++)
            {
                float z = Mathf.Lerp(-8.9f, 8.95f, i / 6f);
                Cube("Side gallery support", new Vector3(-6.45f, 2.69f, z), new Vector3(.045f, .76f, .045f), trim, .78f, .04f);
            }
            for (int i = 0; i < 5; i++)
            {
                float x = Mathf.Lerp(-5.6f, 5.6f, i / 4f);
                var strip = Cube("Pavilion overhead light", new Vector3(x, 5.57f, 8.55f), new Vector3(1.12f, .045f, .18f), light, .65f, .55f);
                strip.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            for (int i = 0; i < 4; i++)
            {
                float z = Mathf.Lerp(-7.5f, 6.5f, i / 3f);
                var strip = Cube("Pavilion side light", new Vector3(-6.7f, 5.57f, z), new Vector3(.18f, .045f, 1.12f), light, .65f, .55f);
                strip.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Shader glassShader = Resources.Load<Shader>("AeroGlass");
            if (!glassShader) return;
            var glass = new Material(glassShader) { name = "Pavilion pearl glass" };
            glass.SetColor("_Tint", new Color(.55f, .83f, .94f));
            glass.SetFloat("_Opacity", .035f);
            glass.SetFloat("_RimOpacity", .12f);
            ownedMaterials.Add(glass);
            AddGlassPrimitive(PrimitiveType.Cube, "Rear glass", new Vector3(0f, 2.5f, back + .06f), new Vector3(14.45f, 6.3f, .02f), glass);
            AddGlassPrimitive(PrimitiveType.Cube, "Side glass", new Vector3(left - .06f, 2.5f, .05f), new Vector3(.02f, 6.3f, 18f), glass);
            CreateRightGallery(glass);
            CreatePavilionCanopy(glass);

            var bubbleMaterial = new Material(glassShader) { name = "Floating pearl bubbles" };
            bubbleMaterial.SetColor("_Tint", new Color(.65f, .91f, 1f));
            bubbleMaterial.SetFloat("_Opacity", .018f);
            bubbleMaterial.SetFloat("_RimOpacity", .48f);
            ownedMaterials.Add(bubbleMaterial);
            for (int i = 0; i < 11; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Vector3 position = new Vector3(side * (4.5f + (i % 3) * .6f), .8f + (i % 4) * .7f, -5.5f + (i % 6) * 2.3f);
                float size = .26f + (i % 4) * .1f;
                var bubble = AddGlassPrimitive(PrimitiveType.Sphere, "Drifting glass bubble", position, Vector3.one * size, bubbleMaterial);
                bubbleDrifts.Add(new CloudDrift(bubble.transform, position, i * 1.1f, .35f + i * .015f));
            }
        }

        void CreateRightGallery(Material glass)
        {
            var gallery = new GameObject("Right glass gallery");
            Color frame = new Color(.20f, .42f, .56f);
            Color trim = new Color(.63f, .88f, .94f);
            Color ledgeColor = new Color(.09f, .28f, .42f);
            const float right = 7.25f;
            for (int i = 0; i <= 6; i++)
            {
                float z = Mathf.Lerp(-9f, 9.1f, i / 6f);
                var mullion = Cube("Right pavilion mullion", new Vector3(right, 2.5f, z), new Vector3(.11f, 6.4f, .07f), frame, .8f, .025f);
                mullion.transform.SetParent(gallery.transform, true);
                var support = Cube("Right gallery support", new Vector3(6.45f, 2.69f, z), new Vector3(.045f, .76f, .045f), trim, .78f, .04f);
                support.transform.SetParent(gallery.transform, true);
            }
            for (int i = 0; i < 3; i++)
            {
                var rail = Cube("Right pavilion rail", new Vector3(right, .25f + i * 2.25f, .05f), new Vector3(.09f, .055f, 18.1f), trim, .8f, .025f);
                rail.transform.SetParent(gallery.transform, true);
            }
            var crown = Cube("Right pavilion crown", new Vector3(right, 5.7f, .05f), new Vector3(.22f, .18f, 18.2f), frame, .88f, .04f);
            crown.transform.SetParent(gallery.transform, true);
            var ledge = Cube("Right gallery ledge", new Vector3(6.85f, 2.3f, .05f), new Vector3(.8f, .14f, 18f), ledgeColor, .86f, .025f);
            ledge.transform.SetParent(gallery.transform, true);
            var handrail = Cube("Right gallery handrail", new Vector3(6.45f, 3.05f, .05f), new Vector3(.055f, .055f, 17.9f), trim, .82f, .08f);
            handrail.transform.SetParent(gallery.transform, true);
            var panel = AddGlassPrimitive(PrimitiveType.Cube, "Right glass", new Vector3(right + .06f, 2.5f, .05f), new Vector3(.02f, 6.3f, 18f), glass);
            panel.transform.SetParent(gallery.transform, true);
            // Keep the thin glass structure from casting a heavy grid over the playing floor.
            foreach (Renderer part in gallery.GetComponentsInChildren<Renderer>())
                part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gallery.AddComponent<AeroCourtLevelVisibility>();
        }

        void CreatePavilionCanopy(Material glass)
        {
            // A delicate overhead grid frames the low camera; the overview hides it
            // so its beams cannot cover players seen from above.
            var canopy = new GameObject("Glass arena canopy");
            Color frame = new Color(.16f, .36f, .51f);
            Color light = new Color(.72f, .96f, 1f);
            var roof = AddGlassPrimitive(PrimitiveType.Cube, "Canopy glass", new Vector3(0f, 5.78f, .05f), new Vector3(14.45f, .025f, 18f), glass);
            roof.transform.SetParent(canopy.transform, true);
            for (int i = 0; i <= 6; i++)
            {
                float x = Mathf.Lerp(-7.2f, 7.2f, i / 6f);
                var beam = Cube("Canopy lengthwise beam", new Vector3(x, 5.7f, .05f), new Vector3(.075f, .1f, 18f), frame, .8f, .025f);
                beam.transform.SetParent(canopy.transform, true);
                beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            for (int i = 0; i <= 8; i++)
            {
                float z = Mathf.Lerp(-8.95f, 9.05f, i / 8f);
                var beam = Cube("Canopy cross beam", new Vector3(0f, 5.7f, z), new Vector3(14.4f, .1f, .075f), frame, .8f, .025f);
                beam.transform.SetParent(canopy.transform, true);
                beam.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            for (int row = 0; row < 3; row++)
            for (int col = -1; col <= 1; col++)
            {
                var strip = Cube("Canopy light", new Vector3(col * 4.5f, 5.62f, -5.5f + row * 5.4f), new Vector3(.16f, .04f, 1.6f), light, .6f, .6f);
                strip.transform.SetParent(canopy.transform, true);
                strip.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            canopy.AddComponent<AeroCourtLevelVisibility>();
        }

        void CreateArenaScoreboard()
        {
            var scoreboard = new GameObject("Arena scoreboard");
            var frame = Cube("Scoreboard frame", new Vector3(0f, 4.65f, 8.97f), new Vector3(3.55f, 1.08f, .16f), new Color(.10f, .36f, .47f), .8f, .04f);
            frame.transform.SetParent(scoreboard.transform, true);
            var face = Cube("Scoreboard face", new Vector3(0f, 4.65f, 8.87f), new Vector3(3.42f, .95f, .05f), new Color(.015f, .075f, .16f), .72f, .025f);
            face.transform.SetParent(scoreboard.transform, true);
            var digits = new Renderer[28];
            float[] centers = { -1.08f, -.57f, .57f, 1.08f };
            Vector2[] offsets =
            {
                new Vector2(0f, .30f), new Vector2(.15f, .15f), new Vector2(.15f, -.15f),
                new Vector2(0f, -.30f), new Vector2(-.15f, -.15f), new Vector2(-.15f, .15f), Vector2.zero
            };
            for (int digit = 0; digit < 4; digit++)
            for (int segment = 0; segment < 7; segment++)
            {
                bool horizontal = segment == 0 || segment == 3 || segment == 6;
                Vector2 offset = offsets[segment];
                var stroke = Cube("Score display segment", new Vector3(centers[digit] + offset.x, 4.65f + offset.y, 8.82f),
                    horizontal ? new Vector3(.25f, .045f, .025f) : new Vector3(.045f, .25f, .025f),
                    new Color(.42f, .96f, .94f), .4f, .85f);
                stroke.transform.SetParent(scoreboard.transform, true);
                digits[digit * 7 + segment] = stroke.GetComponent<Renderer>();
                digits[digit * 7 + segment].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var separator = Cube("Score separator", new Vector3(0f, 4.65f, 8.82f), new Vector3(.22f, .045f, .025f), new Color(.42f, .96f, .94f), .4f, .85f);
            separator.transform.SetParent(scoreboard.transform, true);
            separator.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            frame.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            face.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            scoreboard.AddComponent<AeroArenaScoreboard>().Initialize(digits);
        }

        GameObject AddGlassPrimitive(PrimitiveType type, string objectName, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = objectName;
            obj.transform.position = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Collider>().enabled = false;
            var renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return obj;
        }

        static Mesh BuildCourtSheenMesh(float width, float length, float height)
        {
            var mesh = new Mesh { name = "Generated court sheen plane" };
            mesh.vertices = new[]
            {
                new Vector3(-width * .5f, height, -length * .5f),
                new Vector3(width * .5f, height, -length * .5f),
                new Vector3(width * .5f, height, length * .5f),
                new Vector3(-width * .5f, height, length * .5f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals();
            return mesh;
        }

        void CreateNet()
        {
            float y = -.72f;
            Cylinder("Net post left", new Vector3(-3.18f, y + .78f, 0f), new Vector3(.09f, .78f, .09f), new Color(.83f, 1f, 1f), .65f, .35f);
            Cylinder("Net post right", new Vector3(3.18f, y + .78f, 0f), new Vector3(.09f, .78f, .09f), new Color(.83f, 1f, 1f), .65f, .35f);
            Cube("Net top tape", new Vector3(0f, y + 1.56f, 0f), new Vector3(6.35f, .11f, .1f), new Color(.92f, 1f, 1f), .25f, .6f);
            for (int i = -14; i <= 14; i++)
                Cube("Net mesh vertical", new Vector3(i * .21f, y + .81f, -.026f), new Vector3(.012f, 1.46f, .012f), new Color(.77f, 1f, 1f), .1f, .1f);
            for (int i = 0; i <= 7; i++)
                Cube("Net mesh horizontal", new Vector3(0f, y + .11f + i * .2f, -.026f), new Vector3(6.08f, .012f, .012f), new Color(.77f, 1f, 1f), .1f, .1f);
        }

        Transform CreatePlayer(string name, Vector3 basePosition, Color suit)
        {
            var root = new GameObject(name + " silhouette"); root.transform.position = basePosition;
            if (name == "Rival" || name == "Rival Partner" || name == "Rival Wing")
                root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var pose = new GameObject("Animated pose").transform;
            pose.SetParent(root.transform, false);

            Sphere("Glossy sport jacket", pose.TransformPoint(new Vector3(0f, .92f, 0f)), new Vector3(.62f, .82f, .38f), suit, .9f, .07f).transform.SetParent(pose, true);
            CreatePlayerGlaze(pose);
            Sphere("Head", pose.TransformPoint(new Vector3(0f, 1.66f, 0f)), new Vector3(.36f, .39f, .35f), new Color(.94f, .72f, .56f), .55f, .025f).transform.SetParent(pose, true);
            Transform leftLeg = Limb(pose, "Left leg", new Vector3(-.18f, .58f, 0f), new Vector3(-.32f, .07f, 0f), .16f, suit);
            Transform rightLeg = Limb(pose, "Right leg", new Vector3(.18f, .58f, 0f), new Vector3(.32f, .07f, 0f), .16f, suit);
            Transform leftArm = Limb(pose, "Left arm", new Vector3(-.3f, 1.08f, 0f), new Vector3(-.48f, .52f, .05f), .12f, suit);
            Transform rightArm = Limb(pose, "Right arm", new Vector3(.3f, 1.08f, 0f), new Vector3(.52f, 1.38f, 0f), .12f, suit);
            CreatePlayerKit(pose, leftLeg, rightLeg, name, suit);
            bool rival = name == "Rival" || name == "Rival Partner" || name == "Rival Wing";
            Color racketColor = rival ? new Color(1f, .88f, .77f) : new Color(.8f, 1f, .98f);
            Transform racket = CreateRacket(rightArm, pose.TransformPoint(new Vector3(.62f, 1.14f, 0f)), racketColor);
            Renderer shadow = CreatePlayerShadow(root.transform);
            root.AddComponent<AeroPlayerAnimation>().Initialize(pose, leftArm, rightArm, leftLeg, rightLeg, racket, shadow);
            return root.transform;
        }

        void CreatePlayerGlaze(Transform pose)
        {
            if (!playerGlazeMaterial)
            {
                Shader shader = Resources.Load<Shader>("AeroGlass");
                if (!shader) return;
                playerGlazeMaterial = new Material(shader) { name = "Player pearl glaze" };
                playerGlazeMaterial.SetColor("_Tint", new Color(.76f, .95f, 1f));
                playerGlazeMaterial.SetFloat("_Opacity", 0f);
                playerGlazeMaterial.SetFloat("_RimOpacity", .27f);
                ownedMaterials.Add(playerGlazeMaterial);
            }

            var glaze = AddGlassPrimitive(PrimitiveType.Sphere, "Pearl jacket highlights",
                pose.TransformPoint(new Vector3(0f, .92f, 0f)), new Vector3(.64f, .84f, .4f), playerGlazeMaterial);
            glaze.transform.SetParent(pose, true);
        }

        Renderer CreatePlayerShadow(Transform playerRoot)
        {
            if (!playerShadowMaterial)
            {
                Shader shader = Resources.Load<Shader>("PlayerContactShadow");
                if (!shader) return null;
                playerShadowMaterial = new Material(shader) { name = "Procedural player contact shadow" };
                ownedMaterials.Add(playerShadowMaterial);
            }

            var shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            shadow.name = "Soft court contact shadow";
            shadow.GetComponent<Collider>().enabled = false;
            shadow.transform.SetParent(playerRoot, false);
            shadow.transform.localPosition = Vector3.up * .091f;
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadow.transform.localScale = new Vector3(.92f, .58f, 1f);
            var renderer = shadow.GetComponent<Renderer>();
            renderer.sharedMaterial = playerShadowMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        void CreatePlayerKit(Transform pose, Transform leftLeg, Transform rightLeg, string name, Color suit)
        {
            bool rival = name == "Rival" || name == "Rival Partner" || name == "Rival Wing";
            Color trim = rival ? new Color(1f, .78f, .7f) : new Color(.58f, 1f, .95f);
            Color shorts = rival ? new Color(.71f, .26f, .42f) : new Color(.04f, .37f, .47f);
            Color hair = rival ? new Color(.24f, .1f, .27f) : new Color(.035f, .18f, .27f);

            Sphere("Tailored rally shorts", pose.TransformPoint(new Vector3(0f, .53f, 0f)), new Vector3(.55f, .42f, .36f), shorts, .62f, .12f).transform.SetParent(pose, true);
            Sphere("Sculpted sport hair", pose.TransformPoint(new Vector3(0f, 1.82f, -.015f)), new Vector3(.39f, .2f, .38f), hair, .24f, .05f).transform.SetParent(pose, true);
            Sphere("Pearl rally headband", pose.TransformPoint(new Vector3(0f, 1.76f, .21f)), new Vector3(.38f, .075f, .2f), trim, .65f, .35f).transform.SetParent(pose, true);
            Sphere("Swept back hair lock", pose.TransformPoint(new Vector3(0f, 1.62f, -.27f)), new Vector3(.2f, .26f, .22f), hair, .22f, .04f).transform.SetParent(pose, true);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 shoe = new Vector3(side * .31f, .16f, .12f);
                Transform leg = side < 0 ? leftLeg : rightLeg;
                Sphere("Pearl court shoe", pose.TransformPoint(shoe), new Vector3(.31f, .15f, .4f), new Color(.91f, .99f, 1f), .8f, .14f).transform.SetParent(leg, true);
                Sphere("Aqua traction sole", pose.TransformPoint(shoe + Vector3.down * .075f), new Vector3(.32f, .055f, .41f), trim, .48f, .32f).transform.SetParent(leg, true);
                Sphere("Shoe side flash", pose.TransformPoint(shoe + new Vector3(side * .115f, .015f, .015f)), new Vector3(.06f, .055f, .2f), suit, .72f, .3f).transform.SetParent(leg, true);
            }
        }

        Transform CreateRacket(Transform arm, Vector3 center, Color color)
        {
            var racket = new GameObject("Racket swing assembly").transform;
            racket.position = center;
            racket.rotation = Quaternion.Euler(14f, 0f, 18f);
            racket.SetParent(arm, true);

            var ring = new GameObject("Carbon racket oval");
            var mf = ring.AddComponent<MeshFilter>(); var mr = ring.AddComponent<MeshRenderer>();
            var ringMesh = BuildRing(.29f, .235f, .04f, 48);
            ownedMeshes.Add(ringMesh);
            mf.sharedMesh = ringMesh; mr.sharedMaterial = MakeMaterial("Universal Render Pipeline/Lit", color, .68f, .65f);
            ring.transform.SetParent(racket, false);

            var stringBed = new GameObject("Procedural aqua racket strings");
            var stringFilter = stringBed.AddComponent<MeshFilter>();
            var stringMesh = BuildRacketStringBed(.25f, .195f, 19, 15, .007f, .018f);
            ownedMeshes.Add(stringMesh);
            stringFilter.sharedMesh = stringMesh;
            var stringRenderer = stringBed.AddComponent<MeshRenderer>();
            stringRenderer.sharedMaterial = MakeMaterial("Universal Render Pipeline/Lit", new Color(.83f, 1f, .99f), .78f, .32f);
            stringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            stringRenderer.receiveShadows = false;
            stringBed.transform.SetParent(racket, false);

            var grip = Cylinder("Racket grip", Vector3.zero, new Vector3(.055f, .28f, .055f), new Color(.13f, .32f, .39f), .35f, .2f);
            grip.transform.SetParent(racket, false);
            grip.transform.localPosition = new Vector3(.26f, -.25f, 0f);
            grip.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            return racket;
        }

        void CreateCloudBanks()
        {
            Shader shader = Resources.Load<Shader>("CloudMist");
            if (!shader) return;
            var mist = new Material(shader) { name = "Soft cloud mist" };
            ownedMaterials.Add(mist);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < 4; row++)
                {
                    float z = -13f + row * 8.4f;
                    float x = side * (9.7f + (row % 2) * 1.3f);
                    Vector3 origin = new Vector3(x, -1.8f, z);
                    var bank = new GameObject("Drifting cloud bank").transform;
                    bank.position = origin;
                    float phase = (side + 1) * .8f + row * 1.47f;
                    cloudDrifts.Add(new CloudDrift(bank, origin, phase, .075f + row * .009f));
                    for (int lobe = 0; lobe < 5; lobe++)
                    {
                        float dx = (lobe - 2) * .7f;
                        float lift = (lobe == 1 || lobe == 3) ? .3f : 0f;
                        var lobeObject = AddGlassPrimitive(PrimitiveType.Sphere, "Procedural cloud mist",
                            origin + new Vector3(dx, lift, Mathf.Sin(lobe * 1.7f) * .3f),
                            new Vector3(2.1f - Mathf.Abs(lobe - 2) * .18f, 1.05f + lift * .4f, 1.65f), mist);
                        lobeObject.transform.SetParent(bank, true);
                    }
                }
            }
        }

        void CreateSpectatorOrbs()
        {
            Shader shader = Resources.Load<Shader>("AeroGlass");
            if (!shader) return;
            var aqua = new Material(shader) { name = "Aqua arena bubbles" };
            aqua.SetColor("_Tint", new Color(.62f, .9f, 1f));
            aqua.SetFloat("_Opacity", .018f);
            aqua.SetFloat("_RimOpacity", .38f);
            ownedMaterials.Add(aqua);
            var pearl = new Material(shader) { name = "Pearl arena bubbles" };
            pearl.SetColor("_Tint", new Color(.82f, .79f, 1f));
            pearl.SetFloat("_Opacity", .018f);
            pearl.SetFloat("_RimOpacity", .38f);
            ownedMaterials.Add(pearl);
            for (int i = 0; i < 12; i++)
            {
                float angle = i / 12f * Mathf.PI * 2f;
                float height = 1.4f + (i % 3) * .55f;
                float size = .28f + (i % 4) * .08f;
                var orb = AddGlassPrimitive(PrimitiveType.Sphere, "Floating arena bubble",
                    new Vector3(Mathf.Cos(angle) * 10.3f, height, Mathf.Sin(angle) * 10.3f),
                    Vector3.one * size, i % 3 == 0 ? pearl : aqua);
                lanternOrbits.Add(new LanternOrbit(orb.transform, 10.3f, height, angle, angle, .014f + (i % 3) * .002f, size));
            }
        }

        void CreateCamera()
        {
            var cameraObject = new GameObject("Court camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 44f; camera.nearClipPlane = .1f; camera.farClipPlane = 250f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.34f, .72f, .86f);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(12f, 10f, -19f);
            cameraObject.transform.LookAt(new Vector3(0f, .3f, 0f));
        }

        void CreateSolarHalo()
        {
            Camera camera = Camera.main;
            if (!camera) return;

            Vector3 center = camera.transform.position + camera.transform.forward * 120f
                + camera.transform.up * 24f - camera.transform.right * 38f;
            var halo = new GameObject("Pearl sun halo").transform;
            halo.position = center;
            halo.rotation = Quaternion.LookRotation(camera.transform.position - center, Vector3.up);

            var sun = Sphere("Soft pearl sun", center, Vector3.one * 5.4f, new Color(1f, .84f, .66f), .25f, .65f);
            sun.transform.SetParent(halo, true);
            sun.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sun.GetComponent<Renderer>().receiveShadows = false;

            Mesh haloMesh = BuildRing(9.4f, 9.4f, .12f, 72);
            ownedMeshes.Add(haloMesh);
            var ring = new GameObject("Aqua solar halo");
            ring.transform.SetParent(halo, false);
            ring.AddComponent<MeshFilter>().sharedMesh = haloMesh;
            var ringRenderer = ring.AddComponent<MeshRenderer>();
            ringRenderer.sharedMaterial = MakeMaterial("Universal Render Pipeline/Lit", new Color(.58f, .9f, 1f), .72f, .62f);
            ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringRenderer.receiveShadows = false;
        }

        Transform Limb(Transform parent, string name, Vector3 from, Vector3 to, float radius, Color color)
        {
            Vector3 delta = to - from;
            var joint = new GameObject(name + " pivot").transform;
            joint.SetParent(parent, false);
            joint.localPosition = from;

            var limb = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            limb.name = "Rounded " + name.ToLowerInvariant();
            limb.GetComponent<Collider>().enabled = false;
            limb.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Universal Render Pipeline/Lit", color, .84f, .06f);
            limb.transform.SetParent(joint, false);
            limb.transform.localScale = new Vector3(radius, delta.magnitude * .5f, radius);
            limb.transform.localPosition = delta * .5f;
            limb.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            return joint;
        }

        GameObject Line(string name, Vector3 center, Vector3 scale, Color color) => Cube(name, center, scale, color, .1f, .05f);

        GameObject Cube(string name, Vector3 position, Vector3 scale, Color color, float smoothness, float emission)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Collider>().enabled = false;
            go.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Universal Render Pipeline/Lit", color, smoothness, emission);
            return go;
        }

        GameObject Sphere(string name, Vector3 position, Vector3 scale, Color color, float smoothness, float emission)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name; go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Collider>().enabled = false;
            go.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Universal Render Pipeline/Lit", color, smoothness, emission);
            return go;
        }

        GameObject Cylinder(string name, Vector3 position, Vector3 scale, Color color, float smoothness, float emission)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name; go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Collider>().enabled = false;
            go.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Universal Render Pipeline/Lit", color, smoothness, emission);
            return go;
        }

        Material MakeMaterial(string shaderName, Color color, float smoothness, float emission, Shader explicitShader = null)
        {
            string key = shaderName + ColorUtility.ToHtmlStringRGBA(color) + smoothness + emission;
            if (materialCache.TryGetValue(key, out var cached)) return cached;
            Shader shader = explicitShader ? explicitShader : AeroRuntimeShaderLibrary.Resolve(shaderName);
            var material = new Material(shader) { name = "Procedural " + ColorUtility.ToHtmlStringRGBA(color) };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color); else material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", .14f);
            if (material.HasProperty("_EmissionColor")) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * emission); }
            ownedMaterials.Add(material);
            materialCache.Add(key, material);
            return material;
        }

        static Mesh BuildRing(float outerX, float outerY, float width, int steps)
        {
            var mesh = new Mesh { name = "Generated racket ring" };
            var vertices = new Vector3[steps * 4]; var triangles = new int[steps * 12];
            for (int i = 0; i < steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 2f;
                int outer = i * 2, inner = outer + 1, outerBack = steps * 2 + outer, innerBack = outerBack + 1;
                vertices[outer] = new Vector3(Mathf.Cos(a) * outerX, Mathf.Sin(a) * outerY, 0f);
                vertices[inner] = new Vector3(Mathf.Cos(a) * (outerX - width), Mathf.Sin(a) * (outerY - width), 0f);
                vertices[outerBack] = vertices[outer];
                vertices[innerBack] = vertices[inner];
                int j = i * 12, nextOuter = ((i + 1) % steps) * 2, nextInner = nextOuter + 1;
                triangles[j] = outer; triangles[j + 1] = nextOuter; triangles[j + 2] = inner;
                triangles[j + 3] = inner; triangles[j + 4] = nextOuter; triangles[j + 5] = nextInner;
                triangles[j + 6] = innerBack; triangles[j + 7] = steps * 2 + nextOuter; triangles[j + 8] = outerBack;
                triangles[j + 9] = steps * 2 + nextInner; triangles[j + 10] = steps * 2 + nextOuter; triangles[j + 11] = innerBack;
            }
            mesh.vertices = vertices; mesh.triangles = triangles; mesh.RecalculateNormals(); return mesh;
        }

        static Mesh BuildRacketStringBed(float innerX, float innerY, int verticalCount, int horizontalCount, float width, float depth)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < verticalCount; i++)
            {
                float normalizedX = Mathf.Lerp(-.88f, .88f, i / (float)(verticalCount - 1));
                float x = normalizedX * innerX;
                float halfLength = innerY * Mathf.Sqrt(1f - normalizedX * normalizedX) * .96f;
                AddString(vertices, triangles, new Vector2(x, -halfLength), new Vector2(x, halfLength), width, depth);
            }
            for (int i = 0; i < horizontalCount; i++)
            {
                float normalizedY = Mathf.Lerp(-.84f, .84f, i / (float)(horizontalCount - 1));
                float y = normalizedY * innerY;
                float halfLength = innerX * Mathf.Sqrt(1f - normalizedY * normalizedY) * .96f;
                AddString(vertices, triangles, new Vector2(-halfLength, y), new Vector2(halfLength, y), width, depth);
            }

            var mesh = new Mesh { name = "Generated crossed racket strings" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        static void AddString(List<Vector3> vertices, List<int> triangles, Vector2 start, Vector2 end, float width, float depth)
        {
            Vector2 direction = (end - start).normalized;
            Vector2 side = new Vector2(-direction.y, direction.x) * (width * .5f);
            int first = vertices.Count;
            vertices.Add(new Vector3(start.x + side.x, start.y + side.y, depth));
            vertices.Add(new Vector3(end.x + side.x, end.y + side.y, depth));
            vertices.Add(new Vector3(end.x - side.x, end.y - side.y, depth));
            vertices.Add(new Vector3(start.x - side.x, start.y - side.y, depth));
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
            triangles.Add(first); triangles.Add(first + 3); triangles.Add(first + 2);
            int back = vertices.Count;
            vertices.Add(new Vector3(start.x + side.x, start.y + side.y, depth));
            vertices.Add(new Vector3(end.x + side.x, end.y + side.y, depth));
            vertices.Add(new Vector3(end.x - side.x, end.y - side.y, depth));
            vertices.Add(new Vector3(start.x - side.x, start.y - side.y, depth));
            triangles.Add(back + 1); triangles.Add(back + 2); triangles.Add(back);
            triangles.Add(back + 2); triangles.Add(back + 3); triangles.Add(back);
        }
    }
}
