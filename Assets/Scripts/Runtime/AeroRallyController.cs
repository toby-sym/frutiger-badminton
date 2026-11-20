using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FrutigerBadminton
{
    /// <summary>Runs a local practice rally using generated shuttle geometry and classic keyboard/mouse input.</summary>
    public sealed class AeroRallyController : MonoBehaviour
    {
        const float GroundY = -.72f;
        const float CourtHalfWidth = 3.05f;
        const float CourtHalfLength = 6.7f;
        const float NetHeight = 1.56f;
        const float Gravity = 9.81f;

        readonly List<Object> ownedObjects = new List<Object>();
        Transform player;
        Transform rival;
        Transform shuttle;
        TrailRenderer shuttleTrail;
        Material shuttleMaterial;
        Material corkMaterial;
        Material bandMaterial;
        Material trailMaterial;
        GUIStyle scoreStyle;
        GUIStyle labelStyle;

        Vector3 flightStart;
        Vector3 flightTarget;
        Vector3 flightVelocity;
        Vector3 previousShuttlePosition;
        Vector3 dashDirection;
        float flightDuration;
        float flightElapsed;
        float dashTime;
        float dashCooldown;
        float jumpHeight;
        float jumpVelocity;
        float serveDelay;
        float messageDelay;
        int playerScore;
        int rivalScore;
        int rallyHits;
        bool playerMustReturn;
        bool flightWasPlayerShot;
        bool flightActive;
        bool servePending;
        bool isDashing;
        string message = "Move under the return, then time your shot.";

        void Start()
        {
            var playerObject = GameObject.Find("Player silhouette");
            var rivalObject = GameObject.Find("Rival silhouette");
            if (!playerObject || !rivalObject)
            {
                Debug.LogError("The Aero Court scene needs both generated player silhouettes before the rally can start.");
                enabled = false;
                return;
            }

            player = playerObject.transform;
            rival = rivalObject.transform;
            player.position = new Vector3(0f, GroundY, -4.6f);
            rival.position = new Vector3(0f, GroundY, 4.6f);
            CreateShuttlecock();
            BeginServe();
        }

        void Update()
        {
            float deltaTime = Mathf.Min(Time.deltaTime, .05f);
            if (servePending)
            {
                serveDelay -= deltaTime;
                if (serveDelay <= 0f) BeginServe();
            }
            if (messageDelay > 0f) messageDelay -= deltaTime;
            if (dashCooldown > 0f) dashCooldown -= deltaTime;

            MovePlayer(deltaTime);
            MoveRival(deltaTime);
            if (flightActive) UpdateShuttlecock(deltaTime);
            if (flightActive && playerMustReturn && IsShotInput() && !MouseIsOverHud()) TryPlayerShot();
        }

        void MovePlayer(float deltaTime)
        {
            float horizontal = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                             - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            float vertical = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                           - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
            Vector3 direction = new Vector3(horizontal, 0f, vertical);
            if (direction.sqrMagnitude > 1f) direction.Normalize();

            if (Input.GetKeyDown(KeyCode.Space) && jumpHeight <= .01f)
                jumpVelocity = 5.1f;
            if (Input.GetKeyDown(KeyCode.LeftShift) && dashCooldown <= 0f)
            {
                dashDirection = direction.sqrMagnitude > .01f ? direction : Vector3.forward;
                dashTime = .16f;
                dashCooldown = 1.05f;
                isDashing = true;
            }

            float speed = isDashing ? 12f : 4.6f;
            if (dashTime > 0f)
            {
                direction = dashDirection;
                dashTime -= deltaTime;
            }
            else isDashing = false;

            Vector3 next = player.position + direction * speed * deltaTime;
            jumpHeight += jumpVelocity * deltaTime;
            jumpVelocity -= 13.5f * deltaTime;
            if (jumpHeight <= 0f)
            {
                jumpHeight = 0f;
                jumpVelocity = 0f;
            }

            next.x = Mathf.Clamp(next.x, -CourtHalfWidth + .35f, CourtHalfWidth - .35f);
            next.z = Mathf.Clamp(next.z, -CourtHalfLength + .45f, -.42f);
            next.y = GroundY + jumpHeight;
            player.position = next;
        }

        void MoveRival(float deltaTime)
        {
            if (!flightActive || playerMustReturn) return;
            Vector3 destination = new Vector3(flightTarget.x, GroundY, flightTarget.z + .25f);
            destination.x = Mathf.Clamp(destination.x, -CourtHalfWidth + .35f, CourtHalfWidth - .35f);
            destination.z = Mathf.Clamp(destination.z, .42f, CourtHalfLength - .45f);
            rival.position = Vector3.MoveTowards(rival.position, destination, 5.1f * deltaTime);
        }

        void UpdateShuttlecock(float deltaTime)
        {
            previousShuttlePosition = shuttle.position;
            flightElapsed += deltaTime;
            float time = Mathf.Min(flightElapsed, flightDuration);
            Vector3 current = flightStart + flightVelocity * time + Vector3.down * (.5f * Gravity * time * time);
            shuttle.position = current;
            Vector3 velocity = flightVelocity + Vector3.down * (Gravity * time);
            if (velocity.sqrMagnitude > .01f) shuttle.up = velocity.normalized;

            if (CrossedNet(previousShuttlePosition, current, out float netCrossingHeight) && netCrossingHeight < GroundY + NetHeight)
            {
                if (flightWasPlayerShot) AwardRivalPoint("The shuttle clipped the net.");
                else AwardPlayerPoint("Net fault.");
                return;
            }

            if (flightElapsed < flightDuration) return;
            shuttle.position = flightTarget;
            if (playerMustReturn)
            {
                AwardRivalPoint("The shuttle landed before your return.");
                return;
            }

            float rivalDistance = HorizontalDistance(rival.position, flightTarget);
            if (rivalDistance > 1.05f)
            {
                AwardPlayerPoint("Clean winner.");
                return;
            }
            BeginRivalReturn();
        }

        void TryPlayerShot()
        {
            if (shuttle.position.z >= -.15f) return;
            float reach = 1.7f + jumpHeight * .5f;
            if (HorizontalDistance(player.position, shuttle.position) > reach || shuttle.position.y > GroundY + 2.9f + jumpHeight)
            {
                ShowMessage("Move closer to the shuttle.", .8f);
                return;
            }

            ShotStyle style = CurrentShotStyle();
            if (style == ShotStyle.Smash && jumpHeight < .8f)
            {
                ShowMessage("Jump before a smash.", .8f);
                return;
            }

            Vector3 target = AimTarget(style);
            float duration = style == ShotStyle.Clear ? 1.48f
                           : style == ShotStyle.Drop ? 1.27f
                           : style == ShotStyle.Drive ? 1.22f
                           : .83f;
            Launch(shuttle.position, target, duration, false, true);
            rallyHits++;
            string hit = style == ShotStyle.Clear ? "Aqua clear"
                       : style == ShotStyle.Drop ? "Feather drop"
                       : style == ShotStyle.Drive ? "Glass drive" : "Sky smash";
            ShowMessage(hit + "  •  Rally " + rallyHits, .85f);
        }

        Vector3 AimTarget(ShotStyle style)
        {
            float minZ = style == ShotStyle.Drop ? 1.25f : style == ShotStyle.Drive ? 2.2f : 3.6f;
            float maxZ = style == ShotStyle.Drop ? 3.1f : CourtHalfLength - .45f;
            Vector3 target = new Vector3(-player.position.x * .45f, GroundY + .11f, 5.65f);
            Camera camera = Camera.main;
            if (camera)
            {
                Ray ray = camera.ScreenPointToRay(Input.mousePosition);
                Plane court = new Plane(Vector3.up, new Vector3(0f, GroundY + .11f, 0f));
                if (court.Raycast(ray, out float distance)) target = ray.GetPoint(distance);
            }
            target.x = Mathf.Clamp(target.x, -CourtHalfWidth + .35f, CourtHalfWidth - .35f);
            target.z = Mathf.Clamp(target.z, minZ, maxZ);
            target.y = GroundY + .11f;
            return target;
        }

        void BeginServe()
        {
            servePending = false;
            rallyHits = 0;
            Vector3 start = rival.position + Vector3.up * 1.45f;
            Vector3 target = new Vector3(Mathf.Clamp(player.position.x + Random.Range(-1.1f, 1.1f), -2.2f, 2.2f), GroundY + .11f, Random.Range(-5.7f, -2.1f));
            Launch(start, target, 1.42f, true, false);
            ShowMessage("Return the serve.", 1.1f);
        }

        void BeginRivalReturn()
        {
            float targetZ = Mathf.Clamp(player.position.z + Random.Range(-1.9f, 1.9f), -5.9f, -1.35f);
            float targetX = Mathf.Clamp(player.position.x + Random.Range(-1.6f, 1.6f), -2.45f, 2.45f);
            Vector3 start = rival.position + Vector3.up * 1.4f;
            Vector3 target = new Vector3(targetX, GroundY + .11f, targetZ);
            Launch(start, target, Random.Range(1.24f, 1.43f), true, false);
        }

        void Launch(Vector3 start, Vector3 target, float duration, bool receiverIsPlayer, bool playerShot)
        {
            flightStart = start;
            flightTarget = target;
            flightDuration = duration;
            flightElapsed = 0f;
            playerMustReturn = receiverIsPlayer;
            flightWasPlayerShot = playerShot;
            flightActive = true;
            float verticalSpeed = (target.y - start.y + .5f * Gravity * duration * duration) / duration;
            flightVelocity = new Vector3((target.x - start.x) / duration, verticalSpeed, (target.z - start.z) / duration);
            previousShuttlePosition = start;
            shuttle.position = start;
            shuttle.up = flightVelocity.normalized;
            shuttleTrail.Clear();
        }

        void AwardPlayerPoint(string result)
        {
            playerScore++;
            FinishPoint("Point  " + playerScore + " — " + rivalScore + "  " + result);
        }

        void AwardRivalPoint(string result)
        {
            rivalScore++;
            FinishPoint(result + "  " + playerScore + " — " + rivalScore);
        }

        void FinishPoint(string result)
        {
            flightActive = false;
            playerMustReturn = false;
            shuttleTrail.Clear();
            shuttle.position = new Vector3(0f, GroundY + .25f, 0f);
            servePending = true;
            serveDelay = 1.45f;
            ShowMessage(result, 1.45f);
        }

        void ShowMessage(string text, float duration)
        {
            message = text;
            messageDelay = duration;
        }

        bool IsShotInput()
        {
            return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)
                || Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.R);
        }

        bool MouseIsOverHud()
        {
            Vector3 mouse = Input.mousePosition;
            return mouse.x < 290f && mouse.y > Screen.height - 170f;
        }

        ShotStyle CurrentShotStyle()
        {
            if (Input.GetMouseButtonDown(1)) return ShotStyle.Drop;
            if (Input.GetKeyDown(KeyCode.F)) return ShotStyle.Drive;
            if (Input.GetKeyDown(KeyCode.R)) return ShotStyle.Smash;
            return ShotStyle.Clear;
        }

        void CreateShuttlecock()
        {
            var root = new GameObject("Generated shuttlecock");
            ownedObjects.Add(root);
            shuttle = root.transform;
            ownedObjects.Add(BuildFeathers());
            shuttleMaterial = CreateMaterial("Universal Render Pipeline/Lit", new Color(.88f, .99f, 1f), .3f, .35f);
            corkMaterial = CreateMaterial("Universal Render Pipeline/Lit", new Color(.96f, .81f, .62f), .25f, .05f);
            bandMaterial = CreateMaterial("Universal Render Pipeline/Lit", new Color(.1f, .79f, .89f), .5f, .75f);

            var skirt = new GameObject("Generated feather skirt");
            skirt.transform.SetParent(shuttle, false);
            skirt.AddComponent<MeshFilter>().sharedMesh = (Mesh)ownedObjects[0];
            var skirtRenderer = skirt.AddComponent<MeshRenderer>();
            skirtRenderer.sharedMaterial = shuttleMaterial;
            skirtRenderer.shadowCastingMode = ShadowCastingMode.Off;

            var cork = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cork.name = "Generated shuttle cork";
            Destroy(cork.GetComponent<Collider>());
            cork.transform.SetParent(shuttle, false);
            cork.transform.localPosition = Vector3.zero;
            cork.transform.localScale = new Vector3(.13f, .17f, .13f);
            cork.GetComponent<Renderer>().sharedMaterial = corkMaterial;

            var band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            band.name = "Aqua cork band";
            Destroy(band.GetComponent<Collider>());
            band.transform.SetParent(shuttle, false);
            band.transform.localPosition = new Vector3(0f, -.105f, 0f);
            band.transform.localScale = new Vector3(.072f, .025f, .072f);
            band.GetComponent<Renderer>().sharedMaterial = bandMaterial;

            trailMaterial = CreateMaterial("Universal Render Pipeline/Unlit", new Color(.31f, .98f, 1f), 0f, .8f);
            shuttleTrail = root.AddComponent<TrailRenderer>();
            shuttleTrail.time = .24f;
            shuttleTrail.minVertexDistance = .09f;
            shuttleTrail.startWidth = .075f;
            shuttleTrail.endWidth = .005f;
            shuttleTrail.startColor = new Color(.33f, .98f, 1f, .72f);
            shuttleTrail.endColor = new Color(.35f, .94f, 1f, 0f);
            shuttleTrail.sharedMaterial = trailMaterial;
            shuttleTrail.shadowCastingMode = ShadowCastingMode.Off;
        }

        Mesh BuildFeathers()
        {
            const int featherCount = 8;
            var vertices = new List<Vector3>(featherCount * 6);
            var colors = new List<Color>(featherCount * 6);
            var triangles = new List<int>(featherCount * 24);
            float[] heights = { -.12f, -.34f, -.62f };
            float[] radii = { .055f, .17f, .325f };
            float[] widths = { .028f, .064f, .082f };
            Color baseColor = new Color(.22f, .85f, .96f);
            Color tipColor = new Color(.97f, 1f, 1f);

            for (int feather = 0; feather < featherCount; feather++)
            {
                float angle = feather * Mathf.PI * 2f / featherCount;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                int first = vertices.Count;
                for (int row = 0; row < 3; row++)
                {
                    Vector3 center = radial * radii[row];
                    vertices.Add(new Vector3(center.x - tangent.x * widths[row], heights[row], center.z - tangent.z * widths[row]));
                    vertices.Add(new Vector3(center.x + tangent.x * widths[row], heights[row], center.z + tangent.z * widths[row]));
                    Color color = Color.Lerp(baseColor, tipColor, row * .5f);
                    colors.Add(color); colors.Add(color);
                }
                for (int row = 0; row < 2; row++)
                {
                    int left = first + row * 2, right = left + 1, nextLeft = left + 2, nextRight = left + 3;
                    AddDoubleSidedQuad(triangles, left, right, nextRight, nextLeft);
                }
            }

            var mesh = new Mesh { name = "Generated badminton shuttle feathers" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        static void AddDoubleSidedQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
            triangles.Add(c); triangles.Add(b); triangles.Add(a);
            triangles.Add(d); triangles.Add(c); triangles.Add(a);
        }

        Material CreateMaterial(string shaderName, Color color, float smoothness, float emission)
        {
            Shader shader = Shader.Find(shaderName) ?? Shader.Find("Standard");
            var material = new Material(shader) { name = "Rally " + ColorUtility.ToHtmlStringRGBA(color) };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color); else material.color = color;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }
            ownedObjects.Add(material);
            return material;
        }

        void OnGUI()
        {
            if (scoreStyle == null)
            {
                scoreStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                scoreStyle.normal.textColor = Color.white;
                labelStyle.normal.textColor = new Color(.82f, .98f, 1f);
            }

            Color previous = GUI.color;
            GUI.color = new Color(.025f, .15f, .2f, .84f);
            GUI.Box(new Rect(16f, 16f, 272f, 158f), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(28f, 22f, 250f, 30f), "FRUTIGER BADMINTON", labelStyle);
            GUI.Label(new Rect(28f, 52f, 250f, 28f), "WASD / arrows  Move", labelStyle);
            GUI.Label(new Rect(28f, 78f, 250f, 28f), "Space  Jump     Shift  Dash", labelStyle);
            GUI.Label(new Rect(28f, 104f, 250f, 28f), "Aim + click  Clear     Right click  Drop", labelStyle);
            GUI.Label(new Rect(28f, 130f, 250f, 28f), "F  Drive     R  Smash (airborne)", labelStyle);

            GUI.color = new Color(.02f, .23f, .29f, .9f);
            GUI.Box(new Rect(Screen.width * .5f - 118f, 18f, 236f, 48f), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width * .5f - 110f, 21f, 220f, 42f), playerScore + "   —   " + rivalScore, scoreStyle);

            if (messageDelay > 0f)
            {
                GUI.color = new Color(.02f, .16f, .22f, .82f);
                GUI.Box(new Rect(Screen.width * .5f - 235f, Screen.height - 76f, 470f, 48f), GUIContent.none);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * .5f - 225f, Screen.height - 72f, 450f, 40f), message, scoreStyle);
            }
            GUI.color = previous;
        }

        void OnDestroy()
        {
            foreach (Object owned in ownedObjects)
                if (owned) Destroy(owned);
        }

        static bool CrossedNet(Vector3 from, Vector3 to, out float crossingHeight)
        {
            crossingHeight = 0f;
            if (!((from.z < 0f && to.z >= 0f) || (from.z > 0f && to.z <= 0f))) return false;
            float fraction = -from.z / (to.z - from.z);
            crossingHeight = Mathf.Lerp(from.y, to.y, fraction);
            return true;
        }

        static float HorizontalDistance(Vector3 first, Vector3 second)
        {
            float dx = first.x - second.x;
            float dz = first.z - second.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        enum ShotStyle { Clear, Drop, Drive, Smash }
    }
}
