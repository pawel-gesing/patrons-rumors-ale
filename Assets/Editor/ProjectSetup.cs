using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PatronsRumorsAle.Editor
{
    public static class ProjectSetup
    {
        [MenuItem("Patrons Rumors Ale/Regenerate Prototype Scene")]
        public static void Generate()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "PrototypeTavern";

            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.055f, 0.04f);
            cameraObject.tag = "MainCamera";

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/PrototypeTavern.unity");
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/PrototypeTavern.unity", true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log("PrototypeTavern scene and build settings generated.");
        }
    }
}

