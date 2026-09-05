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
        private Texture2D darkTexture;
        private Texture2D orangeTexture;

        public void Configure(CrashoutGameSession gameSession, CrashoutCameraRig rig)
        {
            session = gameSession;
            cameraRig = rig;
        }

        private void OnDestroy()
        {
            if (darkTexture != null) Destroy(darkTexture);
            if (orangeTexture != null) Destroy(orangeTexture);
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
            GUI.Label(new Rect(Margin + 17f, Margin + 42f, 165f, 18f), "JUNCTION 01  /  HOST", labelStyle);

            GUI.Box(new Rect(width * 0.5f - 120f, Margin, 240f, 70f), GUIContent.none, titleStyle);
            GUI.Label(new Rect(width * 0.5f - 110f, Margin + 9f, 220f, 18f), "LIVE DAMAGE", centerStyle);
            GUI.Label(new Rect(width * 0.5f - 110f, Margin + 29f, 220f, 32f), "$" + session.Score.ToString("N0"), scoreStyle);

            float speedMph = session.Player == null ? 0f : session.Player.SpeedMetresPerSecond * 2.23694f;
            GUI.Box(new Rect(width - 164f, Margin, 142f, 70f), GUIContent.none, titleStyle);
            GUI.Label(new Rect(width - 152f, Margin + 9f, 118f, 18f), "SPEED  /  MPH", labelStyle);
            GUI.Label(new Rect(width - 152f, Margin + 29f, 118f, 32f), Mathf.RoundToInt(speedMph).ToString("000"), scoreStyle);

            if (GUI.Button(new Rect(width - 164f, 102f, 142f, 38f), cameraRig.Mode.ToString().ToUpperInvariant() + "  [C]"))
            {
                cameraRig.CycleMode();
            }
            if (GUI.Button(new Rect(width - 164f, 146f, 142f, 38f), "RESET  [R]"))
            {
                session.ResetTurn();
            }

            if (session.Phase == GamePhase.Crash || session.Phase == GamePhase.SettleDelay)
            {
                string impact = session.ChainCount > 1 ? "CHAIN ×" + session.ChainCount : "IMPACT";
                GUI.Label(new Rect(width * 0.5f - 200f, height * 0.26f, 400f, 70f), impact, centerStyle);
            }
            else if (session.Phase == GamePhase.Results)
            {
                GUI.Box(new Rect(width * 0.5f - 190f, height * 0.5f - 110f, 380f, 220f), GUIContent.none, titleStyle);
                GUI.Label(new Rect(width * 0.5f - 170f, height * 0.5f - 80f, 340f, 28f), "RUN COMPLETE", centerStyle);
                GUI.Label(new Rect(width * 0.5f - 170f, height * 0.5f - 35f, 340f, 45f), "$" + session.Score.ToString("N0"), scoreStyle);
                if (GUI.Button(new Rect(width * 0.5f - 130f, height * 0.5f + 38f, 260f, 48f), "RUN IT AGAIN"))
                {
                    session.ResetTurn();
                }
            }

            DrawTouchControls(width, height);
            GUI.matrix = previous;
        }

        private void DrawTouchControls(float width, float height)
        {
            float steer = 0f;
            bool left = GUI.RepeatButton(new Rect(Margin, height - 104f, 76f, 76f), "◀");
            bool right = GUI.RepeatButton(new Rect(Margin + 84f, height - 104f, 76f, 76f), "▶");
            if (left) steer -= 1f;
            if (right) steer += 1f;
            bool throttle = GUI.RepeatButton(new Rect(width - 100f, height - 188f, 76f, 76f), "▲");
            bool brake = GUI.RepeatButton(new Rect(width - 100f, height - 104f, 76f, 76f), "▼");
            session.LocalInput.SetTouchState(steer, throttle, brake);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            darkTexture = MakeTexture(new Color(0.035f, 0.04f, 0.035f, 0.88f));
            orangeTexture = MakeTexture(new Color(0.96f, 0.27f, 0.08f, 1f));
            titleStyle = new GUIStyle(GUI.skin.box) { normal = { background = darkTexture }, border = new RectOffset(1, 1, 1, 1) };
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.72f, 0.9f, 0.35f) } };
            scoreStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            centerStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.98f, 0.34f, 0.12f) } };
            GUI.skin.button.fontStyle = FontStyle.Bold;
            GUI.skin.button.normal.background = orangeTexture;
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
