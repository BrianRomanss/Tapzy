using System;
using System.Collections.Generic;
using Tapzy.Contract;

namespace Tapzy.Core
{
    /// <summary>
    /// The single place that talks to the server. Every coin change goes through here and the
    /// app only displays what comes back. Milestone 1-2 use <see cref="LocalBackend"/>; the
    /// Firebase implementation replaces that one class (see Services.CreateBackend).
    /// Callbacks may arrive on a later frame.
    /// </summary>
    public interface IBackend
    {
        void SignIn(Action<PlayerState> done);
        void GetState(Action<PlayerState> done);
        void ClaimDailyLogin(Action<LoginReply> done);

        void StartRound(string gameId, Action<long> seed);
        void SubmitResult(GameResult result, Action<RoundReply> done);

        /// <summary>Called after a rewarded ad: doubles one completed mission, inside the daily cap.</summary>
        void BoostMission(string missionId, Action<ActionReply> done);

        /// <summary>
        /// Called after a rewarded ad: pays the coins of the round just submitted once more, inside
        /// the daily cap. Missions finished in that round count as boosted, so nothing doubles twice.
        /// </summary>
        void DoubleRound(Action<ActionReply> done);

        void GetLeaderboard(string gameId, Action<Leaderboard> done);

        void GetGiftCards(Action<List<GiftCard>> done);
        void RequestRedemption(string cardId, Action<ActionReply> done);

        void SetDisplayName(string name, Action<ActionReply> done);
        void DeleteMyData(Action<ActionReply> done);
    }
}
