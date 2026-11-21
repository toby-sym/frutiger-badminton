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
        readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();

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

            public LanternOrbit(Transform lantern, float radius, float baseHeight, float angle, float phase, float speed)
            {
                this.lantern = lantern;
                this.radius = radius;
                this.baseHeight = baseHeight;
                this.angle = angle;
                this.phase = phase;
                this.speed = speed;
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
                orbit.lantern.localScale = Vector3.one * (.22f * (1f + twinkle));
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
            RenderSettings.ambientSkyColor = new Color(.39f, .78f, .96f);
            RenderSettings.ambientEquatorColor = new Color(.74f, .94f, .97f);
            RenderSettings.ambientGroundColor = new Color(.17f, .40f, .45f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.56f, .85f, .96f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .006f;

            var key = new GameObject("Aqua sunlight").AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, .91f, .76f);
            key.intensity = 1.65f;
            key.transform.rotation = Quaternion.Euler(42f, -28f, 0f);
            key.shadows = LightShadows.Soft;

            var fill = new GameObject("Sky fill").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(.36f, .83f, 1f);
            fill.intensity = .55f;
            fill.transform.rotation = Quaternion.Euler(18f, 150f, 0f);

            CreateSkyDome();
            CreateFloatingIsland();
            CreateCourt();
            CreateNet();
            CreateCloudBanks();
            CreateSpectatorOrbs();
            CreateCamera();
        }

        void CreateSkyDome()
        {
            var mesh = new Mesh { name = "Procedural gradient sky" };
            const int segments = 48;
            const int rings = 16;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            for (int y = 0; y <= rings; y++)
            {
                float t = y / (float)rings;
                float phi = t * Mathf.PI * .5f;
                for (int x = 0; x <= segments; x++)
                {
                    float theta = x / (float)segments * Mathf.PI * 2f;
                    vertices.Add(new Vector3(Mathf.Cos(theta) * Mathf.Cos(phi), Mathf.Sin(phi), Mathf.Sin(theta) * Mathf.Cos(phi)) * 180f);
                    colors.Add(Color.Lerp(new Color(.76f, .96f, 1f), new Color(.035f, .40f, .78f), Mathf.SmoothStep(0f, 1f, t)));
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

        void CreateFloatingIsland()
        {
            var island = Cube("Floating court island", Vector3.zero, new Vector3(15.5f, .7f, 20.5f), new Color(.15f, .78f, .82f), .78f, .15f);
            island.transform.position = new Vector3(0f, -1.1f, 0f);
            Cube("Glowing lower rim", new Vector3(0f, -1.55f, 0f), new Vector3(14.9f, .18f, 19.9f), new Color(.26f, .96f, 1f), .4f, 1.3f);
            for (int i = 0; i < 9; i++)
            {
                float x = -6.3f + i * 1.58f;
                float z = (i % 2 == 0 ? -8f : 8f);
                var support = Cylinder("Crystal island support", new Vector3(x, -2.2f, z), new Vector3(.35f, 2.2f, .35f), new Color(.24f, .72f, .9f), .7f, .3f);
                support.transform.rotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? -1f : 1f) * 11f);
            }
        }

        void CreateCourt()
        {
            float top = -.72f;
            Cube("Iridescent badminton court", new Vector3(0f, top, 0f), new Vector3(CourtWidth, .12f, CourtLength), new Color(.07f, .48f, .58f), .82f, .18f);
            Cube("Far service box", new Vector3(0f, top + .068f, 3.05f), new Vector3(CourtWidth - .14f, .015f, 2.45f), new Color(.19f, .73f, .76f), .65f, .16f);
            Cube("Near service box", new Vector3(0f, top + .068f, -3.05f), new Vector3(CourtWidth - .14f, .015f, 2.45f), new Color(.15f, .67f, .73f), .65f, .16f);
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
            CreatePlayer("Rival", new Vector3(0f, top, 4.6f), new Color(1f, .52f, .62f));
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
            material.SetColor("_BaseColor", new Color(.18f, .58f, .68f, 1f));
            material.SetColor("_GlintColor", new Color(.76f, 1f, 1f, 1f));
            material.SetFloat("_Opacity", .42f);
            material.SetFloat("_WaveScale", 11f);
            material.SetFloat("_FlowSpeed", .22f);
            material.SetFloat("_FresnelStrength", .38f);
            ownedMaterials.Add(material);

            var renderer = overlay.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
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
            if (name == "Rival") root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var pose = new GameObject("Animated pose").transform;
            pose.SetParent(root.transform, false);

            Sphere("Glossy sport jacket", pose.TransformPoint(new Vector3(0f, .92f, 0f)), new Vector3(.62f, .82f, .38f), suit, .35f, .5f).transform.SetParent(pose, true);
            Sphere("Head", pose.TransformPoint(new Vector3(0f, 1.66f, 0f)), new Vector3(.36f, .39f, .35f), new Color(.94f, .72f, .56f), .2f, .05f).transform.SetParent(pose, true);
            Transform leftLeg = Limb(pose, "Left leg", new Vector3(-.18f, .58f, 0f), new Vector3(-.32f, .07f, 0f), .16f, suit);
            Transform rightLeg = Limb(pose, "Right leg", new Vector3(.18f, .58f, 0f), new Vector3(.32f, .07f, 0f), .16f, suit);
            Transform leftArm = Limb(pose, "Left arm", new Vector3(-.3f, 1.08f, 0f), new Vector3(-.48f, .52f, .05f), .12f, suit);
            Transform rightArm = Limb(pose, "Right arm", new Vector3(.3f, 1.08f, 0f), new Vector3(.52f, 1.38f, 0f), .12f, suit);
            CreatePlayerKit(pose, name, suit);
            Color racketColor = name == "Rival" ? new Color(1f, .88f, .77f) : new Color(.8f, 1f, .98f);
            Transform racket = CreateRacket(rightArm, pose.TransformPoint(new Vector3(.62f, 1.14f, 0f)), racketColor);
            root.AddComponent<AeroPlayerAnimation>().Initialize(pose, leftArm, rightArm, leftLeg, rightLeg, racket);
            return root.transform;
        }

        void CreatePlayerKit(Transform pose, string name, Color suit)
        {
            bool rival = name == "Rival";
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
                Sphere("Pearl court shoe", pose.TransformPoint(shoe), new Vector3(.31f, .15f, .4f), new Color(.91f, .99f, 1f), .8f, .14f).transform.SetParent(pose, true);
                Sphere("Aqua traction sole", pose.TransformPoint(shoe + Vector3.down * .075f), new Vector3(.32f, .055f, .41f), trim, .48f, .32f).transform.SetParent(pose, true);
                Sphere("Shoe side flash", pose.TransformPoint(shoe + new Vector3(side * .115f, .015f, .015f)), new Vector3(.06f, .055f, .2f), suit, .72f, .3f).transform.SetParent(pose, true);
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
                    Color cloud = Color.Lerp(new Color(.92f, .99f, 1f), new Color(.45f, .83f, .98f), row * .07f);
                    for (int lobe = 0; lobe < 5; lobe++)
                    {
                        float dx = (lobe - 2) * .85f;
                        float lift = (lobe == 1 || lobe == 3) ? .5f : 0f;
                        var lobeObject = Sphere("Procedural cloud lobe", origin + new Vector3(dx, lift, Mathf.Sin(lobe * 1.7f) * .5f), new Vector3(1.4f - Mathf.Abs(lobe - 2) * .18f, .75f + lift * .3f, 1.15f), cloud, .08f, .1f);
                        lobeObject.transform.SetParent(bank, true);
                    }
                }
            }
        }

        void CreateSpectatorOrbs()
        {
            for (int i = 0; i < 12; i++)
            {
                float angle = i / 12f * Mathf.PI * 2f;
                float height = 1.4f + (i % 3) * .55f;
                var orb = Sphere("Floating glass lantern", new Vector3(Mathf.Cos(angle) * 10.3f, height, Mathf.Sin(angle) * 10.3f), Vector3.one * .22f, Color.Lerp(new Color(.2f, .94f, 1f), new Color(.9f, .68f, 1f), i / 12f), .8f, 1.8f);
                orb.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lanternOrbits.Add(new LanternOrbit(orb.transform, 10.3f, height, angle, angle, .014f + (i % 3) * .002f));
            }
        }

        void CreateCamera()
        {
            var cameraObject = new GameObject("Court camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 44f; camera.nearClipPlane = .1f; camera.farClipPlane = 250f;
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(12f, 10f, -19f);
            cameraObject.transform.LookAt(new Vector3(0f, .3f, 0f));
        }

        Transform Limb(Transform parent, string name, Vector3 from, Vector3 to, float radius, Color color)
        {
            Vector3 delta = to - from;
            var joint = new GameObject(name + " pivot").transform;
            joint.SetParent(parent, false);
            joint.localPosition = from;

            var limb = Cylinder("Stylized " + name.ToLowerInvariant(), Vector3.zero, new Vector3(radius, delta.magnitude * .5f, radius), color, .25f, .25f);
            limb.transform.SetParent(joint, false);
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
            Shader shader = explicitShader ? explicitShader : Shader.Find(shaderName) ?? Shader.Find("Standard");
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
