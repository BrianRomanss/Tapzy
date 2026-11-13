using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Tapzy.UI
{
    /// <summary>
    /// Code-built uGUI. Everything is laid out in a 1080 x 1920 portrait reference space.
    /// Positions are anchoredPosition relative to <c>anchor</c> (default: the parent's centre),
    /// with the element's own pivot at its centre.
    /// </summary>
    public static class Ui
    {
        public const float W = 1080f;
        public const float H = 1920f;

        static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        public static readonly Vector2 Top = new Vector2(0.5f, 1f);
        public static readonly Vector2 Bottom = new Vector2(0.5f, 0f);

        static Font font, bodyFont;
        static Sprite circle, rounded;

        /// <summary>Plain, readable font for long text.</summary>
        public static Font BodyFont => bodyFont != null ? bodyFont : bodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// <summary>Chunky display font for titles, buttons and numbers (Resources/Art/font.ttf).</summary>
        public static Font Font
        {
            get
            {
                if (font != null) return font;
                font = Resources.Load<Font>("Art/font");
                if (font == null) font = BodyFont;
                return font;
            }
        }

        /// <summary>White shapes with a thick black rim: tinting colours the inside and keeps the outline black.</summary>
        public static Sprite Circle => circle != null ? circle : circle = MakeCircle(128, 8f);
        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRounded(96, 30, 8f);

        // ---------- roots ----------

        public static Canvas Canvas(string name, int sortOrder)
        {
            EnsureEventSystem();
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(W, H);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        /// <summary>A 1080-wide column, centred, as tall as the screen's safe area. Hub screens and the game HUD live in one.</summary>
        public static RectTransform Column(Transform parent, string name)
        {
            var rt = NewRect(parent, name);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            // keep clear of the notch / status bar at the top and the gesture bar at the bottom
            var scale = Mathf.Min(Screen.width / W, Screen.height / H);
            var safe = Screen.safeArea;
            var bottom = scale > 0f ? safe.yMin / scale : 0f;
            var top = scale > 0f ? (Screen.height - safe.yMax) / scale : 0f;
            rt.offsetMin = new Vector2(-W * 0.5f, bottom);
            rt.offsetMax = new Vector2(W * 0.5f, -top);
            return rt;
        }

        /// <summary>A child stretched to its parent, inset by the given margins.</summary>
        public static RectTransform Fill(Transform parent, string name, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            var rt = NewRect(parent, name);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Node(Transform parent, string name, Vector2 pos, Vector2 size, Vector2? anchor = null)
        {
            var rt = NewRect(parent, name);
            Place(rt, pos, size, anchor);
            return rt;
        }

        // ---------- elements ----------

        public static Image Box(Transform parent, Color color, Vector2 pos, Vector2 size, Vector2? anchor = null, bool rounded = true)
        {
            var rt = NewRect(parent, "Box");
            Place(rt, pos, size, anchor);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            if (rounded)
            {
                img.sprite = Rounded;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        public static Image Disc(Transform parent, Color color, Vector2 pos, float diameter, Vector2? anchor = null)
        {
            var rt = NewRect(parent, "Disc");
            Place(rt, pos, new Vector2(diameter, diameter), anchor);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Circle;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, Vector2 pos, Vector2 box,
            TextAnchor align = TextAnchor.MiddleCenter, Vector2? anchor = null, bool bold = false)
        {
            var rt = NewRect(parent, "Label");
            Place(rt, pos, box, anchor);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = bold ? Font : BodyFont;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = bold && Font == BodyFont ? FontStyle.Bold : FontStyle.Normal;
            if (bold && color.Luminance() > 0.5f)
            {
                // light display text always carries a black outline
                var outline = rt.gameObject.AddComponent<Outline>();
                var d = Mathf.Clamp(size * 0.05f, 1.5f, 5f);
                outline.effectColor = Theme.Outline;
                outline.effectDistance = new Vector2(d, -d);
            }
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Btn(Transform parent, string label, Color color, Vector2 pos, Vector2 size, Action onClick,
            Vector2? anchor = null, int fontSize = 44)
        {
            var img = Box(parent, color, pos, size, anchor);
            img.name = "Btn " + label;
            img.raycastTarget = true;
            var art = string.IsNullOrEmpty(label) ? null : Art.Get(ButtonArt(color));
            if (art != null)
            {
                img.sprite = art;
                img.type = art.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
                img.color = Color.white;
            }
            var drop = img.gameObject.AddComponent<Shadow>(); // hard shadow, no blur
            drop.effectColor = Theme.Outline;
            drop.effectDistance = new Vector2(0f, -Mathf.Clamp(size.y * 0.07f, 5f, 10f));
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
            btn.colors = colors;
            if (onClick != null)
                btn.onClick.AddListener(() =>
                {
                    Sfx.Click();
                    onClick();
                });
            if (!string.IsNullOrEmpty(label))
            {
                var dark = color.Luminance() > 0.6f;
                Label(img.transform, label, fontSize, dark ? Theme.Dark : Theme.Text, Vector2.zero, size, bold: true);
            }
            return btn;
        }

        /// <summary>Which button art goes with a button colour. Files are optional; see ArtPrompts.md.</summary>
        static string ButtonArt(Color c)
        {
            if (Near(c, Theme.Primary)) return "ui_button_yellow";
            if (Near(c, Theme.Good)) return "ui_button_green";
            if (Near(c, Theme.Bad)) return "ui_button_red";
            return "ui_button_blue";
        }

        static bool Near(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.1f;

        /// <summary>Drop-in art by name, or null when that file has not been added yet.</summary>
        public static Image Picture(Transform parent, string artName, Vector2 pos, Vector2 size, Vector2? anchor = null)
        {
            var sprite = Art.Get(artName);
            if (sprite == null) return null;
            var img = Box(parent, Color.white, pos, size, anchor, false);
            img.name = artName;
            img.sprite = sprite;
            img.preserveAspect = true;
            return img;
        }

        /// <summary>Uses drop-in art as a full background when present; otherwise keeps the plain colour.</summary>
        public static void Backdrop(Image background, string artName)
        {
            var sprite = Art.Get(artName);
            if (sprite == null) return;
            background.sprite = sprite;
            background.color = Color.white;
            background.type = Image.Type.Simple;
            var fit = background.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = sprite.rect.width / sprite.rect.height;
        }

        /// <summary>Full-parent dark layer that also swallows taps behind a popup.</summary>
        public static Image Dim(Transform parent, float alpha = 0.8f)
        {
            var rt = Fill(parent, "Dim");
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Theme.Dark.Alpha(alpha);
            img.raycastTarget = true;
            return img;
        }

        /// <summary>
        /// Vertical scroll area filling <paramref name="parent"/>. Returns the content rect: add children
        /// with <c>anchor: Ui.Top</c> and negative y, then call <see cref="SetContentHeight"/>.
        /// </summary>
        public static RectTransform Scroll(Transform parent)
        {
            var view = Fill(parent, "Scroll");
            var hit = view.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            view.gameObject.AddComponent<RectMask2D>();
            var content = NewRect(view, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.anchoredPosition = Vector2.zero;
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content;
            sr.viewport = view;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 40f;
            return content;
        }

        public static void SetContentHeight(RectTransform content, float height)
        {
            content.sizeDelta = new Vector2(0f, height);
        }

        public static InputField Input(Transform parent, string value, string placeholder, Vector2 pos, Vector2 size,
            Action<string> onEndEdit, Vector2? anchor = null, int characterLimit = 16)
        {
            var bg = Box(parent, Theme.PanelLight, pos, size, anchor);
            bg.raycastTarget = true;
            var inner = new Vector2(size.x - 48f, size.y);
            var text = Label(bg.transform, "", 44, Theme.Text, Vector2.zero, inner, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var hint = Label(bg.transform, placeholder, 44, Theme.TextDim, Vector2.zero, inner, TextAnchor.MiddleLeft);
            var field = bg.gameObject.AddComponent<InputField>();
            field.targetGraphic = bg;
            field.textComponent = text;
            field.placeholder = hint;
            field.characterLimit = characterLimit;
            field.lineType = InputField.LineType.SingleLine;
            field.text = value ?? "";
            if (onEndEdit != null) field.onEndEdit.AddListener(s => onEndEdit(s));
            return field;
        }

        public static void Clear(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }

        // ---------- small extensions used all over gameplay code ----------

        public static void SetPos(this Component c, Vector2 pos) => ((RectTransform)c.transform).anchoredPosition = pos;
        public static void SetPos(this Component c, float x, float y) => ((RectTransform)c.transform).anchoredPosition = new Vector2(x, y);
        public static Vector2 GetPos(this Component c) => ((RectTransform)c.transform).anchoredPosition;
        public static void SetSize(this Component c, Vector2 size) => ((RectTransform)c.transform).sizeDelta = size;
        public static void SetSize(this Component c, float w, float h) => ((RectTransform)c.transform).sizeDelta = new Vector2(w, h);
        public static Vector2 GetSize(this Component c) => ((RectTransform)c.transform).sizeDelta;
        public static void SetRot(this Component c, float degrees) => c.transform.localEulerAngles = new Vector3(0f, 0f, degrees);
        public static void SetScale(this Component c, float s) => c.transform.localScale = new Vector3(s, s, 1f);
        public static void Show(this Component c, bool on) => c.gameObject.SetActive(on);

        public static string Coins(int n) => n.ToString("N0");

        // ---------- internals ----------

        static RectTransform NewRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Place(RectTransform rt, Vector2 pos, Vector2 size, Vector2? anchor)
        {
            var a = anchor ?? Centre;
            rt.anchorMin = a;
            rt.anchorMax = a;
            rt.pivot = Centre;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        static Sprite MakeCircle(int size, float ring)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            var r = size * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Mathf.Sqrt((x + 0.5f - r) * (x + 0.5f - r) + (y + 0.5f - r) * (y + 0.5f - r));
                var v = 1f - Mathf.Clamp01(d - (r - ring - 0.5f)); // white inside, black in the rim
                px[y * size + x] = new Color(v, v, v, Mathf.Clamp01(r - 0.5f - d));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Keep(Sprite.Create(tex, new Rect(0, 0, size, size), Centre, 100f));
        }

        static Sprite MakeRounded(int size, int radius, float ring)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            var half = size * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                // signed distance to the edge of the rounded rectangle: negative inside
                var qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                var qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                var outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                var sd = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                var v = 1f - Mathf.Clamp01(sd + ring + 0.5f);
                px[y * size + x] = new Color(v, v, v, Mathf.Clamp01(-sd - 0.5f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            var b = radius + 2;
            return Keep(Sprite.Create(tex, new Rect(0, 0, size, size), Centre, 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b)));
        }

        // generated sprites are shared by every scene, so they must survive scene unloads
        static Sprite Keep(Sprite sprite)
        {
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }
}
