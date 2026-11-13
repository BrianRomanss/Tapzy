using System.Collections.Generic;
using System.Reflection;
using Tapzy.Contract;
using Tapzy.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Pointer = UnityEngine.InputSystem.Pointer;

namespace Tapzy.GameKit
{
    /// <summary>
    /// Base class for every mini-game. It owns everything the games have in common: the canvas,
    /// the HUD (score, timer, pause), the start / pause / continue overlays, one-thumb input,
    /// the seeded random generator, and reporting the result through <see cref="Round"/>.
    ///
    /// A game subclasses this, adds [MiniGame], builds its objects in OnStart and advances them
    /// in OnTick. It calls EndRound() when the player fails or finishes.
    ///
    /// COORDINATES: all gameplay happens inside <see cref="Field"/>, a fixed 1080 x 1920 rect with
    /// the origin at its centre (x: -540..540, y: -960..960, y up). The top 200 units (y > 760)
    /// sit under the HUD, so keep interactive things below y = 760.
    ///
    /// A game scene also runs on its own (press Play in the game scene): it then picks its own
    /// seed and shows a retry overlay instead of reporting to the hub.
    /// </summary>
    public abstract class MiniGameBase : MonoBehaviour
    {
        enum State { Ready, Playing, Ended }

        // ----- what a game overrides -----

        /// <summary>Build the round. Use Rng for anything random.</summary>
        protected abstract void OnStart();

        /// <summary>Called every frame while the round is live and not paused. dt is clamped to 0.05.</summary>
        protected abstract void OnTick(float dt);

        /// <summary>Seconds on the countdown clock, or 0 for rounds that end on failure only.</summary>
        protected virtual float TimeLimit => 0f;

        /// <summary>Clock reached zero. Default ends the round.</summary>
        protected virtual void OnTimeUp() => EndRound();

        /// <summary>True if a failed run can be continued once by watching a rewarded ad.</summary>
        protected virtual bool AllowContinue => false;

        /// <summary>Player earned a continue: undo the failure (clear hazards, restore a life...).</summary>
        protected virtual void OnContinue() { }

        /// <summary>At most five words shown under the title before the first tap.</summary>
        protected virtual string Hint => "";

        protected virtual Color Background => Theme.GameBg;

        // ----- what a game uses -----

        protected RectTransform Field { get; private set; }
        protected System.Random Rng { get; private set; }
        protected long Seed { get; private set; }
        protected float Elapsed { get; private set; }
        protected bool Playing => state == State.Playing && !paused;

        protected int Score
        {
            get => score;
            set
            {
                var next = Mathf.Max(0, value);
                var gained = next > score;
                score = next;
                if (scoreText == null) return;
                scoreText.text = score.ToString("N0");
                if (!gained) return;

                // punch on distinct scoring moments, not on counters that tick up every frame
                if (Time.unscaledTime - lastScoreChange > 0.15f) scorePunch = 1f;
                lastScoreChange = Time.unscaledTime;

                if (best > 0 && !beatBest && score > best && state == State.Playing)
                {
                    beatBest = true;
                    bestText.text = "New best!";
                    bestText.color = Theme.Coin;
                    Pop("NEW BEST!", new Vector2(0f, 520f), Theme.Coin, 96);
                    Flash(Theme.Coin, 0.25f);
                    Sfx.Great();
                }
            }
        }

        /// <summary>Seconds left on the clock. Games may add or subtract (bonus time, penalties).</summary>
        protected float TimeLeft { get; set; }

        /// <summary>Pointer in Field coordinates. Valid whenever a finger or mouse exists.</summary>
        protected Vector2 PointerPos { get; private set; }
        /// <summary>True on the frame a press begins.</summary>
        protected bool PointerDown { get; private set; }
        /// <summary>True while pressed.</summary>
        protected bool PointerHeld { get; private set; }
        /// <summary>True on the frame a press ends.</summary>
        protected bool PointerUp { get; private set; }
        /// <summary>Where the current (or last) press began.</summary>
        protected Vector2 PointerDownPos { get; private set; }
        /// <summary>One-shot: (-1,0) (1,0) (0,1) (0,-1) on the frame a press first travels 70 units, else zero.</summary>
        protected Vector2Int Swipe { get; private set; }
        /// <summary>True on the frame a press ends without having become a swipe.</summary>
        protected bool Tapped { get; private set; }

