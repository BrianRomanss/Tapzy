using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Tapzy.Core;
using Tapzy.GameKit;
using Tapzy.Hub;
using Tapzy.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tapzy.Editor
{
    /// <summary>
    /// Generates everything that is not code: the Boot and Hub scenes, one scene and one
    /// GameDefinition per [MiniGame] class, the catalogue, and the build scene list.
    /// Runs by itself when something is missing; Tapzy > Setup Project runs it on demand.
    /// It only ever creates or updates files under Assets/Tapzy and never deletes anything.
    /// </summary>
    [InitializeOnLoad]
    public static class TapzySetup
    {
        const string Root = "Assets/Tapzy";
        const string SceneDir = Root + "/Scenes";
        const string CatalogueDir = Root + "/Catalogue";
        const string ResourcesDir = Root + "/Resources";
        const string CataloguePath = ResourcesDir + "/" + GameCatalogue.ResourcePath + ".asset";

        static TapzySetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                if (NeedsSetup()) Run();
            };
        }

        [MenuItem("Tapzy/Setup Project")]
        public static void Run()
        {
            var active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && active.isDirty)
            {
                Debug.LogWarning("Tapzy: save the open scene first, then run Tapzy > Setup Project.");
                return;
            }

            foreach (var dir in new[] { Root, SceneDir, CatalogueDir, ResourcesDir }) EnsureFolder(dir);

            var definitions = new List<GameDefinition>();
            foreach (var type in GameTypes())
            {
                var info = type.GetCustomAttribute<MiniGameAttribute>();
                var sceneName = SceneName(type);
                var path = CatalogueDir + "/" + info.Id + ".asset";
                var def = AssetDatabase.LoadAssetAtPath<GameDefinition>(path);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<GameDefinition>();
                    AssetDatabase.CreateAsset(def, path);
                }
                def.id = info.Id;
                def.title = info.Title;
                def.kind = info.Kind;
                def.sceneName = sceneName;
                def.tileColour = Theme.Hex(info.Colour);
                def.order = info.Order;
                def.maxScorePerSecond = info.MaxScorePerSecond;
                def.scoreBase = info.ScoreBase;
                def.minDuration = info.MinDuration;
                def.missionScore = info.MissionScore;
                EditorUtility.SetDirty(def);
                definitions.Add(def);
                CreateScene(sceneName, type);
            }
            definitions.Sort((a, b) => a.order.CompareTo(b.order));

            var catalogue = AssetDatabase.LoadAssetAtPath<GameCatalogue>(CataloguePath);
            if (catalogue == null)
            {
                catalogue = ScriptableObject.CreateInstance<GameCatalogue>();
                AssetDatabase.CreateAsset(catalogue, CataloguePath);
            }
            catalogue.games = definitions;
            EditorUtility.SetDirty(catalogue);

            var bootCreated = CreateScene(Services.BootScene, typeof(BootController));
            CreateScene(Services.HubScene, typeof(HubController));

            var scenes = new List<string> { Services.BootScene, Services.HubScene };
            scenes.AddRange(definitions.Select(d => d.sceneName));
            EditorBuildSettings.scenes = scenes.Select(s => new EditorBuildSettingsScene(ScenePath(s), true)).ToArray();

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (bootCreated && !SceneManager.GetActiveScene().isDirty)
                EditorSceneManager.OpenScene(ScenePath(Services.BootScene), OpenSceneMode.Single);

            Debug.Log("Tapzy: setup complete. " + definitions.Count + " games: " +
                      string.Join(", ", definitions.Select(d => d.title)) + ". Open the Boot scene and press Play.");
        }

        static bool NeedsSetup()
        {
            if (!File.Exists(ScenePath(Services.BootScene)) || !File.Exists(ScenePath(Services.HubScene))) return true;
            var catalogue = AssetDatabase.LoadAssetAtPath<GameCatalogue>(CataloguePath);
            if (catalogue == null) return true;
            foreach (var type in GameTypes())
            {
                var info = type.GetCustomAttribute<MiniGameAttribute>();
                if (catalogue.Find(info.Id) == null || !File.Exists(ScenePath(SceneName(type)))) return true;
            }
            return false;
        }

        static IEnumerable<Type> GameTypes()
        {
            return TypeCache.GetTypesDerivedFrom<MiniGameBase>()
                .Where(t => !t.IsAbstract && t.GetCustomAttribute<MiniGameAttribute>() != null)
                .OrderBy(t => t.GetCustomAttribute<MiniGameAttribute>().Order);
        }

        /// <summary>LaneRushGame -> Game_LaneRush</summary>
        static string SceneName(Type type)
        {
            var n = type.Name;
            if (n.EndsWith("Game", StringComparison.Ordinal)) n = n.Substring(0, n.Length - 4);
            return "Game_" + n;
        }

        static string ScenePath(string sceneName) => SceneDir + "/" + sceneName + ".unity";

        /// <summary>Creates a scene holding a camera and one controller object. Returns false if it already existed.</summary>
        static bool CreateScene(string sceneName, Type controller)
        {
            var path = ScenePath(sceneName);
            if (File.Exists(path)) return false;

            var additive = !string.IsNullOrEmpty(SceneManager.GetActiveScene().path);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, additive ? NewSceneMode.Additive : NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Theme.Bg;
            cam.orthographic = true;
            camGo.AddComponent<AudioListener>();
            SceneManager.MoveGameObjectToScene(camGo, scene);

            var go = new GameObject(controller.Name);
            go.AddComponent(controller);
            SceneManager.MoveGameObjectToScene(go, scene);

            EditorSceneManager.SaveScene(scene, path);
            if (additive) EditorSceneManager.CloseScene(scene, true);
            return true;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
