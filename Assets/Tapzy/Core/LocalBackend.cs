using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Tapzy.Contract;
using UnityEngine;

namespace Tapzy.Core
{
    /// <summary>
    /// Stand-in for the server (milestones 1 and 2). It applies the real economy rules but saves
    /// to the device and trusts the device clock, so it can be cheated by editing the save file.
    /// That is expected: milestone 3 replaces this class with the Firebase backend and nothing
    /// else changes. Redemption is deliberately not available here.
    /// </summary>
    public class LocalBackend : IBackend
    {
        [Serializable]
        class Save
        {
            public string playerId;
            public string displayName;
            public string createdDay;
            public int sessions;
            public int balance;

            public string day;
            public int earnedToday;
            public int roundsToday;
            public List<string> gamesToday = new List<string>();
            public List<string> firstRoundPaid = new List<string>();
            public List<MissionState> missions = new List<MissionState>();

            public int streak;
            public string lastLoginDay;

            public List<BestScore> bests = new List<BestScore>();
            public List<long> openSeeds = new List<long>();

            public int lastRoundCoins;                                    // what a "double it" ad would pay again
            public List<string> lastRoundMissions = new List<string>();   // missions finished in that round
        }

        static readonly string[] BotNames =
        {
            "Aarav", "Diya", "Kabir", "Meera", "Rohan", "Isha", "Vivaan", "Anaya", "Arjun", "Sara",
            "Dev", "Kiara", "Reyansh", "Myra", "Aditya", "Zoya", "Ishaan", "Tara", "Krish", "Naina"
        };

        readonly GameCatalogue catalogue;
        readonly string path;
        readonly System.Random seedRng = new System.Random();
        Save save;

        public LocalBackend(GameCatalogue catalogue)
        {
            this.catalogue = catalogue;
            path = Path.Combine(Application.persistentDataPath, "tapzy_save.json");
            Load();
        }

        // ---------- IBackend ----------

        public void SignIn(Action<PlayerState> done)
        {
            save.sessions++;
            Roll();
            Write();
            done?.Invoke(State());
        }

        public void GetState(Action<PlayerState> done)
        {
            if (Roll()) Write();
            done?.Invoke(State());
        }

        public void ClaimDailyLogin(Action<LoginReply> done)
        {
            Roll();
            var reply = new LoginReply();
            if (save.lastLoginDay != save.day)
            {
                save.streak = save.lastLoginDay == DayKey(Today.AddDays(-1))
                    ? Math.Min(save.streak + 1, Economy.MaxStreakDay)
                    : 1;
                save.lastLoginDay = save.day;
                reply.granted = true;
                reply.streakDay = save.streak;
                reply.coins = Grant(Economy.LoginReward(save.streak));
                Write();
            }
            reply.state = State();
            done?.Invoke(reply);
        }

        public void StartRound(string gameId, Action<long> seed)
        {
            var bytes = new byte[8];
            seedRng.NextBytes(bytes);
            var s = BitConverter.ToInt64(bytes, 0);
            save.openSeeds.Add(s);
            while (save.openSeeds.Count > 8) save.openSeeds.RemoveAt(0);
            Write();
            seed?.Invoke(s);
        }