        protected float Rand(float min, float max) => min + (float)Rng.NextDouble() * (max - min);
        /// <summary>min inclusive, max exclusive.</summary>
        protected int Rand(int min, int max) => Rng.Next(min, max);

        protected Image Box(Vector2 pos, Vector2 size, Color color, bool rounded = true) => Ui.Box(Field, color, pos, size, null, rounded);
        protected Image Disc(Vector2 pos, float diameter, Color color) => Ui.Disc(Field, color, pos, diameter);
        protected Text Label(string text, Vector2 pos, int size, Color color, float width = 900f) =>
            Ui.Label(Field, text, size, color, pos, new Vector2(width, size * 1.4f), bold: true);

        /// <summary>Floating text that drifts up and fades ("+10", "Miss").</summary>
        protected void Pop(string text, Vector2 pos, Color color, int size = 56)
        {
            var t = Ui.Label(popLayer, text, size, color, pos, new Vector2(Mathf.Max(600f, size * 10f), size * 1.4f), bold: true);
            t.SetScale(0.6f);
            pops.Add(new PopText { text = t, age = 0f });
        }

        /// <summary>Rattles the playfield. Use it for hits, crashes and big clears.</summary>
        protected void Shake(float strength = 22f, float seconds = 0.3f)
        {
            shakeStrength = strength;
            shakeLeft = shakeTime = Mathf.Max(0.01f, seconds);
        }

        /// <summary>A full-screen colour flash that fades out in a fraction of a second.</summary>
        protected void Flash(Color color, float alpha = 0.35f)
        {
            if (flash != null) flash.color = color.Alpha(alpha);
        }

        /// <summary>The player failed or finished. Offers the one continue if allowed, then reports the score.</summary>
        protected void EndRound()
        {
            if (state != State.Playing) return;
            state = State.Ended;
            if (!timeUp)
            {
                // a failure should be felt, a clock running out should not
                Shake();
                Flash(Theme.Bad);
            }
            if (AllowContinue && !continueUsed && Round.CanOfferContinue) ShowContinue();
            else Finish();
        }

        // ----- internals -----

        const float SwipeDistance = 70f;
        const float ContinueSeconds = 6f;
        const float ContinueBarWidth = 700f;

        struct PopText { public Text text; public float age; }

        State state = State.Ready;
        bool paused, continueUsed, swiped, pressValid;
        int score;
        Canvas canvas;
        RectTransform popLayer, pauseRect;
        Text scoreText, timeText;
        GameObject overlay;
        readonly List<PopText> pops = new List<PopText>();

        // feedback
        int best, lastSecond = -1;
        bool beatBest, timeUp;
        float scorePunch, timePunch, lastScoreChange = -1f;
        float shakeLeft, shakeTime, shakeStrength, continueLeft;
        Text bestText;
        Image flash;
        RectTransform continueBar;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Seed = Round.Active ? Round.Seed : System.DateTime.UtcNow.Ticks;
            Rng = new System.Random(unchecked((int)(Seed ^ (Seed >> 32))));
            BuildCanvas();
        }

        void Start()
        {
            TimeLeft = TimeLimit;
            OnStart();
            Score = score;
            UpdateClock();
            ShowStart();
        }

