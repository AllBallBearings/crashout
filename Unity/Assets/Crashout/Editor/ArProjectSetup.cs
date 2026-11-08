using System.Collections.Generic;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;

namespace Crashout.Editor
{
    /// <summary>
    /// One-time project configuration AR needs on iPhone: ARKit as the iOS XR loader,
    /// the AR camera-background feature on the render pipeline's renderer, and a camera
    /// usage string. Safe to run repeatedly.
    /// </summary>
    [InitializeOnLoad]
    public static class ArProjectSetup
    {
        private const string PipelinePath = "Assets/Crashout/CrashoutPipeline.asset";
        private const string RendererPath = "Assets/Crashout/CrashoutRenderer.asset";
        private const string PackageRendererPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset";
        private const string XrSettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        private const string ArKitSettingsPath = "Assets/XR/Settings/ARKitSettings.asset";
        private const string CameraUsage = "Crashout uses the camera to show the game on your table.";

        static ArProjectSetup()
        {
            EditorApplication.delayCall += () => Configure(false);
        }

        [MenuItem("Crashout/Configure AR For iPhone")]
        public static void ConfigureFromMenu()
        {
            Configure(true);
        }

        public static void Configure(bool verbose)
        {
            var changes = new List<string>();
            ConfigureXrLoader(changes);
            ConfigureRenderer(changes);
            ConfigureArKitRequirement(changes);
            if (PlayerSettings.iOS.cameraUsageDescription != CameraUsage)
            {
                PlayerSettings.iOS.cameraUsageDescription = CameraUsage;
                changes.Add("camera usage description");
            }

            if (changes.Count > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("Crashout AR setup changed: " + string.Join(", ", changes));
            }
            else if (verbose)
            {
                Debug.Log("Crashout AR setup already complete.");
            }
        }

        private static void ConfigureXrLoader(List<string> changes)
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
            {
                perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(XrSettingsPath);
                if (perTarget == null)
                {
                    perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(perTarget, XrSettingsPath);
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
                changes.Add("XR settings asset");
            }

            const BuildTargetGroup group = BuildTargetGroup.iOS;
            if (!perTarget.HasSettingsForBuildTarget(group))
            {
                perTarget.CreateDefaultSettingsForBuildTarget(group);
                changes.Add("iOS XR general settings");
            }
            if (!perTarget.HasManagerSettingsForBuildTarget(group))
            {
                perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
                changes.Add("iOS XR manager");
            }

            XRGeneralSettings general = perTarget.SettingsForBuildTarget(group);
            if (!general.InitManagerOnStart)
            {
                general.InitManagerOnStart = true;
                EditorUtility.SetDirty(general);
                changes.Add("initialise XR on startup");
            }

            XRManagerSettings manager = perTarget.ManagerSettingsForBuildTarget(group);
            if (!XRPackageMetadataStore.IsLoaderAssigned("UnityEngine.XR.ARKit.ARKitLoader", group))
            {
                if (XRPackageMetadataStore.AssignLoader(manager, "UnityEngine.XR.ARKit.ARKitLoader", group))
                {
                    EditorUtility.SetDirty(manager);
                    changes.Add("ARKit loader");
                }
            }
        }

        private static void ConfigureRenderer(List<string> changes)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                if (!AssetDatabase.CopyAsset(PackageRendererPath, RendererPath)) return;
                renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                if (renderer == null) return;
                changes.Add("project renderer asset");
            }

            bool hasFeature = false;
            foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
            {
                if (feature is ARBackgroundRendererFeature) hasFeature = true;
            }
            if (!hasFeature)
            {
                var feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
                feature.name = "AR Background";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId);

                var serialized = new SerializedObject(renderer);
                SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
                SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
                features.arraySize++;
                features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(renderer);
                changes.Add("AR background renderer feature");
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null) return;
            var pipelineObject = new SerializedObject(pipeline);
            SerializedProperty list = pipelineObject.FindProperty("m_RendererDataList");
            if (list.arraySize > 0 && list.GetArrayElementAtIndex(0).objectReferenceValue != renderer)
            {
                list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                pipelineObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pipeline);
                changes.Add("pipeline uses project renderer");
            }
        }

        private static void ConfigureArKitRequirement(List<string> changes)
        {
            // Optional lets the app launch on non-ARKit devices and fall back to the virtual camera.
            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ArKitSettingsPath);
            if (settings == null) return;
            var serialized = new SerializedObject(settings);
            SerializedProperty requirement = serialized.FindProperty("m_Requirement");
            if (requirement != null && requirement.intValue != 1)
            {
                requirement.intValue = 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
                changes.Add("ARKit optional");
            }
        }
    }
}
