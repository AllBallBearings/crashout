using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>Compact playback dock in HUD points, inside the device's safe area.</summary>
    public readonly struct ReplayControlsLayout
    {
        public const float SpeedWidth = 44f;
        public readonly Rect Dock, Seek, Track, Restart, Play;
        public readonly Vector2 SpeedOrigin;

        public ReplayControlsLayout(Rect safeArea, bool landscape)
        {
            float height = landscape ? 68f : 100f;
            Dock = new Rect(safeArea.x, safeArea.yMax - height, safeArea.width, height);
            float rowY = Dock.y + (landscape ? 12f : 48f);
            Play = new Rect(Dock.xMax - 56f, rowY - 2f, 48f, 48f);
            Restart = new Rect(Play.x - 48f, rowY, 44f, 44f);
            SpeedOrigin = new Vector2(landscape ? Restart.x - 8f - SpeedWidth * 4f : Dock.x + 8f, rowY);
            float seekRight = landscape ? SpeedOrigin.x - 12f : Dock.xMax - 12f;
            Seek = new Rect(Dock.x + 12f, Dock.y + (landscape ? 12f : 2f), seekRight - Dock.x - 12f, 44f);
            Track = new Rect(Seek.x, Seek.y + 30f, Seek.width, 3f);
        }

        public Rect SpeedOption(int index)
        {
            return new Rect(SpeedOrigin.x + index * SpeedWidth, SpeedOrigin.y, SpeedWidth, 44f);
        }
    }
}
