using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace FrutigerBadminton
{
    /// <summary>Runs a local doubles practice rally using generated characters and controller-first input.</summary>
    public sealed class AeroRallyController : MonoBehaviour
    {
        const float GroundY = -.72f;
        const float CourtHalfWidth = 3.05f;
        const float CourtHalfLength = 6.7f;
        const float NetHeight = 1.56f;
        const float Gravity = 9.81f;
        const int UpgradeScoreInterval = 3;
        const int MaxUpgradeLevel = 5;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly string[] UpgradeNames =
        {
            "Aero Laces", "Extended Sweet Spot", "Partner Drills", "Turbo Dash", "Shuttle Sense"
        };
        static readonly string[] UpgradeDescriptions =
        {
            "Move faster around the court.",
            "Return shots from farther away.",
            "Your partner runs and reaches farther.",
            "Dash faster and recover sooner.",
            "See the landing reticle turn urgent earlier."
        };

        readonly List<Object> ownedObjects = new List<Object>();
        readonly int[] upgradeLevels = new int[5];
        readonly SoloUpgrade[] upgradeOffers = new SoloUpgrade[3];
        readonly MaterialPropertyBlock landingMarkerProperties = new MaterialPropertyBlock();
        Transform player;
        Transform partner;
        Transform rival;
        Transform shuttle;
        Transform landingMarker;
        AeroPlayerAnimation playerAnimation;
        AeroPlayerAnimation partnerAnimation;
        AeroPlayerAnimation rivalAnimation;
        TrailRenderer shuttleTrail;
        TrailRenderer dashTrail;
        Renderer landingMarkerRenderer;
        Mesh landingMarkerMesh;
        Material shuttleMaterial;
        Material corkMaterial;
        Material bandMaterial;
        Material trailMaterial;
        GUIStyle scoreStyle;
        GUIStyle labelStyle;
        GUIStyle upgradeButtonStyle;
        GUIStyle upgradeDescriptionStyle;

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
        int nextUpgradeScore = UpgradeScoreInterval;
        int upgradeOfferCount;
        int selectedUpgrade;
        int upgradeInputFrame = -1;
        bool playerMustReturn;
        bool flightWasPlayerShot;
        bool flightActive;
        bool servePending;
        bool isDashing;
        bool upgradeDraftActive;
        string message = "Move under the return, then time your shot.";

        void Start()
        {
            var playerObject = GameObject.Find("Player silhouette");
            var partnerObject = GameObject.Find("Partner silhouette");
            var rivalObject = GameObject.Find("Rival silhouette");
            if (!playerObject || !partnerObject || !rivalObject)
            {
                Debug.LogError("The Aero Court scene needs generated player, partner, and rival silhouettes before the rally can start.");
                enabled = false;
                return;
            }

            player = playerObject.transform;
            partner = partnerObject.transform;
            rival = rivalObject.transform;
            playerAnimation = playerObject.GetComponent<AeroPlayerAnimation>();
            partnerAnimation = partnerObject.GetComponent<AeroPlayerAnimation>();
            rivalAnimation = rivalObject.GetComponent<AeroPlayerAnimation>();
            player.position = new Vector3(0f, GroundY, -4.6f);
            partner.position = new Vector3(-1.65f, GroundY, -4.8f);
            rival.position = new Vector3(0f, GroundY, 4.6f);
            CreateShuttlecock();
            CreateLandingMarker();
            CreateDashTrail();
            BeginServe();
        }

        void Update()
        {
            if (upgradeDraftActive) return;

            float deltaTime = Mathf.Min(Time.deltaTime, .05f);
            if (servePending)
            {
                serveDelay -= deltaTime;
                if (serveDelay <= 0f) BeginServe();
            }
            if (messageDelay > 0f) messageDelay -= deltaTime;
            if (dashCooldown > 0f) dashCooldown -= deltaTime;

            MovePlayer(deltaTime);
            MovePartner(deltaTime);
            MoveRival(deltaTime);
            if (flightActive)
            {
                UpdateShuttlecock(deltaTime);
                if (flightActive && playerMustReturn) TryPartnerReturn();
            }
            UpdateLandingMarker();
            if (flightActive && playerMustReturn && IsShotInput()
                && (!IsMouseShotInput() || !MouseIsOverHud())) TryPlayerShot();
        }

        void MovePlayer(float deltaTime)
        {
            Gamepad gamepad = Gamepad.current;
            Vector2 directionInput = gamepad != null ? gamepad.leftStick.ReadValue() : Vector2.zero;
            if (gamepad != null && gamepad.dpad.ReadValue().sqrMagnitude > directionInput.sqrMagnitude)
                directionInput = gamepad.dpad.ReadValue();

            Vector2 keyboardInput = new Vector2(
                (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f),
                (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f));
            if (keyboardInput.sqrMagnitude > directionInput.sqrMagnitude) directionInput = keyboardInput;
            directionInput = Vector2.ClampMagnitude(directionInput, 1f);
            Vector3 direction = new Vector3(directionInput.x, 0f, directionInput.y);

            if ((Input.GetKeyDown(KeyCode.Space) || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)) && jumpHeight <= .01f)
                jumpVelocity = 5.1f;
            if ((Input.GetKeyDown(KeyCode.LeftShift) || (gamepad != null && gamepad.rightShoulder.wasPressedThisFrame)) && dashCooldown <= 0f)
            {
                dashDirection = direction.sqrMagnitude > .01f ? direction.normalized : Vector3.forward;
                dashTime = .16f;
                dashCooldown = Mathf.Max(.55f, 1.05f - upgradeLevels[(int)SoloUpgrade.TurboDash] * .1f);
                isDashing = true;
                if (dashTrail)
                {
                    dashTrail.Clear();
                    dashTrail.emitting = true;
                }
                AeroHitEffect.Spawn(player.position + Vector3.up * .82f, new Color(.26f, .96f, 1f), .82f);
            }

            float speed = isDashing ? 12f + upgradeLevels[(int)SoloUpgrade.TurboDash] * .65f : 4.6f;
            speed *= 1f + upgradeLevels[(int)SoloUpgrade.AeroLaces] * .07f;
            if (dashTime > 0f)
            {
                direction = dashDirection;
                dashTime -= deltaTime;
                if (dashTime <= 0f && dashTrail) dashTrail.emitting = false;
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

        void MovePartner(float deltaTime)
        {
            if (!partner) return;

            Vector3 destination = new Vector3(player.position.x >= 0f ? -1.65f : 1.65f, GroundY, -4.8f);
            if (flightActive && playerMustReturn)
            {
                float timeToLanding = Mathf.Max(0f, flightDuration - flightElapsed);
                float playerDistance = HorizontalDistance(player.position, flightTarget);
                float partnerDistance = HorizontalDistance(partner.position, flightTarget);
                float playerReach = 1.7f + upgradeLevels[(int)SoloUpgrade.ExtendedSweetSpot] * .16f + jumpHeight * .5f;
                bool playerCannotReach = playerDistance - playerReach > timeToLanding * 4.6f;
                bool partnerHasCoverageAdvantage = partnerDistance + .85f < playerDistance;
                if (playerCannotReach || partnerHasCoverageAdvantage)
                {
                    destination.x = Mathf.Clamp(flightTarget.x, -CourtHalfWidth + .45f, CourtHalfWidth - .45f);
                    destination.z = Mathf.Clamp(flightTarget.z, -CourtHalfLength + .45f, -.42f);
                }
            }

            float partnerSpeed = 5.2f + upgradeLevels[(int)SoloUpgrade.PartnerDrills] * .48f;
            partner.position = Vector3.MoveTowards(partner.position, destination, partnerSpeed * deltaTime);
        }

        bool TryPartnerReturn()
        {
            if (!partner || !flightActive || !playerMustReturn || shuttle.position.z >= -.15f) return false;

            float partnerReach = 1.5f + upgradeLevels[(int)SoloUpgrade.PartnerDrills] * .1f;
            bool playerCanReturn = HorizontalDistance(player.position, shuttle.position)
                <= 1.7f + upgradeLevels[(int)SoloUpgrade.ExtendedSweetSpot] * .16f + jumpHeight * .5f
                && shuttle.position.y <= GroundY + 2.9f + jumpHeight;
            if (playerCanReturn || shuttle.position.y > GroundY + 2.85f
                || HorizontalDistance(partner.position, shuttle.position) > partnerReach) return false;

            Vector3 target = new Vector3(
                Mathf.Clamp(-rival.position.x * .55f + Random.Range(-.85f, .85f), -CourtHalfWidth + .35f, CourtHalfWidth - .35f),
                GroundY + .11f,
                Random.Range(4.25f, 5.85f));
            AeroHitEffect.Spawn(shuttle.position, new Color(.73f, 1f, .48f), .98f);
            Launch(shuttle.position, target, Random.Range(1.16f, 1.36f), false, true);
            if (partnerAnimation) partnerAnimation.Swing(false);
            rallyHits++;
            ShowMessage("Partner return  •  Rally " + rallyHits, .9f);
            return true;
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
                if (TryPartnerReturn()) return;
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
            float reach = 1.7f + upgradeLevels[(int)SoloUpgrade.ExtendedSweetSpot] * .16f + jumpHeight * .5f;
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
            AeroHitEffect.Spawn(shuttle.position, ShotTint(style), style == ShotStyle.Smash ? 1.55f : 1f);
            Launch(shuttle.position, target, duration, false, true);
            if (playerAnimation) playerAnimation.Swing(style == ShotStyle.Smash);
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
            Gamepad gamepad = Gamepad.current;
            Vector2 stickAim = gamepad != null ? gamepad.rightStick.ReadValue() : Vector2.zero;
            if (gamepad != null)
            {
                if (stickAim.sqrMagnitude > .035f)
                {
                    target.x = stickAim.x * (CourtHalfWidth - .35f);
                    target.z = Mathf.Lerp(minZ, maxZ, Mathf.Clamp01((stickAim.y + 1f) * .5f));
                }
            }
            else if (camera && Mouse.current != null)
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
            AeroHitEffect.Spawn(start, new Color(.42f, .96f, 1f), .9f);
            Launch(start, target, 1.42f, true, false);
            if (rivalAnimation) rivalAnimation.Swing(false);
            ShowMessage("Return the serve.", 1.1f);
        }

        void BeginRivalReturn()
        {
            float targetZ = Mathf.Clamp(player.position.z + Random.Range(-1.9f, 1.9f), -5.9f, -1.35f);
            float targetX = Mathf.Clamp(player.position.x + Random.Range(-1.6f, 1.6f), -2.45f, 2.45f);
            Vector3 start = rival.position + Vector3.up * 1.4f;
            Vector3 target = new Vector3(targetX, GroundY + .11f, targetZ);
            AeroHitEffect.Spawn(start, new Color(1f, .72f, .63f), .95f);
            Launch(start, target, Random.Range(1.24f, 1.43f), true, false);
            if (rivalAnimation) rivalAnimation.Swing(false);
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
            bool reachedUpgradeMilestone = playerScore >= nextUpgradeScore;
            if (reachedUpgradeMilestone) nextUpgradeScore += UpgradeScoreInterval;
            FinishPoint("Point  " + playerScore + " — " + rivalScore + "  " + result, reachedUpgradeMilestone);
        }

        void AwardRivalPoint(string result)
        {
            rivalScore++;
            FinishPoint(result + "  " + playerScore + " — " + rivalScore);
        }

        void FinishPoint(string result, bool offerUpgrade = false)
        {
            flightActive = false;
            playerMustReturn = false;
            shuttleTrail.Clear();
            shuttle.position = new Vector3(0f, GroundY + .25f, 0f);
            servePending = true;
            serveDelay = 1.45f;
            ShowMessage(result, 1.45f);
            if (offerUpgrade) BeginUpgradeDraft();
        }

        void BeginUpgradeDraft()
        {
            var candidates = new List<SoloUpgrade>(upgradeLevels.Length);
            for (int i = 0; i < upgradeLevels.Length; i++)
                if (upgradeLevels[i] < MaxUpgradeLevel) candidates.Add((SoloUpgrade)i);

            upgradeOfferCount = Mathf.Min(upgradeOffers.Length, candidates.Count);
            if (upgradeOfferCount == 0) return;

            for (int i = 0; i < upgradeOfferCount; i++)
            {
                int choice = Random.Range(0, candidates.Count);
                upgradeOffers[i] = candidates[choice];
                candidates.RemoveAt(choice);
            }

            selectedUpgrade = 0;
            upgradeInputFrame = -1;
            upgradeDraftActive = true;
            servePending = false;
        }

        void HandleUpgradeInput()
        {
            if (upgradeInputFrame == Time.frameCount) return;
            upgradeInputFrame = Time.frameCount;

            int keyboardChoice = Input.GetKeyDown(KeyCode.Alpha1) ? 0
                : Input.GetKeyDown(KeyCode.Alpha2) ? 1
                : Input.GetKeyDown(KeyCode.Alpha3) ? 2 : -1;
            if (keyboardChoice >= 0 && keyboardChoice < upgradeOfferCount)
            {
                ChooseUpgrade(keyboardChoice);
                return;
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                if (gamepad.dpad.left.wasPressedThisFrame)
                    selectedUpgrade = (selectedUpgrade + upgradeOfferCount - 1) % upgradeOfferCount;
                else if (gamepad.dpad.right.wasPressedThisFrame)
                    selectedUpgrade = (selectedUpgrade + 1) % upgradeOfferCount;
                else if (gamepad.buttonSouth.wasPressedThisFrame)
                    ChooseUpgrade(selectedUpgrade);
            }
        }

        void ChooseUpgrade(int offerIndex)
        {
            if (!upgradeDraftActive || offerIndex < 0 || offerIndex >= upgradeOfferCount) return;
            SoloUpgrade upgrade = upgradeOffers[offerIndex];
            upgradeLevels[(int)upgrade]++;
            upgradeDraftActive = false;
            servePending = true;
            serveDelay = .65f;
            ShowMessage(UpgradeNames[(int)upgrade] + " upgraded  •  Level " + upgradeLevels[(int)upgrade], 1.2f);
        }

        void ShowMessage(string text, float duration)
        {
            message = text;
            messageDelay = duration;
        }

        bool IsShotInput()
        {
            return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)
                || Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.R)
                || IsGamepadShotInput();
        }

        bool IsMouseShotInput()
        {
            return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
        }

        bool IsGamepadShotInput()
        {
            Gamepad gamepad = Gamepad.current;
            return gamepad != null && (gamepad.leftShoulder.wasPressedThisFrame
                || gamepad.buttonWest.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame
                || gamepad.buttonNorth.wasPressedThisFrame);
        }

        bool MouseIsOverHud()
        {
            Vector3 mouse = Input.mousePosition;
            return mouse.x < 290f && mouse.y > Screen.height - 170f;
        }

        ShotStyle CurrentShotStyle()
        {
            Gamepad gamepad = Gamepad.current;
            if (Input.GetMouseButtonDown(1) || (gamepad != null && gamepad.buttonWest.wasPressedThisFrame)) return ShotStyle.Drop;
            if (Input.GetKeyDown(KeyCode.F) || (gamepad != null && gamepad.buttonEast.wasPressedThisFrame)) return ShotStyle.Drive;
            if (Input.GetKeyDown(KeyCode.R) || (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame)) return ShotStyle.Smash;
            return ShotStyle.Clear;
        }

        Color ShotTint(ShotStyle style)
        {
            if (style == ShotStyle.Drop) return new Color(.57f, 1f, .8f);
            if (style == ShotStyle.Drive) return new Color(1f, .52f, .72f);
            if (style == ShotStyle.Smash) return new Color(1f, .84f, .48f);
            return new Color(.34f, .97f, 1f);
        }

        void CreateShuttlecock()
        {
            var root = new GameObject("Generated shuttlecock");
            ownedObjects.Add(root);
            shuttle = root.transform;
            Mesh featherMesh = BuildFeathers();
            ownedObjects.Add(featherMesh);
            shuttleMaterial = CreateMaterial("Universal Render Pipeline/Lit", new Color(.88f, .99f, 1f), .3f, .35f);
            corkMaterial = CreateMaterial("Universal Render Pipeline/Lit", new Color(.96f, .81f, .62f), .25f, .05f);
            bandMaterial = CreateMaterial("Universal Render Pipeline/Lit", new Color(.1f, .79f, .89f), .5f, .75f);

            var skirt = new GameObject("Generated feather skirt");
            skirt.transform.SetParent(shuttle, false);
            skirt.AddComponent<MeshFilter>().sharedMesh = featherMesh;
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

        void CreateLandingMarker()
        {
            var marker = new GameObject("Predicted shuttle landing marker");
            ownedObjects.Add(marker);
            landingMarker = marker.transform;
            landingMarker.position = new Vector3(0f, GroundY + .12f, 0f);

            landingMarkerMesh = BuildLandingMarkerMesh();
            ownedObjects.Add(landingMarkerMesh);
            marker.AddComponent<MeshFilter>().sharedMesh = landingMarkerMesh;
            landingMarkerRenderer = marker.AddComponent<MeshRenderer>();
            landingMarkerRenderer.sharedMaterial = CreateMaterial("Universal Render Pipeline/Lit", new Color(.36f, .96f, 1f), .82f, .4f);
            landingMarkerRenderer.shadowCastingMode = ShadowCastingMode.Off;
            landingMarkerRenderer.receiveShadows = false;
            marker.SetActive(false);
        }

        void CreateDashTrail()
        {
            var anchor = new GameObject("Dash trail anchor").transform;
            anchor.SetParent(player, false);
            anchor.localPosition = Vector3.up * .92f;

            var trailMaterial = CreateMaterial("Universal Render Pipeline/Particles/Unlit", new Color(.3f, .95f, 1f), 0f, 1.1f);
            if (trailMaterial.HasProperty("_Surface")) trailMaterial.SetFloat("_Surface", 1f);
            if (trailMaterial.HasProperty("_SrcBlend")) trailMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (trailMaterial.HasProperty("_DstBlend")) trailMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (trailMaterial.HasProperty("_ZWrite")) trailMaterial.SetFloat("_ZWrite", 0f);
            trailMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            trailMaterial.renderQueue = (int)RenderQueue.Transparent;

            dashTrail = anchor.gameObject.AddComponent<TrailRenderer>();
            dashTrail.time = .24f;
            dashTrail.minVertexDistance = .035f;
            dashTrail.startWidth = .38f;
            dashTrail.endWidth = .012f;
            dashTrail.startColor = new Color(.66f, 1f, 1f, .88f);
            dashTrail.endColor = new Color(.27f, .88f, 1f, 0f);
            dashTrail.sharedMaterial = trailMaterial;
            dashTrail.alignment = LineAlignment.View;
            dashTrail.textureMode = LineTextureMode.Stretch;
            dashTrail.shadowCastingMode = ShadowCastingMode.Off;
            dashTrail.receiveShadows = false;
            dashTrail.emitting = false;
        }

        void UpdateLandingMarker()
        {
            bool show = flightActive && playerMustReturn;
            if (landingMarker.gameObject.activeSelf != show) landingMarker.gameObject.SetActive(show);
            if (!show) return;

            landingMarker.position = new Vector3(flightTarget.x, GroundY + .12f, flightTarget.z);
            float timeToLanding = Mathf.Max(0f, flightDuration - flightElapsed);
            float warningLead = 1.35f + upgradeLevels[(int)SoloUpgrade.ShuttleSense] * .16f;
            float urgency = Mathf.InverseLerp(warningLead, .2f, timeToLanding);
            float pulse = Mathf.Sin(Time.time * Mathf.Lerp(1.35f, 3.2f, urgency) * Mathf.PI * 2f) * .5f + .5f;
            float scale = Mathf.Lerp(.93f, 1.08f, pulse) * Mathf.Lerp(1f, 1.1f, urgency);
            landingMarker.localScale = Vector3.one * scale;

            Color aqua = new Color(.3f, .98f, 1f);
            Color coral = new Color(1f, .53f, .43f);
            Color tint = Color.Lerp(aqua, coral, urgency * .72f);
            float brightness = Mathf.Lerp(.65f, 1f, urgency) * Mathf.Lerp(.82f, 1.12f, pulse);
            landingMarkerProperties.Clear();
            landingMarkerProperties.SetColor(BaseColorId, tint * brightness);
            landingMarkerProperties.SetColor(ColorId, tint * brightness);
            landingMarkerProperties.SetColor(EmissionColorId, tint * Mathf.Lerp(.55f, 1.5f, urgency) * pulse);
            landingMarkerRenderer.SetPropertyBlock(landingMarkerProperties);
        }

        static Mesh BuildLandingMarkerMesh()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            AddSegmentedRing(vertices, triangles, .82f, .76f, 12, .12f);
            AddSegmentedRing(vertices, triangles, .46f, .42f, 8, .2f);

            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * .5f;
                AddSegment(vertices, triangles, .98f, .76f, angle - .045f, angle + .045f);
            }

            int first = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, .14f));
            vertices.Add(new Vector3(.11f, 0f, 0f));
            vertices.Add(new Vector3(0f, 0f, -.14f));
            vertices.Add(new Vector3(-.11f, 0f, 0f));
            triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);

            var mesh = new Mesh { name = "Generated shuttle landing reticle" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        static void AddSegmentedRing(List<Vector3> vertices, List<int> triangles, float outerRadius, float innerRadius, int count, float gap)
        {
            float segmentAngle = Mathf.PI * 2f / count;
            for (int i = 0; i < count; i++)
            {
                float start = i * segmentAngle + gap * .5f;
                float end = (i + 1) * segmentAngle - gap * .5f;
                AddSegment(vertices, triangles, outerRadius, innerRadius, start, end);
            }
        }

        static void AddSegment(List<Vector3> vertices, List<int> triangles, float outerRadius, float innerRadius, float start, float end)
        {
            int first = vertices.Count;
            vertices.Add(Polar(outerRadius, start));
            vertices.Add(Polar(outerRadius, end));
            vertices.Add(Polar(innerRadius, end));
            vertices.Add(Polar(innerRadius, start));
            triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 1);
            triangles.Add(first); triangles.Add(first + 3); triangles.Add(first + 2);
        }

        static Vector3 Polar(float radius, float angle)
        {
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
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
                upgradeButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
                upgradeDescriptionStyle = new GUIStyle(labelStyle)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = true
                };
                scoreStyle.normal.textColor = Color.white;
                labelStyle.normal.textColor = new Color(.82f, .98f, 1f);
                upgradeDescriptionStyle.normal.textColor = new Color(.82f, .98f, 1f);
                upgradeButtonStyle.normal.textColor = new Color(.03f, .18f, .2f);
            }

            Color previous = GUI.color;
            GUI.color = new Color(.025f, .15f, .2f, .84f);
            GUI.Box(new Rect(16f, 16f, 320f, 204f), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(28f, 22f, 298f, 30f), "FRUTIGER BADMINTON", labelStyle);
            bool controllerConnected = Gamepad.current != null;
            GUI.Label(new Rect(28f, 52f, 298f, 24f), controllerConnected ? "Left stick / D-pad   Move" : "WASD / arrows   Move", labelStyle);
            GUI.Label(new Rect(28f, 76f, 298f, 24f), controllerConnected ? "Right stick   Aim" : "Mouse   Aim", labelStyle);
            GUI.Label(new Rect(28f, 100f, 298f, 24f), controllerConnected ? "A   Jump       RB   Dash" : "Space   Jump       Shift   Dash", labelStyle);
            GUI.Label(new Rect(28f, 124f, 298f, 24f), controllerConnected ? "LB   Clear       X   Drop" : "Click   Clear       Right click   Drop", labelStyle);
            GUI.Label(new Rect(28f, 148f, 298f, 24f), controllerConnected ? "B   Drive       Y   Smash" : "F   Drive       R   Smash (airborne)", labelStyle);
            GUI.Label(new Rect(28f, 172f, 298f, 24f), "Follow the glowing ring to the landing spot", labelStyle);
            GUI.Label(new Rect(28f, 194f, 298f, 24f), "Choose a run upgrade every 3 points", labelStyle);

            GUI.color = new Color(.02f, .23f, .29f, .9f);
            GUI.Box(new Rect(Screen.width * .5f - 118f, 18f, 236f, 48f), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width * .5f - 110f, 21f, 220f, 42f), playerScore + "   —   " + rivalScore, scoreStyle);

            if (upgradeDraftActive) DrawUpgradeDraft(controllerConnected);

            if (!upgradeDraftActive && messageDelay > 0f)
            {
                GUI.color = new Color(.02f, .16f, .22f, .82f);
                GUI.Box(new Rect(Screen.width * .5f - 235f, Screen.height - 76f, 470f, 48f), GUIContent.none);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * .5f - 225f, Screen.height - 72f, 450f, 40f), message, scoreStyle);
            }
            GUI.color = previous;
        }

        void DrawUpgradeDraft(bool controllerConnected)
        {
            HandleUpgradeInput();
            if (!upgradeDraftActive) return;

            float panelWidth = Mathf.Min(900f, Screen.width - 32f);
            float panelHeight = 310f;
            float left = (Screen.width - panelWidth) * .5f;
            float top = (Screen.height - panelHeight) * .5f;
            GUI.color = new Color(.02f, .13f, .19f, .96f);
            GUI.Box(new Rect(left, top, panelWidth, panelHeight), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(left + 20f, top + 16f, panelWidth - 40f, 36f), "RALLY BREAKTHROUGH", scoreStyle);
            GUI.Label(new Rect(left + 20f, top + 54f, panelWidth - 40f, 28f), "Choose one upgrade for this run", labelStyle);

            float gap = 12f;
            float cardWidth = (panelWidth - 40f - gap * (upgradeOfferCount - 1)) / upgradeOfferCount;
            float cardTop = top + 92f;
            float cardHeight = 168f;
            for (int i = 0; i < upgradeOfferCount; i++)
            {
                SoloUpgrade upgrade = upgradeOffers[i];
                int level = upgradeLevels[(int)upgrade];
                float x = left + 20f + i * (cardWidth + gap);
                GUI.color = i == selectedUpgrade ? new Color(.14f, .53f, .58f, .96f) : new Color(.08f, .28f, .34f, .94f);
                GUI.Box(new Rect(x, cardTop, cardWidth, cardHeight), GUIContent.none);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 10f, cardTop + 12f, cardWidth - 20f, 28f), (i + 1) + ". " + UpgradeNames[(int)upgrade], labelStyle);
                GUI.Label(new Rect(x + 10f, cardTop + 48f, cardWidth - 20f, 58f), UpgradeDescriptions[(int)upgrade], upgradeDescriptionStyle);
                GUI.Label(new Rect(x + 10f, cardTop + 108f, cardWidth - 20f, 24f), "Level " + (level + 1) + " / " + MaxUpgradeLevel, labelStyle);
                if (GUI.Button(new Rect(x + 12f, cardTop + 132f, cardWidth - 24f, 28f), "SELECT", upgradeButtonStyle))
                    ChooseUpgrade(i);
            }

            GUI.color = Color.white;
            string inputHint = controllerConnected ? "D-pad to choose  •  A to confirm" : "Press 1, 2, or 3 to choose";
            GUI.Label(new Rect(left + 20f, top + 270f, panelWidth - 40f, 24f), inputHint, labelStyle);
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

        enum SoloUpgrade { AeroLaces, ExtendedSweetSpot, PartnerDrills, TurboDash, ShuttleSense }
        enum ShotStyle { Clear, Drop, Drive, Smash }
    }
}
