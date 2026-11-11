using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Crashout.Editor
{
    /// <summary>
    /// Adds screen-space ambient occlusion to the project renderer so cars, kerbs and buildings sit in
    /// their contact shadows. Runs automatically on editor load and from Crashout > Configure Visual Quality.
    /// Batch mode: Unity -batchmode -quit -projectPath Unity -executeMethod Crashout.Editor.VisualQualitySetup.Configure
    /// </summary>
    [InitializeOnLoad]
    public static class VisualQualitySetup
    {
        private const string RendererPath = "Assets/Crashout/CrashoutRenderer.asset";

        static VisualQualitySetup()
        {
            EditorApplication.delayCall += () => Apply(false);
        }

        [MenuItem("Crashout/Configure Visual Quality")]
        public static void Configure()
        {
            Apply(true);
        }

        private static void Apply(bool verbose)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null) return;
            foreach (ScriptableRendererFeature feature in renderer.rendererFeatures)
            {
                if (feature is ScreenSpaceAmbientOcclusion)
                {
                    if (verbose) Debug.Log("Crashout: SSAO already configured.");
                    return;
                }
            }

            var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = "Screen Space Ambient Occlusion";
            AssetDatabase.AddObjectToAsset(ssao, renderer);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out _, out long localId);

            var settings = new SerializedObject(ssao);
            SetFloat(settings, "m_Settings.Intensity", 1.4f);
            SetFloat(settings, "m_Settings.Radius", 0.6f);
            SetFloat(settings, "m_Settings.Falloff", 80f);
            SetFloat(settings, "m_Settings.DirectLightingStrength", 0.2f);
            settings.ApplyModifiedPropertiesWithoutUndo();

            var serialized = new SerializedObject(renderer);
            SerializedProperty features = serialized.FindProperty("m_RendererFeatures");
            SerializedProperty map = serialized.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ssao;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssets();
            Debug.Log("Crashout: added SSAO to the renderer.");
        }

        private static void SetFloat(SerializedObject target, string path, float value)
        {
            SerializedProperty property = target.FindProperty(path);
            if (property != null) property.floatValue = value;
        }
    }
}