        void BuildCanvas()
        {
            canvas = Ui.Canvas("GameCanvas", 0);
            var bg = Ui.Fill(canvas.transform, "Background").gameObject.AddComponent<Image>();
            bg.color = Background;
            bg.raycastTarget = false;
            var info = GetType().GetCustomAttribute<MiniGameAttribute>();
            if (info != null) Ui.Backdrop(bg, "bg_" + info.Id); // optional per-game backdrop art

            Field = Ui.Node(canvas.transform, "Field", Vector2.zero, new Vector2(Ui.W, Ui.H));
            Field.gameObject.AddComponent<RectMask2D>();
            popLayer = Ui.Node(canvas.transform, "Pops", Vector2.zero, new Vector2(Ui.W, Ui.H));
            flash = Ui.Fill(canvas.transform, "Flash").gameObject.AddComponent<Image>();
            flash.color = Color.clear;
            flash.raycastTarget = false;

            var hud = Ui.Column(canvas.transform, "Hud");
            best = Round.Active ? Round.Best : 0; // the score to chase, shown right under the live score
            bestText = Ui.Label(hud, best > 0 ? "Best " + best.ToString("N0") : "", 30, Theme.TextDim, new Vector2(0f, -182f), new Vector2(500f, 40f), anchor: Ui.Top);
            scoreText = Ui.Label(hud, "0", 96, Theme.Text, new Vector2(0f, -110f), new Vector2(500f, 130f), anchor: Ui.Top, bold: true);
            timeText = Ui.Label(hud, "", 64, Theme.Coin, new Vector2(-390f, -110f), new Vector2(260f, 100f), TextAnchor.MiddleLeft, Ui.Top, true);
            var pause = Ui.Btn(hud, "II", Theme.PanelLight, new Vector2(440f, -110f), new Vector2(120f, 120f), TogglePause, Ui.Top, 48);
            pauseRect = (RectTransform)pause.transform;
        }

        void Update()
        {
            var dt = Mathf.Min(Time.deltaTime, 0.05f);
            ReadPointer();
            TickPops(dt);
            TickFeedback(dt);

            if (state == State.Ready)
            {
                if (PointerDown && overlay != null)
                {
                    CloseOverlay();
                    state = State.Playing;
                    ClearPointer();
                    pressValid = false; // the starting tap is not gameplay input
                }
                return;
            }
            if (state != State.Playing || paused) return;

            Elapsed += dt;
            if (TimeLimit > 0f)
            {
                TimeLeft -= dt;
                if (TimeLeft <= 0f)
                {
                    TimeLeft = 0f;
                    UpdateClock();
                    timeUp = true;
                    OnTimeUp();
                    timeUp = false;
                    return;
                }
                UpdateClock();
            }
            OnTick(dt);
        }

        void UpdateClock()
        {
            if (TimeLimit <= 0f) return;
            var second = Mathf.CeilToInt(TimeLeft);
            timeText.text = second.ToString();
            timeText.color = TimeLeft <= 5f ? Theme.Bad : Theme.Coin;
            // the last five seconds tick audibly and the clock thumps
            if (second != lastSecond && second > 0 && second <= 5 && state == State.Playing)
            {
                timePunch = 1f;
                Sfx.Blip(990f, 0.05f, 0.2f);
            }
            lastSecond = second;
        }

        void TickFeedback(float dt)
        {
            if (scorePunch > 0f)
            {
                scorePunch = Mathf.Max(0f, scorePunch - dt * 6f);
                scoreText.SetScale(1f + 0.28f * scorePunch);
            }
            if (timePunch > 0f)
            {
                timePunch = Mathf.Max(0f, timePunch - dt * 5f);
                timeText.SetScale(1f + 0.35f * timePunch);
            }
            if (shakeLeft > 0f)
            {
                // cosmetic only, so it uses Unity's random and leaves the seeded Rng untouched
                shakeLeft -= dt;
                var k = shakeLeft > 0f ? shakeStrength * shakeLeft / shakeTime : 0f;
                Field.anchoredPosition = new Vector2(Random.Range(-k, k), Random.Range(-k, k));
            }
            if (flash.color.a > 0f)
            {
                var c = flash.color;
                c.a = Mathf.Max(0f, c.a - dt * 1.6f);
                flash.color = c;
            }
            if (continueLeft > 0f)
            {
                // the continue offer runs out on its own, like an arcade "continue?" screen
                continueLeft -= dt;
                if (continueLeft <= 0f) Finish();
                else if (continueBar != null)
                    continueBar.sizeDelta = new Vector2(Mathf.Max(16f, ContinueBarWidth * continueLeft / ContinueSeconds), 16f);
            }
        }

