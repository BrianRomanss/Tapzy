using System;

namespace Tapzy.Contract
{
    /// <summary>The one thing a mini-game hands back to the hub.</summary>
    [Serializable]
    public struct GameResult
    {
        public string gameId;
        public int score;
        public float duration;
        public long seed;
    }

    /// <summary>
    /// The whole surface between the hub and a mini-game. The hub calls Begin before loading a
    /// game scene; the game reads Seed, then calls Report (round over) or Abandon (player quit).
    /// Nothing here mentions coins, missions or gift cards, and it must stay that way.
    /// </summary>
    public static class Round
    {
        public static bool Active { get; private set; }
        public static string GameId { get; private set; }
        public static long Seed { get; private set; }
        /// <summary>The player's best score in this game before the round (0 if none), so the game can cheer when it falls.</summary>
        public static int Best { get; private set; }

        /// <summary>Set by the hub. Shows a rewarded ad and calls back with whether it was watched.</summary>
        public static Action<Action<bool>> ContinueProvider;

        static Action<GameResult> onResult;
        static Action onAbandon;

        public static bool CanOfferContinue => Active && ContinueProvider != null;

        public static void Begin(string gameId, long seed, Action<GameResult> result, Action abandon, int best = 0)
        {
            GameId = gameId;
            Seed = seed;
            Best = best;
            onResult = result;
            onAbandon = abandon;
            Active = true;
        }

        public static void Report(int score, float duration)
        {
            if (!Active) return;
            Active = false;
            var cb = onResult;
            onResult = null;
            onAbandon = null;
            cb?.Invoke(new GameResult { gameId = GameId, score = score, duration = duration, seed = Seed });
        }

        public static void Abandon()
        {
            if (!Active) return;
            Active = false;
            var cb = onAbandon;
            onResult = null;
            onAbandon = null;
            cb?.Invoke();
        }

        public static void RequestContinue(Action<bool> done)
        {
            if (CanOfferContinue) ContinueProvider(done);
            else done?.Invoke(false);
        }
    }
}
