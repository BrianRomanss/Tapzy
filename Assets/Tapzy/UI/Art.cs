using System.Collections.Generic;
using UnityEngine;

namespace Tapzy.UI
{
    /// <summary>
    /// Drop-in art. Any PNG placed in Assets/Tapzy/Resources/Art is picked up by file name
    /// (without extension); while a file is missing the game draws its code-made stand-in.
    /// The full list of names is in ArtPrompts.md at the project root.
    /// </summary>
    public static class Art
    {
        const string Folder = "Art/";
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>The sprite with this name, or null if that art has not been added yet.</summary>
        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (cache.TryGetValue(name, out var sprite) && sprite != null) return sprite;
            sprite = Resources.Load<Sprite>(Folder + name);
            cache[name] = sprite;
            return sprite;
        }

        public static bool Has(string name) => Get(name) != null;
    }
}
