using System.Collections.Generic;
using Crashout.Simulation;
using UnityEngine;

namespace Crashout.Presentation
{
    public sealed class CrashoutHud : MonoBehaviour
    {
        private const float Margin = 22f;
        private CrashoutGameSession session;
        private CrashoutCameraRig cameraRig;
        private GUIStyle titleStyle;
        private GUIStyle labelStyle;
        private GUIStyle scoreStyle;
        private GUIStyle centerStyle;
        private GUIStyle buttonStyle;
        private GUIStyle touchStyle;
        private GUIStyle boostStyle;
        private GUIStyle bigStyle;
        private Texture2D panelTexture;
        private Texture2D buttonTexture;
        private Texture2D buttonHotTexture;
        private Texture2D touchTexture;
        private Texture2D touchHotTexture;

        private struct Popup
        {
            public string Text;
            public Vector3 World;
            public float Born;
            public Color Colour;
            public int Size;
        }

        private readonly List<Popup> popups = new List<Popup>();
        private GUIStyle popupStyle;
        private Texture2D barTexture;

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

        private void OnScore(ScoreEvent scoreEvent)
        {
            if (scoreEvent.Amount <= 0 || !session.TryGetVehiclePosition(scoreEvent.EntityId, out Vector3 position)) return;
            AddPopup("+$" + scoreEvent.Amount.ToString("N0"), position + Vector3.up * 1.2f,
                Color.Lerp(Color.white, new Color(1f, 0.75f, 0.2f), Mathf.Clamp01(scoreEvent.Amount / 4000f)),
                Mathf.Clamp(18 + scoreEvent.Amount / 220, 18, 40));
            if (scoreEvent.ChainCount >= 3 && scoreEvent.ChainCount % 2 == 1)
            {
                AddPopup("CHAIN ×" + scoreEvent.ChainCount, position + Vector3.up * 2.4f, new Color(1f, 0.4f, 0.15f), 30);
            }
        }

        private void OnBonus(string label, int amount, Vector3 position)
        {
            AddPopup(label + "  +$" + amount.ToString("N0"), position + Vector3.up * 1.4f, new Color(0.4f, 0.95f, 1f), 38);
        }

        private void AddPopup(string text, Vector3 world, Color colour, int size)
        {
            if (popups.Count > 14) popups.RemoveAt(0);
            popups.Add(new Popup { Text = text, World = world, Born = Time.unscaledTime, Colour = colour, Size = size });
        }

        private void OnDestroy()
        {
            if (session != null)
            {
                session.ScoreChanged -= OnScore;
                session.BonusAwarded -= OnBonus;
            }
            foreach (Texture2D texture in new[] { panelTexture, buttonTexture, buttonHotTexture, touchTexture, touchHotTexture, barTexture })
            {
                if (texture != null) Destroy(texture);
            }
        }

