using Crashout.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Crashout.Presentation
{
    /// <summary>
    /// Directs the post-processing stack like a camera operator: speed blur and lens squeeze on boost,
    /// a tightening vignette as the junction approaches, a bleach-bypass slow-motion grade with shallow
    /// focus during impact time, and exposure / aberration punches on hits and explosions.
    /// </summary>
    public sealed class CinematicFx : MonoBehaviour
    {
        private CrashoutGameSession session;
        private CrashoutCameraRig rig;
        private bool tabletop;

        private ColorAdjustments adjustments;
        private Vignette vignette;
        private ChromaticAberration chromatic;
        private MotionBlur motionBlur;
        private LensDistortion lens;
        private DepthOfField depthOfField;
        private Bloom bloom;
        private WhiteBalance whiteBalance;

        private float baseExposure, baseSaturation, baseContrast, baseVignette, baseChromatic, baseBloom, baseTemperature;
        private float punch;
        private float flash;
        private float slowMotion;

        public void Configure(CrashoutGameSession gameSession, CrashoutCameraRig cameraRig, VolumeProfile profile, bool isTabletop)
        {
            session = gameSession;
            rig = cameraRig;
            tabletop = isTabletop;
            profile.TryGet(out adjustments);
            profile.TryGet(out vignette);
            profile.TryGet(out chromatic);
            profile.TryGet(out motionBlur);
            profile.TryGet(out lens);
            profile.TryGet(out depthOfField);
            profile.TryGet(out bloom);
            profile.TryGet(out whiteBalance);
            if (adjustments != null)
            {
                baseExposure = adjustments.postExposure.value;
                baseSaturation = adjustments.saturation.value;
                baseContrast = adjustments.contrast.value;
            }
            if (vignette != null) baseVignette = vignette.intensity.value;
            if (chromatic != null) baseChromatic = chromatic.intensity.value;
            if (bloom != null) baseBloom = bloom.intensity.value;
            if (whiteBalance != null) baseTemperature = whiteBalance.temperature.value;
        }

        /// <summary>A hit: brief exposure punch and lens fringing scaled by severity 0..1.</summary>
        public void Impact(float severity)
        {
            punch = Mathf.Max(punch, Mathf.Clamp01(severity));
        }

        public void Explosion(float size)
        {
            flash = Mathf.Max(flash, 0.6f + 0.4f * Mathf.Clamp01(size));
            punch = 1f;
        }

        private void LateUpdate()
        {
            if (session == null) return;
            float dt = Time.unscaledDeltaTime;
            GamePhase phase = session.Phase;
            float speed = session.Player != null ? session.Player.SpeedMetresPerSecond : 0f;
            bool boosting = session.Player != null && session.Player.IsBoosting && phase == GamePhase.Driving;
            bool impact = session.IsImpactTime;
            slowMotion = Mathf.MoveTowards(slowMotion, impact ? 1f : 0f, dt * (impact ? 6f : 1.5f));
            punch = Mathf.MoveTowards(punch, 0f, dt * 3f);
            flash = Mathf.MoveTowards(flash, 0f, dt * 2.2f);

            // Tension: the frame closes in as the junction approaches.
            float tension = phase == GamePhase.Driving ? Mathf.Clamp01(1f - session.JunctionEta / 4f) : 0f;

            if (adjustments != null)
            {
                adjustments.postExposure.Override(baseExposure + flash * 1.1f + punch * 0.18f + slowMotion * 0.1f);
                adjustments.saturation.Override(baseSaturation - slowMotion * 14f);
                adjustments.contrast.Override(baseContrast + slowMotion * 6f + tension * 4f);
            }
            if (whiteBalance != null) whiteBalance.temperature.Override(baseTemperature - slowMotion * 12f + flash * 20f);
            if (vignette != null) vignette.intensity.Override(baseVignette + tension * (tabletop ? 0f : 0.08f) + slowMotion * (tabletop ? 0.05f : 0.1f));
            if (bloom != null) bloom.intensity.Override(baseBloom + flash * 0.9f + slowMotion * 0.15f);
            if (tabletop) return;

            if (chromatic != null) chromatic.intensity.Override(baseChromatic + (boosting ? 0.1f : 0f) + punch * 0.16f + slowMotion * 0.08f);
            if (motionBlur != null) motionBlur.intensity.Override(Mathf.Lerp(0.06f, 0.34f, Mathf.InverseLerp(12f, 45f, speed)) * (impact ? 0.3f : 1f));
            if (lens != null)
            {
                float squeeze = boosting ? -0.16f : phase == GamePhase.Driving ? -Mathf.InverseLerp(20f, 45f, speed) * 0.06f : 0f;
                lens.intensity.Override(Mathf.Lerp(lens.intensity.value, squeeze - punch * 0.12f, 1f - Mathf.Exp(-6f * dt)));
            }
            if (depthOfField != null && rig != null)
            {
                bool shallow = slowMotion > 0.05f || phase == GamePhase.Results;
                depthOfField.mode.Override(shallow ? DepthOfFieldMode.Bokeh : DepthOfFieldMode.Off);
                if (shallow)
                {
                    depthOfField.focusDistance.Override(Mathf.Max(1f, rig.FocusDistance));
                    depthOfField.aperture.Override(Mathf.Lerp(8f, 2.4f, slowMotion));
                    depthOfField.focalLength.Override(Mathf.Lerp(40f, 70f, slowMotion));
                }
            }
        }
    }
}
