using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Tapzy.Contract;
using Tapzy.Core;
using Tapzy.GameKit;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tapzy.Tests
{
    /// <summary>
    /// Boots the app, then starts every game from the hub, lets it run, reports a result and
    /// checks the hub and the coin rules. Any error logged along the way fails the test.
    /// WARNING: this wipes the local save file before and after it runs.
    /// </summary>
    public class SmokeTests
    {
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator BootThenPlayEveryGame()
        {
            SceneManager.LoadScene(Services.BootScene);
            yield return WaitForScene(Services.HubScene);
            yield return Frames(3);
            Assert.IsTrue(Services.Ready, "services signed in");

            Services.Backend.DeleteMyData(r => Services.Apply(r.state));
            Assert.AreEqual(0, Services.Wallet.Balance, "fresh account starts at zero");
            Assert.AreEqual(Economy.MissionsPerDay, Services.Missions.Today.Count, "three missions a day");

            LoginReply login = null;
            Services.Backend.ClaimDailyLogin(r =>
            {
                login = r;
                Services.Apply(r.state);
            });
            Assert.IsTrue(login.granted, "day 1 login reward granted");
            Assert.AreEqual(10, login.coins, "day 1 login reward is 10");
            Services.Backend.ClaimDailyLogin(r => login = r);
            Assert.IsFalse(login.granted, "login reward only once a day");

            var games = Services.Games;
            Assert.AreEqual(20, games.Count, "twenty games in the catalogue");

            foreach (var game in games)
            {
                var before = Services.Wallet.Balance;
                Services.SceneLoader.StartGame(game);
                yield return WaitForScene(game.sceneName);
                yield return Frames(2);

                var mini = UnityEngine.Object.FindFirstObjectByType<MiniGameBase>();
                Assert.IsNotNull(mini, game.title + ": scene has its game component");
                Assert.IsTrue(Round.Active, game.title + ": round is active");
                Assert.AreEqual(game.id, Round.GameId);

                // skip the "tap to start" overlay and let the real gameplay loop run for a while
                Invoke(mini, "CloseOverlay");
                SetState(mini, 1);
                var t = 0f;
                while (t < 1.5f && Round.Active)
                {
                    t += Time.deltaTime;
                    yield return null;
                }

                if (Round.Active) Round.Report(1, 30f);
                yield return WaitForScene(Services.HubScene);
                yield return Frames(3);

                Assert.GreaterOrEqual(Services.State.BestFor(game.id), 1, game.title + ": result accepted and best recorded");
                Assert.GreaterOrEqual(Services.Wallet.Balance - before, Economy.FirstRoundReward, game.title + ": first round of the day paid");
            }

            var state = Services.State;
            Assert.AreEqual(state.earnedToday, state.balance, "every coin is counted against today's cap");
            Assert.LessOrEqual(state.earnedToday, Economy.DailyCap, "daily cap holds");
            // login 10 + 20 first rounds 40 + "3 different games" 20; the third mission is either
            // "5 rounds" (done here, +20) or "2 rounds of one game" (not done here), depending on the day
            Assert.That(state.balance, Is.EqualTo(70).Or.EqualTo(90), "login, first rounds and play missions paid");

            // the rewarded "double it" pays the last round (a first-round bonus) once more, and only once
            ActionReply twice = null;
            Services.Backend.DoubleRound(r => twice = r);
            Assert.IsTrue(twice.ok, "last round can be doubled");
            Assert.AreEqual(Economy.FirstRoundReward, twice.coins, "doubling pays the round's coins again");
            Assert.AreEqual(state.balance + Economy.FirstRoundReward, twice.state.balance, "bonus reaches the balance");
            Services.Backend.DoubleRound(r => twice = r);
            Assert.IsFalse(twice.ok, "a round doubles only once");

            // a replayed seed and an impossible score must both be refused
            RoundReply reply = null;
            Services.Backend.SubmitResult(new GameResult { gameId = games[0].id, score = 5, duration = 30f, seed = 12345 }, r => reply = r);
            Assert.IsFalse(reply.accepted, "unknown seed rejected");
            long seed = 0;
            Services.Backend.StartRound(games[0].id, s => seed = s);
            Services.Backend.SubmitResult(new GameResult { gameId = games[0].id, score = 99999999, duration = 30f, seed = seed }, r => reply = r);
            Assert.IsFalse(reply.accepted, "impossible score rejected");

            Services.Backend.DeleteMyData(r => Services.Apply(r.state));
        }

        static IEnumerator WaitForScene(string sceneName)
        {
            var t = 0f;
            while (SceneManager.GetActiveScene().name != sceneName)
            {
                t += Time.unscaledDeltaTime;
                Assert.Less(t, 15f, "timed out waiting for scene " + sceneName);
                yield return null;
            }
        }

        static IEnumerator Frames(int n)
        {
            for (var i = 0; i < n; i++) yield return null;
        }

        static void Invoke(MiniGameBase game, string method)
        {
            typeof(MiniGameBase).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, null);
        }

        static void SetState(MiniGameBase game, int value)
        {
            var field = typeof(MiniGameBase).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(game, Enum.ToObject(field.FieldType, value));
        }
    }
}
