using System.Collections.Generic;
using Crashout.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Crashout.Presentation
{
    /// <summary>
    /// Minimal touch HUD. The board stays visible: most elements have no background at all, controls
    /// are faded, the score is a corner readout until the crash, and steering is a floating thumb
    /// joystick. Sizes are in points (about 1/160 inch); every control keeps a hit area of 44 pt or more.
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

        private enum Kind { Button, Soft, Pad, Icon }
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
        private Vector2 joystickOrigin;
        private Vector2 joystickCurrent;
        private bool controlsActive = true;

        private float scale = 1f;
        private bool showBoardTools;
        private GUIStyle panelStyle, ghostStyle, softStyle, softHotStyle, buttonStyle, buttonHotStyle, padStyle, padHotStyle;
        private GUIStyle smallLabel, microLabel, scoreSmall, scoreBig, speedStyle, messageStyle, bigStyle, buttonText, softText, padText,
            popupStyle, statLabelStyle, statValueStyle, valueStyle, iconBackStyle, iconBackHotStyle;
        private Texture2D panelTexture, ghostTexture, softTexture, softHotTexture, buttonTexture, buttonHotTexture, padTexture, padHotTexture,
            barTexture, ringTexture, knobTexture, iconBackTexture, iconBackHotTexture;
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
            foreach (Texture2D texture in new[] { panelTexture, ghostTexture, softTexture, softHotTexture, buttonTexture, buttonHotTexture, padTexture,
                padHotTexture, barTexture, ringTexture, knobTexture, iconBackTexture, iconBackHotTexture, chevronUp, chevronDown, chevronLeft, chevronRight })
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
            UpdateJoystick();

            pressed.Clear();
            taps.Clear();
            foreach (Pointer pointer in pointers)
            {
                if (pointer.Id == joystickPointer) continue;
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

        private void HandleTap(string id)
        {
            if (id != "gas" && id != "launch") CrashAudio.PlayClick();
            switch (id)
            {
                case "gas":
                case "launch":
                    session.TryLaunch();
                    break;
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
            if (scoreEvent.Amount <= 0 || !session.TryGetVehiclePosition(scoreEvent.EntityId, out Vector3 position)) return;
            AddPopup("+$" + scoreEvent.Amount.ToString("N0"), position + Vector3.up * 1.2f,
                Color.Lerp(Color.white, new Color(1f, 0.75f, 0.2f), Mathf.Clamp01(scoreEvent.Amount / 4000f)),
                Mathf.Clamp(20 + scoreEvent.Amount / 250, 20, 38));
            if (scoreEvent.ChainCount >= 3 && scoreEvent.ChainCount % 2 == 1)
            {
                AddPopup("CHAIN ×" + scoreEvent.ChainCount, position + Vector3.up * 2.4f, new Color(1f, 0.45f, 0.15f), 32);
            }
        }

        private void OnBonus(string label, int amount, Vector3 position)
        {
            AddPopup(amount > 0 ? label + "  +$" + amount.ToString("N0") : label, position + Vector3.up * 1.4f, new Color(0.4f, 0.95f, 1f), 34);
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
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            GamePhase phase = session.Phase;
            bool arSetup = ArMode != null && ArMode.IsActive && ArMode.Stage != ArStage.Playing;
            bool results = phase == GamePhase.Results;
            bool crashView = phase == GamePhase.Crash || phase == GamePhase.SettleDelay;
            controlsActive = !arSetup && !results;

            DrawPopups();
            if (!arSetup && !results)
            {
                DrawIconRow(f);
                DrawSpeed(f);
            }

            float cursor = f.Y0 + 66f;
            if (arSetup)
            {
                DrawArSetup(f);
            }
            else if (results)
            {
                DrawResults(f);
            }
            else
            {
                if (crashView) cursor = DrawCrashScore(f);
                else DrawCornerScore(f);
                DrawStatusText(f, cursor);
                DrawBoardTools(f);
                DrawJoystick(f);
                DrawDrivingPads(f);
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

        // Top-right: small icon buttons. Hit areas are 44 pt; the drawn icons are smaller.
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

        private void DrawSpeed(Frame f)
        {
            float mph = session.Player == null ? 0f : session.Player.SpeedMetresPerSecond * 2.23694f;
            Rect rect = new Rect(f.X1 - 120f, f.Y0 + 38f, 120f, 18f);
            Shadowed(rect, Mathf.RoundToInt(mph) + " MPH", speedStyle);
            if (session.Phase == GamePhase.Driving && session.Player != null)
            {
                DrawBar(new Rect(f.X1 - 70f, f.Y0 + 58f, 70f, 3f), session.Player.BoostFraction,
                    session.Player.IsBoosting ? new Color(1f, 0.9f, 0.4f) : new Color(0.4f, 0.85f, 1f));
            }
        }

        // Top-left: junction name, live damage and medal progress, with no panel behind them.
        private void DrawCornerScore(Frame f)
        {
            CrashScenario scenario = session.Scenario;
            Shadowed(new Rect(f.X0, f.Y0 - 2f, 200f, 16f), scenario != null ? scenario.Name : "CRASH JUNCTION", smallLabel);
            Shadowed(new Rect(f.X0, f.Y0 + 12f, 200f, 30f), "$" + session.Score.ToString("N0"), scoreSmall);
            if (scenario == null) return;
            MedalProgress(scenario, out string medalText, out float fraction, out Color colour);
            DrawBar(new Rect(f.X0 + 2f, f.Y0 + 46f, 150f, 3f), fraction, colour);
            Shadowed(new Rect(f.X0, f.Y0 + 50f, 200f, 14f), medalText, microLabel);
        }

        // At the crash the score takes centre stage.
        private float DrawCrashScore(Frame f)
        {
            float centreX = (f.X0 + f.X1) * 0.5f;
            Shadowed(new Rect(f.X0, f.Y0 - 2f, f.X1 - f.X0, 18f), "LIVE DAMAGE", messageStyle);
            Shadowed(new Rect(f.X0, f.Y0 + 14f, f.X1 - f.X0, 52f), "$" + session.Score.ToString("N0"), scoreBig);
            CrashScenario scenario = session.Scenario;
            float y = f.Y0 + 68f;
            if (scenario != null)
            {
                MedalProgress(scenario, out string medalText, out float fraction, out Color colour);
                DrawBar(new Rect(centreX - 100f, y, 200f, 4f), fraction, colour);
                Shadowed(new Rect(f.X0, y + 4f, f.X1 - f.X0, 16f), medalText, new GUIStyle(microLabel) { alignment = TextAnchor.MiddleCenter });
                y += 24f;
            }
            return y;
        }

        private void MedalProgress(CrashScenario scenario, out string text, out float fraction, out Color colour)
        {
            int score = session.Score;
            int target = scenario.NextTarget(score);
            bool top = score >= scenario.Gold;
            string name = score < scenario.Bronze ? "BRONZE" : score < scenario.Silver ? "SILVER" : "GOLD";
            float previousTarget = score < scenario.Bronze ? 0f : score < scenario.Silver ? scenario.Bronze : score < scenario.Gold ? scenario.Silver : scenario.Gold;
            fraction = top || target <= previousTarget ? 1f : Mathf.Clamp01((score - previousTarget) / (target - previousTarget));
            text = top ? "GOLD MEDAL" : name + "  " + Compact(target);
            colour = name == "BRONZE" ? new Color(0.9f, 0.55f, 0.28f) : name == "SILVER" ? new Color(0.82f, 0.87f, 0.92f) : new Color(1f, 0.84f, 0.25f);
        }

        private static string Compact(int amount)
        {
            return amount >= 1000000 ? "$" + (amount / 1000000f).ToString("0.#") + "M"
                : amount >= 1000 ? "$" + (amount / 1000f).ToString("0.#") + "K"
                : "$" + amount;
        }

        private void DrawStatusText(Frame f, float y)
        {
            GamePhase phase = session.Phase;
            float width = f.X1 - f.X0;
            if (ArMode != null && ArMode.IsActive && ArMode.Stage == ArStage.Playing && !ArMode.IsTracking)
            {
                Shadowed(new Rect(f.X0, y + 6f, width, 24f), "TRACKING LOST: POINT AT THE TABLE", messageStyle);
            }
            else if (phase == GamePhase.ReadyCheck && session.Scenario != null)
            {
                Shadowed(new Rect(f.X0, f.Y0 + (f.Landscape ? 0f : 70f), width, 22f), session.Scenario.Brief.ToUpperInvariant(), new GUIStyle(messageStyle) { fontSize = 14, normal = { textColor = new Color(0.7f, 0.95f, 1f) } });
            }
            else if (phase == GamePhase.Crash || phase == GamePhase.SettleDelay)
            {
                Shadowed(new Rect(f.X0, y, width, 50f), session.ChainCount > 1 ? "CHAIN ×" + session.ChainCount : "IMPACT", bigStyle);
                if (phase == GamePhase.Crash && session.AftertouchFraction > 0.02f)
                {
                    Shadowed(new Rect(f.X0, y + 46f, width, 20f), "AFTERTOUCH: STEER THE WRECK", messageStyle);
                    DrawBar(new Rect((f.X0 + f.X1) * 0.5f - 90f, y + 70f, 180f, 5f), session.AftertouchFraction, new Color(0.4f, 0.95f, 1f));
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
            float y = f.Y0 + 70f;
            Control("move", new Rect(x, y, widthEach, 38f), "MOVE", Kind.Soft);
            Control("smaller", new Rect(x + widthEach + 6f, y, widthEach, 38f), "SIZE −", Kind.Soft);
            Control("larger", new Rect(x + 2 * (widthEach + 6f), y, widthEach, 38f), "SIZE +", Kind.Soft);
        }

        private void DrawJoystick(Frame f)
        {
            Vector2 origin;
            Vector2 knob;
            float alpha;
            if (joystickPointer != NoPointer)
            {
                origin = ToGui(joystickOrigin);
                Vector2 delta = Vector2.ClampMagnitude(ToGui(joystickCurrent) - origin, JoystickRadius);
                knob = origin + delta;
                alpha = 0.85f;
            }
            else
            {
                origin = new Vector2(f.X0 + JoystickRadius + 6f, f.Y1 - JoystickRadius - 2f);
                knob = origin;
                alpha = 0.32f;
            }

            Color previous = GUI.color;
            const float knobRadius = 24f;
            Rect ringRect = new Rect(origin.x - JoystickRadius, origin.y - JoystickRadius, JoystickRadius * 2f, JoystickRadius * 2f);
            Rect knobRect = new Rect(knob.x - knobRadius, knob.y - knobRadius, knobRadius * 2f, knobRadius * 2f);
            // A dark edge underneath keeps the stick visible over pale ground.
            GUI.color = new Color(0f, 0f, 0f, alpha * 0.45f);
            GUI.DrawTexture(new Rect(ringRect.x - 1f, ringRect.y - 1f, ringRect.width + 2f, ringRect.height + 2f), ringTexture);
            GUI.DrawTexture(new Rect(knobRect.x - 1f, knobRect.y - 1f, knobRect.width + 2f, knobRect.height + 2f), knobTexture);
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(ringRect, ringTexture);
            GUI.DrawTexture(knobRect, knobTexture);
            if (joystickPointer == NoPointer)
            {
                // Resting hint: a pair of arrows showing the stick steers left and right.
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                GUI.DrawTexture(new Rect(origin.x - JoystickRadius * 0.78f - 11f, origin.y - 11f, 22f, 22f), chevronLeft);
                GUI.DrawTexture(new Rect(origin.x + JoystickRadius * 0.78f - 11f, origin.y - 11f, 22f, 22f), chevronRight);
            }
            GUI.color = previous;
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
            Control("boost", new Rect(boostX, f.Y1 - gas - Gap - pad, pad, pad), "BOOST", Kind.Pad, true, null);
            if (f.Landscape)
            {
                Control("brake", new Rect(rightX - Gap - pad, f.Y1 - pad, pad, pad), "BRAKE", Kind.Pad, true, chevronDown);
            }
            else
            {
                Control("brake", new Rect(boostX, f.Y1 - gas - Gap - pad - Gap - pad, pad, pad), "BRAKE", Kind.Pad, true, chevronDown);
            }
        }

        // Bottom-centre in landscape, above the stick in portrait.
        private Rect CentreBottomArea(Frame f, float height)
        {
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
            Rect area = CentreBottomArea(f, 76f);
            GUI.Box(area, GUIContent.none, ghostStyle);
            Shadowed(new Rect(area.x + 10f, area.y + 5f, area.width - 20f, 16f), "LAUNCH: TAP GAS IN THE GREEN", smallLabel);

            Rect bar = new Rect(area.x + 10f, area.y + 25f, area.width - 20f, 12f);
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.22f);
            GUI.DrawTexture(bar, barTexture);
            GUI.color = new Color(0.3f, 0.95f, 0.45f, 0.95f);
            GUI.DrawTexture(new Rect(bar.x + bar.width * CrashoutGameSession.LaunchZoneMin, bar.y, bar.width * (CrashoutGameSession.LaunchZoneMax - CrashoutGameSession.LaunchZoneMin), bar.height), barTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(bar.x + bar.width * session.LaunchMeter - 2f, bar.y - 4f, 4f, bar.height + 8f), barTexture);
            GUI.color = previous;

            Control("launch", new Rect(area.x + 10f, area.y + 42f, area.width - 20f, 28f), "LAUNCH", Kind.Soft);
        }

        private void DrawCrashbreaker(Frame f)
        {
            float charge = session.CrashbreakerCharge;
            bool ready = session.CanCrashbreak;
            Rect area = CentreBottomArea(f, 76f);
            GUI.Box(area, GUIContent.none, ghostStyle);
            Shadowed(new Rect(area.x + 10f, area.y + 5f, area.width - 20f, 16f), "CRASHBREAKER", smallLabel);
            DrawBar(new Rect(area.x + 10f, area.y + 25f, area.width - 20f, 8f), charge, ready ? new Color(1f, 0.4f, 0.12f) : new Color(0.6f, 0.6f, 0.65f));
            Control("boom", new Rect(area.x + 10f, area.y + 38f, area.width - 20f, 32f), ready ? "DETONATE" : "CHARGING", Kind.Button, ready);
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
            if (message != null) Shadowed(new Rect(f.X0, f.Y0 + 8f, f.X1 - f.X0, 26f), message, messageStyle);

            float centreX = (f.X0 + f.X1) * 0.5f;
            float height = 56f;
            Control("place", new Rect(centreX - 120f, f.Y1 - height, 240f, height), "PLACE BOARD", Kind.Button);
            Control("smaller", new Rect(centreX - 120f - Gap - height, f.Y1 - height, height, height), "−", Kind.Soft);
            Control("larger", new Rect(centreX + 120f + Gap, f.Y1 - height, height, height), "+", Kind.Soft);
        }

        private void DrawResults(Frame f)
        {
            CrashScenario scenario = session.Scenario;
            CrashMedal medal = session.Medal;
            float width = Mathf.Min(f.X1 - f.X0, 540f);
            float height = Mathf.Min(f.Y1 - f.Y0, 312f);
            Rect box = new Rect((f.X0 + f.X1) * 0.5f - width * 0.5f, (f.Y0 + f.Y1) * 0.5f - height * 0.5f, width, height);
            GUI.Box(box, GUIContent.none, panelStyle);
            GUI.Label(new Rect(box.x, box.y + 12f, box.width, 22f), scenario != null ? scenario.Name : "RUN COMPLETE", messageStyle);
            string medalText = medal == CrashMedal.None ? "NO MEDAL" : medal.ToString().ToUpperInvariant() + " MEDAL";
            Color medalColour = medal == CrashMedal.Gold ? new Color(1f, 0.84f, 0.25f) : medal == CrashMedal.Silver ? new Color(0.82f, 0.87f, 0.92f) : medal == CrashMedal.Bronze ? new Color(0.9f, 0.55f, 0.28f) : new Color(0.65f, 0.65f, 0.7f);
            Color previous = valueStyle.normal.textColor;
            valueStyle.normal.textColor = medalColour;
            GUI.Label(new Rect(box.x, box.y + 36f, box.width, 36f), medalText, valueStyle);
            valueStyle.normal.textColor = previous;
            GUI.Label(new Rect(box.x, box.y + 70f, box.width, 40f), "$" + session.Score.ToString("N0"), valueStyle);

            float columnWidth = (box.width - 48f) * 0.5f;
            float rowY = box.y + 118f;
            DrawStat(box.x + 24f, rowY, columnWidth, "CARS WRECKED", session.WreckedCount.ToString());
            DrawStat(box.x + 24f + columnWidth, rowY, columnWidth, "BEST CHAIN", "×" + session.BestChain);
            DrawStat(box.x + 24f, rowY + 28f, columnWidth, "CRASHBREAKERS", session.CrashbreakersUsed.ToString());
            DrawStat(box.x + 24f + columnWidth, rowY + 28f, columnWidth, "BONUSES", "$" + session.BonusScore.ToString("N0"));
            if (scenario != null)
            {
                DrawStat(box.x + 24f, rowY + 56f, box.width - 48f, "TARGET CAR", session.TargetDown ? "DOWN  +$" + scenario.TargetBonus.ToString("N0") : "MISSED");
            }

            float buttonWidth = (box.width - 24f * 2f - Gap) * 0.5f;
            float buttonY = box.yMax - 24f - 50f;
            Control("retry", new Rect(box.x + 24f, buttonY, buttonWidth, 50f), "RETRY", Kind.Button);
            Control("next", new Rect(box.x + 24f + buttonWidth + Gap, buttonY, buttonWidth, 50f), "NEXT JUNCTION", Kind.Button);
        }

        private void DrawStat(float x, float y, float width, string label, string value)
        {
            GUI.Label(new Rect(x, y + 3f, width * 0.6f, 22f), label, statLabelStyle);
            GUI.Label(new Rect(x, y, width - 8f, 26f), value, statValueStyle);
        }

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
                float rise = age * 46f;
                float alpha = Mathf.Clamp01(1.6f - age) * Mathf.Clamp01(age * 8f);
                Rect rect = new Rect(screen.x / scale - 180f, (Screen.height - screen.y) / scale - rise - 22f, 360f, 48f);
                popupStyle.fontSize = popup.Size;
                popupStyle.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.8f);
                GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), popup.Text, popupStyle);
                popupStyle.normal.textColor = new Color(popup.Colour.r, popup.Colour.g, popup.Colour.b, alpha);
                GUI.Label(rect, popup.Text, popupStyle);
            }
        }

        // Text with a soft shadow, so it reads over the board without a panel behind it.
        private void Shadowed(Rect rect, string text, GUIStyle style)
        {
            Color original = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.8f * original.a);
            GUI.Label(new Rect(rect.x + 1.2f, rect.y + 1.2f, rect.width, rect.height), text, style);
            style.normal.textColor = original;
            GUI.Label(rect, text, style);
        }

        // Draws a control and registers its hit area (in screen pixels) for the next frame.
        private void Control(string id, Rect rect, string text, Kind kind, bool enabled = true, Texture2D glyph = null)
        {
            bool isPressed = enabled && pressed.Contains(id);
            Color previous = GUI.color;
            GUI.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            switch (kind)
            {
                case Kind.Icon:
                {
                    // A faint disc behind a smaller icon keeps the 44 pt target without a heavy button.
                    float disc = 34f;
                    Rect discRect = new Rect(rect.center.x - disc * 0.5f, rect.center.y - disc * 0.5f, disc, disc);
                    GUI.Box(discRect, GUIContent.none, isPressed ? iconBackHotStyle : iconBackStyle);
                    float size = 20f;
                    Rect iconRect = new Rect(rect.center.x - size * 0.5f, rect.center.y - size * 0.5f, size, size);
                    GUI.color = new Color(0f, 0f, 0f, 0.6f);
                    GUI.DrawTexture(new Rect(iconRect.x + 1f, iconRect.y + 1f, size, size), glyph);
                    GUI.color = Color.white;
                    GUI.DrawTexture(iconRect, glyph);
                    break;
                }
                case Kind.Pad:
                {
                    GUI.Box(rect, GUIContent.none, isPressed ? padHotStyle : padStyle);
                    bool hasText = text.Length > 0;
                    if (glyph != null)
                    {
                        float glyphSize = rect.width * 0.5f;
                        float glyphY = hasText ? rect.y + rect.height * 0.12f : rect.center.y - glyphSize * 0.5f;
                        GUI.color = new Color(1f, 1f, 1f, enabled ? 0.8f : 0.4f);
                        GUI.DrawTexture(new Rect(rect.center.x - glyphSize * 0.5f, glyphY, glyphSize, glyphSize), glyph);
                    }
                    if (hasText)
                    {
                        Rect labelRect = glyph != null ? new Rect(rect.x, rect.yMax - 30f, rect.width, 24f) : rect;
                        Shadowed(labelRect, text, padText);
                    }
                    break;
                }
                case Kind.Soft:
                    GUI.Box(rect, GUIContent.none, isPressed ? softHotStyle : softStyle);
                    Shadowed(rect, text, softText);
                    break;
                default:
                    GUI.Box(rect, GUIContent.none, isPressed ? buttonHotStyle : buttonStyle);
                    GUI.Label(rect, text, buttonText);
                    break;
            }
            GUI.color = previous;

            if (enabled)
            {
                nextRects[id] = new Rect(rect.x * scale, Screen.height - rect.yMax * scale, rect.width * scale, rect.height * scale);
            }
        }

        private void DrawBar(Rect rect, float fraction, Color colour)
        {
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.2f);
            GUI.DrawTexture(rect, barTexture);
            GUI.color = colour;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), barTexture);
            GUI.color = previous;
        }

        // ------------------------------------------------------------------ styles and textures

        private void EnsureStyles()
        {
            if (panelStyle != null) return;
            panelTexture = MakeRounded(64, 16, new Color(0.04f, 0.05f, 0.09f, 0.82f), new Color(1f, 1f, 1f, 0.2f));
            ghostTexture = MakeRounded(64, 16, new Color(0.02f, 0.03f, 0.06f, 0.3f), new Color(1f, 1f, 1f, 0.14f));
            softTexture = MakeRounded(64, 16, new Color(1f, 1f, 1f, 0.12f), new Color(1f, 1f, 1f, 0.38f));
            softHotTexture = MakeRounded(64, 16, new Color(1f, 0.55f, 0.2f, 0.6f), new Color(1f, 1f, 1f, 0.9f));
            buttonTexture = MakeRounded(64, 16, new Color(1f, 0.4f, 0.1f, 0.82f), new Color(1f, 0.75f, 0.5f, 0.7f));
            buttonHotTexture = MakeRounded(64, 16, new Color(1f, 0.62f, 0.25f, 1f), Color.white);
            padTexture = MakeRounded(96, 30, new Color(0.02f, 0.03f, 0.06f, 0.2f), new Color(1f, 1f, 1f, 0.34f));
            padHotTexture = MakeRounded(96, 30, new Color(1f, 0.45f, 0.12f, 0.6f), new Color(1f, 1f, 1f, 0.9f));
            ringTexture = MakeRounded(128, 64, new Color(1f, 1f, 1f, 0.06f), new Color(1f, 1f, 1f, 0.85f));
            knobTexture = MakeRounded(64, 32, new Color(1f, 1f, 1f, 0.5f), new Color(1f, 1f, 1f, 0.95f));
            iconBackTexture = MakeRounded(64, 32, new Color(0f, 0f, 0f, 0.22f), new Color(1f, 1f, 1f, 0.22f));
            iconBackHotTexture = MakeRounded(64, 32, new Color(1f, 0.5f, 0.15f, 0.7f), new Color(1f, 1f, 1f, 0.9f));
            barTexture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            barTexture.SetPixel(0, 0, Color.white);
            barTexture.Apply();
            chevronUp = MakeChevron(64, 0);
            chevronRight = MakeChevron(64, 1);
            chevronDown = MakeChevron(64, 2);
            chevronLeft = MakeChevron(64, 3);
            foreach (Icon icon in new[] { Icon.Retry, Icon.SoundOn, Icon.SoundOff, Icon.Camera, Icon.Board, Icon.Check })
            {
                icons[icon] = MakeIcon(icon);
            }

            GUIStyle baseLabel = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
            panelStyle = BoxStyle(panelTexture, 16);
            ghostStyle = BoxStyle(ghostTexture, 16);
            softStyle = BoxStyle(softTexture, 16);
            softHotStyle = BoxStyle(softHotTexture, 16);
            buttonStyle = BoxStyle(buttonTexture, 16);
            buttonHotStyle = BoxStyle(buttonHotTexture, 16);
            padStyle = BoxStyle(padTexture, 30);
            padHotStyle = BoxStyle(padHotTexture, 30);
            iconBackStyle = BoxStyle(iconBackTexture, 16);
            iconBackHotStyle = BoxStyle(iconBackHotTexture, 16);

            Color cyan = new Color(0.6f, 0.93f, 0.97f);
            smallLabel = new GUIStyle(baseLabel) { fontSize = 12, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(cyan.r, cyan.g, cyan.b, 0.9f) } };
            microLabel = new GUIStyle(baseLabel) { fontSize = 11, alignment = TextAnchor.MiddleLeft, normal = { textColor = new Color(1f, 1f, 1f, 0.78f) } };
            scoreSmall = new GUIStyle(baseLabel) { fontSize = 24, alignment = TextAnchor.MiddleLeft, normal = { textColor = Color.white } };
            scoreBig = new GUIStyle(baseLabel) { fontSize = 46, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            speedStyle = new GUIStyle(baseLabel) { fontSize = 14, alignment = TextAnchor.MiddleRight, normal = { textColor = new Color(1f, 1f, 1f, 0.9f) } };
            messageStyle = new GUIStyle(baseLabel) { fontSize = 15, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.64f, 0.28f) } };
            bigStyle = new GUIStyle(baseLabel) { fontSize = 42, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.55f, 0.15f) } };
            popupStyle = new GUIStyle(baseLabel) { alignment = TextAnchor.MiddleCenter };
            buttonText = new GUIStyle(baseLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            softText = new GUIStyle(baseLabel) { fontSize = 15, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            padText = new GUIStyle(baseLabel) { fontSize = 13, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 1f, 1f, 0.85f) } };
            statLabelStyle = new GUIStyle(baseLabel) { fontSize = 13, alignment = TextAnchor.MiddleLeft, normal = { textColor = cyan } };
            statValueStyle = new GUIStyle(baseLabel) { fontSize = 18, alignment = TextAnchor.MiddleRight, normal = { textColor = Color.white } };
            valueStyle = new GUIStyle(baseLabel) { fontSize = 34, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        }

        private static GUIStyle BoxStyle(Texture2D texture, int border)
        {
            return new GUIStyle(GUI.skin.box) { normal = { background = texture }, border = new RectOffset(border, border, border, border) };
        }

        private static Texture2D MakeRounded(int size, int radius, Color fill, Color outline)
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
                    Color colour = Color.Lerp(fill, outline, edge * outline.a);
                    colour.a = Mathf.Max(fill.a, edge * outline.a) * coverage;
                    pixels[y * size + x] = colour;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
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
