using System;
using System.Collections;
using System.IO;
using Tapzy.Contract;
using Tapzy.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tapzy.Hub
{
    /// <summary>
    /// Developer tool, inactive in normal play. Launching the app with
    ///   -tapzyShots "C:\some\folder"
    /// saves a screenshot of every hub tab and of every game's first screen, then quits.
    /// Used to review the look of the whole app without clicking through it.
    /// </summary>
    public static class DevCapture
    {
        static bool started;

        public static void TryStart(HubController hub)
        {
            if (started) return;
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-tapzyShots");
            if (index < 0 || index + 1 >= args.Length) return;
            started = true;
            Directory.CreateDirectory(args[index + 1]);
            Services.Runner.StartCoroutine(Run(args[index + 1]));
        }

        static IEnumerator Run(string folder)
        {
            yield return Shot(folder, "hub_0_home_with_popup");
            foreach (var tab in new[] { HubController.Tab.Home, HubController.Tab.Ranks, HubController.Tab.Redeem, HubController.Tab.Profile })
            {
                var hub = UnityEngine.Object.FindFirstObjectByType<HubController>();
                hub.ClosePopups();
                hub.Show(tab);
                yield return Shot(folder, "hub_" + ((int)tab + 1) + "_" + tab.ToString().ToLowerInvariant());
            }

            foreach (var game in Services.Games)
            {
                Services.SceneLoader.StartGame(game);
                while (SceneManager.GetActiveScene().name != game.sceneName) yield return null;
                yield return Shot(folder, "game_" + game.id);
                Round.Abandon();
                while (SceneManager.GetActiveScene().name != Services.HubScene) yield return null;
                yield return new WaitForSecondsRealtime(0.3f);
            }
            Application.Quit();
        }

        static IEnumerator Shot(string folder, string name)
        {
            yield return new WaitForSecondsRealtime(0.8f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            yield return new WaitForSecondsRealtime(0.3f);
        }
    }
}
