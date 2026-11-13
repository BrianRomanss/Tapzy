using System;
using System.Collections.Generic;
using Tapzy.Contract;
using Tapzy.UI;
using UnityEngine;

namespace Tapzy.Core
{
    /// <summary>
    /// The shared services that stay alive across every scene. Created once (by the Boot scene,
    /// or lazily if the Hub scene is played directly) and never destroyed.
    /// </summary>
    public static class Services
    {
        public const string HubScene = "Hub";
        public const string BootScene = "Boot";

        public static IBackend Backend { get; private set; }
        public static GameCatalogue Catalogue { get; private set; }
        public static Auth Auth { get; private set; }
        public static Wallet Wallet { get; private set; }
        public static Missions Missions { get; private set; }
        public static IAds Ads { get; private set; }
        public static SceneLoader SceneLoader { get; private set; }
        public static MonoBehaviour Runner { get; private set; }

        /// <summary>Latest state the backend reported. Null until signed in.</summary>
        public static PlayerState State { get; private set; }

        /// <summary>Raised whenever the backend reports a new state. Screens redraw from it.</summary>
        public static event Action Changed;

        public static bool Ready => State != null;
        public static bool IsFirstSession => State == null || State.sessions <= 1;

        public static bool SoundOn
        {
            get => Sfx.Enabled;
            set => Sfx.Enabled = value;
        }

        /// <summary>Creates the services if needed, signs in, then calls <paramref name="ready"/>.</summary>
        public static void Ensure(Action ready)
        {
            if (Runner == null)
            {
                var go = new GameObject("TapzyServices");
                UnityEngine.Object.DontDestroyOnLoad(go);
                Runner = go.AddComponent<ServiceRunner>();

                Catalogue = Resources.Load<GameCatalogue>(GameCatalogue.ResourcePath);
                if (Catalogue == null)
                {
                    Debug.LogError("Tapzy: catalogue not found. Run Tapzy > Setup Project in the editor.");
                    Catalogue = ScriptableObject.CreateInstance<GameCatalogue>();
                }
                Backend = CreateBackend();
                Auth = new Auth();
                Wallet = new Wallet();
                Missions = new Missions();
                Ads = new PlaceholderAds();
                SceneLoader = new SceneLoader();
                Round.ContinueProvider = done => Ads.ShowRewarded(done);
            }
            if (Ready)
            {
                ready?.Invoke();
                return;
            }
            Backend.SignIn(state =>
            {
                Apply(state);
                ready?.Invoke();
            });
        }

        /// <summary>Milestone 3: return the Firebase backend here. Nothing else in the app changes.</summary>
        static IBackend CreateBackend() => new LocalBackend(Catalogue);

        public static void Apply(PlayerState state)
        {
            if (state == null) return;
            State = state;
            Changed?.Invoke();
        }

        public static void Refresh(Action done = null)
        {
            Backend.GetState(state =>
            {
                // screens rebuild on every change, so only report a state that really differs
                if (State == null || JsonUtility.ToJson(state) != JsonUtility.ToJson(State)) Apply(state);
                done?.Invoke();
            });
        }

        public static List<GameDefinition> Games
        {
            get
            {
                var list = Catalogue.games.FindAll(g => g != null);
                list.Sort((a, b) => a.order.CompareTo(b.order));
                return list;
            }
        }

        class ServiceRunner : MonoBehaviour
        {
            // picks up a new day when the app comes back from the background
            void OnApplicationFocus(bool focus)
            {
                if (focus && Ready) Refresh();
            }
        }
    }

    /// <summary>Sign-in state and the player's id. Phone sign-in arrives with the Firebase backend.</summary>
    public class Auth
    {
        public string PlayerId => Services.State?.playerId;
        public string DisplayName => Services.State?.displayName ?? "";
        public bool IsGuest => Services.State == null || Services.State.isGuest;
    }

    /// <summary>Shows the coin balance. It never edits it: every change is the backend's.</summary>
    public class Wallet
    {
        public int Balance => Services.State?.balance ?? 0;
        public int EarnedToday => Services.State?.earnedToday ?? 0;
        public int RemainingToday => Services.State?.RemainingToday ?? 0;
        public int DailyCap => Services.State?.dailyCap ?? Economy.DailyCap;

        /// <summary>Rewarded ad, then the backend pays the last round's coins once more inside the daily cap.</summary>
        public void DoubleLastRound(Action<ActionReply> done)
        {
            Services.Ads.ShowRewarded(watched =>
            {
                if (!watched)
                {
                    done?.Invoke(new ActionReply { ok = false, message = "Ad not finished." });
                    return;
                }
                Services.Backend.DoubleRound(reply =>
                {
                    Services.Apply(reply.state);
                    done?.Invoke(reply);
                });
            });
        }
    }

    /// <summary>Today's missions and their progress.</summary>
    public class Missions
    {
        static readonly List<MissionState> None = new List<MissionState>();

        public List<MissionState> Today => Services.State?.missions ?? None;

        /// <summary>Rewarded ad, then the backend doubles one completed mission inside the daily cap.</summary>
        public void Boost(MissionState mission, Action<ActionReply> done)
        {
            Services.Ads.ShowRewarded(watched =>
            {
                if (!watched)
                {
                    done?.Invoke(new ActionReply { ok = false, message = "Ad not finished." });
                    return;
                }
                Services.Backend.BoostMission(mission.id, reply =>
                {
                    Services.Apply(reply.state);
                    done?.Invoke(reply);
                });
            });
        }
    }
}