        public void SubmitResult(GameResult result, Action<RoundReply> done)
        {
            Roll();
            save.lastRoundCoins = 0;
            save.lastRoundMissions.Clear();
            var reply = new RoundReply { gameId = result.gameId, score = result.score };
            var def = catalogue != null ? catalogue.Find(result.gameId) : null;

            // the same checks the server will make
            if (def == null) reply.rejectReason = "Unknown game.";
            else if (!save.openSeeds.Remove(result.seed)) reply.rejectReason = "This round was already submitted.";
            else if (result.score < 0) reply.rejectReason = "Invalid score.";
            else if (result.duration < def.minDuration) reply.rejectReason = "Round was too short to count.";
            else if (result.score > def.scoreBase + def.maxScorePerSecond * result.duration) reply.rejectReason = "Score could not be verified.";

            if (reply.rejectReason != null)
            {
                reply.personalBest = BestFor(result.gameId).best;
                Write();
                reply.state = State();
                done?.Invoke(reply);
                return;
            }

            reply.accepted = true;

            var best = BestFor(result.gameId);
            var week = WeekKey();
            if (best.week != week)
            {
                best.week = week;
                best.weekBest = 0;
            }
            best.weekBest = Math.Max(best.weekBest, result.score);
            if (result.score > best.best)
            {
                reply.newBest = best.best > 0;
                best.best = result.score;
            }
            reply.personalBest = best.best;

            save.roundsToday++;
            if (!save.gamesToday.Contains(result.gameId)) save.gamesToday.Add(result.gameId);

            if (!save.firstRoundPaid.Contains(result.gameId))
            {
                save.firstRoundPaid.Add(result.gameId);
                AddLine(reply, "First round today", Grant(Economy.FirstRoundReward));
            }

            foreach (var m in save.missions)
            {
                if (m.completed) continue;
                switch (m.type)
                {
                    case MissionType.PlayDifferentGames: m.progress = save.gamesToday.Count; break;
                    case MissionType.PlayRounds: m.progress = save.roundsToday; break;
                    case MissionType.ScoreInGame:
                        if (m.gameId == result.gameId) m.progress = Math.Max(m.progress, result.score);
                        break;
                    case MissionType.PlayGame:
                        if (m.gameId == result.gameId) m.progress++;
                        break;
                }
                if (m.progress < m.target) continue;
                m.progress = m.target;
                m.completed = true;
                AddLine(reply, "Mission: " + m.description, Grant(m.reward));
                save.lastRoundMissions.Add(m.id);
            }

            reply.capReached = save.earnedToday >= Economy.DailyCap;
            save.lastRoundCoins = reply.coinsEarned;
            reply.doubleCoins = Math.Min(reply.coinsEarned, Economy.DailyCap - save.earnedToday);
            Write();
            reply.state = State();
            done?.Invoke(reply);
        }

        public void BoostMission(string missionId, Action<ActionReply> done)
        {
            Roll();
            var reply = new ActionReply();
            var m = save.missions.Find(x => x.id == missionId);
            if (m == null || !m.completed) reply.message = "Finish the mission first.";
            else if (m.boosted) reply.message = "Already boosted.";
            else
            {
                m.boosted = true;
                var got = Grant(m.reward);
                reply.ok = true;
                reply.message = got > 0 ? "+" + got + " coins" : "Daily limit reached. Come back tomorrow!";
                Write();
            }
            reply.state = State();
            done?.Invoke(reply);
        }

        public void DoubleRound(Action<ActionReply> done)
        {
            Roll();
            var reply = new ActionReply();
            if (save.lastRoundCoins <= 0) reply.message = "Nothing to double.";
            else
            {
                reply.coins = Grant(save.lastRoundCoins);
                reply.ok = true;
                reply.message = reply.coins > 0 ? "+" + reply.coins + " bonus coins" : "Daily limit reached. Come back tomorrow!";
                // their reward has now been doubled, so the separate mission boost is used up
                foreach (var id in save.lastRoundMissions)
                {
                    var m = save.missions.Find(x => x.id == id);
                    if (m != null) m.boosted = true;
                }
                save.lastRoundCoins = 0;
                save.lastRoundMissions.Clear();
                Write();
            }
            reply.state = State();
            done?.Invoke(reply);
        }

