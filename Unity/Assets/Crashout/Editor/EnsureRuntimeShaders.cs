using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashout.Editor
{
    /// <summary>
    /// The art layer builds materials at runtime with Shader.Find, so player builds
    /// strip those shaders unless they are listed under Always Included Shaders.
    /// </summary>
    [InitializeOnLoad]
    public static class EnsureRuntimeShaders
    {
        private static readonly string[] ShaderNames =
        {
            "Universal Render Pipeline/Lit",
            "Skybox/Procedural",
            "Sprites/Default"
        };

        static EnsureRuntimeShaders()
        {
            EditorApplication.delayCall += Ensure;
        }

        [MenuItem("Crashout/Ensure Runtime Shaders")]
        public static void Ensure()
        {
            var graphics = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            if (graphics == null) return;
            var serialized = new SerializedObject(graphics);
            SerializedProperty list = serialized.FindProperty("m_AlwaysIncludedShaders");
            bool changed = false;
            foreach (string name in ShaderNames)
            {
                Shader shader = Shader.Find(name);
                if (shader == null) continue;
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) { present = true; break; }
                }
                if (present) continue;
                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                changed = true;
            }
            if (!changed) return;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("Crashout: added runtime shaders to Always Included Shaders.");
        }
    }
}
