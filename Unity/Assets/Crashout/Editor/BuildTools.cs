using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Crashout.Editor
{
    /// <summary>
    /// Headless iOS build:
    /// Unity -batchmode -quit -projectPath Unity -buildTarget iOS -executeMethod Crashout.Editor.BuildTools.BuildIos -teamId XXXXXXXXXX [-output Builds/iOS] [-development]
    /// Produces an Xcode project; open or xcodebuild it to sign and install.
    /// </summary>
    public static class BuildTools
    {
        public static void BuildIos()
        {
            string teamId = Argument("-teamId");
            string output = Argument("-output") ?? "Builds/iOS";
            bool development = Environment.GetCommandLineArgs().Contains("-development");

            if (!string.IsNullOrEmpty(teamId))
            {
                PlayerSettings.iOS.appleDeveloperTeamID = teamId;
                PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            }
            else if (string.IsNullOrEmpty(PlayerSettings.iOS.appleDeveloperTeamID))
            {
                Debug.LogWarning("No -teamId given and none set in Player Settings; Xcode will need a team chosen by hand.");
            }

            // Make sure the AR project setup is applied before building.
            ArProjectSetup.Configure(false);

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No enabled scenes in the build list.");

            Directory.CreateDirectory(output);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.iOS,
                options = development ? BuildOptions.Development : BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"Crashout iOS build {summary.result}: {summary.totalErrors} errors, {summary.totalWarnings} warnings, {summary.totalSize / (1024 * 1024)} MB -> {summary.outputPath}");
            if (summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
            }
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
