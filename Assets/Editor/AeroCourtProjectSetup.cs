using System.IO;
using System.Reflection;
using FrutigerBadminton;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FrutigerBadminton.Editor
{
    /// <summary>Creates the initial scene and URP pipeline when this project is first opened.</summary>
    [InitializeOnLoad]
    public static class AeroCourtProjectSetup
    {
        const string ScenePath = "Assets/Scenes/AeroCourt.unity";
        const string PipelinePath = "Assets/Settings/FrutigerUniversalRenderPipeline.asset";

        static AeroCourtProjectSetup() { EditorApplication.delayCall += EnsureProjectAssets; }

        public static void EnsureProjectAssets()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EnsureControllerInput();
            EnsurePipeline();
            if (File.Exists(ScenePath)) return;
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Frutiger Badminton");
            var court = root.AddComponent<AeroCourtScene>();
            root.AddComponent<AeroRallyController>();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/VertexColorSky.shader");
            if (shader)
            {
                var serialized = new SerializedObject(court);
                serialized.FindProperty("vertexColorShader").objectReferenceValue = shader;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Created the procedural Aero Court scene. Press Play to explore it.");
        }

        static void EnsureControllerInput()
        {
            const int BothInputBackends = 2;
            // Unity 6 stores activeInputHandler on global or active Build Profile PlayerSettings.
            var buildProfileType = typeof(BuildProfile);
            var globalSettingsField = buildProfileType.GetField("s_GlobalPlayerSettings", BindingFlags.Static | BindingFlags.NonPublic);
            if (globalSettingsField == null)
                throw new System.InvalidOperationException("Unity did not expose global PlayerSettings for Input System configuration.");

            var playerSettings = (PlayerSettings)globalSettingsField.GetValue(null);
            var activeProfile = BuildProfile.GetActiveBuildProfile();
            if (activeProfile != null)
            {
                var overrideField = buildProfileType.GetField("m_PlayerSettings", BindingFlags.Instance | BindingFlags.NonPublic);
                if (overrideField == null)
                    throw new System.InvalidOperationException("Unity did not expose active Build Profile PlayerSettings for Input System configuration.");
                var profileSettings = (PlayerSettings)overrideField.GetValue(activeProfile);
                if (profileSettings != null) playerSettings = profileSettings;
            }

            if (!playerSettings)
                throw new System.InvalidOperationException("Unity PlayerSettings are unavailable; controller input cannot be enabled.");

            var serializedSettings = new SerializedObject(playerSettings);
            var activeInputHandler = serializedSettings.FindProperty("activeInputHandler");
            if (activeInputHandler == null)
                throw new System.InvalidOperationException("Unity PlayerSettings are missing activeInputHandler.");

            if (activeInputHandler.intValue != BothInputBackends)
                throw new System.InvalidOperationException("Both input backends must be enabled in ProjectSettings before Unity starts; changing them after startup can make Editor and player assemblies incompatible.");
        }

        static void EnsurePipeline()
        {
            if (GraphicsSettings.defaultRenderPipeline != null) return;
            Directory.CreateDirectory("Assets/Settings");
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            renderer.name = "Frutiger Universal Renderer";
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.name = "Frutiger Universal Render Pipeline";
            AssetDatabase.CreateAsset(renderer, "Assets/Settings/FrutigerUniversalRenderer.asset");
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            AssetDatabase.SaveAssets();
        }
    }

    public static class WindowsBuild
    {
        const string GameCiOutput = "build/StandaloneWindows64/FrutigerBadminton";

        public static void BuildWindowsGameCiDevelopment()
        {
            ConfigureGameCiBuild("Development");
            BuildWindows();
        }

        public static void BuildWindowsGameCiRelease()
        {
            ConfigureGameCiBuild("Release");
            string version = GetCommandLineArgument("-releaseVersion");
            if (string.IsNullOrEmpty(version))
                throw new System.ArgumentException("The GameCI release build requires -releaseVersion.");
            System.Environment.SetEnvironmentVariable("RELEASE_VERSION", version);
            BuildWindows();
        }

        static void ConfigureGameCiBuild(string configuration)
        {
            System.Environment.SetEnvironmentVariable("BUILD_OUTPUT", GameCiOutput);
            System.Environment.SetEnvironmentVariable("BUILD_CONFIGURATION", configuration);
        }

        static string GetCommandLineArgument(string name)
        {
            string[] arguments = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
                if (string.Equals(arguments[i], name, System.StringComparison.OrdinalIgnoreCase))
                    return arguments[i + 1];
            return null;
        }

        public static void BuildWindows()
        {
            AeroCourtProjectSetup.EnsureProjectAssets();
            string output = System.Environment.GetEnvironmentVariable("BUILD_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = "Build/Windows";
            Directory.CreateDirectory(output);
            var scenes = new[] { "Assets/Scenes/AeroCourt.unity" };
            if (!File.Exists(scenes[0]))
                throw new FileNotFoundException("The Aero Court scene was not generated. Open the project once in Unity before building.", scenes[0]);
            string version = System.Environment.GetEnvironmentVariable("RELEASE_VERSION");
            if (!string.IsNullOrEmpty(version)) PlayerSettings.bundleVersion = version;
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(output, "FrutigerBadminton.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = System.Environment.GetEnvironmentVariable("BUILD_CONFIGURATION") == "Release"
                    ? BuildOptions.None
                    : BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new System.Exception("Windows player build failed with result " + report.summary.result + ". See the Unity Editor log for details.");
            Debug.Log("Windows development player built at " + options.locationPathName);
        }
    }
}
