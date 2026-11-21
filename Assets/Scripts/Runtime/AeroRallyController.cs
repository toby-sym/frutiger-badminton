using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace FrutigerBadminton
{
    /// <summary>Runs a local 3v3 practice rally using generated characters and controller-first input.</summary>
    public sealed class AeroRallyController : MonoBehaviour
    {
        const float GroundY = -.72f;
        const float CourtHalfWidth = 3.05f;
        const float CourtHalfLength = 6.7f;
        const float NetHeight = 1.56f;
        const float Gravity = 9.81f;
        const int SoloRunTargetScore = 7;
        const int SoloRunMaximumScore = 9;
        const int UpgradeScoreInterval = 3;
        const int MaxUpgradeLevel = 5;
        const float CameraShakeDuration = .18f;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly string[] UpgradeNames =
        {
            "Aero Laces", "Extended Sweet Spot", "Partner Drills", "Turbo Dash", "Shuttle Sense", "Prism Strings"
        };
        static readonly string[] UpgradeShortNames = { "LACES", "REACH", "TEAM", "DASH", "SENSE", "PRISM" };
        static readonly string[] UpgradeDescriptions =
        {
            "Move faster around the court.",
            "Return shots from farther away.",
            "Your partner runs and reaches farther.",
            "Dash faster and recover sooner.",
            "See the landing reticle turn urgent earlier.",
            "Player shots fly faster with shifting pearl trails."
        };

        readonly List<Object> ownedObjects = new List<Object>();
        readonly List<AudioClip> ownedAudioClips = new List<AudioClip>();
        readonly int[] upgradeLevels = new int[6];
        readonly SoloUpgrade[] upgradeOffers = new SoloUpgrade[3];
        readonly AudioClip[] shotAudioClips = new AudioClip[4];
        MaterialPropertyBlock landingMarkerProperties;
        MaterialPropertyBlock aimMarkerProperties;
        Transform player;
        Transform partner;
        Transform partnerTwo;
        Transform rival;
        Transform rivalPartner;
        Transform rivalWing;
        Transform shuttle;
        Transform landingMarker;
        Transform aimMarker;
        Transform partnerReceiverMarker;
        Transform partnerTwoReceiverMarker;
        Transform rivalReceiverMarker;
        Transform rivalPartnerReceiverMarker;
        Transform rivalWingReceiverMarker;
        AeroPlayerAnimation playerAnimation;
        AeroPlayerAnimation partnerAnimation;
        AeroPlayerAnimation partnerTwoAnimation;
        AeroPlayerAnimation rivalAnimation;
        AeroPlayerAnimation rivalPartnerAnimation;
        AeroPlayerAnimation rivalWingAnimation;
        TrailRenderer shuttleTrail;
        TrailRenderer dashTrail;
        AudioSource rallyAudio;
        AudioClip dashAudioClip;
        AudioClip playerPointAudioClip;
        AudioClip rivalPointAudioClip;
        Transform courtCamera;
        Renderer landingMarkerRenderer;
        Renderer aimMarkerRenderer;
        Mesh landingMarkerMesh;
        Material shuttleMaterial;
        Material corkMaterial;
        Material bandMaterial;
        Material trailMaterial;
        GUIStyle scoreStyle;
        GUIStyle scoreTeamStyle;
        GUIStyle labelStyle;
        GUIStyle upgradeButtonStyle;
        GUIStyle upgradeDescriptionStyle;
        GUIStyle upgradeChipStyle;
        GUIStyle hintStyle;
        GUIStyle titleStyle;
        GUIStyle titleEyebrowStyle;
        GUIStyle titleSubtitleStyle;
        GUIStyle titleBodyStyle;
        GUIStyle titleButtonStyle;

        Vector3 flightStart;
        Vector3 flightTarget;
        Vector3 flightVelocity;
        Vector3 previousShuttlePosition;
        Vector3 dashDirection;
        Vector3 playerVelocity;
        Vector3 cameraRestPosition;
        float flightDuration;
        float flightElapsed;
        float shuttleSpinDegreesPerSecond;
        float shuttleSpinAngle;
        float dashTime;
        float dashCooldown;
        float jumpHeight;
        float jumpVelocity;
        float cameraShakeRemaining;
        float cameraShakeStrength;
        float scorePulseStart = -10f;
        Color scorePulseColor = Color.white;
        float serveDelay;
        float messageDelay;
        int playerScore;
        int rivalScore;
        int rallyHits;
        int longestRallyHits;
        int smashesHit;
        int partnerReturns;
        int dashUses;
        int nextUpgradeScore = UpgradeScoreInterval;
        int upgradeOfferCount;
        int selectedUpgrade;
        int upgradeInputFrame = -1;
        ShotStyle flightShotStyle;
        bool playerMustReturn;
        bool playerServeReady;
        bool playerServesNext = true;
        bool flightWasPlayerShot;
        bool flightActive;
        bool servePending;
        bool isDashing;
        bool isPaused;
        bool titleScreenActive = true;
        bool upgradeDraftActive;
        bool runComplete;
        bool runWon;
        bool audioMuted;
        TitleMenuPage titleMenuPage;
        int titleMenuSelection;
        string message = "Move under the return, then time your shot.";

        void Awake()
        {
            landingMarkerProperties = new MaterialPropertyBlock();
            aimMarkerProperties = new MaterialPropertyBlock();
        }

        void Start()
        {
            Time.timeScale = 1f;
            audioMuted = PlayerPrefs.GetInt("Frutiger.AudioMuted", 0) != 0;
            AudioListener.volume = audioMuted ? 0f : 1f;
            if (PlayerPrefs.HasKey("Frutiger.Fullscreen"))
                Screen.fullScreen = PlayerPrefs.GetInt("Frutiger.Fullscreen") != 0;
            var playerObject = GameObject.Find("Player silhouette");
            var partnerObject = GameObject.Find("Partner silhouette");
            var partnerTwoObject = GameObject.Find("Partner Two silhouette");
            var rivalObject = GameObject.Find("Rival silhouette");
            var rivalPartnerObject = GameObject.Find("Rival Partner silhouette");
            var rivalWingObject = GameObject.Find("Rival Wing silhouette");
            if (!playerObject || !partnerObject || !partnerTwoObject || !rivalObject || !rivalPartnerObject || !rivalWingObject)
            {
                Debug.LogError("The Aero Court scene needs all six generated players before the rally can start.");
                enabled = false;
                return;
            }

            player = playerObject.transform;
            partner = partnerObject.transform;
            partnerTwo = partnerTwoObject.transform;
            rival = rivalObject.transform;
            rivalPartner = rivalPartnerObject.transform;
            rivalWing = rivalWingObject.transform;
            playerAnimation = playerObject.GetComponent<AeroPlayerAnimation>();
            partnerAnimation = partnerObject.GetComponent<AeroPlayerAnimation>();
            partnerTwoAnimation = partnerTwoObject.GetComponent<AeroPlayerAnimation>();
            rivalAnimation = rivalObject.GetComponent<AeroPlayerAnimation>();
            rivalPartnerAnimation = rivalPartnerObject.GetComponent<AeroPlayerAnimation>();
            rivalWingAnimation = rivalWingObject.GetComponent<AeroPlayerAnimation>();
            Camera mainCamera = Camera.main;
            if (mainCamera)
            {
                courtCamera = mainCamera.transform;
                cameraRestPosition = courtCamera.position;
            }
            CreateAudioFeedback();
            player.position = new Vector3(0f, GroundY, -4.6f);
            partner.position = new Vector3(-1.65f, GroundY, -4.8f);
            partnerTwo.position = new Vector3(1.65f, GroundY, -4.2f);
            rival.position = new Vector3(0f, GroundY, 4.6f);
            rivalPartner.position = new Vector3(1.65f, GroundY, 4.8f);
            rivalWing.position = new Vector3(-1.65f, GroundY, 4.2f);
            CreateShuttlecock();
            CreateLandingMarker();
            CreateAimMarker();
            CreateReceiverMarkers();
            CreateDashTrail();
            shuttle.position = new Vector3(0f, GroundY + 3.5f, 0f);
            shuttle.rotation = Quaternion.Euler(0f, 0f, 25f);
        }

        void Update()
        {
            Gamepad gamepad = Gamepad.current;
            bool pausePressed = Input.GetKeyDown(KeyCode.Escape)
                || (gamepad != null && gamepad.startButton.wasPressedThisFrame);
            if (pausePressed && !titleScreenActive && !upgradeDraftActive && !runComplete) SetPaused(!isPaused);

            float deltaTime = Mathf.Min(Time.deltaTime, .05f);
            UpdateCameraFeedback(isPaused ? Mathf.Min(Time.unscaledDeltaTime, .05f) : deltaTime);
            if (titleScreenActive)
            {
                UpdateTitleScreen();
                HandleTitleMenuInput();
                return;
            }
            if (isPaused)
            {
                HandlePauseMenuInput();
                return;
            }
            if (upgradeDraftActive || runComplete) return;

            if (servePending)
            {
                serveDelay -= deltaTime;
                if (serveDelay <= 0f) BeginServe();
            }
            if (messageDelay > 0f) messageDelay -= deltaTime;
            if (dashCooldown > 0f) dashCooldown -= deltaTime;

            MovePlayer(deltaTime);
            if (playerServeReady)
            {
                shuttle.position = player.position + Vector3.up * (1.18f + jumpHeight) + player.right * .18f;
                shuttle.up = Vector3.up;
            }
            MovePartner(deltaTime);
            MoveRival(deltaTime);
            MoveRivalPartner(deltaTime);
            MoveRivalWing(deltaTime);
            if (flightActive)
            {
                UpdateShuttlecock(deltaTime);
                if (flightActive && playerMustReturn) TryPartnerReturn();
            }
            UpdateAIReceiverMarkers();
            UpdateLandingMarker();
            UpdateAimMarker();
            if (playerServeReady && IsShotInput()
                && (!IsMouseShotInput() || !MouseIsOverHud())) TryPlayerServe();
            else if (flightActive && playerMustReturn && IsShotInput()
                && (!IsMouseShotInput() || !MouseIsOverHud())) TryPlayerShot();
        }

        void UpdateTitleScreen()
        {
            if (!shuttle) return;
            float time = Time.unscaledTime;
            shuttle.position = new Vector3(0f, GroundY + 3.5f + Mathf.Sin(time * 1.35f) * .12f, 0f);
            shuttle.rotation = Quaternion.Euler(0f, time * 42f, 25f + Mathf.Sin(time * .8f) * 5f);
        }

        void StartRally()
        {
            if (!titleScreenActive) return;
            titleScreenActive = false;
            BeginServe();
        }

        void HandleTitleMenuInput()
        {
            Gamepad gamepad = Gamepad.current;
            bool up = Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)
                || (gamepad != null && gamepad.dpad.up.wasPressedThisFrame);
            bool down = Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)
                || (gamepad != null && gamepad.dpad.down.wasPressedThisFrame);
            bool confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || (gamepad != null && (gamepad.buttonSouth.wasPressedThisFrame || gamepad.startButton.wasPressedThisFrame));
            bool back = Input.GetKeyDown(KeyCode.Escape)
                || (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);

            if (titleMenuPage != TitleMenuPage.Home)
            {
                int optionCount = titleMenuPage == TitleMenuPage.Settings ? 3 : 1;
                if (up) titleMenuSelection = (titleMenuSelection + optionCount - 1) % optionCount;
                if (down) titleMenuSelection = (titleMenuSelection + 1) % optionCount;
                if (titleMenuPage == TitleMenuPage.Settings
                    && (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)
                        || (gamepad != null && (gamepad.dpad.left.wasPressedThisFrame || gamepad.dpad.right.wasPressedThisFrame))))
                    ToggleSetting(titleMenuSelection);
                if (back)
                {
                    titleMenuPage = TitleMenuPage.Home;
                    titleMenuSelection = 0;
                }
                else if (confirm) ActivateTitleMenuOption(titleMenuSelection);
                return;
            }

            const int homeOptionCount = 4;
            if (up) titleMenuSelection = (titleMenuSelection + homeOptionCount - 1) % homeOptionCount;
            if (down) titleMenuSelection = (titleMenuSelection + 1) % homeOptionCount;
            if (confirm) ActivateTitleMenuOption(titleMenuSelection);
        }

        void ActivateTitleMenuOption(int option)
        {
            if (titleMenuPage == TitleMenuPage.Home)
            {
                switch (option)
                {
                    case 0: StartRally(); break;
                    case 1: titleMenuPage = TitleMenuPage.Controls; titleMenuSelection = 0; break;
                    case 2: titleMenuPage = TitleMenuPage.Settings; titleMenuSelection = 0; break;
                    default: Application.Quit(); break;
                }
                return;
            }

            if (titleMenuPage == TitleMenuPage.Settings && option < 2)
            {
                ToggleSetting(option);
                return;
            }

            titleMenuPage = TitleMenuPage.Home;
            titleMenuSelection = 0;
        }

        void ToggleSetting(int option)
        {
            if (option == 0)
            {
                audioMuted = !audioMuted;
                AudioListener.volume = audioMuted ? 0f : 1f;
                PlayerPrefs.SetInt("Frutiger.AudioMuted", audioMuted ? 1 : 0);
                PlayerPrefs.Save();
            }
            else if (option == 1)
            {
                Screen.fullScreen = !Screen.fullScreen;
                PlayerPrefs.SetInt("Frutiger.Fullscreen", Screen.fullScreen ? 1 : 0);
                PlayerPrefs.Save();
            }
            else
            {
                titleMenuPage = TitleMenuPage.Home;
                titleMenuSelection = 0;
            }
        }

        void CreateAudioFeedback()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera && !mainCamera.GetComponent<AudioListener>())
                mainCamera.gameObject.AddComponent<AudioListener>();

            rallyAudio = gameObject.AddComponent<AudioSource>();
            rallyAudio.playOnAwake = false;
            rallyAudio.spatialBlend = 0f;
            rallyAudio.volume = .48f;
            rallyAudio.dopplerLevel = 0f;

            shotAudioClips[(int)ShotStyle.Clear] = CreateTone("Aero clear chime", 720f, 510f, .22f, .16f);
            shotAudioClips[(int)ShotStyle.Drop] = CreateTone("Feather drop ping", 920f, 660f, .18f, .14f);
            shotAudioClips[(int)ShotStyle.Drive] = CreateTone("Glass drive snap", 460f, 190f, .14f, .2f);
            shotAudioClips[(int)ShotStyle.Smash] = CreateTone("Sky smash thump", 250f, 78f, .3f, .24f);
            dashAudioClip = CreateTone("Aero dash sweep", 180f, 680f, .19f, .18f);
            playerPointAudioClip = CreateTone("Aqua point arpeggio", 520f, 920f, .42f, .2f);
            rivalPointAudioClip = CreateTone("Coral point fall", 660f, 250f, .38f, .17f);
        }

        AudioClip CreateTone(string clipName, float startFrequency, float endFrequency, float duration, float volume)
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];
            float phase = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float normalized = i / (float)(sampleCount - 1);
                float time = i / (float)sampleRate;
                float frequency = Mathf.Lerp(startFrequency, endFrequency, normalized);
                phase += Mathf.PI * 2f * frequency / sampleRate;
                float attack = Mathf.Clamp01(time / .008f);
                float release = Mathf.Pow(1f - normalized, 2.2f);
                float tone = Mathf.Sin(phase) + .2f * Mathf.Sin(phase * 2f);
                samples[i] = tone * attack * release * volume;
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            ownedAudioClips.Add(clip);
            return clip;
        }

        void PlayShotAudio(ShotStyle style)
        {
            PlayAudioClip(shotAudioClips[(int)style]);
        }

        void PlayAudioClip(AudioClip clip, float volume = 1f)
        {
            if (rallyAudio && clip) rallyAudio.PlayOneShot(clip, volume);
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
            bool dashPressed = Input.GetKeyDown(KeyCode.LeftShift) || (gamepad != null && gamepad.rightShoulder.wasPressedThisFrame);
            if (dashPressed && dashCooldown <= 0f)
            {
                dashDirection = direction.sqrMagnitude > .01f ? direction.normalized : Vector3.forward;
                dashTime = .16f;
                dashCooldown = Mathf.Max(.55f, 1.05f - upgradeLevels[(int)SoloUpgrade.TurboDash] * .1f);
                isDashing = true;
                dashUses++;
                if (dashTrail)
                {
                    dashTrail.Clear();
                    dashTrail.emitting = true;
                }
                AeroHitEffect.Spawn(player.position + Vector3.up * .82f, new Color(.26f, .96f, 1f), .82f);
                PlayAudioClip(dashAudioClip, .75f);
            }
            else if (dashPressed) ShowMessage("Aero dash is recharging.", .55f);

            bool dashActive = dashTime > 0f;
            float speed = dashActive ? 12f + upgradeLevels[(int)SoloUpgrade.TurboDash] * .65f : 4.6f;
            speed *= 1f + upgradeLevels[(int)SoloUpgrade.AeroLaces] * .07f;
            if (dashActive)
            {
                direction = dashDirection;
                dashTime -= deltaTime;
                if (dashTime <= 0f && dashTrail) dashTrail.emitting = false;
                playerVelocity = direction * speed;
            }
            else
            {
                isDashing = false;
                Vector3 desiredVelocity = direction * speed;
                float acceleration = direction.sqrMagnitude > .001f ? 24f : 31f;
                playerVelocity = Vector3.MoveTowards(playerVelocity, desiredVelocity, acceleration * deltaTime);
            }

            Vector3 next = player.position + playerVelocity * deltaTime;
            jumpHeight += jumpVelocity * deltaTime;
            jumpVelocity -= 13.5f * deltaTime;
            if (jumpHeight <= 0f)
            {
                jumpHeight = 0f;
                jumpVelocity = 0f;
            }

            float clampedX = Mathf.Clamp(next.x, -CourtHalfWidth + .35f, CourtHalfWidth - .35f);
            float clampedZ = Mathf.Clamp(next.z, -CourtHalfLength + .45f, -.42f);
            if (!Mathf.Approximately(next.x, clampedX)) playerVelocity.x = 0f;
            if (!Mathf.Approximately(next.z, clampedZ)) playerVelocity.z = 0f;
            next.x = clampedX;
            next.z = clampedZ;
            next.y = GroundY + jumpHeight;
            player.position = next;
        }

        void MoveRival(float deltaTime)
        {
            if (!flightActive || playerMustReturn || !IsClosestOpponent(rival)) return;
            Vector3 destination = new Vector3(flightTarget.x, GroundY, flightTarget.z + .25f);
            destination.x = Mathf.Clamp(destination.x, -CourtHalfWidth + .35f, CourtHalfWidth - .35f);
            destination.z = Mathf.Clamp(destination.z, .42f, CourtHalfLength - .45f);
            rival.position = Vector3.MoveTowards(rival.position, destination, 5.1f * deltaTime);
        }

        void MoveRivalPartner(float deltaTime)
        {
            MoveRivalSupport(rivalPartner, -1.65f, 4.8f, deltaTime);
        }

        void MoveRivalWing(float deltaTime)
        {
            MoveRivalSupport(rivalWing, 1.65f, 4.2f, deltaTime);
        }

        void MoveRivalSupport(Transform member, float homeX, float homeZ, float deltaTime)
        {
            if (!member) return;
            Vector3 destination = new Vector3(homeX, GroundY, homeZ);
            if (flightActive && !playerMustReturn && IsClosestOpponent(member))
            {
                destination.x = Mathf.Clamp(flightTarget.x, -CourtHalfWidth + .45f, CourtHalfWidth - .45f);
                destination.z = Mathf.Clamp(flightTarget.z, .42f, CourtHalfLength - .45f);
            }
            member.position = Vector3.MoveTowards(member.position, destination, 5.1f * deltaTime);
        }

        bool IsClosestOpponent(Transform member)
        {
            return GetClosestOpponent() == member;
        }

        Transform GetClosestOpponent()
        {
            Transform closest = rival;
            float closestDistance = HorizontalDistance(rival.position, flightTarget);
            float partnerDistance = HorizontalDistance(rivalPartner.position, flightTarget);
            if (partnerDistance < closestDistance)
            {
                closest = rivalPartner;
                closestDistance = partnerDistance;
            }

            if (HorizontalDistance(rivalWing.position, flightTarget) < closestDistance)
                closest = rivalWing;
            return closest;
        }

        AeroPlayerAnimation GetOpponentAnimation(Transform opponent)
        {
            if (opponent == rivalPartner) return rivalPartnerAnimation;
            if (opponent == rivalWing) return rivalWingAnimation;
            return rivalAnimation;
        }

        void MovePartner(float deltaTime)
        {
            float side = player.position.x >= 0f ? -1f : 1f;
            Transform assignedReceiver = GetAssignedFriendlyReceiver();
            MoveFriendlySupport(partner, assignedReceiver, side * 1.65f, -4.8f, deltaTime);
            MoveFriendlySupport(partnerTwo, assignedReceiver, side * .85f, -2.9f, deltaTime);
        }

        Transform GetAssignedFriendlyReceiver()
        {
            if (!flightActive || !playerMustReturn) return null;

            float timeToLanding = Mathf.Max(0f, flightDuration - flightElapsed);
            float playerDistance = HorizontalDistance(player.position, flightTarget);
            float firstPartnerDistance = HorizontalDistance(partner.position, flightTarget);
            float secondPartnerDistance = HorizontalDistance(partnerTwo.position, flightTarget);
            float playerReach = 1.7f + upgradeLevels[(int)SoloUpgrade.ExtendedSweetSpot] * .16f + jumpHeight * .5f;
            bool playerCannotReach = playerDistance - playerReach > timeToLanding * 4.6f;
            Transform nearestPartner = firstPartnerDistance <= secondPartnerDistance ? partner : partnerTwo;
            float nearestDistance = Mathf.Min(firstPartnerDistance, secondPartnerDistance);
            return playerCannotReach || nearestDistance + .85f < playerDistance ? nearestPartner : null;
        }

        void CreateReceiverMarkers()
        {
            Material friendlyMaterial = CreateMaterial("Universal Render Pipeline/Lit", new Color(.62f, 1f, .48f), .72f, 1.2f);
            Material opponentMaterial = CreateMaterial("Universal Render Pipeline/Lit", new Color(1f, .64f, .76f), .72f, 1.1f);
            partnerReceiverMarker = CreateReceiverMarker("Partner coverage reticle", friendlyMaterial);
            partnerTwoReceiverMarker = CreateReceiverMarker("Partner two coverage reticle", friendlyMaterial);
            rivalReceiverMarker = CreateReceiverMarker("Rival coverage reticle", opponentMaterial);
            rivalPartnerReceiverMarker = CreateReceiverMarker("Rival partner coverage reticle", opponentMaterial);
            rivalWingReceiverMarker = CreateReceiverMarker("Rival wing coverage reticle", opponentMaterial);
        }

        Transform CreateReceiverMarker(string objectName, Material material)
        {
            var marker = new GameObject(objectName);
            marker.transform.SetParent(transform, false);
            marker.AddComponent<MeshFilter>().sharedMesh = landingMarkerMesh;
            var renderer = marker.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            marker.SetActive(false);
            return marker.transform;
        }

        void UpdateAIReceiverMarkers()
        {
            Transform friendlyReceiver = GetAssignedFriendlyReceiver();
            Transform opponentReceiver = flightActive && !playerMustReturn ? GetClosestOpponent() : null;
            UpdateReceiverMarker(partnerReceiverMarker, partner, friendlyReceiver);
            UpdateReceiverMarker(partnerTwoReceiverMarker, partnerTwo, friendlyReceiver);
            UpdateReceiverMarker(rivalReceiverMarker, rival, opponentReceiver);
            UpdateReceiverMarker(rivalPartnerReceiverMarker, rivalPartner, opponentReceiver);
            UpdateReceiverMarker(rivalWingReceiverMarker, rivalWing, opponentReceiver);
        }

        void UpdateReceiverMarker(Transform marker, Transform target, Transform selected)
        {
            bool show = marker && target == selected;
            if (marker && marker.gameObject.activeSelf != show) marker.gameObject.SetActive(show);
            if (!show) return;

            marker.position = new Vector3(target.position.x, GroundY + .13f, target.position.z);
            float pulse = .82f + Mathf.Sin(Time.time * 6f) * .07f;
            marker.localScale = Vector3.one * pulse;
        }

        void MoveFriendlySupport(Transform member, Transform assignedReceiver, float homeX, float homeZ, float deltaTime)
        {
            if (!member) return;
            Vector3 destination = new Vector3(homeX, GroundY, homeZ);
            if (flightActive && playerMustReturn && member == assignedReceiver)
            {
                destination.x = Mathf.Clamp(flightTarget.x, -CourtHalfWidth + .45f, CourtHalfWidth - .45f);
                destination.z = Mathf.Clamp(flightTarget.z, -CourtHalfLength + .45f, -.42f);
            }

            float partnerSpeed = 5.2f + upgradeLevels[(int)SoloUpgrade.PartnerDrills] * .48f;
            member.position = Vector3.MoveTowards(member.position, destination, partnerSpeed * deltaTime);
        }

        bool TryPartnerReturn()
        {
            if (!partner || !partnerTwo || !flightActive || !playerMustReturn || shuttle.position.z >= -.15f) return false;

            float partnerReach = 1.5f + upgradeLevels[(int)SoloUpgrade.PartnerDrills] * .1f;
            float firstPartnerDistance = HorizontalDistance(partner.position, shuttle.position);
            float secondPartnerDistance = HorizontalDistance(partnerTwo.position, shuttle.position);
            Transform returner = firstPartnerDistance <= secondPartnerDistance ? partner : partnerTwo;
            bool playerCanReturn = HorizontalDistance(player.position, shuttle.position)
                <= 1.7f + upgradeLevels[(int)SoloUpgrade.ExtendedSweetSpot] * .16f + jumpHeight * .5f
                && shuttle.position.y <= GroundY + 2.9f + jumpHeight;
            if (playerCanReturn || shuttle.position.y > GroundY + 2.85f
                || Mathf.Min(firstPartnerDistance, secondPartnerDistance) > partnerReach) return false;

            Vector3 target = new Vector3(
                Mathf.Clamp(-rival.position.x * .55f + Random.Range(-.85f, .85f), -CourtHalfWidth + .35f, CourtHalfWidth - .35f),
                GroundY + .11f,
                Random.Range(4.25f, 5.85f));
            AeroHitEffect.Spawn(shuttle.position, PlayerShotTint(ShotStyle.Clear), .98f);
            PlayShotAudio(ShotStyle.Clear);
            Launch(shuttle.position, target, Random.Range(1.16f, 1.36f), false, true);
            AeroPlayerAnimation returnerAnimation = returner == partner ? partnerAnimation : partnerTwoAnimation;
            if (returnerAnimation) returnerAnimation.Swing(false);
            rallyHits++;
            partnerReturns++;
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
            if (flightWasPlayerShot && upgradeLevels[(int)SoloUpgrade.PrismStrings] > 0)
                SetShuttleTrailTint(PlayerShotTint(flightShotStyle));
            Vector3 velocity = flightVelocity + Vector3.down * (Gravity * time);
            if (velocity.sqrMagnitude > .01f)
            {
                Vector3 direction = velocity.normalized;
                shuttleSpinAngle = Mathf.Repeat(shuttleSpinAngle + shuttleSpinDegreesPerSecond * deltaTime, 360f);
                shuttle.rotation = Quaternion.AngleAxis(shuttleSpinAngle, direction)
                    * Quaternion.FromToRotation(Vector3.up, direction);
            }

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

            Transform returner = GetClosestOpponent();
            if (HorizontalDistance(returner.position, flightTarget) > 1.05f)
            {
                AwardPlayerPoint("Clean winner.");
                return;
            }
            BeginRivalReturn(returner, GetOpponentAnimation(returner));
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
            AeroHitEffect.Spawn(shuttle.position, PlayerShotTint(style), style == ShotStyle.Smash ? 1.55f : 1f);
            PlayShotAudio(style);
            Launch(shuttle.position, target, duration, false, true, style);
            if (playerAnimation) playerAnimation.Swing(style == ShotStyle.Smash);
            rallyHits++;
            if (style == ShotStyle.Smash) smashesHit++;
            string hit = style == ShotStyle.Clear ? "Aqua clear"
                       : style == ShotStyle.Drop ? "Feather drop"
                       : style == ShotStyle.Drive ? "Glass drive" : "Sky smash";
            ShowMessage(hit + "  •  Rally " + rallyHits, .85f);
        }

        void TryPlayerServe()
        {
            if (!playerServeReady) return;

            ShotStyle style = CurrentShotStyle();
            if (style == ShotStyle.Smash && jumpHeight < .8f)
            {
                ShowMessage("Jump before a smash serve.", .8f);
                return;
            }

            Vector3 start = player.position + Vector3.up * (1.18f + jumpHeight) + player.right * .18f;
            Vector3 target = ServeAimTarget(style);
            float duration = style == ShotStyle.Clear ? 1.48f
                           : style == ShotStyle.Drop ? 1.27f
                           : style == ShotStyle.Drive ? 1.22f
                           : 1.15f;
            AeroHitEffect.Spawn(start, PlayerShotTint(style), style == ShotStyle.Smash ? 1.55f : 1f);
            PlayShotAudio(style);
            Launch(start, target, duration, false, true, style);
            if (playerAnimation) playerAnimation.Swing(style == ShotStyle.Smash);
            rallyHits++;
            if (style == ShotStyle.Smash) smashesHit++;
            string hit = style == ShotStyle.Clear ? "Aqua clear"
                       : style == ShotStyle.Drop ? "Feather drop"
                       : style == ShotStyle.Drive ? "Glass drive" : "Sky smash";
            ShowMessage(hit + " serve  •  Rally " + rallyHits, .95f);
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
            else if (camera && Mouse.current != null && !MouseIsOverHud())
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

        Vector3 ServeAimTarget(ShotStyle style)
        {
            Vector3 target = AimTarget(style);
            target.z = Mathf.Max(target.z, 4f);
            return target;
        }

        void BeginServe()
        {
            servePending = false;
            rallyHits = 0;
            bool playerServing = playerServesNext;
            playerServesNext = !playerServesNext;
            if (playerServing)
            {
                playerServeReady = true;
                shuttleTrail.Clear();
                shuttle.position = player.position + Vector3.up * (1.18f + jumpHeight) + player.right * .18f;
                shuttle.up = Vector3.up;
                ShowMessage("Your serve — aim the ring, then choose a shot.", 2.2f);
                return;
            }

            playerServeReady = false;
            Vector3 start = rival.position + Vector3.up * 1.45f;
            Vector3 target = new Vector3(Mathf.Clamp(player.position.x + Random.Range(-1.1f, 1.1f), -2.2f, 2.2f), GroundY + .11f, Random.Range(-5.7f, -2.1f));
            AeroHitEffect.Spawn(start, new Color(.42f, .96f, 1f), .9f);
            PlayShotAudio(ShotStyle.Clear);
            Launch(start, target, 1.42f, true, false);
            if (rivalAnimation) rivalAnimation.Swing(false);
            ShowMessage("Return the serve.", 1.1f);
        }

        void BeginRivalReturn(Transform returner, AeroPlayerAnimation returnerAnimation)
        {
            ShotStyle style = ChooseRivalShot();
            float targetZ = style == ShotStyle.Drop ? Random.Range(-2.35f, -1.5f)
                         : style == ShotStyle.Drive ? Random.Range(-4.15f, -3.15f)
                         : Random.Range(-5.8f, -4.9f);
            float leftCoverage = Mathf.Min(Mathf.Abs(player.position.x + 2.2f), Mathf.Abs(partner.position.x + 2.2f), Mathf.Abs(partnerTwo.position.x + 2.2f));
            float rightCoverage = Mathf.Min(Mathf.Abs(player.position.x - 2.2f), Mathf.Abs(partner.position.x - 2.2f), Mathf.Abs(partnerTwo.position.x - 2.2f));
            bool aimLeft = leftCoverage > rightCoverage || (Mathf.Approximately(leftCoverage, rightCoverage) && Random.value < .5f);
            float targetX = (aimLeft ? -2.25f : 2.25f) + Random.Range(-.45f, .45f);
            Vector3 start = returner.position + Vector3.up * 1.4f;
            Vector3 target = new Vector3(Mathf.Clamp(targetX, -CourtHalfWidth + .35f, CourtHalfWidth - .35f), GroundY + .11f, targetZ);
            AeroHitEffect.Spawn(start, ShotTint(style), style == ShotStyle.Drive ? 1.08f : .95f);
            PlayShotAudio(style);
            float duration = style == ShotStyle.Drop ? Random.Range(1.62f, 1.74f)
                           : style == ShotStyle.Drive ? Random.Range(1.27f, 1.36f)
                           : Random.Range(1.42f, 1.54f);
            Launch(start, target, duration, true, false, style);
            if (returnerAnimation) returnerAnimation.Swing(false);
            rallyHits++;
            string shotName = style == ShotStyle.Drop ? "feather drop"
                            : style == ShotStyle.Drive ? "glass drive" : "aqua clear";
            ShowMessage("Rival " + shotName + "  •  Rally " + rallyHits, .9f);
        }

        ShotStyle ChooseRivalShot()
        {
            float dropChance = rallyHits >= 3 ? .38f : .2f;
            float roll = Random.value;
            if (roll < dropChance) return ShotStyle.Drop;
            if (roll < dropChance + .32f) return ShotStyle.Drive;
            return ShotStyle.Clear;
        }

        void Launch(Vector3 start, Vector3 target, float duration, bool receiverIsPlayer, bool playerShot, ShotStyle style = ShotStyle.Clear)
        {
            playerServeReady = false;
            flightStart = start;
            flightTarget = target;
            if (playerShot)
                duration *= Mathf.Lerp(1f, .875f, upgradeLevels[(int)SoloUpgrade.PrismStrings] / (float)MaxUpgradeLevel);
            flightDuration = duration;
            flightElapsed = 0f;
            playerMustReturn = receiverIsPlayer;
            flightWasPlayerShot = playerShot;
            flightShotStyle = style;
            shuttleSpinDegreesPerSecond = style == ShotStyle.Smash ? 1440f
                : style == ShotStyle.Drive ? 960f
                : style == ShotStyle.Drop ? 300f : 540f;
            shuttleSpinAngle = 0f;
            flightActive = true;
            float verticalSpeed = (target.y - start.y + .5f * Gravity * duration * duration) / duration;
            flightVelocity = new Vector3((target.x - start.x) / duration, verticalSpeed, (target.z - start.z) / duration);
            previousShuttlePosition = start;
            shuttle.position = start;
            shuttle.up = flightVelocity.normalized;
            shuttleTrail.Clear();
            SetShuttleTrailTint(playerShot ? PlayerShotTint(style) : ShotTint(style));
            float shake = style == ShotStyle.Smash ? .075f
                        : style == ShotStyle.Drive ? .043f
                        : style == ShotStyle.Drop ? .028f : .018f;
            TriggerCameraShake(shake);
        }

        void AwardPlayerPoint(string result)
        {
            playerScore++;
            PlayAudioClip(playerPointAudioClip);
            CelebratePoint(new Color(.36f, 1f, .82f));
            bool finishedRun = IsSoloRunComplete();
            bool reachedUpgradeMilestone = !finishedRun && playerScore >= nextUpgradeScore;
            if (reachedUpgradeMilestone) nextUpgradeScore += UpgradeScoreInterval;
            FinishPoint("Point  " + playerScore + " — " + rivalScore + "  " + result,
                reachedUpgradeMilestone, finishedRun, finishedRun);
        }

        void AwardRivalPoint(string result)
        {
            rivalScore++;
            PlayAudioClip(rivalPointAudioClip);
            CelebratePoint(new Color(1f, .54f, .69f));
            bool finishedRun = IsSoloRunComplete();
            FinishPoint(result + "  " + playerScore + " — " + rivalScore, false, finishedRun, false);
        }

        void CelebratePoint(Color color)
        {
            scorePulseStart = Time.unscaledTime;
            scorePulseColor = color;
            float rallyBonus = Mathf.Clamp01(rallyHits / 7f);
            AeroHitEffect.Spawn(shuttle.position + Vector3.up * .12f, color, Mathf.Lerp(1.1f, 1.8f, rallyBonus));
            TriggerCameraShake(.08f + .045f * rallyBonus);
        }

        void TriggerCameraShake(float strength)
        {
            if (!courtCamera) return;
            cameraShakeStrength = Mathf.Max(cameraShakeStrength, strength);
            cameraShakeRemaining = CameraShakeDuration;
        }

        void UpdateCameraFeedback(float deltaTime)
        {
            if (!courtCamera) return;
            if (cameraShakeRemaining <= 0f)
            {
                courtCamera.position = cameraRestPosition;
                cameraShakeStrength = 0f;
                return;
            }

            cameraShakeRemaining = Mathf.Max(0f, cameraShakeRemaining - deltaTime);
            float envelope = cameraShakeStrength * (cameraShakeRemaining / CameraShakeDuration);
            float phase = Time.unscaledTime * 68f;
            Vector3 offset = courtCamera.right * Mathf.Sin(phase)
                           + courtCamera.up * Mathf.Sin(phase * 1.31f);
            courtCamera.position = cameraRestPosition + offset * envelope;
        }

        bool IsSoloRunComplete()
        {
            int highScore = Mathf.Max(playerScore, rivalScore);
            int lead = Mathf.Abs(playerScore - rivalScore);
            return highScore >= SoloRunMaximumScore || (highScore >= SoloRunTargetScore && lead >= 2);
        }

        void FinishPoint(string result, bool offerUpgrade = false, bool endRun = false, bool won = false)
        {
            longestRallyHits = Mathf.Max(longestRallyHits, rallyHits);
            flightActive = false;
            playerMustReturn = false;
            shuttleTrail.Clear();
            shuttle.position = new Vector3(0f, GroundY + .25f, 0f);
            shuttle.rotation = Quaternion.identity;
            shuttleSpinAngle = 0f;
            servePending = !endRun;
            serveDelay = endRun ? 0f : 1.45f;
            ShowMessage(result, 1.45f);
            if (endRun)
            {
                runComplete = true;
                runWon = won;
                return;
            }
            if (offerUpgrade) BeginUpgradeDraft();
        }

        void StartNewRun()
        {
            isPaused = false;
            Time.timeScale = 1f;
            if (rallyAudio) rallyAudio.Stop();
            playerScore = 0;
            rivalScore = 0;
            rallyHits = 0;
            longestRallyHits = 0;
            smashesHit = 0;
            partnerReturns = 0;
            dashUses = 0;
            nextUpgradeScore = UpgradeScoreInterval;
            System.Array.Clear(upgradeLevels, 0, upgradeLevels.Length);
            runComplete = false;
            runWon = false;
            playerServesNext = true;
            playerServeReady = false;
            cameraShakeRemaining = 0f;
            cameraShakeStrength = 0f;
            if (courtCamera) courtCamera.position = cameraRestPosition;
            upgradeDraftActive = false;
            servePending = false;
            flightActive = false;
            playerMustReturn = false;
            isDashing = false;
            dashTime = 0f;
            dashCooldown = 0f;
            playerVelocity = Vector3.zero;
            jumpHeight = 0f;
            jumpVelocity = 0f;
            messageDelay = 0f;
            player.position = new Vector3(0f, GroundY, -4.6f);
            partner.position = new Vector3(-1.65f, GroundY, -4.8f);
            partnerTwo.position = new Vector3(1.65f, GroundY, -4.2f);
            rival.position = new Vector3(0f, GroundY, 4.6f);
            rivalPartner.position = new Vector3(1.65f, GroundY, 4.8f);
            rivalWing.position = new Vector3(-1.65f, GroundY, 4.2f);
            if (dashTrail)
            {
                dashTrail.Clear();
                dashTrail.emitting = false;
            }
            BeginServe();
        }

        void SetPaused(bool paused)
        {
            isPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (rallyAudio)
            {
                if (paused) rallyAudio.Pause();
                else rallyAudio.UnPause();
            }
        }

        void HandlePauseMenuInput()
        {
            Gamepad gamepad = Gamepad.current;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame))
            {
                SetPaused(false);
            }
            else if (Input.GetKeyDown(KeyCode.R)
                || (gamepad != null && gamepad.buttonWest.wasPressedThisFrame))
            {
                StartNewRun();
            }
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
            return mouse.x >= 16f && mouse.x <= 336f
                && mouse.y >= Screen.height - 242f && mouse.y <= Screen.height - 16f;
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

        Color PlayerShotTint(ShotStyle style)
        {
            int level = upgradeLevels[(int)SoloUpgrade.PrismStrings];
            Color shotColor = ShotTint(style);
            if (level <= 0) return shotColor;

            float hue = Mathf.Repeat(Time.time * .22f + (int)style * .21f, 1f);
            Color pearlColor = Color.HSVToRGB(hue, .42f, 1f);
            return Color.Lerp(shotColor, pearlColor, level / (float)MaxUpgradeLevel * .72f);
        }

        void SetShuttleTrailTint(Color tint)
        {
            shuttleTrail.startColor = new Color(tint.r, tint.g, tint.b, .82f);
            shuttleTrail.endColor = new Color(tint.r, tint.g, tint.b, 0f);
            if (trailMaterial && trailMaterial.HasProperty(EmissionColorId))
                trailMaterial.SetColor(EmissionColorId, tint * .72f);
        }

        void CreateShuttlecock()
        {
            var root = new GameObject("Generated shuttlecock");
            ownedObjects.Add(root);
            shuttle = root.transform;
            Mesh featherMesh = BuildFeathers();
            ownedObjects.Add(featherMesh);
            Shader featherShader = Resources.Load<Shader>("ShuttleFeather");
            shuttleMaterial = CreateMaterial("Frutiger/Shuttle Feather", new Color(.88f, .99f, 1f), .3f, .35f, featherShader);
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

            trailMaterial = CreateMaterial("Universal Render Pipeline/Unlit", Color.white, 0f, .8f);
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

        void CreateAimMarker()
        {
            var marker = new GameObject("Generated aimed shot preview");
            ownedObjects.Add(marker);
            aimMarker = marker.transform;
            aimMarker.position = new Vector3(0f, GroundY + .16f, 5.65f);
            marker.AddComponent<MeshFilter>().sharedMesh = landingMarkerMesh;
            aimMarkerRenderer = marker.AddComponent<MeshRenderer>();
            aimMarkerRenderer.sharedMaterial = CreateMaterial("Universal Render Pipeline/Lit", new Color(.34f, .97f, 1f), .82f, 1.05f);
            aimMarkerRenderer.shadowCastingMode = ShadowCastingMode.Off;
            aimMarkerRenderer.receiveShadows = false;
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

        void UpdateAimMarker()
        {
            bool show = playerServeReady || (flightActive && playerMustReturn);
            if (aimMarker.gameObject.activeSelf != show) aimMarker.gameObject.SetActive(show);
            if (!show) return;

            ShotStyle previewStyle = PreviewShotStyle();
            Vector3 target = playerServeReady ? ServeAimTarget(previewStyle) : AimTarget(previewStyle);
            aimMarker.position = new Vector3(target.x, GroundY + .16f, target.z);
            float pulse = Mathf.Sin(Time.time * Mathf.PI * 3f) * .5f + .5f;
            aimMarker.localScale = Vector3.one * Mathf.Lerp(.76f, .92f, pulse);

            Color tint = ShotTint(previewStyle);
            aimMarkerProperties.Clear();
            aimMarkerProperties.SetColor(BaseColorId, tint);
            aimMarkerProperties.SetColor(ColorId, tint);
            aimMarkerProperties.SetColor(EmissionColorId, tint * Mathf.Lerp(.8f, 1.55f, pulse));
            aimMarkerRenderer.SetPropertyBlock(aimMarkerProperties);
        }

        ShotStyle PreviewShotStyle()
        {
            Gamepad gamepad = Gamepad.current;
            if (Input.GetMouseButton(1) || (gamepad != null && gamepad.buttonWest.isPressed)) return ShotStyle.Drop;
            if (Input.GetKey(KeyCode.F) || (gamepad != null && gamepad.buttonEast.isPressed)) return ShotStyle.Drive;
            if (Input.GetKey(KeyCode.R) || (gamepad != null && gamepad.buttonNorth.isPressed)) return ShotStyle.Smash;
            return ShotStyle.Clear;
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
            var normals = new List<Vector3>(featherCount * 6);
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
                Vector3 faceNormal = tangent;
                int first = vertices.Count;
                for (int row = 0; row < 3; row++)
                {
                    Vector3 center = radial * radii[row];
                    vertices.Add(new Vector3(center.x - tangent.x * widths[row], heights[row], center.z - tangent.z * widths[row]));
                    vertices.Add(new Vector3(center.x + tangent.x * widths[row], heights[row], center.z + tangent.z * widths[row]));
                    Color color = Color.Lerp(baseColor, tipColor, row * .5f);
                    colors.Add(color); colors.Add(color);
                    normals.Add(faceNormal); normals.Add(faceNormal);
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
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }

        static void AddDoubleSidedQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
            triangles.Add(c); triangles.Add(b); triangles.Add(a);
            triangles.Add(d); triangles.Add(c); triangles.Add(a);
        }

        Material CreateMaterial(string shaderName, Color color, float smoothness, float emission, Shader explicitShader = null)
        {
            Shader shader = explicitShader ? explicitShader : AeroRuntimeShaderLibrary.Resolve(shaderName);
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
                scoreTeamStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                upgradeButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
                upgradeDescriptionStyle = new GUIStyle(labelStyle)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = true
                };
                upgradeChipStyle = new GUIStyle(labelStyle)
                {
                    fontSize = 10,
                    alignment = TextAnchor.MiddleCenter
                };
                hintStyle = new GUIStyle(labelStyle) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 56,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                titleEyebrowStyle = new GUIStyle(labelStyle)
                {
                    fontSize = 12,
                    alignment = TextAnchor.MiddleCenter
                };
                titleSubtitleStyle = new GUIStyle(labelStyle)
                {
                    fontSize = 24,
                    alignment = TextAnchor.MiddleCenter
                };
                titleBodyStyle = new GUIStyle(labelStyle)
                {
                    fontSize = 15,
                    alignment = TextAnchor.MiddleCenter
                };
                titleButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                titleButtonStyle.normal.background = Texture2D.whiteTexture;
                titleButtonStyle.hover.background = Texture2D.whiteTexture;
                titleButtonStyle.active.background = Texture2D.whiteTexture;
                scoreStyle.normal.textColor = Color.white;
                labelStyle.normal.textColor = new Color(.82f, .98f, 1f);
                upgradeDescriptionStyle.normal.textColor = new Color(.82f, .98f, 1f);
                hintStyle.normal.textColor = new Color(.68f, .89f, .93f);
                upgradeButtonStyle.normal.textColor = new Color(.03f, .18f, .2f);
                upgradeChipStyle.normal.textColor = Color.white;
                titleStyle.normal.textColor = Color.white;
                titleEyebrowStyle.normal.textColor = new Color(.47f, 1f, .94f);
                titleSubtitleStyle.normal.textColor = new Color(.49f, 1f, .94f);
                titleBodyStyle.normal.textColor = new Color(.78f, .95f, .98f);
                titleButtonStyle.normal.textColor = new Color(.02f, .18f, .2f);
                titleButtonStyle.hover.textColor = new Color(.02f, .18f, .2f);
                titleButtonStyle.active.textColor = new Color(.02f, .18f, .2f);
            }

            Color previous = GUI.color;
            if (titleScreenActive)
            {
                DrawTitleScreen(Gamepad.current != null);
                GUI.color = previous;
                return;
            }

            GUI.color = new Color(.02f, .105f, .16f, .91f);
            GUI.Box(new Rect(16f, 16f, 320f, 360f), GUIContent.none);
            GUI.color = new Color(.38f, .94f, .91f, .9f);
            GUI.DrawTexture(new Rect(17f, 17f, 318f, 2f), Texture2D.whiteTexture);
            GUI.color = new Color(.49f, 1f, .94f);
            GUI.Label(new Rect(28f, 22f, 298f, 24f), "FRUTIGER BADMINTON", labelStyle);
            GUI.color = new Color(.67f, .86f, .9f);
            GUI.Label(new Rect(28f, 46f, 298f, 20f), "SOLO CIRCUIT   /   3V3", hintStyle);
            bool controllerConnected = Gamepad.current != null;
            GUI.Label(new Rect(28f, 72f, 66f, 22f), "MOVE", hintStyle);
            GUI.Label(new Rect(104f, 72f, 220f, 22f), controllerConnected ? "Left stick / D-pad" : "WASD / arrows", labelStyle);
            GUI.Label(new Rect(28f, 94f, 66f, 22f), "AIM", hintStyle);
            GUI.Label(new Rect(104f, 94f, 220f, 22f), controllerConnected ? "Right stick" : "Mouse", labelStyle);
            GUI.Label(new Rect(28f, 116f, 66f, 22f), "MOVE+", hintStyle);
            GUI.Label(new Rect(104f, 116f, 220f, 22f), controllerConnected ? "A jump   ·   RB dash" : "Space jump   ·   Shift dash", hintStyle);
            GUI.Label(new Rect(28f, 138f, 66f, 22f), "SHOTS", hintStyle);
            GUI.Label(new Rect(104f, 138f, 220f, 22f), controllerConnected ? "LB clear  X drop  B drive  Y smash" : "LMB clear  RMB drop  F drive  R smash", hintStyle);
            GUI.color = new Color(.78f, .95f, .97f);
            GUI.Label(new Rect(28f, 168f, 298f, 22f), playerServeReady
                ? "SERVE  ·  Aim, then tap a shot"
                : "RETURN  ·  Hold shot to preview aim", labelStyle);
            GUI.color = new Color(.64f, .84f, .88f);
            GUI.Label(new Rect(28f, 192f, 298f, 20f), "First to 7  ·  Win by 2  ·  Draft every 3", hintStyle);

            float dashDuration = Mathf.Max(.55f, 1.05f - upgradeLevels[(int)SoloUpgrade.TurboDash] * .1f);
            float dashReady = 1f - Mathf.Clamp01(dashCooldown / dashDuration);
            GUI.Label(new Rect(28f, 220f, 160f, 22f), controllerConnected ? "RB  AERO DASH" : "SHIFT  AERO DASH", labelStyle);
            GUI.Label(new Rect(190f, 220f, 136f, 22f), dashCooldown <= 0f ? "READY" : "RECHARGING", hintStyle);
            GUI.color = new Color(.04f, .18f, .23f, .95f);
            GUI.DrawTexture(new Rect(28f, 245f, 298f, 8f), Texture2D.whiteTexture);
            GUI.color = Color.Lerp(new Color(1f, .56f, .53f), new Color(.3f, 1f, .82f), dashReady);
            GUI.DrawTexture(new Rect(28f, 245f, 298f * dashReady, 8f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(28f, 260f, 298f, 20f), controllerConnected ? "START  PAUSE   /   A SELECT" : "ESC  PAUSE   /   ENTER SELECT", hintStyle);
            DrawRunBuild();

            float scorePulse = Mathf.Clamp01(1f - (Time.unscaledTime - scorePulseStart) / .72f);
            Color scorePanel = Color.Lerp(new Color(.02f, .23f, .29f, .9f),
                new Color(scorePulseColor.r * .34f, scorePulseColor.g * .34f, scorePulseColor.b * .34f, .98f), scorePulse);
            float scoreLeft = Mathf.Max(16f, Screen.width - 388f);
            bool serveLabelVisible = servePending || upgradeDraftActive || playerServeReady || flightActive;
            bool playerServing = servePending || upgradeDraftActive ? playerServesNext : !playerServesNext;
            GUI.color = scorePanel;
            GUI.Box(new Rect(scoreLeft, 16f, 372f, 60f), GUIContent.none);
            GUI.color = new Color(.45f, 1f, .91f);
            GUI.Label(new Rect(scoreLeft + 18f, 18f, 128f, 20f),
                serveLabelVisible && playerServing ? "AQUA  ·  SERVING" : "AQUA  ·  YOU", scoreTeamStyle);
            GUI.color = Color.Lerp(Color.white, scorePulseColor, scorePulse * .48f);
            scoreStyle.fontSize = 22 + Mathf.RoundToInt(5f * scorePulse);
            GUI.Label(new Rect(scoreLeft + 21f, 37f, 122f, 34f), playerScore.ToString(), scoreStyle);
            GUI.Label(new Rect(scoreLeft + 145f, 29f, 82f, 38f), "—", scoreStyle);
            GUI.Label(new Rect(scoreLeft + 229f, 37f, 122f, 34f), rivalScore.ToString(), scoreStyle);
            scoreStyle.fontSize = 22;
            GUI.color = new Color(1f, .67f, .74f);
            GUI.Label(new Rect(scoreLeft + 226f, 18f, 128f, 20f),
                serveLabelVisible && !playerServing ? "CORAL  ·  SERVING" : "CORAL  ·  RIVALS", scoreTeamStyle);
            GUI.color = Color.white;

            if (runComplete) DrawRunEnd(controllerConnected);
            else if (upgradeDraftActive) DrawUpgradeDraft(controllerConnected);
            else if (isPaused) DrawPauseMenu(controllerConnected);

            if (!runComplete && !upgradeDraftActive && !isPaused && messageDelay > 0f)
            {
                GUI.color = new Color(.02f, .16f, .22f, .82f);
                float messageTop = Mathf.Max(16f, Screen.height - 64f);
                GUI.Box(new Rect(Screen.width * .5f - 235f, messageTop, 470f, 48f), GUIContent.none);
                GUI.color = Color.white;
                GUI.Label(new Rect(Screen.width * .5f - 225f, messageTop + 4f, 450f, 40f), message, scoreStyle);
            }
            GUI.color = previous;
        }

        void DrawRunBuild()
        {
            GUI.color = new Color(.68f, .91f, .95f);
            GUI.Label(new Rect(28f, 284f, 298f, 18f), "RUN BUILD", hintStyle);

            const float chipWidth = 94f;
            const float chipHeight = 22f;
            const float gapX = 8f;
            const float gapY = 3f;
            for (int i = 0; i < upgradeLevels.Length; i++)
            {
                int level = upgradeLevels[i];
                int column = i % 3;
                int row = i / 3;
                float x = 28f + column * (chipWidth + gapX);
                float y = 303f + row * (chipHeight + gapY);
                Color idle = new Color(.045f, .18f, .22f, .76f);
                Color active = Color.Lerp(new Color(.1f, .34f, .39f, .93f), new Color(.19f, .62f, .63f, .98f), level / (float)MaxUpgradeLevel);
                GUI.color = level > 0 ? active : idle;
                GUI.Box(new Rect(x, y, chipWidth, chipHeight), GUIContent.none);
                GUI.color = level > 0 ? new Color(.84f, 1f, .96f) : new Color(.52f, .71f, .74f);
                GUI.Label(new Rect(x + 2f, y, chipWidth - 4f, chipHeight), UpgradeShortNames[i] + "  " + level, upgradeChipStyle);
            }
            GUI.color = Color.white;
        }

        void DrawTitleScreen(bool controllerConnected)
        {
            Color previous = GUI.color;
            GUI.color = new Color(.015f, .075f, .16f, .2f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

            float panelWidth = Mathf.Min(1080f, Screen.width - 48f);
            float panelHeight = Mathf.Min(620f, Screen.height - 48f);
            float left = (Screen.width - panelWidth) * .5f;
            float top = (Screen.height - panelHeight) * .5f;
            float mainWidth = (panelWidth - 20f) * .58f;
            float sideWidth = panelWidth - mainWidth - 20f;
            titleStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(mainWidth * .09f), 36, 56);
            Rect mainPanel = new Rect(left, top, mainWidth, panelHeight);
            Rect sidePanel = new Rect(left + mainWidth + 20f, top, sideWidth, panelHeight);

            GUI.color = new Color(.018f, .105f, .17f, .91f);
            GUI.Box(mainPanel, GUIContent.none);
            GUI.color = new Color(.035f, .2f, .27f, .84f);
            GUI.Box(sidePanel, GUIContent.none);
            GUI.color = new Color(.39f, .98f, .94f, .94f);
            GUI.DrawTexture(new Rect(mainPanel.x + 1f, mainPanel.y + 1f, mainPanel.width - 2f, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(sidePanel.x + 1f, sidePanel.y + 1f, sidePanel.width - 2f, 2f), Texture2D.whiteTexture);

            GUI.color = new Color(.48f, 1f, .94f);
            GUI.Label(new Rect(mainPanel.x + 36f, top + 28f, mainWidth - 72f, 22f),
                "ABOVE THE CLOUDLINE  /  ARCADE BADMINTON", titleEyebrowStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(mainPanel.x + 30f, top + 62f, mainWidth - 60f, 64f), "FRUTIGER", titleStyle);
            GUI.color = new Color(.49f, 1f, .94f);
            GUI.Label(new Rect(mainPanel.x + 36f, top + 122f, mainWidth - 72f, 36f), "BADMINTON", titleSubtitleStyle);
            GUI.color = new Color(.82f, .96f, .99f);
            GUI.Label(new Rect(mainPanel.x + 36f, top + 170f, mainWidth - 72f, 26f),
                "Own the flight. Build the rally.", titleBodyStyle);
            GUI.color = new Color(.27f, .7f, .78f, .72f);
            GUI.DrawTexture(new Rect(mainPanel.x + 36f, top + 215f, mainWidth - 72f, 1f), Texture2D.whiteTexture);

            if (titleMenuPage == TitleMenuPage.Home)
            {
                GUI.color = new Color(.58f, .86f, .91f);
                GUI.Label(new Rect(mainPanel.x + 36f, top + 232f, mainWidth - 72f, 24f), "SELECT YOUR CIRCUIT", hintStyle);
                float buttonWidth = mainWidth - 72f;
                if (DrawTitleButton(new Rect(mainPanel.x + 36f, top + 266f, buttonWidth, 60f), "START SOLO CIRCUIT", titleMenuSelection == 0))
                    StartRally();

                float smallWidth = (buttonWidth - 12f) * .5f;
                if (DrawTitleButton(new Rect(mainPanel.x + 36f, top + 340f, smallWidth, 52f), "HOW TO PLAY", titleMenuSelection == 1))
                {
                    titleMenuPage = TitleMenuPage.Controls;
                    titleMenuSelection = 0;
                }
                if (DrawTitleButton(new Rect(mainPanel.x + 36f + smallWidth + 12f, top + 340f, smallWidth, 52f), "SETTINGS", titleMenuSelection == 2))
                {
                    titleMenuPage = TitleMenuPage.Settings;
                    titleMenuSelection = 0;
                }
                if (DrawTitleButton(new Rect(mainPanel.x + 36f, top + 408f, buttonWidth, 44f), "EXIT GAME", titleMenuSelection == 3))
                    Application.Quit();

                GUI.color = new Color(.64f, .86f, .9f);
                GUI.Label(new Rect(mainPanel.x + 36f, top + panelHeight - 52f, buttonWidth, 24f),
                    controllerConnected ? "D-PAD NAVIGATE     A SELECT     B BACK" : "ARROWS NAVIGATE     ENTER SELECT",
                    hintStyle);
            }
            else if (titleMenuPage == TitleMenuPage.Controls)
            {
                GUI.color = Color.white;
                GUI.Label(new Rect(mainPanel.x + 36f, top + 238f, mainWidth - 72f, 34f), "RALLY CONTROLS", titleSubtitleStyle);
                string move = controllerConnected ? "LEFT STICK / D-PAD     MOVE" : "WASD / ARROWS     MOVE";
                string aim = controllerConnected ? "RIGHT STICK     AIM" : "MOUSE     AIM";
                string jump = controllerConnected ? "A     JUMP" : "SPACE     JUMP";
                string dash = controllerConnected ? "RB     AERO DASH" : "SHIFT     AERO DASH";
                string clear = controllerConnected ? "LB     CLEAR" : "LEFT CLICK     CLEAR";
                string drop = controllerConnected ? "X     DROP" : "RIGHT CLICK     DROP";
                string drive = controllerConnected ? "B     DRIVE" : "F     DRIVE";
                string smash = controllerConnected ? "Y     AIR SMASH" : "R     AIR SMASH";
                string[] controls = { move, aim, jump + "      " + dash, clear + "      " + drop, drive + "      " + smash };
                for (int i = 0; i < controls.Length; i++)
                {
                    GUI.color = new Color(.66f, .91f, .94f);
                    GUI.Label(new Rect(mainPanel.x + 40f, top + 284f + i * 38f, mainWidth - 80f, 30f), controls[i], labelStyle);
                }
                if (DrawTitleButton(new Rect(mainPanel.x + 36f, top + 490f, mainWidth - 72f, 50f), "BACK TO MENU", true))
                {
                    titleMenuPage = TitleMenuPage.Home;
                    titleMenuSelection = 0;
                }
            }
            else
            {
                GUI.color = Color.white;
                GUI.Label(new Rect(mainPanel.x + 36f, top + 238f, mainWidth - 72f, 34f), "PREFERENCES", titleSubtitleStyle);
                string audio = audioMuted ? "AUDIO     OFF" : "AUDIO     ON";
                string display = Screen.fullScreen ? "DISPLAY     FULLSCREEN" : "DISPLAY     WINDOWED";
                float buttonWidth = mainWidth - 72f;
                if (DrawTitleButton(new Rect(mainPanel.x + 36f, top + 292f, buttonWidth, 54f), audio, titleMenuSelection == 0))
                    ToggleSetting(0);
                if (DrawTitleButton(new Rect(mainPanel.x + 36f, top + 362f, buttonWidth, 54f), display, titleMenuSelection == 1))
                    ToggleSetting(1);
                if (DrawTitleButton(new Rect(mainPanel.x + 36f, top + 432f, buttonWidth, 50f), "BACK TO MENU", titleMenuSelection == 2))
                {
                    titleMenuPage = TitleMenuPage.Home;
                    titleMenuSelection = 0;
                }
                GUI.color = new Color(.66f, .91f, .94f);
                GUI.Label(new Rect(mainPanel.x + 36f, top + 500f, buttonWidth, 30f), "LEFT / RIGHT TO CHANGE  ·  A TO CONFIRM", hintStyle);
            }

            GUI.color = new Color(.48f, 1f, .94f);
            GUI.Label(new Rect(sidePanel.x + 28f, top + 32f, sideWidth - 56f, 22f), "THE AERO COURT", titleEyebrowStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(sidePanel.x + 24f, top + 76f, sideWidth - 48f, 66f), "3  ×  3", titleStyle);
            GUI.color = new Color(.82f, .96f, .99f);
            GUI.Label(new Rect(sidePanel.x + 30f, top + 142f, sideWidth - 60f, 30f), "SOLO CIRCUIT", titleSubtitleStyle);
            GUI.color = new Color(.27f, .7f, .78f, .72f);
            GUI.DrawTexture(new Rect(sidePanel.x + 30f, top + 192f, sideWidth - 60f, 1f), Texture2D.whiteTexture);

            string[] features =
            {
                "YOU + TWO AI TEAMMATES",
                "AIM CLEARS, DROPS & SMASHES",
                "DRAFT RUN UPGRADES",
                "FIRST TO 7  ·  WIN BY 2"
            };
            for (int i = 0; i < features.Length; i++)
            {
                GUI.color = new Color(.64f, .9f, .94f);
                GUI.Label(new Rect(sidePanel.x + 32f, top + 220f + i * 45f, sideWidth - 64f, 30f), features[i], labelStyle);
            }

            GUI.color = new Color(.11f, .41f, .5f, .88f);
            GUI.Box(new Rect(sidePanel.x + 28f, top + panelHeight - 116f, sideWidth - 56f, 72f), GUIContent.none);
            GUI.color = new Color(.49f, 1f, .94f);
            GUI.Label(new Rect(sidePanel.x + 38f, top + panelHeight - 103f, sideWidth - 76f, 24f), "A NEW RUN EVERY MATCH", titleEyebrowStyle);
            GUI.color = new Color(.8f, .93f, .96f);
            GUI.Label(new Rect(sidePanel.x + 38f, top + panelHeight - 77f, sideWidth - 76f, 24f), "Build your style one rally at a time.", titleBodyStyle);

            GUI.color = new Color(.61f, .82f, .87f);
            GUI.Label(new Rect(left, top - 28f, panelWidth, 22f), "FRUTIGER BADMINTON   /   ABOVE THE CLOUDS", hintStyle);
            GUI.color = previous;
        }

        bool DrawTitleButton(Rect rect, string label, bool selected)
        {
            GUI.color = selected ? new Color(.43f, .98f, .91f, .98f) : new Color(.055f, .25f, .33f, .96f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            titleButtonStyle.normal.background = null;
            titleButtonStyle.hover.background = null;
            titleButtonStyle.active.background = null;
            Color textColor = selected ? new Color(.015f, .13f, .18f) : new Color(.82f, .97f, .99f);
            titleButtonStyle.normal.textColor = textColor;
            titleButtonStyle.hover.textColor = textColor;
            titleButtonStyle.active.textColor = textColor;
            bool clicked = GUI.Button(rect, label, titleButtonStyle);
            GUI.color = Color.white;
            return clicked;
        }

        void DrawPauseMenu(bool controllerConnected)
        {
            float panelWidth = Mathf.Min(480f, Screen.width - 32f);
            float panelHeight = 292f;
            float left = (Screen.width - panelWidth) * .5f;
            float top = (Screen.height - panelHeight) * .5f;
            GUI.color = new Color(.02f, .13f, .19f, .97f);
            GUI.Box(new Rect(left, top, panelWidth, panelHeight), GUIContent.none);
            GUI.color = new Color(.55f, 1f, .92f);
            GUI.Label(new Rect(left + 24f, top + 24f, panelWidth - 48f, 46f), "RALLY PAUSED", scoreStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(left + 24f, top + 78f, panelWidth - 48f, 28f),
                "Take a breather. Your current run and upgrades are still here.", hintStyle);
            if (GUI.Button(new Rect(left + 95f, top + 125f, panelWidth - 190f, 42f), "RESUME RALLY", upgradeButtonStyle))
                SetPaused(false);
            if (GUI.Button(new Rect(left + 95f, top + 180f, panelWidth - 190f, 42f), "RESTART RUN", upgradeButtonStyle))
                StartNewRun();
            string inputHint = controllerConnected
                ? "Start / Esc resume  •  A to resume  •  X restart"
                : "Esc / Enter resume  •  R restart";
            GUI.Label(new Rect(left + 24f, top + 246f, panelWidth - 48f, 24f), inputHint, hintStyle);
        }

        void DrawRunEnd(bool controllerConnected)
        {
            Gamepad gamepad = Gamepad.current;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame))
            {
                StartNewRun();
                return;
            }

            float panelWidth = Mathf.Min(540f, Screen.width - 32f);
            float panelHeight = 430f;
            float left = (Screen.width - panelWidth) * .5f;
            float top = (Screen.height - panelHeight) * .5f;
            GUI.color = new Color(.02f, .13f, .19f, .97f);
            GUI.Box(new Rect(left, top, panelWidth, panelHeight), GUIContent.none);
            GUI.color = runWon ? new Color(.55f, 1f, .83f) : new Color(1f, .63f, .69f);
            GUI.Label(new Rect(left + 24f, top + 22f, panelWidth - 48f, 46f), runWon ? "RUN COMPLETE" : "RUN OVER", scoreStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(left + 24f, top + 72f, panelWidth - 48f, 40f), "SOLO RALLY   " + playerScore + "  —  " + rivalScore, scoreStyle);
            GUI.color = new Color(.68f, .91f, .95f);
            GUI.Label(new Rect(left + 24f, top + 119f, panelWidth - 48f, 20f), "RUN STATS", hintStyle);

            const float statGap = 8f;
            float statWidth = (panelWidth - 48f - statGap * 3f) / 4f;
            float statTop = top + 145f;
            string[] statNames = { "BEST RALLY", "SMASHES", "TEAM SAVES", "AERO DASHES" };
            int[] statValues = { longestRallyHits, smashesHit, partnerReturns, dashUses };
            for (int i = 0; i < statNames.Length; i++)
            {
                float x = left + 24f + i * (statWidth + statGap);
                GUI.color = new Color(.06f, .23f, .29f, .96f);
                GUI.Box(new Rect(x, statTop, statWidth, 78f), GUIContent.none);
                GUI.color = new Color(.61f, .91f, .94f);
                GUI.Label(new Rect(x + 4f, statTop + 8f, statWidth - 8f, 20f), statNames[i], scoreTeamStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 4f, statTop + 30f, statWidth - 8f, 40f), statValues[i].ToString(), scoreStyle);
            }

            GUI.color = new Color(.08f, .27f, .32f, .92f);
            GUI.Box(new Rect(left + 24f, top + 234f, panelWidth - 48f, 48f), GUIContent.none);
            GUI.color = new Color(.82f, .98f, 1f);
            GUI.Label(new Rect(left + 34f, top + 238f, panelWidth - 68f, 40f), RunUpgradeSummary(), upgradeDescriptionStyle);
            GUI.color = Color.white;
            GUI.Label(new Rect(left + 24f, top + 290f, panelWidth - 48f, 22f), "First to 7 points, win by 2 (cap 9)", labelStyle);
            GUI.Label(new Rect(left + 24f, top + 315f, panelWidth - 48f, 22f), "Upgrades reset when you start a new run.", hintStyle);
            if (GUI.Button(new Rect(left + 115f, top + 348f, panelWidth - 230f, 42f), "START A NEW RUN", upgradeButtonStyle))
                StartNewRun();
            GUI.Label(new Rect(left + 24f, top + 394f, panelWidth - 48f, 22f),
                controllerConnected ? "Press A to play again" : "Press Enter to play again", hintStyle);
        }

        string RunUpgradeSummary()
        {
            var activeUpgrades = new List<string>();
            for (int i = 0; i < upgradeLevels.Length; i++)
                if (upgradeLevels[i] > 0) activeUpgrades.Add(UpgradeShortNames[i] + " " + upgradeLevels[i]);
            return activeUpgrades.Count == 0
                ? "RUN BUILD  ·  No upgrades"
                : "RUN BUILD  ·  " + string.Join("  ·  ", activeUpgrades);
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
            Time.timeScale = 1f;
            foreach (Object owned in ownedObjects)
                if (owned) Destroy(owned);
            foreach (AudioClip clip in ownedAudioClips)
                if (clip) Destroy(clip);
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

        enum TitleMenuPage { Home, Controls, Settings }
        enum SoloUpgrade { AeroLaces, ExtendedSweetSpot, PartnerDrills, TurboDash, ShuttleSense, PrismStrings }
        enum ShotStyle { Clear, Drop, Drive, Smash }
    }
}
