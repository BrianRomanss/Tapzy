using System;

namespace Tapzy.GameKit
{
    /// <summary>
    /// Catalogue entry for a mini-game. Tapzy > Setup Project reads this to create the game's
    /// scene and its GameDefinition asset, so adding a game never touches hub code.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class MiniGameAttribute : Attribute
    {
        /// <summary>Stable id, lower_snake_case. Never change it after release.</summary>
        public string Id { get; }
        public string Title { get; }

        /// <summary>"Arcade", "Puzzle", "Board", "Card" or "Sports".</summary>
        public string Kind = "Arcade";

        /// <summary>Tile colour on the home grid, RRGGBB.</summary>
        public string Colour = "7C5CFF";

        /// <summary>Position on the home grid, ascending.</summary>
        public int Order = 100;

        /// <summary>Anti-cheat: a result is rejected above ScoreBase + MaxScorePerSecond * duration.</summary>
        public float MaxScorePerSecond = 100f;
        public float ScoreBase = 0f;

        /// <summary>Anti-cheat: shortest round that can honestly happen, in seconds.</summary>
        public float MinDuration = 1f;

        /// <summary>Score an average player reaches after a few tries; used for "beat N points" missions.</summary>
        public int MissionScore = 100;

        public MiniGameAttribute(string id, string title)
        {
            Id = id;
            Title = title;
        }
    }
}