        public void GetLeaderboard(string gameId, Action<Leaderboard> done)
        {
            var board = new Leaderboard { gameId = gameId };
            var def = catalogue != null ? catalogue.Find(gameId) : null;
            var scale = def != null ? Math.Max(10, def.missionScore) : 100;
            var rng = new System.Random(Hash(WeekKey() + gameId));
            foreach (var botName in BotNames)
                board.top.Add(new LeaderboardEntry { name = botName, score = (int)(scale * (0.5 + rng.NextDouble() * 3.0)) });

            var best = BestFor(gameId);
            if (best.week == WeekKey() && best.weekBest > 0)
            {
                board.player = new LeaderboardEntry { name = save.displayName, score = best.weekBest, isPlayer = true };
                board.top.Add(board.player);
            }
            board.top.Sort((a, b) => b.score.CompareTo(a.score));
            for (var i = 0; i < board.top.Count; i++) board.top[i].rank = i + 1;
            done?.Invoke(board);
        }

        public void GetGiftCards(Action<List<GiftCard>> done)
        {
            var cards = new List<GiftCard>();
            foreach (var brand in new[] { "Amazon Pay", "Google Play" })
            foreach (var rupees in new[] { 10, 25, 50 })
                cards.Add(new GiftCard
                {
                    id = brand.Replace(" ", "").ToLowerInvariant() + "_" + rupees,
                    brand = brand,
                    rupees = rupees,
                    price = rupees * Economy.CoinsPerRupee
                });
            done?.Invoke(cards);
        }

        public void RequestRedemption(string cardId, Action<ActionReply> done)
        {
            // No coins ever leave the local stand-in: redemption needs the server (milestone 5).
            done?.Invoke(new ActionReply
            {
                ok = false,
                message = "Gift cards open when Tapzy goes online. Your coins are safe until then.",
                state = State()
            });
        }

        public void SetDisplayName(string name, Action<ActionReply> done)
        {
            name = (name ?? "").Trim();
            var reply = new ActionReply();
            if (name.Length < 3 || name.Length > 16) reply.message = "Use 3 to 16 characters.";
            else
            {
                save.displayName = name;
                Write();
                reply.ok = true;
                reply.message = "Saved.";
            }
            reply.state = State();
            done?.Invoke(reply);
        }

