using System.Collections.Generic;
using Crashout.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Crashout.Presentation
{
    /// <summary>
    /// The heads-up display: a damage plate with a medal track, a speed and boost cluster, a junction
    /// countdown, the launch meter, the crash read-out (chain, impact time, aftertouch, Crashbreaker
    /// dial) and the results card. Cinema bars close in during impact time. Touch controls (floating
    /// stick, pedals) appear on touch devices; on a keyboard the HUD shows key hints instead.
    /// Sizes are in points (about 1/160 inch); every control keeps a hit area of 44 pt or more.
    /// IMGUI only draws; input comes from the Input System so several fingers work at once.
    /// </summary>
    public sealed class CrashoutHud : MonoBehaviour
    {
        private const float DesignShortSide = 400f;
        private const float Margin = 14f;
        private const float Gap = 10f;
        private const float MinTarget = 44f;
        private const float JoystickRadius = 54f;
        private const float JoystickDeadzone = 0.12f;

        private enum Kind { Button, Soft, Pad, Icon, Hidden }
        private enum Icon { Retry, SoundOn, SoundOff, Camera, Board, Check }

        private struct Popup
        {
            public string Text;
            public Vector3 World;
            public float Born;
            public Color Colour;
            public int Size;
        }

        private struct Pointer
        {
            public int Id;
            public Vector2 Position;
            public bool Began;
        }

        private struct Frame
        {
            public float W, H, X0, X1, Y0, Y1, Pad, Short;
            public bool Landscape;
        }

        private const int NoPointer = int.MinValue;

        private CrashoutGameSession session;
        private CrashoutCameraRig cameraRig;
        private readonly List<Popup> popups = new List<Popup>();

        // Hit rectangles in screen pixels (origin bottom-left), rebuilt every repaint.
        private readonly Dictionary<string, Rect> nextRects = new Dictionary<string, Rect>();
        private readonly Dictionary<string, Rect> hitRects = new Dictionary<string, Rect>();
        private readonly HashSet<string> pressed = new HashSet<string>();
        private readonly List<string> taps = new List<string>();
        private readonly List<Pointer> pointers = new List<Pointer>();

        private int joystickPointer = NoPointer;
        private int replayScrubPointer = NoPointer;
        private bool resumeAfterScrub;
        private Vector2 joystickOrigin;
        private Vector2 joystickCurrent;
        private bool controlsActive = true;

        private float scale = 1f;
        private bool showBoardTools;
        private bool touchUi;
        private Matrix4x4 baseMatrix = Matrix4x4.identity;

        // Animation state (unscaled time, so it keeps moving through slow motion).
        private float shownScore;
        private float scorePulseAt = -10f;
        private int lastChain;
        private float chainPulseAt = -10f;
        private float letterbox;
        private float resultsShownAt = -10f;
        private float resultsScore;
        private GamePhase lastPhase = GamePhase.Lobby;
        private float readyPulseAt = -10f;
        private bool wasReady;

        private static readonly Color Accent = new Color(1f, 0.42f, 0.08f);
        private static readonly Color Hot = new Color(1f, 0.82f, 0.25f);
        private static readonly Color Cool = new Color(0.36f, 0.9f, 1f);
        private static readonly Color Good = new Color(0.32f, 0.95f, 0.5f);
        private static readonly Color Ink = new Color(0.03f, 0.04f, 0.07f);
        private static readonly Color BronzeColour = new Color(0.9f, 0.55f, 0.28f);
        private static readonly Color SilverColour = new Color(0.82f, 0.87f, 0.92f);
        private static readonly Color GoldColour = new Color(1f, 0.84f, 0.25f);

        private Font displayFont, textFont;
        private GUIStyle panelStyle, plateStyle, ghostStyle, softStyle, softHotStyle, buttonStyle, buttonHotStyle, padStyle, padHotStyle, iconBackStyle, iconBackHotStyle, chipStyle;
        private GUIStyle display, displayLeft, displayRight, text, textLeft, textRight;
        private Texture2D panelTexture, plateTexture, ghostTexture, softTexture, softHotTexture, buttonTexture, buttonHotTexture, padTexture, padHotTexture,
            barTexture, ringTexture, knobTexture, iconBackTexture, iconBackHotTexture, chipTexture, slantTexture, glowTexture, medalTexture, arrowTexture, edgeTexture;
        private Texture2D chevronUp, chevronDown, chevronLeft, chevronRight;
        private readonly Dictionary<Icon, Texture2D> icons = new Dictionary<Icon, Texture2D>();

        public ArTabletopMode ArMode { get; set; }

        public void Configure(CrashoutGameSession gameSession, CrashoutCameraRig rig)
        {
            if (session != null)
            {
                session.ScoreChanged -= OnScore;
                session.BonusAwarded -= OnBonus;
            }
            session = gameSession;
            cameraRig = rig;
            session.ScoreChanged += OnScore;
            session.BonusAwarded += OnBonus;
        }

        private void OnDestroy()
        {
            if (session != null)
            {
                session.ScoreChanged -= OnScore;
                session.BonusAwarded -= OnBonus;
            }
            foreach (Texture2D texture in new[] { panelTexture, plateTexture, ghostTexture, softTexture, softHotTexture, buttonTexture, buttonHotTexture, padTexture,
                padHotTexture, barTexture, ringTexture, knobTexture, iconBackTexture, iconBackHotTexture, chipTexture, slantTexture, glowTexture, medalTexture, arrowTexture,
                edgeTexture, chevronUp, chevronDown, chevronLeft, chevronRight })
            {
                if (texture != null) Destroy(texture);
            }
            foreach (Texture2D icon in icons.Values)
            {
                if (icon != null) Destroy(icon);
            }
        }

        // ------------------------------------------------------------------ input

        private void Update()
        {
            if (session == null) return;
            CollectPointers();
            UpdateReplayScrubber();
            UpdateJoystick();

            pressed.Clear();
            taps.Clear();
            foreach (Pointer pointer in pointers)
            {
                if (pointer.Id == joystickPointer || pointer.Id == replayScrubPointer) continue;
                foreach (KeyValuePair<string, Rect> entry in hitRects)
                {
                    if (!entry.Value.Contains(pointer.Position)) continue;
                    pressed.Add(entry.Key);
                    if (pointer.Began) taps.Add(entry.Key);
                    break;
                }
            }

            session.LocalInput.SetTouchState(SteerAxis(), pressed.Contains("gas"), pressed.Contains("brake"));
            session.LocalInput.SetTouchBoost(pressed.Contains("boost"));
            // During the crash the stick steers the wreck in two axes.
            GamePhase phase = session.Phase;
            session.LocalInput.SetTouchPush(phase == GamePhase.Crash || phase == GamePhase.SettleDelay ? PushAxis() : 0f);
            Animate(phase);

            foreach (string tap in taps) HandleTap(tap);
        }

        private void CollectPointers()
        {
            pointers.Clear();
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                foreach (var touch in touchscreen.touches)
                {
                    if (!touch.press.isPressed) continue;
                    pointers.Add(new Pointer { Id = touch.touchId.ReadValue(), Position = touch.position.ReadValue(), Began = touch.press.wasPressedThisFrame });
                }
            }
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                pointers.Add(new Pointer { Id = -1, Position = mouse.position.ReadValue(), Began = mouse.leftButton.wasPressedThisFrame });
            }
        }

        // A floating joystick: it appears wherever the left thumb lands in the lower-left area.
        private void UpdateJoystick()
        {
            if (!controlsActive)
            {
                joystickPointer = NoPointer;
                return;
            }

            if (joystickPointer != NoPointer)
            {
                bool found = false;
                foreach (Pointer pointer in pointers)
                {
                    if (pointer.Id != joystickPointer) continue;
                    joystickCurrent = pointer.Position;
                    found = true;
                    break;
                }
                if (!found) joystickPointer = NoPointer;
                return;
            }

            foreach (Pointer pointer in pointers)
            {
                if (!pointer.Began || !InSteerZone(pointer.Position) || InsideAnyControl(pointer.Position)) continue;
                joystickPointer = pointer.Id;
                joystickOrigin = pointer.Position;
                joystickCurrent = pointer.Position;
                break;
            }
        }

        private bool InSteerZone(Vector2 screenPoint)
        {
            return screenPoint.x < Screen.width * 0.5f && screenPoint.y < Screen.height * 0.7f;
        }

        private bool InsideAnyControl(Vector2 screenPoint)
        {
            foreach (Rect rect in hitRects.Values)
            {
                if (rect.Contains(screenPoint)) return true;
            }
            return false;
        }

        private float SteerAxis()
        {
            if (joystickPointer == NoPointer) return 0f;
            float raw = Mathf.Clamp((joystickCurrent.x - joystickOrigin.x) / (JoystickRadius * scale), -1f, 1f);
            float magnitude = Mathf.Abs(raw);
            if (magnitude < JoystickDeadzone) return 0f;
            return Mathf.Sign(raw) * Mathf.Clamp01((magnitude - JoystickDeadzone) / (1f - JoystickDeadzone));
        }

        private float PushAxis()
        {
            if (joystickPointer == NoPointer) return 0f;
            float raw = Mathf.Clamp((joystickCurrent.y - joystickOrigin.y) / (JoystickRadius * scale), -1f, 1f);
            float magnitude = Mathf.Abs(raw);
            if (magnitude < JoystickDeadzone) return 0f;
            return Mathf.Sign(raw) * Mathf.Clamp01((magnitude - JoystickDeadzone) / (1f - JoystickDeadzone));
        }

        private void Animate(GamePhase phase)
        {
            float dt = Time.unscaledDeltaTime;
            int score = session.Score;
            // The counter rolls up to the real score: quickly, but always visibly.
            float gap = score - shownScore;
            shownScore = Mathf.Abs(gap) < 1f ? score : shownScore + gap * (1f - Mathf.Exp(-9f * dt)) + Mathf.Sign(gap) * Mathf.Min(Mathf.Abs(gap), 40f * dt);
            if (score < shownScore) shownScore = score;

            int chain = session.ChainCount;
            if (chain > lastChain) chainPulseAt = Time.unscaledTime;
            lastChain = chain;

            bool cinema = session.IsImpactTime;
            letterbox = Mathf.MoveTowards(letterbox, cinema ? 1f : 0f, dt * (cinema ? 5f : 2.2f));

            if (phase != lastPhase)
            {
                if (phase == GamePhase.Results)
                {
                    resultsShownAt = Time.unscaledTime;
                    resultsScore = 0f;
                }
                lastPhase = phase;
            }
            if (phase == GamePhase.Results)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - resultsShownAt - 0.25f) / 1.1f);
                resultsScore = score * (1f - Mathf.Pow(1f - t, 3f));
            }

            bool ready = session.CanCrashbreak;
            if (ready && !wasReady) readyPulseAt = Time.unscaledTime;
            wasReady = ready;
        }

        private void HandleTap(string id)
        {
            if (id != "gas" && id != "launch") CrashAudio.PlayClick();
            switch (id)
            {
                case "gas":
                case "launch":
                    session.TryLaunch();
                    break;
                case "replay": session.Replay?.Open(); break;
                case "replay-close": session.Replay?.Close(); break;
                case "replay-play": session.Replay?.TogglePlayback(); break;
                case "replay-start": session.Replay?.Seek(0f); break;
                case "replay-025": session.Replay?.SetSpeed(0.25f); break;
                case "replay-05": session.Replay?.SetSpeed(0.5f); break;
                case "replay-1": session.Replay?.SetSpeed(1f); break;
                case "replay-2": session.Replay?.SetSpeed(2f); break;
                case "retry": session.ResetTurn(); break;
                case "camera": cameraRig.CycleMode(); break;
                case "boom": session.TryCrashbreaker(); break;
                case "next": session.RequestScenarioChange(1); break;
                case "place": ArMode?.PlaceBoard(); break;
                case "board": showBoardTools = !showBoardTools; break;
                case "sound": CrashAudio.ToggleMute(); break;
                case "move": ArMode?.Relocate(); showBoardTools = false; break;
                case "smaller": ArMode?.ScaleBoard(1f / 1.12f); break;
                case "larger": ArMode?.ScaleBoard(1.12f); break;
            }
        }

        // ------------------------------------------------------------------ events to popups

        private void OnScore(ScoreEvent scoreEvent)
        {
            if (scoreEvent.Amount > 0) scorePulseAt = Time.unscaledTime;
            if (scoreEvent.Amount <= 0 || !session.TryGetVehiclePosition(scoreEvent.EntityId, out Vector3 position)) return;
            AddPopup("+$" + scoreEvent.Amount.ToString("N0"), position + Vector3.up * 1.2f,
                Color.Lerp(Color.white, Hot, Mathf.Clamp01(scoreEvent.Amount / 4000f)),
                Mathf.Clamp(22 + scoreEvent.Amount / 220, 22, 44));
            if (scoreEvent.ChainCount >= 3 && scoreEvent.ChainCount % 2 == 1)
            {
                AddPopup("CHAIN ×" + scoreEvent.ChainCount, position + Vector3.up * 2.4f, Accent, 36);
            }
        }

        private void OnBonus(string label, int amount, Vector3 position)
        {
            AddPopup(amount > 0 ? label + "  +$" + amount.ToString("N0") : label, position + Vector3.up * 1.4f, Cool, 38);
        }

        private void AddPopup(string text, Vector3 world, Color colour, int size)
        {
            if (popups.Count > 14) popups.RemoveAt(0);
            popups.Add(new Popup { Text = text, World = world, Born = Time.unscaledTime, Colour = colour, Size = size });
        }

        // ------------------------------------------------------------------ drawing

        private void OnGUI()
        {
            if (session == null || cameraRig == null) return;
            // Drawing only; input is handled in Update so multi-touch works.
            if (Event.current.type != EventType.Repaint) return;
            EnsureStyles();

            nextRects.Clear();
            Matrix4x4 previous = GUI.matrix;
            Frame f = ComputeFrame();
            baseMatrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.matrix = baseMatrix;

            GamePhase phase = session.Phase;
            bool arSetup = ArMode != null && ArMode.IsActive && ArMode.Stage != ArStage.Playing;
            bool results = phase == GamePhase.Results;
            bool replaying = session.Replay != null && session.Replay.IsActive;
            bool crashView = phase == GamePhase.Crash || phase == GamePhase.SettleDelay;
            controlsActive = !arSetup && !results;
            touchUi = Application.isMobilePlatform || Touchscreen.current != null;

            DrawCinemaBars(f);
            if (!replaying) DrawPopups();
            if (!arSetup && !results)
            {
                DrawIconRow(f);
                DrawSpeedCluster(f, phase);
            }

            if (arSetup)
            {
                DrawArSetup(f);
            }
            else if (replaying)
            {
                DrawReplay(f);
            }
            else if (results)
            {
                DrawResults(f);
            }
            else
            {
                DrawScorePlate(f, crashView);
                DrawStatus(f, phase, crashView);
                DrawBoardTools(f);
                if (touchUi)
                {
                    DrawJoystick(f, crashView);
                    if (!crashView) DrawDrivingPads(f);
                }
                else
                {
                    DrawKeyHints(f, phase);
                }
                if (phase == GamePhase.ReadyCheck) DrawLaunchMeter(f);
                if (crashView) DrawCrashbreaker(f);
            }

            GUI.matrix = previous;
            hitRects.Clear();
            foreach (KeyValuePair<string, Rect> entry in nextRects) hitRects[entry.Key] = entry.Value;
        }

        private Frame ComputeFrame()
        {
            float shortPx = Mathf.Min(Screen.width, Screen.height);
            scale = Application.isMobilePlatform && Screen.dpi > 0f
                ? Mathf.Min(Screen.dpi / 160f, shortPx / 340f)
                : shortPx / DesignShortSide;
            scale = Mathf.Max(0.5f, scale);

            Rect safe = Screen.safeArea;
            float w = Screen.width / scale;
            float h = Screen.height / scale;
            float left = safe.xMin / scale;
            float right = (Screen.width - safe.xMax) / scale;
            float top = (Screen.height - safe.yMax) / scale;
            float bottom = safe.yMin / scale;
            float shortSide = Mathf.Min(w, h);
            return new Frame
            {
                W = w,
                H = h,
                X0 = left + Margin,
                X1 = w - right - Margin,
                Y0 = top + Margin,
                Y1 = h - bottom - Margin,
                Pad = Mathf.Clamp(shortSide * 0.23f, 68f, 92f),
                Short = shortSide,
                Landscape = w > h
            };
        }

        // ------------------------------------------------------------------ primitives

        private void Tint(Rect rect, Texture2D texture, Color colour)
        {
            Color previous = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(rect, texture);
            GUI.color = previous;
        }

        private static Color Fade(Color colour, float alpha)
        {
            return new Color(colour.r, colour.g, colour.b, colour.a * alpha);
        }

        /// <summary>Text with a soft drop shadow, so it reads over any part of the scene.</summary>
        private void Label(Rect rect, string content, GUIStyle style, int size, Color colour, float shadow = 0.75f)
        {
            style.fontSize = size;
            if (shadow > 0f)
            {
                style.normal.textColor = new Color(0f, 0f, 0f, shadow * colour.a);
                float offset = Mathf.Max(1f, size * 0.045f);
                GUI.Label(new Rect(rect.x + offset, rect.y + offset, rect.width, rect.height), content, style);
                GUI.Label(new Rect(rect.x, rect.y + offset * 0.5f, rect.width, rect.height), content, style);
            }
            style.normal.textColor = colour;
            GUI.Label(rect, content, style);
        }

        private void Scaled(Vector2 pivot, float amount, float degrees = 0f)
        {
            GUI.matrix = baseMatrix * Matrix4x4.TRS(pivot, Quaternion.Euler(0f, 0f, degrees), new Vector3(amount, amount, 1f)) * Matrix4x4.Translate(-pivot);
        }

        private void Unscaled()
        {
            GUI.matrix = baseMatrix;
        }

        private static float Pulse(float since, float duration)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - since) / duration);
            return 1f - t * t * (3f - 2f * t);
        }

        /// <summary>A gauge made of slanted segments, the lit ones coloured by <paramref name="ramp"/>.</summary>
        private void Segments(Rect rect, int count, float fraction, Color from, Color to, float dim = 0.16f)
        {
            float pitch = rect.width / count;
            float lit = Mathf.Clamp01(fraction) * count;
            for (int i = 0; i < count; i++)
            {
                float fill = Mathf.Clamp01(lit - i);
                Color colour = Color.Lerp(from, to, count > 1 ? i / (float)(count - 1) : 0f);
                Rect segment = new Rect(rect.x + i * pitch, rect.y, pitch * 1.18f, rect.height);
                Tint(segment, slantTexture, new Color(1f, 1f, 1f, dim));
                if (fill > 0f) Tint(segment, slantTexture, Fade(colour, 0.35f + 0.65f * fill));
            }
        }

        private void Bar(Rect rect, float fraction, Color colour, bool glow = false)
        {
            Tint(rect, barTexture, new Color(0f, 0f, 0f, 0.45f));
            Tint(new Rect(rect.x, rect.y, rect.width, 1f), barTexture, new Color(1f, 1f, 1f, 0.12f));
            Rect fill = new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height);
            if (glow && fill.width > 1f) Tint(new Rect(fill.x - 6f, fill.y - 7f, fill.width + 12f, fill.height + 14f), glowTexture, Fade(colour, 0.35f));
            Tint(fill, barTexture, colour);
            Tint(new Rect(fill.x, fill.y, fill.width, Mathf.Max(1f, fill.height * 0.35f)), barTexture, new Color(1f, 1f, 1f, 0.25f));
        }

        // ------------------------------------------------------------------ cinema

        // Letterbox bars slide in for impact time, like a film cutting to its slow-motion shot.
        private void DrawCinemaBars(Frame f)
        {
            if (letterbox <= 0.001f) return;
            float eased = letterbox * letterbox * (3f - 2f * letterbox);
            float height = f.H * 0.085f * eased;
            Tint(new Rect(0f, 0f, f.W, height), barTexture, Color.black);
            Tint(new Rect(0f, f.H - height, f.W, height), barTexture, Color.black);
            // A warm edge glow sells the hit.
            Tint(new Rect(0f, height, f.W, f.H * 0.16f), edgeTexture, Fade(Accent, 0.16f * eased));
        }

        // ------------------------------------------------------------------ top right: icons, speed, boost

        private void DrawIconRow(Frame f)
        {
            var items = new List<KeyValuePair<string, Icon>>();
            bool arPlaying = ArMode != null && ArMode.IsActive && ArMode.Stage == ArStage.Playing;
            if (!arPlaying) showBoardTools = false;
            if (arPlaying)
            {
                items.Add(new KeyValuePair<string, Icon>("board", showBoardTools ? Icon.Check : Icon.Board));
            }
            else
            {
                items.Add(new KeyValuePair<string, Icon>("camera", Icon.Camera));
            }
            items.Add(new KeyValuePair<string, Icon>("retry", Icon.Retry));
            items.Add(new KeyValuePair<string, Icon>("sound", CrashAudio.Muted ? Icon.SoundOff : Icon.SoundOn));

            float x = f.X1 - items.Count * MinTarget - (items.Count - 1) * 2f;
            foreach (KeyValuePair<string, Icon> item in items)
            {
                Control(item.Key, new Rect(x, f.Y0 - 4f, MinTarget, MinTarget), "", Kind.Icon, true, icons[item.Value]);
                x += MinTarget + 2f;
            }
        }

        private void DrawSpeedCluster(Frame f, GamePhase phase)
        {
            if (session.Player == null) return;
            float metres = session.Player.SpeedMetresPerSecond;
            int mph = Mathf.RoundToInt(metres * 2.23694f);
            bool driving = phase == GamePhase.Driving;
            bool boosting = driving && session.Player.IsBoosting;
            float width = 132f;
            Rect plate = new Rect(f.X1 - width, f.Y0 + 42f, width, 62f);
            GUI.Box(plate, GUIContent.none, plateStyle);
            Tint(new Rect(plate.xMax - 3f, plate.y + 8f, 3f, plate.height - 16f), barTexture, boosting ? Hot : Accent);

            Label(new Rect(plate.x + 8f, plate.y + 1f, width - 46f, 40f), mph.ToString(), displayRight, 40, boosting ? Hot : Color.white);
            Label(new Rect(plate.xMax - 36f, plate.y + 21f, 28f, 14f), "MPH", textLeft, 10, Fade(Cool, 0.9f), 0.5f);

            // Rev band: road speed against the car's boosted top speed.
            float top = Mathf.Max(1f, session.Player.TopSpeed / (session.Player.IsBoosting ? 1f : 1f / 1.45f));
            Segments(new Rect(plate.x + 10f, plate.y + 41f, width - 24f, 6f), 18, metres / top, Cool, Accent);

            float boost = session.Player.BoostFraction;
            Color boostColour = boosting ? Hot : boost > 0.15f ? Cool : new Color(1f, 0.35f, 0.3f);
            Label(new Rect(plate.x + 10f, plate.y + 48f, 40f, 12f), "BOOST", textLeft, 8, Fade(Color.white, 0.75f), 0.4f);
            Bar(new Rect(plate.x + 46f, plate.y + 52f, width - 60f, 4f), boost, boostColour, boosting);
        }

        // ------------------------------------------------------------------ top left: damage plate

        private void DrawScorePlate(Frame f, bool crashView)
        {
            CrashScenario scenario = session.Scenario;
            float width = crashView ? 0f : 172f;
            if (crashView) return;
            Rect plate = new Rect(f.X0, f.Y0 - 2f, width, 74f);
            GUI.Box(plate, GUIContent.none, plateStyle);
            Tint(new Rect(plate.x, plate.y + 8f, 3f, plate.height - 16f), barTexture, Accent);
            Label(new Rect(plate.x + 12f, plate.y + 5f, width - 20f, 14f), scenario != null ? scenario.Name : "CRASH JUNCTION", textLeft, 10, Fade(Cool, 0.95f), 0.5f);
            float pop = 1f + 0.12f * Pulse(scorePulseAt, 0.3f);
            Vector2 pivot = new Vector2(plate.x + 12f, plate.y + 36f);
            Scaled(pivot, pop);
            Label(new Rect(plate.x + 12f, plate.y + 16f, width - 20f, 38f), "$" + Mathf.RoundToInt(shownScore).ToString("N0"), displayLeft, 34, Color.white);
            Unscaled();
            if (scenario != null) MedalTrack(new Rect(plate.x + 12f, plate.y + 56f, width - 24f, 12f), scenario, false);
        }

        /// <summary>Bronze, silver and gold along one bar, with the live score sweeping through them.</summary>
        private void MedalTrack(Rect rect, CrashScenario scenario, bool labels)
        {
            float gold = Mathf.Max(1, scenario.Gold);
            float fraction = Mathf.Clamp01(shownScore / gold);
            int score = session.Score;
            Color colour = score >= scenario.Gold ? GoldColour : score >= scenario.Silver ? SilverColour : score >= scenario.Bronze ? BronzeColour : Color.Lerp(Color.white, BronzeColour, 0.4f);
            Rect bar = new Rect(rect.x, rect.y + rect.height * 0.5f - 2f, rect.width, 4f);
            Bar(bar, fraction, colour, score >= scenario.Bronze);
            int[] targets = { scenario.Bronze, scenario.Silver, scenario.Gold };
            Color[] colours = { BronzeColour, SilverColour, GoldColour };
            string[] names = { "BRONZE", "SILVER", "GOLD" };
            for (int i = 0; i < 3; i++)
            {
                float x = bar.x + bar.width * Mathf.Clamp01(targets[i] / gold);
                bool reached = score >= targets[i];
                float size = reached ? 10f : 8f;
                Tint(new Rect(x - size * 0.5f - 1f, bar.center.y - size * 0.5f - 1f, size + 2f, size + 2f), knobTexture, new Color(0f, 0f, 0f, 0.7f));
                Tint(new Rect(x - size * 0.5f, bar.center.y - size * 0.5f, size, size), knobTexture, reached ? colours[i] : Fade(colours[i], 0.45f));
                if (labels)
                {
                    Rect tag = i == 2 ? new Rect(x - 120f, bar.yMax + 4f, 120f, 12f) : new Rect(x - 60f, bar.yMax + 4f, 120f, 12f);
                    Label(tag, names[i] + "  " + Compact(targets[i]), i == 2 ? textRight : text, 9, reached ? colours[i] : Fade(Color.white, 0.6f), 0.5f);
                }
            }
        }

        private static string Compact(int amount)
        {
            return amount >= 1000000 ? "$" + (amount / 1000000f).ToString("0.#") + "M"
                : amount >= 1000 ? "$" + (amount / 1000f).ToString("0.#") + "K"
                : "$" + amount;
        }

        // ------------------------------------------------------------------ top centre: status

        private void DrawStatus(Frame f, GamePhase phase, bool crashView)
        {
            float width = f.X1 - f.X0;
            float centreX = (f.X0 + f.X1) * 0.5f;
            float top = f.Y0 + (f.Landscape ? 0f : 82f);

            if (ArMode != null && ArMode.IsActive && ArMode.Stage == ArStage.Playing && !ArMode.IsTracking)
            {
                Label(new Rect(f.X0, top + 90f, width, 24f), "TRACKING LOST · POINT AT THE TABLE", text, 14, Hot);
            }

            if (crashView)
            {
                DrawCrashReadout(f, phase);
                return;
            }

            if (phase == GamePhase.ReadyCheck && session.Scenario != null)
            {
                Label(new Rect(f.X0, top - 2f, width, 26f), session.Scenario.Brief.ToUpperInvariant(), display, 22, Color.white);
                RunwayProfile runway = session.Runway;
                if (runway != null && !string.IsNullOrEmpty(runway.Title))
                {
                    string approach = runway.Title.ToUpperInvariant() + "  ·  " + Mathf.RoundToInt(-runway.StartZ) + " M TO THE JUNCTION";
                    Label(new Rect(f.X0, top + 24f, width, 16f), approach, text, 11, Fade(Hot, 0.95f), 0.6f);
                }
            }
            else if (phase == GamePhase.Driving && session.DistanceToJunction > 6f)
            {
                // Countdown to the junction: distance, and a track that empties as the car arrives.
                float metres = session.DistanceToJunction;
                RunwayProfile runway = session.Runway;
                float total = runway != null ? Mathf.Max(30f, -runway.StartZ) : 120f;
                bool close = metres < 45f;
                float beat = close ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 16f) : 0f;
                Color colour = close ? Color.Lerp(Hot, new Color(1f, 0.3f, 0.2f), beat) : Color.white;
                float trackWidth = Mathf.Min(220f, width * 0.42f);
                Label(new Rect(centreX - 100f, top - 4f, 200f, 30f), Mathf.RoundToInt(metres) + " M", display, close ? 30 : 26, colour);
                Rect track = new Rect(centreX - trackWidth * 0.5f, top + 28f, trackWidth, 5f);
                Segments(track, 22, 1f - Mathf.Clamp01(metres / total), Cool, close ? new Color(1f, 0.3f, 0.2f) : Hot);
                Label(new Rect(centreX - 100f, top + 34f, 200f, 12f), "JUNCTION", text, 8, Fade(Color.white, 0.7f), 0.4f);
            }

            // A knock the driver has just survived: tell them the car is helping.
            if (phase == GamePhase.Driving && session.Player != null && session.Player.RecoveryFraction > 0.05f)
            {
                float alpha = Mathf.Clamp01(session.Player.RecoveryFraction * 2f);
                Label(new Rect(f.X0, top + (f.Landscape ? 52f : 52f), width, 16f), "STEER TO RECOVER", text, 11, Fade(Cool, alpha), 0.6f * alpha);
            }
        }

        private void DrawCrashReadout(Frame f, GamePhase phase)
        {
            float width = f.X1 - f.X0;
            float centreX = (f.X0 + f.X1) * 0.5f;
            float y = f.Y0 - 4f + f.H * 0.085f * letterbox * 0.6f;

            Label(new Rect(f.X0, y, width, 14f), session.IsImpactTime ? "IMPACT TIME" : "LIVE DAMAGE", text, 11, session.IsImpactTime ? Hot : Fade(Cool, 0.95f), 0.6f);
            float pop = 1f + 0.1f * Pulse(scorePulseAt, 0.3f);
            Scaled(new Vector2(centreX, y + 40f), pop);
            Label(new Rect(f.X0, y + 10f, width, 58f), "$" + Mathf.RoundToInt(shownScore).ToString("N0"), display, 56, Color.white);
            Unscaled();
            y += 70f;

            CrashScenario scenario = session.Scenario;
            if (scenario != null)
            {
                float trackWidth = Mathf.Min(260f, width * 0.6f);
                MedalTrack(new Rect(centreX - trackWidth * 0.5f, y, trackWidth, 12f), scenario, true);
                y += 34f;
            }

            int chain = session.ChainCount;
            if (chain > 1)
            {
                float punch = Pulse(chainPulseAt, 0.35f);
                Scaled(new Vector2(centreX, y + 20f), 1f + 0.35f * punch, -3f);
                Label(new Rect(f.X0, y, width, 42f), "CHAIN ×" + chain, display, 40, Color.Lerp(Accent, Hot, punch));
                Unscaled();
                y += 44f;
            }

            if (session.AftertouchFraction > 0.02f)
            {
                bool steering = session.AftertouchActive;
                string hint = touchUi ? "AFTERTOUCH · DRAG TO STEER THE WRECK" : "AFTERTOUCH · WASD STEERS THE WRECK";
                Label(new Rect(f.X0, y, width, 14f), steering ? "AFTERTOUCH" : hint, text, 10, steering ? Cool : Fade(Color.white, 0.8f), 0.6f);
                Bar(new Rect(centreX - 80f, y + 17f, 160f, 4f), session.AftertouchFraction, Cool, steering);
                if (steering)
                {
                    // An arrow showing which way the wreck is being pushed on screen.
                    Vector2 wish = session.AftertouchInput;
                    float degrees = Mathf.Atan2(wish.x, wish.y) * Mathf.Rad2Deg;
                    Vector2 pivot = new Vector2(centreX, y + 44f);
                    Scaled(pivot, 1f, degrees);
                    Tint(new Rect(pivot.x - 13f, pivot.y - 13f, 26f, 26f), arrowTexture, Cool);
                    Unscaled();
                }
            }
        }

        // Board tools stay hidden until the board button is tapped.
        private void DrawBoardTools(Frame f)
        {
            bool arPlaying = ArMode != null && ArMode.IsActive && ArMode.Stage == ArStage.Playing;
            if (!arPlaying || !showBoardTools) return;
            float widthEach = 92f;
            float x = f.X1 - 3 * widthEach - 2 * 6f;
            float y = f.Y0 + 112f;
            Control("move", new Rect(x, y, widthEach, 38f), "MOVE", Kind.Soft);
            Control("smaller", new Rect(x + widthEach + 6f, y, widthEach, 38f), "SIZE −", Kind.Soft);
            Control("larger", new Rect(x + 2 * (widthEach + 6f), y, widthEach, 38f), "SIZE +", Kind.Soft);
        }

        // ------------------------------------------------------------------ bottom: controls

        private void DrawJoystick(Frame f, bool crashView)
        {
            Vector2 origin;
            Vector2 knob;
            float alpha;
            if (joystickPointer != NoPointer)
            {
                origin = ToGui(joystickOrigin);
                Vector2 delta = ToGui(joystickCurrent) - origin;
                // Driving the stick only steers; steering a wreck it moves freely.
                if (!crashView) delta.y = 0f;
                knob = origin + Vector2.ClampMagnitude(delta, JoystickRadius);
                alpha = 0.9f;
            }
            else
            {
                origin = new Vector2(f.X0 + JoystickRadius + 6f, f.Y1 - JoystickRadius - 2f);
                knob = origin;
                alpha = 0.34f;
            }

            const float knobRadius = 24f;
            Rect ringRect = new Rect(origin.x - JoystickRadius, origin.y - JoystickRadius, JoystickRadius * 2f, JoystickRadius * 2f);
            Rect knobRect = new Rect(knob.x - knobRadius, knob.y - knobRadius, knobRadius * 2f, knobRadius * 2f);
            // A dark edge underneath keeps the stick visible over pale ground.
            Tint(new Rect(ringRect.x - 1f, ringRect.y - 1f, ringRect.width + 2f, ringRect.height + 2f), ringTexture, new Color(0f, 0f, 0f, alpha * 0.45f));
            Tint(ringRect, ringTexture, new Color(1f, 1f, 1f, alpha));
            if (joystickPointer != NoPointer) Tint(new Rect(knobRect.x - 10f, knobRect.y - 10f, knobRect.width + 20f, knobRect.height + 20f), glowTexture, Fade(crashView ? Cool : Accent, 0.5f));
            Tint(new Rect(knobRect.x - 1f, knobRect.y - 1f, knobRect.width + 2f, knobRect.height + 2f), knobTexture, new Color(0f, 0f, 0f, alpha * 0.45f));
            Tint(knobRect, knobTexture, new Color(1f, 1f, 1f, alpha));
            if (joystickPointer == NoPointer)
            {
                // Resting hint: arrows showing which way the stick works in this phase.
                Color hint = new Color(1f, 1f, 1f, 0.55f);
                Tint(new Rect(origin.x - JoystickRadius * 0.78f - 11f, origin.y - 11f, 22f, 22f), chevronLeft, hint);
                Tint(new Rect(origin.x + JoystickRadius * 0.78f - 11f, origin.y - 11f, 22f, 22f), chevronRight, hint);
                if (crashView)
                {
                    Tint(new Rect(origin.x - 11f, origin.y - JoystickRadius * 0.78f - 11f, 22f, 22f), chevronDown, hint);
                    Tint(new Rect(origin.x - 11f, origin.y + JoystickRadius * 0.78f - 11f, 22f, 22f), chevronUp, hint);
                }
            }
        }

        private Vector2 ToGui(Vector2 screenPoint)
        {
            return new Vector2(screenPoint.x / scale, (Screen.height - screenPoint.y) / scale);
        }

        private void DrawDrivingPads(Frame f)
        {
            float pad = f.Pad;
            float gas = pad * 1.2f;
            float rightX = f.X1 - gas;
            Control("gas", new Rect(rightX, f.Y1 - gas, gas, gas), "GAS", Kind.Pad, true, chevronUp);
            float boostX = f.X1 - gas * 0.5f - pad * 0.5f;
            bool hasBoost = session.Player != null && session.Player.BoostFraction > 0.02f;
            Control("boost", new Rect(boostX, f.Y1 - gas - Gap - pad, pad, pad), "BOOST", Kind.Pad, hasBoost || session.Phase != GamePhase.Driving, null);
            if (f.Landscape)
            {
                Control("brake", new Rect(rightX - Gap - pad, f.Y1 - pad, pad, pad), "BRAKE", Kind.Pad, true, chevronDown);
            }
            else
            {
                Control("brake", new Rect(boostX, f.Y1 - gas - Gap - pad - Gap - pad, pad, pad), "BRAKE", Kind.Pad, true, chevronDown);
            }
        }

        private void DrawKeyHints(Frame f, GamePhase phase)
        {
            string hint = phase == GamePhase.ReadyCheck ? "SPACE  LAUNCH      C  CAMERA      N  NEXT JUNCTION"
                : phase == GamePhase.Driving ? "A / D  STEER      SHIFT  BOOST      S  BRAKE      R  RETRY"
                : "W A S D  STEER THE WRECK      SPACE  CRASHBREAKER      R  RETRY";
            Label(new Rect(f.X0, f.Y1 - 12f, f.X1 - f.X0, 14f), hint, phase == GamePhase.ReadyCheck ? text : textLeft, 9, Fade(Color.white, 0.62f), 0.5f);
        }

        // Bottom-centre in landscape, above the stick in portrait.
        private Rect CentreBottomArea(Frame f, float height)
        {
            if (!touchUi)
            {
                float desktopWidth = Mathf.Min(320f, f.X1 - f.X0);
                return new Rect((f.X0 + f.X1) * 0.5f - desktopWidth * 0.5f, f.Y1 - height - 18f, desktopWidth, height);
            }
            if (f.Landscape)
            {
                float freeLeft = f.X0 + JoystickRadius * 2f + 20f;
                float freeRight = f.X1 - f.Pad * 1.2f - Gap - f.Pad - Gap;
                float width = Mathf.Clamp(freeRight - freeLeft, 180f, 300f);
                return new Rect((freeLeft + freeRight) * 0.5f - width * 0.5f, f.Y1 - height, width, height);
            }
            float portraitWidth = Mathf.Min(300f, f.X1 - f.X0 - f.Pad * 1.2f - Gap);
            return new Rect(f.X0, f.Y1 - JoystickRadius * 2f - Gap - height, portraitWidth, height);
        }

        private void DrawLaunchMeter(Frame f)
        {
            Rect area = CentreBottomArea(f, 84f);
            GUI.Box(area, GUIContent.none, plateStyle);
            float meter = session.LaunchMeter;
            bool inZone = meter >= CrashoutGameSession.LaunchZoneMin && meter <= CrashoutGameSession.LaunchZoneMax;
            Label(new Rect(area.x + 12f, area.y + 6f, area.width - 24f, 14f), touchUi ? "LAUNCH · TAP GAS IN THE GREEN" : "LAUNCH · PRESS SPACE IN THE GREEN", textLeft, 10, inZone ? Good : Fade(Cool, 0.95f), 0.5f);

            Rect bar = new Rect(area.x + 12f, area.y + 27f, area.width - 24f, 12f);
            Tint(bar, barTexture, new Color(0f, 0f, 0f, 0.5f));
            // Power ramp behind the needle, then the sweet spot.
            Segments(new Rect(bar.x, bar.y + 2f, bar.width, bar.height - 4f), 30, meter, Fade(Cool, 0.7f), Fade(Hot, 0.85f), 0.1f);
            Rect zone = new Rect(bar.x + bar.width * CrashoutGameSession.LaunchZoneMin, bar.y, bar.width * (CrashoutGameSession.LaunchZoneMax - CrashoutGameSession.LaunchZoneMin), bar.height);
            float throb = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 9f);
            Tint(new Rect(zone.x - 8f, zone.y - 9f, zone.width + 16f, zone.height + 18f), glowTexture, Fade(Good, inZone ? 0.75f : 0.3f * throb));
            Tint(zone, barTexture, Fade(Good, inZone ? 1f : 0.8f));
            float needle = bar.x + bar.width * meter;
            Tint(new Rect(needle - 9f, bar.y - 10f, 18f, bar.height + 20f), glowTexture, new Color(1f, 1f, 1f, 0.5f));
            Tint(new Rect(needle - 1.5f, bar.y - 5f, 3f, bar.height + 10f), barTexture, Color.white);

            Control("launch", new Rect(area.x + 12f, area.y + 46f, area.width - 24f, 30f), inZone ? "PERFECT!" : "LAUNCH", Kind.Button);
        }

        private void DrawCrashbreaker(Frame f)
        {
            float charge = session.CrashbreakerCharge;
            bool ready = session.CanCrashbreak;
            // A big round trigger where the right thumb already is, clear of the action mid-screen.
            float size = Mathf.Min(touchUi ? 132f : 112f, f.Pad * 1.6f);
            Rect area = new Rect(f.X1 - size - 6f, f.Y1 - size - 6f, size, size);
            Vector2 centre = area.center;
            float radius = area.width * 0.5f;
            float throb = ready ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f) : 0f;
            float burst = Pulse(readyPulseAt, 0.5f);

            if (ready) Tint(new Rect(area.x - 26f, area.y - 26f, area.width + 52f, area.height + 52f), glowTexture, Fade(Accent, 0.35f + 0.3f * throb + 0.3f * burst));
            bool isPressed = ready && pressed.Contains("boom");
            Scaled(centre, 1f + 0.06f * throb + 0.2f * burst - (isPressed ? 0.06f : 0f));
            Tint(new Rect(area.x + 8f, area.y + 8f, area.width - 16f, area.height - 16f), knobTexture, ready ? Color.Lerp(Accent, Hot, isPressed ? 0.6f : 0.15f * throb) : new Color(0.05f, 0.06f, 0.1f, 0.7f));
            Tint(new Rect(area.x + 8f, area.y + 8f, area.width - 16f, area.height - 16f), ringTexture, new Color(1f, 1f, 1f, ready ? 0.9f : 0.3f));
            Label(new Rect(area.x, centre.y - 22f, area.width, 24f), ready ? "DETONATE" : Mathf.RoundToInt(charge * 100f) + "%", display, ready ? 22 : 26, Color.white);
            Label(new Rect(area.x, centre.y + 2f, area.width, 12f), ready ? (touchUi ? "TAP" : "SPACE") : "CRASHBREAKER", text, 8, Fade(Color.white, 0.85f), 0.5f);
            Unscaled();

            // Charge dial: a ring of ticks that fills clockwise from the top.
            const int ticks = 40;
            for (int i = 0; i < ticks; i++)
            {
                float at = (i + 0.5f) / ticks;
                bool lit = at <= charge;
                Scaled(centre, 1f, at * 360f);
                Tint(new Rect(centre.x - 1.6f, centre.y - radius + 1f, 3.2f, lit ? 7f : 5f), barTexture, lit ? (ready ? Color.Lerp(Hot, Color.white, throb * 0.5f) : Cool) : new Color(1f, 1f, 1f, 0.2f));
            }
            Unscaled();

            Control("boom", area, "", Kind.Hidden, ready);
        }

        private void DrawArSetup(Frame f)
        {
            string message = null;
            switch (ArMode.Stage)
            {
                case ArStage.Starting: message = "STARTING AR…"; break;
                case ArStage.Searching: message = "MOVE YOUR PHONE SLOWLY TO FIND A FLAT TABLE"; break;
                case ArStage.ReadyToPlace: message = "BOARD IS " + Mathf.RoundToInt(ArMode.BoardWidthMetres * 100f) + " CM WIDE"; break;
            }
            if (message != null) Label(new Rect(f.X0, f.Y0 + 8f, f.X1 - f.X0, 26f), message, display, 20, Color.white);

            float centreX = (f.X0 + f.X1) * 0.5f;
            float height = 56f;
            Control("place", new Rect(centreX - 120f, f.Y1 - height, 240f, height), "PLACE BOARD", Kind.Button);
            Control("smaller", new Rect(centreX - 120f - Gap - height, f.Y1 - height, height, height), "−", Kind.Soft);
            Control("larger", new Rect(centreX + 120f + Gap, f.Y1 - height, height, height), "+", Kind.Soft);
        }

        // ------------------------------------------------------------------ results

        private void DrawResults(Frame f)
        {
            CrashScenario scenario = session.Scenario;
            CrashMedal medal = session.Medal;
            float age = Time.unscaledTime - resultsShownAt;
            float enter = Mathf.Clamp01(age / 0.35f);
            enter = 1f - Mathf.Pow(1f - enter, 3f);

            Tint(new Rect(0f, 0f, f.W, f.H), barTexture, new Color(0f, 0f, 0f, 0.45f * enter));
            float width = Mathf.Min(f.X1 - f.X0, 520f);
            float height = Mathf.Min(f.Y1 - f.Y0, 336f);
            Rect box = new Rect((f.X0 + f.X1) * 0.5f - width * 0.5f, (f.Y0 + f.Y1) * 0.5f - height * 0.5f + (1f - enter) * 30f, width, height);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, enter);
            GUI.Box(box, GUIContent.none, panelStyle);
            GUI.color = previous;
            Tint(new Rect(box.x + 16f, box.y, 64f, 3f), barTexture, Fade(Accent, enter));

            Color medalColour = medal == CrashMedal.Gold ? GoldColour : medal == CrashMedal.Silver ? SilverColour : medal == CrashMedal.Bronze ? BronzeColour : new Color(0.5f, 0.52f, 0.58f);
            string medalText = medal == CrashMedal.None ? "NO MEDAL" : medal.ToString().ToUpperInvariant() + " MEDAL";
            float pad = 20f;
            float inner = box.width - pad * 2f;

            Label(new Rect(box.x + pad, box.y + 12f, inner, 14f), scenario != null ? scenario.Name : "RUN COMPLETE", textLeft, 11, Fade(Cool, enter), 0.5f);
            Label(new Rect(box.x + pad, box.y + 12f, inner, 14f), "RUN COMPLETE", textRight, 11, Fade(Color.white, 0.6f * enter), 0.4f);

            // Medal emblem lands with a thump once the count has finished.
            float land = Mathf.Clamp01((age - 1.3f) / 0.3f);
            float emblem = 84f;
            Vector2 emblemCentre = new Vector2(box.x + pad + emblem * 0.5f, box.y + 34f + emblem * 0.5f);
            if (land > 0f)
            {
                float overshoot = 1f + 0.6f * Mathf.Pow(1f - land, 2f);
                Tint(new Rect(emblemCentre.x - emblem, emblemCentre.y - emblem, emblem * 2f, emblem * 2f), glowTexture, Fade(medalColour, 0.4f * land));
                Scaled(emblemCentre, overshoot, (1f - land) * -25f);
                Tint(new Rect(emblemCentre.x - emblem * 0.5f, emblemCentre.y - emblem * 0.5f, emblem, emblem), medalTexture, Fade(medalColour, land));
                Unscaled();
            }
            else
            {
                Tint(new Rect(emblemCentre.x - emblem * 0.5f, emblemCentre.y - emblem * 0.5f, emblem, emblem), medalTexture, new Color(1f, 1f, 1f, 0.08f));
            }

            float heroX = box.x + pad + emblem + 14f;
            float heroWidth = box.xMax - pad - heroX;
            Label(new Rect(heroX, box.y + 36f, heroWidth, 22f), land > 0f ? medalText : "TOTAL DAMAGE", displayLeft, 20, land > 0f ? medalColour : Fade(Color.white, 0.7f));
            Label(new Rect(heroX, box.y + 54f, heroWidth, 62f), "$" + Mathf.RoundToInt(resultsScore).ToString("N0"), displayLeft, 58, Fade(Color.white, enter));

            float y = box.y + 128f;
            if (scenario != null)
            {
                float saved = shownScore;
                shownScore = resultsScore;
                MedalTrack(new Rect(box.x + pad, y, inner, 12f), scenario, true);
                shownScore = saved;
                y += 36f;
            }

            Tint(new Rect(box.x + pad, y, inner, 1f), barTexture, new Color(1f, 1f, 1f, 0.12f));
            y += 8f;
            float column = (inner - 16f) * 0.5f;
            Stat(box.x + pad, y, column, "CARS WRECKED", session.WreckedCount.ToString(), Color.white);
            Stat(box.x + pad + column + 16f, y, column, "BEST CHAIN", "×" + session.BestChain, Color.white);
            Stat(box.x + pad, y + 24f, column, "CRASHBREAKERS", session.CrashbreakersUsed.ToString(), Color.white);
            Stat(box.x + pad + column + 16f, y + 24f, column, "BONUSES", "$" + session.BonusScore.ToString("N0"), Color.white);
            if (scenario != null)
            {
                Stat(box.x + pad, y + 48f, column, "TARGET CAR", session.TargetDown ? "DOWN  +$" + scenario.TargetBonus.ToString("N0") : "MISSED", session.TargetDown ? Good : new Color(1f, 0.45f, 0.4f));
            }
            Destruction city = Destruction.Instance;
            if (city != null)
            {
                Stat(box.x + pad + column + 16f, y + 48f, column, "CITY DAMAGE", (city.BrokenWindowCount + city.ScarCount + city.BrokenPropCount).ToString() + " HITS", Hot);
            }

            float buttonWidth = (inner - Gap * 2f) / 3f;
            float buttonY = box.yMax - 16f - 46f;
            Control("replay", new Rect(box.x + pad, buttonY, buttonWidth, 46f), "REPLAY", Kind.Soft, session.Replay != null && session.Replay.IsAvailable);
            Control("retry", new Rect(box.x + pad + buttonWidth + Gap, buttonY, buttonWidth, 46f), "RETRY", Kind.Soft);
            Control("next", new Rect(box.x + pad + (buttonWidth + Gap) * 2f, buttonY, buttonWidth, 46f), "NEXT", Kind.Button);
        }

        private void UpdateReplayScrubber()
        {
            TurnReplay replay = session.Replay;
            if (replay == null || !replay.IsActive)
            {
                replayScrubPointer = NoPointer;
                return;
            }
            if (!hitRects.TryGetValue("replay-seek", out Rect track)) return;
            foreach (Pointer pointer in pointers)
            {
                if (replayScrubPointer == NoPointer && pointer.Began && track.Contains(pointer.Position))
                {
                    replayScrubPointer = pointer.Id;
                    resumeAfterScrub = replay.IsPlaying;
                    replay.Pause();
                }
                if (pointer.Id != replayScrubPointer) continue;
                replay.Seek(Mathf.InverseLerp(track.xMin, track.xMax, pointer.Position.x) * replay.Duration);
                return;
            }
            if (replayScrubPointer != NoPointer)
            {
                replayScrubPointer = NoPointer;
                if (resumeAfterScrub && replay.Position < replay.Duration) replay.TogglePlayback();
            }
        }

        private static string ReplayTime(float time)
        {
            int minutes = Mathf.FloorToInt(time / 60f);
            return minutes + ":" + (time - minutes * 60f).ToString("00.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void DrawReplay(Frame f)
        {
            TurnReplay replay = session.Replay;
            Label(new Rect(f.X0, f.Y0, 180f, 22f), "TURN REPLAY", displayLeft, 22, Cool);
            Label(new Rect(f.X0, f.Y0 + 24f, 180f, 20f), "$" + replay.Score.ToString("N0") + " DAMAGE", textLeft, 13, Color.white);
            Control("replay-close", new Rect(f.X1 - 110f, f.Y0, 110f, MinTarget), "RESULTS", Kind.Soft);
            if (ArMode == null || !ArMode.IsActive)
                Control("camera", new Rect(f.X1 - 110f, f.Y0 + 50f, 110f, MinTarget), "CAMERA", Kind.Soft);

            float width = Mathf.Min(f.X1 - f.X0, 520f);
            Rect box = new Rect((f.X0 + f.X1 - width) * 0.5f, f.Y1 - 162f, width, 162f);
            GUI.Box(box, GUIContent.none, panelStyle);
            float x = box.x + 12f, inner = box.width - 24f;
            Rect slider = new Rect(x + 8f, box.y + 4f, inner - 16f, MinTarget);
            float progress = replay.Duration > 0f ? replay.Position / replay.Duration : 0f;
            Bar(new Rect(slider.x, slider.center.y - 2f, slider.width, 4f), progress, Cool, false);
            Tint(new Rect(slider.x + slider.width * progress - 7f, slider.center.y - 7f, 14f, 14f), knobTexture, Color.white);
            Control("replay-seek", slider, "", Kind.Hidden);

            float y = box.y + 52f;
            Control("replay-play", new Rect(x, y, 82f, MinTarget), replay.IsPlaying ? "PAUSE" : "PLAY", Kind.Soft);
            Control("replay-start", new Rect(x + 88f, y, 72f, MinTarget), "RESTART", Kind.Soft);
            Label(new Rect(x + 164f, y, inner - 164f, MinTarget), ReplayTime(replay.Position) + " / " + ReplayTime(replay.Duration), textRight, 12, Color.white);

            float speedWidth = (inner - 18f) / 4f;
            float[] speeds = { 0.25f, 0.5f, 1f, 2f };
            string[] ids = { "replay-025", "replay-05", "replay-1", "replay-2" };
            string[] labels = { "0.25×", "0.5×", "1×", "2×" };
            for (int i = 0; i < speeds.Length; i++)
                Control(ids[i], new Rect(x + i * (speedWidth + 6f), y + 50f, speedWidth, MinTarget), labels[i], replay.Speed == speeds[i] ? Kind.Button : Kind.Soft);

            Camera camera = Camera.main;
            if (camera == null) return;
            foreach (TurnReplay.Cue cue in replay.Cues)
            {
                float age = replay.Position - cue.Time;
                if (age < 0f || age > 1.6f) continue;
                Vector3 screen = camera.WorldToScreenPoint(cue.Position);
                if (screen.z <= 0f) continue;
                Label(new Rect(screen.x / scale - 180f, (Screen.height - screen.y) / scale - age * 40f, 360f, 40f),
                    cue.Text, display, 26, Fade(Hot, Mathf.Clamp01((1.6f - age) * 2.5f)));
            }
        }

        private void Stat(float x, float y, float width, string label, string value, Color colour)
        {
            Label(new Rect(x, y, width, 20f), label, textLeft, 10, Fade(Color.white, 0.6f), 0.3f);
            Label(new Rect(x, y - 2f, width, 24f), value, displayRight, 19, colour, 0.5f);
            Tint(new Rect(x, y + 21f, width, 1f), barTexture, new Color(1f, 1f, 1f, 0.07f));
        }

        // ------------------------------------------------------------------ popups

        private void DrawPopups()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                Popup popup = popups[i];
                float age = Time.unscaledTime - popup.Born;
                if (age > 1.6f)
                {
                    popups.RemoveAt(i);
                    continue;
                }
                Vector3 screen = camera.WorldToScreenPoint(popup.World);
                if (screen.z <= 0f) continue;
                float rise = age * 40f;
                float alpha = Mathf.Clamp01((1.6f - age) * 2.5f) * Mathf.Clamp01(age * 12f);
                // Punch in oversized, settle, then drift up.
                float pop = 1f + 0.7f * Mathf.Pow(Mathf.Clamp01(1f - age / 0.18f), 2f);
                Vector2 centre = new Vector2(screen.x / scale, (Screen.height - screen.y) / scale - rise);
                Scaled(centre, pop, (i % 2 == 0 ? -2.5f : 2.5f));
                Label(new Rect(centre.x - 200f, centre.y - 24f, 400f, 48f), popup.Text, display, popup.Size, Fade(popup.Colour, alpha), 0.85f);
                Unscaled();
            }
        }

        // ------------------------------------------------------------------ controls

        // Draws a control and registers its hit area (in screen pixels) for the next frame.
        private void Control(string id, Rect rect, string content, Kind kind, bool enabled = true, Texture2D glyph = null)
        {
            bool isPressed = enabled && pressed.Contains(id);
            Color previous = GUI.color;
            GUI.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            switch (kind)
            {
                case Kind.Hidden:
                    break;
                case Kind.Icon:
                {
                    // A faint disc behind a smaller icon keeps the 44 pt target without a heavy button.
                    float disc = 34f;
                    Rect discRect = new Rect(rect.center.x - disc * 0.5f, rect.center.y - disc * 0.5f, disc, disc);
                    Tint(discRect, knobTexture, isPressed ? Fade(Accent, 0.8f) : new Color(0.02f, 0.03f, 0.06f, 0.5f));
                    Tint(discRect, ringTexture, new Color(1f, 1f, 1f, isPressed ? 0.9f : 0.3f));
                    GUI.color = Color.white;
                    float size = 18f;
                    Rect iconRect = new Rect(rect.center.x - size * 0.5f, rect.center.y - size * 0.5f, size, size);
                    GUI.color = new Color(0f, 0f, 0f, 0.6f);
                    GUI.DrawTexture(new Rect(iconRect.x + 1f, iconRect.y + 1f, size, size), glyph);
                    GUI.color = Color.white;
                    GUI.DrawTexture(iconRect, glyph);
                    break;
                }
                case Kind.Pad:
                {
                    if (isPressed) Tint(new Rect(rect.x - 14f, rect.y - 14f, rect.width + 28f, rect.height + 28f), glowTexture, Fade(id == "boost" ? Hot : id == "brake" ? new Color(1f, 0.3f, 0.25f) : Accent, 0.6f));
                    GUI.Box(rect, GUIContent.none, isPressed ? padHotStyle : padStyle);
                    bool hasText = content.Length > 0;
                    if (glyph != null)
                    {
                        float glyphSize = rect.width * 0.46f;
                        float glyphY = hasText ? rect.y + rect.height * 0.14f : rect.center.y - glyphSize * 0.5f;
                        GUI.color = new Color(1f, 1f, 1f, enabled ? 0.9f : 0.4f);
                        GUI.DrawTexture(new Rect(rect.center.x - glyphSize * 0.5f, glyphY, glyphSize, glyphSize), glyph);
                    }
                    GUI.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                    if (hasText)
                    {
                        Rect labelRect = glyph != null ? new Rect(rect.x, rect.yMax - 30f, rect.width, 22f) : rect;
                        Label(labelRect, content, display, glyph != null ? 15 : 17, new Color(1f, 1f, 1f, 0.95f), 0.6f);
                    }
                    if (id == "boost" && session.Player != null)
                    {
                        Bar(new Rect(rect.x + rect.width * 0.2f, rect.yMax - 14f, rect.width * 0.6f, 3f), session.Player.BoostFraction, session.Player.IsBoosting ? Hot : Cool, session.Player.IsBoosting);
                    }
                    break;
                }
                case Kind.Soft:
                    GUI.Box(rect, GUIContent.none, isPressed ? softHotStyle : softStyle);
                    Label(rect, content, display, 17, Color.white, 0.5f);
                    break;
                default:
                    GUI.Box(rect, GUIContent.none, isPressed ? buttonHotStyle : buttonStyle);
                    Label(rect, content, display, 18, Color.white, 0.45f);
                    break;
            }
            GUI.color = previous;

            if (enabled)
            {
                nextRects[id] = new Rect(rect.x * scale, Screen.height - rect.yMax * scale, rect.width * scale, rect.height * scale);
            }
        }

        // ------------------------------------------------------------------ styles and textures

        private void EnsureStyles()
        {
            if (panelStyle != null) return;
            panelTexture = MakeRounded(64, 14, new Color(0.035f, 0.045f, 0.08f, 0.93f), new Color(0.09f, 0.1f, 0.16f, 0.93f), new Color(1f, 1f, 1f, 0.22f));
            plateTexture = MakeRounded(64, 10, new Color(0.02f, 0.03f, 0.06f, 0.62f), new Color(0.05f, 0.06f, 0.1f, 0.42f), new Color(1f, 1f, 1f, 0.14f));
            ghostTexture = MakeRounded(64, 16, new Color(0.02f, 0.03f, 0.06f, 0.3f), new Color(0.02f, 0.03f, 0.06f, 0.3f), new Color(1f, 1f, 1f, 0.14f));
            softTexture = MakeRounded(64, 12, new Color(1f, 1f, 1f, 0.08f), new Color(1f, 1f, 1f, 0.16f), new Color(1f, 1f, 1f, 0.42f));
            softHotTexture = MakeRounded(64, 12, new Color(1f, 0.5f, 0.15f, 0.55f), new Color(1f, 0.6f, 0.25f, 0.7f), new Color(1f, 1f, 1f, 0.9f));
            buttonTexture = MakeRounded(64, 12, new Color(0.92f, 0.3f, 0.04f, 0.95f), new Color(1f, 0.5f, 0.12f, 0.95f), new Color(1f, 0.8f, 0.55f, 0.8f));
            buttonHotTexture = MakeRounded(64, 12, new Color(1f, 0.55f, 0.18f, 1f), new Color(1f, 0.75f, 0.35f, 1f), Color.white);
            padTexture = MakeRounded(128, 64, new Color(0.02f, 0.03f, 0.06f, 0.32f), new Color(0.1f, 0.12f, 0.18f, 0.2f), new Color(1f, 1f, 1f, 0.5f));
            padHotTexture = MakeRounded(128, 64, new Color(1f, 0.4f, 0.08f, 0.7f), new Color(1f, 0.6f, 0.2f, 0.55f), new Color(1f, 1f, 1f, 0.95f));
            ringTexture = MakeRounded(128, 64, new Color(1f, 1f, 1f, 0.05f), new Color(1f, 1f, 1f, 0.05f), new Color(1f, 1f, 1f, 0.85f));
            knobTexture = MakeRounded(64, 32, new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 1f));
            iconBackTexture = MakeRounded(64, 32, new Color(0.02f, 0.03f, 0.06f, 0.5f), new Color(0.02f, 0.03f, 0.06f, 0.4f), new Color(1f, 1f, 1f, 0.26f));
            iconBackHotTexture = MakeRounded(64, 32, new Color(1f, 0.5f, 0.15f, 0.7f), new Color(1f, 0.5f, 0.15f, 0.7f), new Color(1f, 1f, 1f, 0.9f));
            chipTexture = MakeRounded(32, 8, new Color(1f, 1f, 1f, 0.1f), new Color(1f, 1f, 1f, 0.1f), new Color(1f, 1f, 1f, 0.3f));
            barTexture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            barTexture.SetPixel(0, 0, Color.white);
            barTexture.Apply();
            slantTexture = MakeShape(32, 32, (u, v) =>
            {
                // A parallelogram leaning right, with a hairline gap between neighbours.
                float x = u - (v - 0.5f) * 0.3f;
                return Mathf.Clamp01(Mathf.Min(x - 0.17f, 0.83f - x) * 32f * 0.7f);
            });
            glowTexture = MakeShape(64, 64, (u, v) =>
            {
                float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f;
                float a = Mathf.Clamp01(1f - d);
                return a * a;
            });
            edgeTexture = MakeShape(4, 32, (u, v) => v * v);
            arrowTexture = MakeShape(64, 64, (u, v) =>
            {
                float head = Polygon(new Vector2(u, v), new[] { new Vector2(0.5f, 0.95f), new Vector2(0.14f, 0.45f), new Vector2(0.86f, 0.45f) });
                float shaft = Box(new Vector2(u, v), new Vector2(0.5f, 0.28f), new Vector2(0.13f, 0.22f), 0.03f);
                return Mathf.Clamp01(0.5f - Mathf.Min(head, shaft) * 64f);
            });
            medalTexture = MakeMedal(160);
            chevronUp = MakeChevron(64, 0);
            chevronRight = MakeChevron(64, 1);
            chevronDown = MakeChevron(64, 2);
            chevronLeft = MakeChevron(64, 3);
            foreach (Icon icon in new[] { Icon.Retry, Icon.SoundOn, Icon.SoundOff, Icon.Camera, Icon.Board, Icon.Check })
            {
                icons[icon] = MakeIcon(icon);
            }

            // A condensed display face for numbers and calls to action; a clean sans for labels.
            displayFont = OsFont(40, "DIN Condensed", "DINCondensed-Bold", "Avenir Next Condensed", "AvenirNextCondensed-Heavy", "Bahnschrift", "Roboto Condensed", "Impact", "Arial Narrow");
            textFont = OsFont(16, "Avenir Next", "AvenirNext-DemiBold", "SF Pro Text", "Helvetica Neue", "Segoe UI", "Roboto", "Arial");

            panelStyle = BoxStyle(panelTexture, 14);
            plateStyle = BoxStyle(plateTexture, 10);
            ghostStyle = BoxStyle(ghostTexture, 16);
            softStyle = BoxStyle(softTexture, 12);
            softHotStyle = BoxStyle(softHotTexture, 12);
            buttonStyle = BoxStyle(buttonTexture, 12);
            buttonHotStyle = BoxStyle(buttonHotTexture, 12);
            padStyle = BoxStyle(padTexture, 60);
            padHotStyle = BoxStyle(padHotTexture, 60);
            iconBackStyle = BoxStyle(iconBackTexture, 16);
            iconBackHotStyle = BoxStyle(iconBackHotTexture, 16);
            chipStyle = BoxStyle(chipTexture, 8);

            GUIStyle baseDisplay = new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Overflow, fontStyle = displayFont != null ? FontStyle.Normal : FontStyle.Bold };
            if (displayFont != null) baseDisplay.font = displayFont;
            GUIStyle baseText = new GUIStyle(GUI.skin.label) { wordWrap = false, clipping = TextClipping.Overflow, fontStyle = FontStyle.Bold };
            if (textFont != null) baseText.font = textFont;
            display = new GUIStyle(baseDisplay) { alignment = TextAnchor.MiddleCenter };
            displayLeft = new GUIStyle(baseDisplay) { alignment = TextAnchor.MiddleLeft };
            displayRight = new GUIStyle(baseDisplay) { alignment = TextAnchor.MiddleRight };
            text = new GUIStyle(baseText) { alignment = TextAnchor.MiddleCenter };
            textLeft = new GUIStyle(baseText) { alignment = TextAnchor.MiddleLeft };
            textRight = new GUIStyle(baseText) { alignment = TextAnchor.MiddleRight };
        }

        private static Font OsFont(int size, params string[] candidates)
        {
            try
            {
                var installed = new HashSet<string>(Font.GetOSInstalledFontNames());
                foreach (string name in candidates)
                {
                    if (installed.Contains(name)) return Font.CreateDynamicFontFromOSFont(name, size);
                }
            }
            catch (System.Exception)
            {
                // Some platforms cannot enumerate fonts; the built-in face is fine.
            }
            return null;
        }

        private static GUIStyle BoxStyle(Texture2D texture, int border)
        {
            return new GUIStyle(GUI.skin.box) { normal = { background = texture }, border = new RectOffset(border, border, border, border) };
        }

        /// <summary>A rounded rectangle with a vertical gradient (bottom to top) and a hairline rim that is brighter along the top.</summary>
        private static Texture2D MakeRounded(int size, int radius, Color bottom, Color top, Color outline)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius));
                    float dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius));
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    float coverage = Mathf.Clamp01(0.5f - distance);
                    float edge = Mathf.Clamp01(1f - Mathf.Abs(distance + 1f));
                    float lift = y / (float)(size - 1);
                    Color fill = Color.Lerp(bottom, top, lift);
                    float rim = edge * outline.a * Mathf.Lerp(0.45f, 1f, lift);
                    Color colour = Color.Lerp(fill, outline, rim);
                    colour.a = Mathf.Max(fill.a, rim) * coverage;
                    pixels[y * size + x] = colour;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D MakeShape(int width, int height, System.Func<float, float, float> alpha)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha((x + 0.5f) / width, (y + 0.5f) / height)));
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>A struck medal: bevelled rim, a field with a five-point star, and two ribbon tails.</summary>
        private static Texture2D MakeMedal(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color[] pixels = new Color[size * size];
            var star = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float angle = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? 0.2f : 0.085f;
                star[i] = new Vector2(0.5f + Mathf.Cos(angle) * r, 0.56f + Mathf.Sin(angle) * r);
            }
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    float d = Vector2.Distance(p, new Vector2(0.5f, 0.56f));
                    float disc = d - 0.36f;
                    float ribbonLeft = Polygon(p, new[] { new Vector2(0.3f, 0.36f), new Vector2(0.46f, 0.3f), new Vector2(0.38f, 0.02f), new Vector2(0.3f, 0.1f), new Vector2(0.2f, 0.06f) });
                    float ribbonRight = Polygon(p, new[] { new Vector2(0.7f, 0.36f), new Vector2(0.54f, 0.3f), new Vector2(0.62f, 0.02f), new Vector2(0.7f, 0.1f), new Vector2(0.8f, 0.06f) });
                    float ribbon = Mathf.Min(ribbonLeft, ribbonRight);
                    float alpha = Mathf.Clamp01(0.5f - Mathf.Min(disc, ribbon) * size);
                    float shade = 0.55f;
                    if (disc < 0f)
                    {
                        // Lit from the top-left: bright rim, darker field, bright star.
                        float light = 0.5f + 0.5f * Vector2.Dot((p - new Vector2(0.5f, 0.56f)).normalized, new Vector2(-0.6f, 0.8f));
                        shade = d > 0.3f ? Mathf.Lerp(0.6f, 1.25f, light) : Mathf.Lerp(0.5f, 0.72f, light);
                        float starDistance = Polygon(p, star);
                        if (starDistance < 0f) shade = Mathf.Lerp(1.1f, 1.35f, light);
                        else if (starDistance < 0.012f) shade *= 0.7f;
                    }
                    pixels[y * size + x] = new Color(Mathf.Clamp01(shade), Mathf.Clamp01(shade), Mathf.Clamp01(shade), alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        // Anti-aliased chevron; direction 0 up, 1 right, 2 down, 3 left.
        private static Texture2D MakeChevron(int size, int direction)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color[] pixels = new Color[size * size];
            Vector2 a = new Vector2(0.2f, 0.36f), b = new Vector2(0.5f, 0.68f), c = new Vector2(0.8f, 0.36f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    Vector2 p = direction == 0 ? new Vector2(u, v)
                        : direction == 1 ? new Vector2(v, u)
                        : direction == 2 ? new Vector2(u, 1f - v)
                        : new Vector2(v, 1f - u);
                    float d = Mathf.Min(SegmentDistance(p, a, b), SegmentDistance(p, b, c));
                    float alpha = Mathf.Clamp01((0.085f - d) / 0.025f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        // ------------------------------------------------------------------ icons (signed distance shapes)

        private static Texture2D MakeIcon(Icon icon)
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                    float sd = IconDistance(icon, p);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - sd * size));
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        private static float IconDistance(Icon icon, Vector2 p)
        {
            switch (icon)
            {
                case Icon.Retry:
                {
                    Vector2 c = new Vector2(0.5f, 0.5f);
                    float radius = 0.27f;
                    float angle = Mathf.Atan2(p.y - c.y, p.x - c.x) * Mathf.Rad2Deg;
                    float arc = angle > 35f && angle < 110f ? 1f : Mathf.Abs(Vector2.Distance(p, c) - radius) - 0.055f;
                    float end = 35f * Mathf.Deg2Rad;
                    Vector2 radial = new Vector2(Mathf.Cos(end), Mathf.Sin(end));
                    Vector2 tangent = new Vector2(-radial.y, radial.x);
                    Vector2 e = c + radial * radius;
                    float head = Polygon(p, new[] { e + radial * 0.13f, e - radial * 0.13f, e + tangent * 0.17f });
                    return Mathf.Min(arc, head);
                }
                case Icon.SoundOn:
                case Icon.SoundOff:
                {
                    float body = Polygon(p, new[]
                    {
                        new Vector2(0.16f, 0.4f), new Vector2(0.3f, 0.4f), new Vector2(0.48f, 0.25f),
                        new Vector2(0.48f, 0.75f), new Vector2(0.3f, 0.6f), new Vector2(0.16f, 0.6f)
                    });
                    if (icon == Icon.SoundOff)
                    {
                        float x1 = SegmentDistance(p, new Vector2(0.6f, 0.36f), new Vector2(0.84f, 0.64f)) - 0.04f;
                        float x2 = SegmentDistance(p, new Vector2(0.6f, 0.64f), new Vector2(0.84f, 0.36f)) - 0.04f;
                        return Mathf.Min(body, Mathf.Min(x1, x2));
                    }
                    Vector2 c = new Vector2(0.5f, 0.5f);
                    float angle = Mathf.Abs(Mathf.Atan2(p.y - c.y, p.x - c.x)) * Mathf.Rad2Deg;
                    float distance = Vector2.Distance(p, c);
                    float wave1 = angle < 42f ? Mathf.Abs(distance - 0.2f) - 0.03f : 1f;
                    float wave2 = angle < 42f ? Mathf.Abs(distance - 0.32f) - 0.03f : 1f;
                    return Mathf.Min(body, Mathf.Min(wave1, wave2));
                }
                case Icon.Camera:
                {
                    float body = Box(p, new Vector2(0.38f, 0.5f), new Vector2(0.22f, 0.17f), 0.05f);
                    float lens = Polygon(p, new[] { new Vector2(0.62f, 0.5f), new Vector2(0.84f, 0.35f), new Vector2(0.84f, 0.65f) });
                    return Mathf.Min(body, lens);
                }
                case Icon.Board:
                {
                    float d = 1f;
                    foreach (Vector2 centre in new[] { new Vector2(0.31f, 0.31f), new Vector2(0.69f, 0.31f), new Vector2(0.31f, 0.69f), new Vector2(0.69f, 0.69f) })
                    {
                        d = Mathf.Min(d, Box(p, centre, new Vector2(0.14f, 0.14f), 0.03f));
                    }
                    return d;
                }
                default:
                {
                    float a = SegmentDistance(p, new Vector2(0.22f, 0.5f), new Vector2(0.42f, 0.3f)) - 0.05f;
                    float b = SegmentDistance(p, new Vector2(0.42f, 0.3f), new Vector2(0.8f, 0.72f)) - 0.05f;
                    return Mathf.Min(a, b);
                }
            }
        }

        private static float Box(Vector2 p, Vector2 centre, Vector2 half, float round)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x - centre.x), Mathf.Abs(p.y - centre.y)) - half + Vector2.one * round;
            return Vector2.Max(d, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - round;
        }

        // Signed distance to a polygon (negative inside).
        private static float Polygon(Vector2 p, Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i];
                Vector2 w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }
    }
}