        private void OnGUI()
        {
            if (session == null || cameraRig == null) return;
            EnsureStyles();
            float scale = Mathf.Clamp(Screen.height / 860f, 0.72f, 1.35f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            GUI.Box(new Rect(Margin, Margin, 190f, 70f), GUIContent.none, titleStyle);
            GUI.Label(new Rect(Margin + 16f, Margin + 11f, 165f, 25f), "CRASHOUT", scoreStyle);
            GUI.Label(new Rect(Margin + 17f, Margin + 42f, 165f, 18f), session.Scenario != null ? session.Scenario.Name : "CRASH JUNCTION", labelStyle);

            GUI.Box(new Rect(width * 0.5f - 120f, Margin, 240f, 70f), GUIContent.none, titleStyle);
            GUI.Label(new Rect(width * 0.5f - 110f, Margin + 9f, 220f, 18f), "LIVE DAMAGE", centerStyle);
            GUI.Label(new Rect(width * 0.5f - 110f, Margin + 29f, 220f, 32f), "$" + session.Score.ToString("N0"), scoreStyle);

            float speedMph = session.Player == null ? 0f : session.Player.SpeedMetresPerSecond * 2.23694f;
            GUI.Box(new Rect(width - 164f, Margin, 142f, 70f), GUIContent.none, titleStyle);
            GUI.Label(new Rect(width - 152f, Margin + 9f, 118f, 18f), "SPEED  /  MPH", labelStyle);
            GUI.Label(new Rect(width - 152f, Margin + 29f, 118f, 32f), Mathf.RoundToInt(speedMph).ToString("000"), scoreStyle);

            DrawMedalProgress(width);
            DrawBoostBar(width);
            DrawPopups(scale);

            if (ArMode != null && ArMode.IsActive)
            {
                DrawArControls(width, height);
            }
            else
            {
                if (GUI.Button(new Rect(width - 164f, 102f, 142f, 38f), cameraRig.Mode.ToString().ToUpperInvariant() + "  [C]", buttonStyle))
                {
                    cameraRig.CycleMode();
                }
                if (GUI.Button(new Rect(width - 164f, 146f, 142f, 38f), "RESET  [R]", buttonStyle))
                {
                    session.ResetTurn();
                }
            }

            if (session.Phase == GamePhase.Driving && session.Scenario != null && (ArMode == null || ArMode.Stage == ArStage.Playing || !ArMode.IsActive))
            {
                GUI.Label(new Rect(width * 0.5f - 260f, Margin + 78f, 520f, 24f), session.Scenario.Brief.ToUpperInvariant(), labelStyle);
            }
            else if (session.Phase == GamePhase.Crash || session.Phase == GamePhase.SettleDelay)
            {
                string impact = session.ChainCount > 1 ? "CHAIN ×" + session.ChainCount : "IMPACT";
                GUI.Label(new Rect(width * 0.5f - 200f, height * 0.2f - 6f, 400f, 50f), impact, bigStyle);
                if (session.Phase == GamePhase.Crash && session.AftertouchFraction > 0.02f)
                {
                    GUI.Label(new Rect(width * 0.5f - 200f, height * 0.2f + 36f, 400f, 20f), "AFTERTOUCH: STEER THE WRECK", labelStyle);
                    DrawBar(new Rect(width * 0.5f - 80f, height * 0.2f + 60f, 160f, 8f), session.AftertouchFraction, new Color(0.4f, 0.95f, 1f));
                }
                DrawCrashbreaker(width, height);
            }
            else if (session.Phase == GamePhase.Results)
            {
                DrawResults(width, height);
            }

            if (ArMode == null || !ArMode.IsActive || ArMode.Stage == ArStage.Playing) DrawTouchControls(width, height);
            GUI.matrix = previous;
        }

        private void DrawMedalProgress(float width)
        {
            CrashScenario scenario = session.Scenario;
            if (scenario == null) return;
            int target = scenario.NextTarget(session.Score);
            string medal = session.Score < scenario.Bronze ? "BRONZE" : session.Score < scenario.Silver ? "SILVER" : session.Score < scenario.Gold ? "GOLD" : "GOLD ★";
            float previousTarget = session.Score < scenario.Bronze ? 0f : session.Score < scenario.Silver ? scenario.Bronze : session.Score < scenario.Gold ? scenario.Silver : scenario.Gold;
            float fraction = target <= previousTarget ? 1f : Mathf.Clamp01((session.Score - previousTarget) / (target - previousTarget));
            float x = width * 0.5f - 120f;
            DrawBar(new Rect(x + 8f, Margin + 62f, 224f, 5f), fraction, MedalColour(medal));
            GUI.Label(new Rect(x, Margin + 66f, 240f, 14f), "", labelStyle);
        }

        private static Color MedalColour(string medal)
        {
            return medal.StartsWith("BRONZE") ? new Color(0.86f, 0.52f, 0.25f)
                : medal.StartsWith("SILVER") ? new Color(0.8f, 0.85f, 0.9f)
                : new Color(1f, 0.84f, 0.25f);
        }

        private void DrawBoostBar(float width)
        {
            if (session.Player == null || session.Phase != GamePhase.Driving) return;
            GUI.Label(new Rect(width - 152f, Margin + 72f, 118f, 14f), "BOOST", labelStyle);
            DrawBar(new Rect(width - 152f, Margin + 88f, 118f, 6f), session.Player.BoostFraction,
                session.Player.IsBoosting ? new Color(1f, 0.9f, 0.4f) : new Color(0.3f, 0.85f, 1f));
        }

        private void DrawCrashbreaker(float width, float height)
        {
            float charge = session.CrashbreakerCharge;
            bool ready = session.CanCrashbreak;
            Rect area = new Rect(width * 0.5f - 150f, height - 112f, 300f, 76f);
            GUI.Box(area, GUIContent.none, titleStyle);
            GUI.Label(new Rect(area.x, area.y + 6f, area.width, 16f), "CRASHBREAKER  [SPACE]", labelStyle);
            DrawBar(new Rect(area.x + 16f, area.y + 26f, area.width - 32f, 10f), charge, ready ? new Color(1f, 0.35f, 0.1f) : new Color(0.55f, 0.55f, 0.6f));
            GUI.enabled = ready;
            if (GUI.Button(new Rect(area.x + 16f, area.y + 42f, area.width - 32f, 26f), ready ? "DETONATE" : "CHARGING", buttonStyle))
            {
                session.TryCrashbreaker();
            }
            GUI.enabled = true;
        }

        private void DrawResults(float width, float height)
        {
            CrashScenario scenario = session.Scenario;
            CrashMedal medal = session.Medal;
            Rect box = new Rect(width * 0.5f - 230f, height * 0.5f - 170f, 460f, 340f);
            GUI.Box(box, GUIContent.none, titleStyle);
            GUI.Label(new Rect(box.x, box.y + 14f, box.width, 24f), scenario != null ? scenario.Name : "RUN COMPLETE", centerStyle);
            string medalText = medal == CrashMedal.None ? "NO MEDAL" : medal.ToString().ToUpperInvariant() + " MEDAL";
            Color previous = scoreStyle.normal.textColor;
            scoreStyle.normal.textColor = medal == CrashMedal.Gold ? new Color(1f, 0.84f, 0.25f) : medal == CrashMedal.Silver ? new Color(0.82f, 0.87f, 0.92f) : medal == CrashMedal.Bronze ? new Color(0.9f, 0.55f, 0.28f) : new Color(0.65f, 0.65f, 0.7f);
            GUI.Label(new Rect(box.x, box.y + 40f, box.width, 36f), medalText, scoreStyle);
            scoreStyle.normal.textColor = previous;
            GUI.Label(new Rect(box.x, box.y + 78f, box.width, 40f), "$" + session.Score.ToString("N0"), scoreStyle);

            float rowY = box.y + 130f;
            DrawStat(box.x + 40f, rowY, "CARS WRECKED", session.WreckedCount.ToString());
            DrawStat(box.x + 40f, rowY + 26f, "BEST CHAIN", "×" + session.BestChain);
            DrawStat(box.x + 40f, rowY + 52f, "CRASHBREAKERS", session.CrashbreakersUsed.ToString());
            DrawStat(box.x + 40f, rowY + 78f, "BONUSES", "$" + session.BonusScore.ToString("N0"));
            if (scenario != null)
            {
                DrawStat(box.x + 40f, rowY + 104f, "TARGET", session.TargetDown ? "DOWN  +$" + scenario.TargetBonus.ToString("N0") : "MISSED");
            }

            if (GUI.Button(new Rect(box.x + 24f, box.yMax - 62f, 196f, 44f), "RETRY", buttonStyle)) session.ResetTurn();
            if (GUI.Button(new Rect(box.xMax - 220f, box.yMax - 62f, 196f, 44f), "NEXT JUNCTION  [N]", buttonStyle)) session.RequestScenarioChange(1);
        }

        private void DrawStat(float x, float y, string label, string value)
        {
            GUI.Label(new Rect(x, y, 200f, 22f), label, labelStyle);
            GUI.Label(new Rect(x + 150f, y - 4f, 230f, 26f), value, new GUIStyle(labelStyle) { fontSize = 16, alignment = TextAnchor.MiddleRight, normal = { textColor = Color.white } });
        }

        private void DrawBar(Rect rect, float fraction, Color colour)
        {
            if (barTexture == null)
            {
                barTexture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                barTexture.SetPixel(0, 0, Color.white);
                barTexture.Apply();
            }
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.18f);
            GUI.DrawTexture(rect, barTexture);
            GUI.color = colour;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), barTexture);
            GUI.color = previous;
        }

        private void DrawPopups(float scale)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            if (popupStyle == null)
            {
                popupStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
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
                Rect rect = new Rect(screen.x / scale - 160f, (Screen.height - screen.y) / scale - rise - 20f, 320f, 44f);
                popupStyle.fontSize = popup.Size;
                popupStyle.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.8f);
                GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), popup.Text, popupStyle);
                popupStyle.normal.textColor = new Color(popup.Colour.r, popup.Colour.g, popup.Colour.b, alpha);
                GUI.Label(rect, popup.Text, popupStyle);
            }
        }

        private void DrawArControls(float width, float height)
        {
            string message = null;
            switch (ArMode.Stage)
            {
                case ArStage.Starting:
                    message = "STARTING AR…";
                    break;
                case ArStage.Searching:
                    message = "MOVE YOUR PHONE SLOWLY TO FIND A FLAT TABLE";
                    break;
                case ArStage.ReadyToPlace:
                    message = "BOARD IS " + Mathf.RoundToInt(ArMode.BoardWidthMetres * 100f) + " CM WIDE";
                    break;
                case ArStage.Playing:
                    if (!ArMode.IsTracking) message = "TRACKING LOST — POINT AT THE TABLE";
                    break;
            }

            if (message != null)
            {
                GUI.Box(new Rect(width * 0.5f - 240f, height * 0.18f, 480f, 44f), GUIContent.none, titleStyle);
                GUI.Label(new Rect(width * 0.5f - 240f, height * 0.18f, 480f, 44f), message, centerStyle);
            }

            if (ArMode.Stage == ArStage.Searching || ArMode.Stage == ArStage.ReadyToPlace)
            {
                if (GUI.Button(new Rect(width * 0.5f - 110f, height - 120f, 220f, 56f), "PLACE BOARD", buttonStyle)) ArMode.PlaceBoard();
                if (GUI.Button(new Rect(width * 0.5f - 200f, height - 120f, 76f, 56f), "−", buttonStyle)) ArMode.ScaleBoard(1f / 1.12f);
                if (GUI.Button(new Rect(width * 0.5f + 124f, height - 120f, 76f, 56f), "+", buttonStyle)) ArMode.ScaleBoard(1.12f);
            }
            else if (ArMode.Stage == ArStage.Playing)
            {
                if (GUI.Button(new Rect(width - 164f, 102f, 142f, 38f), "MOVE BOARD", buttonStyle)) ArMode.Relocate();
                if (GUI.Button(new Rect(width - 164f, 146f, 142f, 38f), "RESET  [R]", buttonStyle)) session.ResetTurn();
                if (GUI.Button(new Rect(width - 164f, 190f, 68f, 38f), "−", buttonStyle)) ArMode.ScaleBoard(1f / 1.12f);
                if (GUI.Button(new Rect(width - 90f, 190f, 68f, 38f), "+", buttonStyle)) ArMode.ScaleBoard(1.12f);
            }
        }

        private void DrawTouchControls(float width, float height)
        {
            float steer = 0f;
            bool left = GUI.RepeatButton(new Rect(Margin, height - 104f, 76f, 76f), "<", touchStyle);
            bool right = GUI.RepeatButton(new Rect(Margin + 84f, height - 104f, 76f, 76f), ">", touchStyle);
            if (left) steer -= 1f;
            if (right) steer += 1f;
            bool boost = GUI.RepeatButton(new Rect(width - 190f, height - 146f, 76f, 76f), "BOOST", boostStyle);
            session.LocalInput.SetTouchBoost(boost);
            bool throttle = GUI.RepeatButton(new Rect(width - 100f, height - 188f, 76f, 76f), "^", touchStyle);
            bool brake = GUI.RepeatButton(new Rect(width - 100f, height - 104f, 76f, 76f), "v", touchStyle);
            session.LocalInput.SetTouchState(steer, throttle, brake);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            panelTexture = MakeRounded(64, 16, new Color(0.05f, 0.06f, 0.1f, 0.72f), new Color(1f, 1f, 1f, 0.16f));
            buttonTexture = MakeRounded(64, 16, new Color(1f, 0.38f, 0.1f, 0.95f), new Color(1f, 0.72f, 0.45f, 0.9f));
            buttonHotTexture = MakeRounded(64, 16, new Color(1f, 0.55f, 0.2f, 1f), Color.white);
            touchTexture = MakeRounded(64, 32, new Color(0.05f, 0.06f, 0.1f, 0.55f), new Color(1f, 1f, 1f, 0.45f));
            touchHotTexture = MakeRounded(64, 32, new Color(1f, 0.4f, 0.1f, 0.9f), Color.white);

            titleStyle = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture }, border = new RectOffset(16, 16, 16, 16) };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.55f, 0.92f, 0.95f) } };
            scoreStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            centerStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 0.6f, 0.2f) } };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(16, 16, 16, 16),
                normal = { background = buttonTexture, textColor = Color.white },
                hover = { background = buttonHotTexture, textColor = Color.white },
                active = { background = buttonHotTexture, textColor = Color.white },
                focused = { background = buttonTexture, textColor = Color.white }
            };
            touchStyle = new GUIStyle(buttonStyle)
            {
                fontSize = 30,
                border = new RectOffset(30, 30, 30, 30),
                normal = { background = touchTexture, textColor = Color.white },
                hover = { background = touchTexture, textColor = Color.white },
                active = { background = touchHotTexture, textColor = Color.white },
                focused = { background = touchTexture, textColor = Color.white }
            };
            boostStyle = new GUIStyle(touchStyle) { fontSize = 13 };
            bigStyle = new GUIStyle(centerStyle) { fontSize = 34, normal = { textColor = new Color(1f, 0.55f, 0.15f) } };
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
    }
}