        void ReadPointer()
        {
            PointerDown = PointerUp = Tapped = false;
            Swipe = Vector2Int.zero;
            var p = Pointer.current;
            if (p == null)
            {
                PointerHeld = false;
                return;
            }
            Vector2 screen = p.position.ReadValue();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(Field, screen, null, out var local);
            PointerPos = local;

            if (p.press.wasPressedThisFrame)
            {
                // presses on the pause button, or while an overlay is up (other than "tap to start"), are not gameplay
                var onPause = RectTransformUtility.RectangleContainsScreenPoint(pauseRect, screen, null);
                pressValid = !onPause && !paused && (state == State.Playing || state == State.Ready);
                if (pressValid)
                {
                    PointerDown = true;
                    PointerDownPos = local;
                    swiped = false;
                }
            }
            PointerHeld = pressValid && p.press.isPressed;

            if (PointerHeld && !swiped)
            {
                var d = local - PointerDownPos;
                if (d.magnitude >= SwipeDistance)
                {
                    swiped = true;
                    Swipe = Mathf.Abs(d.x) > Mathf.Abs(d.y)
                        ? new Vector2Int(d.x > 0 ? 1 : -1, 0)
                        : new Vector2Int(0, d.y > 0 ? 1 : -1);
                }
            }

            if (p.press.wasReleasedThisFrame && pressValid)
            {
                PointerUp = true;
                Tapped = !swiped;
                pressValid = false;
                PointerHeld = false;
            }
        }

        void ClearPointer()
        {
            PointerDown = PointerHeld = PointerUp = Tapped = false;
            Swipe = Vector2Int.zero;
        }

