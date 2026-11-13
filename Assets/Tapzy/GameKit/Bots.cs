using System.Collections.Generic;
using UnityEngine;

namespace Tapzy.GameKit
{
    /// <summary>
    /// A computer opponent for games that need more than one player. The bot runs on the device,
    /// so versus games work offline. Each game supplies its own move logic and asks the bot only
    /// how good it is and how long it "thinks".
    /// </summary>
    public sealed class Bot
    {
        public string Name { get; }

        /// <summary>0 = plays almost at random, 1 = always plays the best move it can find.</summary>
        public float Skill { get; }

        public Bot(string name, float skill)
        {
            Name = name;
            Skill = Mathf.Clamp01(skill);
        }

        /// <summary>Shown wherever the bot is named, so a bot is never passed off as a real player.</summary>
        public string Label => Name + " (CPU)";

        /// <summary>True when the bot should play a careless move this time. Uses the round's seeded generator.</summary>
        public bool Blunders(System.Random rng) => rng.NextDouble() < (1f - Skill) * 0.5f;

        /// <summary>Seconds to wait before acting. Cosmetic, so it leaves the seeded generator untouched.</summary>
        public float ThinkTime() => Random.Range(0.45f, 0.95f);
    }

    public static class Bots
    {
        static readonly string[] Names =
        {
            "Bolt", "Pixel", "Nova", "Dash", "Echo", "Turbo", "Ziggy", "Mango", "Rocket", "Pepper", "Comet", "Biscuit"
        };

        /// <summary>Picks <paramref name="count"/> opponents with different names and a spread of skill.</summary>
        public static Bot[] Pick(System.Random rng, int count, float minSkill = 0.35f, float maxSkill = 0.9f)
        {
            var pool = new List<string>(Names);
            var bots = new Bot[count];
            for (var i = 0; i < count; i++)
            {
                var n = rng.Next(pool.Count);
                var skill = minSkill + (float)rng.NextDouble() * (maxSkill - minSkill);
                bots[i] = new Bot(pool[n], skill);
                pool.RemoveAt(n);
                if (pool.Count == 0) pool.AddRange(Names);
            }
            return bots;
        }
    }
}
