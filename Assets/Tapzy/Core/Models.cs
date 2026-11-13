using System;
using System.Collections.Generic;

namespace Tapzy.Core
{
    /// <summary>
    /// Starting values from the GDD's coin economy. These live on the server once the backend
    /// exists; the client copy is only used by the local stand-in and for display.
    /// </summary>
    public static class Economy
    {
        public const int DailyCap = 100;
        public const int LoginBase = 10;
        public const int LoginPerStreakDay = 2;
        public const int MaxStreakDay = 7;
        public const int MissionReward = 20;
        public const int MissionsPerDay = 3;
        public const int FirstRoundReward = 2;
        public const int LeaderboardReward = 200;
        public const int LeaderboardWinners = 10;
        public const int CoinsPerRupee = 500;
        public const int RedeemMinAccountAgeDays = 14;
        public const int InterstitialEveryRounds = 3;

        public static int LoginReward(int streakDay) =>
            LoginBase + LoginPerStreakDay * (Math.Min(Math.Max(streakDay, 1), MaxStreakDay) - 1);
    }

    public enum MissionType { PlayDifferentGames, ScoreInGame, PlayRounds, PlayGame }

    [Serializable]
    public class MissionState
    {
        public string id;
        public MissionType type;
        public string gameId;
        public int target;
        public int progress;
        public bool completed;
        public bool boosted;
        public string description;
        public int reward;
    }

    /// <summary>Everything the hub displays. Always comes from the backend, never computed by the UI.</summary>
    [Serializable]
    public class PlayerState
    {
        public string playerId;
        public string displayName;
        public bool isGuest;
        public int accountAgeDays;
        public int sessions;

        public int balance;
        public int earnedToday;
        public int dailyCap;

        public int streakDay;          // 1..7, the day the next/last claim counts as
        public bool loginClaimable;
        public int loginReward;

        public List<MissionState> missions = new List<MissionState>();
        public List<BestScore> bests = new List<BestScore>();

        public int RemainingToday => Math.Max(0, dailyCap - earnedToday);

        public int BestFor(string gameId)
        {
            foreach (var b in bests)
                if (b.gameId == gameId)
                    return b.best;
            return 0;
        }
    }

    [Serializable]
    public class BestScore
    {
        public string gameId;
        public int best;
        public string week;
        public int weekBest;
    }

    [Serializable]
    public class CoinLine
    {
        public string label;
        public int coins;
    }

    /// <summary>The server's answer to a submitted round. The result screen shows exactly this.</summary>
    public class RoundReply
    {
        public bool accepted;
        public string rejectReason;
        public string gameId;
        public int score;
        public int personalBest;
        public bool newBest;
        public int coinsEarned;
        public bool capReached;
        /// <summary>Coins a rewarded ad would add on top of this round (0 = no offer). Already limited by the daily cap.</summary>
        public int doubleCoins;
        public List<CoinLine> lines = new List<CoinLine>();
        public PlayerState state;
    }

    public class LoginReply
    {
        public bool granted;
        public int coins;
        public int streakDay;
        public PlayerState state;
    }

    public class LeaderboardEntry
    {
        public int rank;
        public string name;
        public int score;
        public bool isPlayer;
    }

    public class Leaderboard
    {
        public string gameId;
        public List<LeaderboardEntry> top = new List<LeaderboardEntry>();
        public LeaderboardEntry player; // null until the player has a score this week
    }

    public class GiftCard
    {
        public string id;
        public string brand;
        public int rupees;
        public int price;
    }

    public class ActionReply
    {
        public bool ok;
        public string message;
        public int coins; // coins granted by this action, if any
        public PlayerState state;
    }
}