        public void DeleteMyData(Action<ActionReply> done)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Tapzy: could not delete save: " + e.Message);
            }
            save = null;
            Load();
            save.sessions = 1;
            Roll();
            Write();
            done?.Invoke(new ActionReply { ok = true, message = "Your data was deleted.", state = State() });
        }

        // ---------- rules ----------

        /// <summary>Adds coins up to the daily cap and returns how many were actually granted.</summary>
        int Grant(int coins)
        {
            var granted = Mathf.Clamp(coins, 0, Economy.DailyCap - save.earnedToday);
            save.earnedToday += granted;
            save.balance += granted;
            return granted;
        }

        static void AddLine(RoundReply reply, string label, int coins)
        {
            reply.lines.Add(new CoinLine { label = label, coins = coins });
            reply.coinsEarned += coins;
        }

        /// <summary>Starts a new day if the date changed. Returns true if anything was reset.</summary>
        bool Roll()
        {
            var today = DayKey(Today);
            if (save.day == today)
            {
                // same day: only fill in missions if there were no games in the catalogue earlier
                if (save.missions.Count > 0) return false;
                save.missions = MakeMissions();
                return save.missions.Count > 0;
            }
            save.day = today;
            save.earnedToday = 0;
            save.roundsToday = 0;
            save.gamesToday.Clear();
            save.firstRoundPaid.Clear();
            save.lastRoundCoins = 0;
            save.lastRoundMissions.Clear();
            save.missions = MakeMissions();
            if (save.lastLoginDay != today && save.lastLoginDay != DayKey(Today.AddDays(-1))) save.streak = 0;
            return true;
        }

        List<MissionState> MakeMissions()
        {
            var list = new List<MissionState>();
            var games = catalogue != null ? catalogue.games.FindAll(g => g != null) : new List<GameDefinition>();
            var n = games.Count;
            if (n == 0) return list;
            var dayNumber = (int)(Today - new DateTime(2026, 1, 1)).TotalDays;

            // 1: variety, so players try every game
            if (n >= 2)
            {
                var count = Math.Min(3, n);
                list.Add(Mission(MissionType.PlayDifferentGames, null, count, "Play " + count + " different games"));
            }
            else list.Add(Mission(MissionType.PlayRounds, null, 3, "Play 3 rounds"));

            // 2: skill, in a game that rotates daily
            var skill = games[Mod(dayNumber, n)];
            list.Add(Mission(MissionType.ScoreInGame, skill.id, skill.missionScore,
                "Score " + skill.missionScore.ToString("N0") + " in " + skill.title));

            // 3: volume, alternating between any game and one particular other game
            var other = games[Mod(dayNumber + 1 + n / 2, n)];
            if (n >= 2 && dayNumber % 2 == 0 && other != skill)
                list.Add(Mission(MissionType.PlayGame, other.id, 2, "Play 2 rounds of " + other.title));
            else
                list.Add(Mission(MissionType.PlayRounds, null, 5, "Play 5 rounds"));

            for (var i = 0; i < list.Count; i++) list[i].id = save.day + "_" + i;
            return list;
        }

        static MissionState Mission(MissionType type, string gameId, int target, string description)
        {
            return new MissionState
            {
                type = type, gameId = gameId, target = target, description = description, reward = Economy.MissionReward
            };
        }

        PlayerState State()
        {
            var claimable = save.lastLoginDay != save.day;
            var streakDay = claimable
                ? (save.lastLoginDay == DayKey(Today.AddDays(-1)) ? Math.Min(save.streak + 1, Economy.MaxStreakDay) : 1)
                : Math.Max(1, save.streak);
            var created = DateTime.TryParseExact(save.createdDay, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var c) ? c : Today;

            // copies, so UI code can never mutate the "server" data
            var json = JsonUtility.ToJson(new Lists { missions = save.missions, bests = save.bests });
            var copy = JsonUtility.FromJson<Lists>(json);

            return new PlayerState
            {
                playerId = save.playerId,
                displayName = save.displayName,
                isGuest = true,
                accountAgeDays = Math.Max(0, (int)(Today - created).TotalDays),
                sessions = save.sessions,
                balance = save.balance,
                earnedToday = save.earnedToday,
                dailyCap = Economy.DailyCap,
                streakDay = streakDay,
                loginClaimable = claimable,
                loginReward = Economy.LoginReward(streakDay),
                missions = copy.missions,
                bests = copy.bests
            };
        }

        [Serializable]
        class Lists
        {
            public List<MissionState> missions;
            public List<BestScore> bests;
        }

        BestScore BestFor(string gameId)
        {
            var b = save.bests.Find(x => x.gameId == gameId);
            if (b == null)
            {
                b = new BestScore { gameId = gameId };
                save.bests.Add(b);
            }
            return b;
        }

        // ---------- storage and time ----------

        static DateTime Today => DateTime.Now.Date;
        const string DayFormat = "yyyy-MM-dd";
        // invariant culture: the key must not depend on the phone's language or calendar
        static string DayKey(DateTime d) => d.ToString(DayFormat, CultureInfo.InvariantCulture);
        static int Mod(int a, int n) => (a % n + n) % n;

        static string WeekKey()
        {
            var d = Today;
            var sinceMonday = ((int)d.DayOfWeek + 6) % 7;
            return DayKey(d.AddDays(-sinceMonday));
        }

        static int Hash(string s)
        {
            unchecked
            {
                var h = (int)2166136261;
                foreach (var ch in s) h = (h ^ ch) * 16777619;
                return h;
            }
        }

        void Load()
        {
            try
            {
                if (File.Exists(path)) save = JsonUtility.FromJson<Save>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Tapzy: save file unreadable, starting fresh: " + e.Message);
            }
            if (save == null || string.IsNullOrEmpty(save.playerId))
            {
                save = new Save
                {
                    playerId = Guid.NewGuid().ToString("N"),
                    displayName = "Player" + new System.Random().Next(1000, 9999),
                    createdDay = DayKey(Today)
                };
            }
        }

        void Write()
        {
            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(save));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Tapzy: could not save: " + e.Message);
            }
        }
    }
}
