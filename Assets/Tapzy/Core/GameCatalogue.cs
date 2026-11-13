using System.Collections.Generic;
using UnityEngine;

namespace Tapzy.Core
{
    /// <summary>The list of games the hub shows. Loaded from Resources so the hub never names a game in code.</summary>
    public class GameCatalogue : ScriptableObject
    {
        public const string ResourcePath = "TapzyCatalogue";

        public List<GameDefinition> games = new List<GameDefinition>();

        public GameDefinition Find(string id)
        {
            foreach (var g in games)
                if (g != null && g.id == id)
                    return g;
            return null;
        }
    }
}
