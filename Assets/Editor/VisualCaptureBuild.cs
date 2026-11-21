using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FrutigerBadminton.Editor
{
    /// <summary>Builds a Linux player for optional automated visual captures.</summary>
    public static class VisualCaptureBuild
    {
        public static void BuildLinux()
        {
            AeroCourtProjectSetup.EnsureProjectAssets();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            const string scene = "Assets/Scenes/AeroCourt.unity";
            if (!File.Exists(scene)) throw new FileNotFoundException("The Aero Court scene was not generated.", scene);
            const string output = "build/StandaloneLinux64";
            Directory.CreateDirectory(output);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = Path.Combine(output, "FrutigerBadminton.x86_64"),
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Linux capture player build failed with result " + report.summary.result + ".");
            Debug.Log("Linux capture player built at " + options.locationPathName);
        }
    }
}
