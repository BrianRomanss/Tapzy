using UnityEditor;
using UnityEngine;

namespace Tapzy.Editor
{
    /// <summary>
    /// Makes every image dropped into Assets/Tapzy/Resources/Art usable straight away: imported as
    /// a single sprite with transparency, and buttons / panels get stretch borders so they can be
    /// drawn at any size without distorting their corners.
    /// </summary>
    public class TapzyArtImporter : AssetPostprocessor
    {
        const string Folder = "Assets/Tapzy/Resources/Art/";

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(Folder)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;

            var file = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            importer.GetSourceTextureWidthAndHeight(out var w, out var h);
            if (file.StartsWith("ui_button_"))
            {
                var b = Mathf.Round(Mathf.Min(w, h) * 0.42f);
                importer.spriteBorder = new Vector4(b, b, b, b);
            }
            else if (file.StartsWith("ui_panel_"))
            {
                var b = Mathf.Round(Mathf.Min(w, h) * 0.3f);
                importer.spriteBorder = new Vector4(b, b, b, b);
            }
        }
    }
}