        void TickPops(float dt)
        {
            for (var i = pops.Count - 1; i >= 0; i--)
            {
                var p = pops[i];
                p.age += dt;
                if (p.age >= 0.7f)
                {
                    Destroy(p.text.gameObject);
                    pops.RemoveAt(i);
                    continue;
                }
                p.text.rectTransform.anchoredPosition += new Vector2(0f, 220f * dt);
                // pop in past full size, then settle
                p.text.SetScale(p.age < 0.1f
                    ? Mathf.Lerp(0.6f, 1.25f, p.age / 0.1f)
                    : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((p.age - 0.1f) / 0.12f)));
                var c = p.text.color;
                c.a = 1f - p.age / 0.7f;
                p.text.color = c;
                pops[i] = p;
            }
        }

        // ----- overlays -----

        RectTransform OpenOverlay(float dim)
        {
            CloseOverlay();
            var root = Ui.Fill(canvas.transform, "Overlay");
            overlay = root.gameObject;
            Ui.Dim(root, dim);
            return root;
        }

        void CloseOverlay()
        {
            if (overlay != null) Destroy(overlay);
            overlay = null;
        }

        void ShowStart()
        {
            var root = OpenOverlay(0.6f);
            var info = GetType().GetCustomAttribute<MiniGameAttribute>();
            Ui.Label(root, info != null ? info.Title : name, 110, Theme.Text, new Vector2(0f, 220f), new Vector2(1000f, 160f), bold: true);
            if (!string.IsNullOrEmpty(Hint))
                Ui.Label(root, Hint, 54, Theme.TextDim, new Vector2(0f, 80f), new Vector2(960f, 80f));
            Ui.Label(root, "Tap to start", 60, Theme.Coin, new Vector2(0f, -160f), new Vector2(900f, 90f), bold: true);
        }

        void TogglePause()
        {
            if (state != State.Playing) return;
            if (paused)
            {
                paused = false;
                CloseOverlay();
                return;
            }
            paused = true;
            ClearPointer();
            var root = OpenOverlay(0.85f);
            Ui.Label(root, "Paused", 100, Theme.Text, new Vector2(0f, 260f), new Vector2(900f, 140f), bold: true);
            Ui.Btn(root, "Resume", Theme.Primary, new Vector2(0f, 20f), new Vector2(560f, 150f), TogglePause);
            Ui.Btn(root, Sfx.Enabled ? "Sound: On" : "Sound: Off", Theme.PanelLight, new Vector2(0f, -160f), new Vector2(560f, 130f), () =>
            {
                Sfx.Enabled = !Sfx.Enabled;
                paused = false;
                TogglePause(); // rebuild with the new label
            });
            Ui.Btn(root, "Quit", Theme.Bad, new Vector2(0f, -330f), new Vector2(560f, 130f), Quit);
        }

        void OnApplicationPause(bool pausing)
        {
            if (pausing && state == State.Playing && !paused) TogglePause();
        }

        void ShowContinue()
        {
            var root = OpenOverlay(0.85f);
            Ui.Label(root, "Keep going?", 96, Theme.Text, new Vector2(0f, 320f), new Vector2(900f, 140f), bold: true);
            Ui.Label(root, "Score " + score.ToString("N0"), 60, Theme.TextDim, new Vector2(0f, 200f), new Vector2(900f, 80f));
            // say exactly what the continue is worth right now
            Ui.Label(root, ContinueTease(), 48, Theme.Coin, new Vector2(0f, 110f), new Vector2(980f, 66f), bold: true);
            var watch = Ui.Btn(root, "Watch ad to continue", Theme.Good, new Vector2(0f, -50f), new Vector2(760f, 160f), () =>
            {
                continueLeft = 0f;
                CloseOverlay();
                Round.RequestContinue(watched =>
                {
                    if (!watched)
                    {
                        Finish();
                        return;
                    }
                    continueUsed = true;
                    OnContinue();
                    ClearPointer();
                    pressValid = false;
                    state = State.Playing;
                    Pop("GO!", new Vector2(0f, 200f), Theme.Good, 120);
                    Flash(Theme.Good, 0.25f);
                    Sfx.Great();
                });
            }, null, 52);
            watch.gameObject.AddComponent<Pulse>();

            Ui.Box(root, Theme.PanelLight, new Vector2(0f, -180f), new Vector2(ContinueBarWidth, 16f));
            continueBar = Ui.Box(root, Theme.Coin, new Vector2(0f, -180f), new Vector2(ContinueBarWidth, 16f)).rectTransform;
            Ui.Btn(root, "No thanks", Theme.PanelLight, new Vector2(0f, -310f), new Vector2(700f, 130f), Finish);
            continueLeft = ContinueSeconds;
        }

        string ContinueTease()
        {
            if (best > 0 && score > best) return "New best run! Keep it alive";
            if (best > 0 && score * 2 >= best) return "Only " + (best - score + 1).ToString("N0") + " to beat your best!";
            return "One free revive this round";
        }

        void Finish()
        {
            continueLeft = 0f;
            CloseOverlay();
            state = State.Ended;
            if (Round.Active)
            {
                Round.Report(score, Elapsed);
                return;
            }
            // running the scene on its own, without the hub
            var root = OpenOverlay(0.85f);
            Ui.Label(root, "Score", 60, Theme.TextDim, new Vector2(0f, 260f), new Vector2(900f, 80f));
            Ui.Label(root, score.ToString("N0"), 150, Theme.Text, new Vector2(0f, 130f), new Vector2(1000f, 200f), bold: true);
            Ui.Btn(root, "Play again", Theme.Primary, new Vector2(0f, -120f), new Vector2(560f, 150f), Reload);
        }

        void Quit()
        {
            if (Round.Active) Round.Abandon();
            else Reload();
        }

        static void Reload() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
