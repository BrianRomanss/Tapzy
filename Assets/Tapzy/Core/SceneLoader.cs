using System;
using Tapzy.Contract;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tapzy.Core
{
    /// <summary>Loads a game scene from the hub and comes back to the hub with the server's reply.</summary>
    public class SceneLoader
    {
        /// <summary>The reply for the round that just ended; the hub shows it once and clears it.</summary>
        public RoundReply PendingReply { get; private set; }
        public GameDefinition LastGame { get; private set; }

        int roundsThisSession;
        bool busy;

        public RoundReply TakePendingReply()
        {
            var r = PendingReply;
            PendingReply = null;
            return r;
        }

        public void StartGame(GameDefinition game)
        {
            if (busy || game == null) return;
            if (!Application.CanStreamedLevelBeLoaded(game.sceneName))
            {
                Debug.LogError("Tapzy: scene '" + game.sceneName + "' is not in the build. Run Tapzy > Setup Project.");
                return;
            }
            busy = true;
            LastGame = game;
            Services.Backend.StartRound(game.id, seed =>
            {
                busy = false;
                Round.Begin(game.id, seed, OnResult, ReturnToHub, Services.State != null ? Services.State.BestFor(game.id) : 0);
                SceneManager.LoadScene(game.sceneName);
            });
        }

        void OnResult(GameResult result)
        {
            Services.Backend.SubmitResult(result, reply =>
            {
                Services.Apply(reply.state);
                PendingReply = reply;
                roundsThisSession++;
                // interstitial after every third round, on the way back, never in the first session
                var showAd = roundsThisSession % Economy.InterstitialEveryRounds == 0 && !Services.IsFirstSession;
                if (showAd) Services.Ads.ShowInterstitial(ReturnToHub);
                else ReturnToHub();
            });
        }

        static void ReturnToHub() => SceneManager.LoadScene(Services.HubScene);
    }
}
