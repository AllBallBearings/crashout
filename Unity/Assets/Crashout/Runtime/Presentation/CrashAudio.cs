using System.Collections.Generic;
using Crashout.Simulation;
using Crashout.Vehicle;
using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>
    /// Sound for the game. Driven entirely by session events and state, so it never touches simulation.
    /// Clips are synthesized at startup (see <see cref="SynthClips"/>).
    /// </summary>
    public sealed class CrashAudio : MonoBehaviour
    {
        private const int PoolSize = 14;
        private const string MutePreference = "crashout.muted";

        private CrashoutGameSession session;
        private AudioSource engine, boost, ambient, ui;
        private readonly List<AudioSource> pool = new List<AudioSource>();
        private int nextPooled;

        private AudioClip crunchLight, crunchHeavy, glass, explosion, slowMotion, click, tickHigh;
        private AudioClip perfect, bonus, targetDown, fail, horn, whoosh, readyBeep;
        private AudioClip medalGold, medalSilver, medalBronze, medalNone;

        private readonly HashSet<uint> honked = new HashSet<uint>();
        private float lastMeter;
        private float nextHornScan;
        private bool couldCrashbreak;

        public static CrashAudio Instance { get; private set; }
        public static bool Muted { get; private set; }

        public static void PlayClick()
        {
            if (Instance != null) Instance.ui.PlayOneShot(Instance.click, 0.7f);
        }

        public static void ToggleMute()
        {
            Muted = !Muted;
            AudioListener.volume = Muted ? 0f : 1f;
            PlayerPrefs.SetInt(MutePreference, Muted ? 1 : 0);
            PlayerPrefs.Save();
            if (!Muted) PlayClick();
        }

        public void Configure(CrashoutGameSession gameSession)
        {
            Unsubscribe();
            session = gameSession;
            session.ScoreChanged += OnScore;
            session.Exploded += OnExploded;
            session.BonusAwarded += OnBonus;
            session.PhaseChanged += OnPhase;
        }

        private void Awake()
        {
            Instance = this;
            Muted = PlayerPrefs.GetInt(MutePreference, 0) == 1;
            AudioListener.volume = Muted ? 0f : 1f;

            crunchLight = SynthClips.Crunch(false, 101);
            crunchHeavy = SynthClips.Crunch(true, 202);
            glass = SynthClips.Glass(303);
            explosion = SynthClips.Explosion(404);
            slowMotion = SynthClips.SlowMotion();
            click = SynthClips.Click();
            tickHigh = SynthClips.Tick(1500f);
            readyBeep = SynthClips.ReadyBeep();
            horn = SynthClips.Horn();
            whoosh = SynthClips.LaunchWhoosh();
            perfect = SynthClips.Chime("Perfect launch", new[] { 659f, 988f }, 0.09f, 0.9f);
            bonus = SynthClips.Chime("Bonus", new[] { 1047f, 1319f }, 0.07f, 0.6f, 6f);
            targetDown = SynthClips.Chime("Target down", new[] { 784f, 1047f, 1319f, 1568f }, 0.08f, 1.1f, 4f);
            fail = SynthClips.Fail("Bad start", 230f, 110f, 0.5f);
            medalGold = SynthClips.Chime("Gold", new[] { 523f, 659f, 784f, 1047f, 1319f }, 0.12f, 1.7f, 3f);
            medalSilver = SynthClips.Chime("Silver", new[] { 523f, 659f, 784f }, 0.13f, 1.3f, 3.5f);
            medalBronze = SynthClips.Chime("Bronze", new[] { 523f, 659f }, 0.14f, 1.0f, 4f);
            medalNone = SynthClips.Fail("No medal", 300f, 170f, 0.9f);

            engine = Source("Engine", SynthClips.EngineLoop(), true);
            boost = Source("Boost", SynthClips.BoostLoop(), true);
            ambient = Source("Ambience", SynthClips.AmbientLoop(), true);
            ui = Source("Interface", null, false);
            engine.volume = 0f;
            boost.volume = 0f;
            ambient.volume = 0.22f;

            for (int i = 0; i < PoolSize; i++)
            {
                AudioSource source = Source("Spatial " + i, null, false);
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 8f;
                source.maxDistance = 90f;
                source.dopplerLevel = 0f;
                pool.Add(source);
            }
        }

        private AudioSource Source(string label, AudioClip clip, bool loop)
        {
            GameObject child = new GameObject(label);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = loop;
            source.clip = clip;
            if (clip != null) source.Play();
            return source;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        private void Unsubscribe()
        {
            if (session == null) return;
            session.ScoreChanged -= OnScore;
            session.Exploded -= OnExploded;
            session.BonusAwarded -= OnBonus;
            session.PhaseChanged -= OnPhase;
        }

        // ------------------------------------------------------------------ continuous sound

        private void Update()
        {
            if (session == null) return;
            float dt = Time.unscaledDeltaTime;
            GamePhase phase = session.Phase;
            ArcadeVehicleController player = session.Player;
            float speed = player != null ? player.SpeedMetresPerSecond : 0f;
            bool boosting = player != null && player.IsBoosting && phase == GamePhase.Driving;

            float engineVolume = phase == GamePhase.Driving ? 0.5f : phase == GamePhase.ReadyCheck ? 0.2f : 0f;
            // While waiting to launch the engine revs with the swing meter, like a driver blipping the throttle.
            float enginePitch = phase == GamePhase.ReadyCheck
                ? 0.8f + 0.6f * session.LaunchMeter
                : Mathf.Lerp(0.8f, 2.5f, Mathf.Clamp01(speed / 30f)) + (boosting ? 0.3f : 0f);
            engine.volume = Mathf.MoveTowards(engine.volume, engineVolume, 2f * dt);
            engine.pitch = Mathf.Lerp(engine.pitch, enginePitch, 8f * dt);
            boost.volume = Mathf.MoveTowards(boost.volume, boosting ? 0.45f : 0f, 3f * dt);
            ambient.volume = Mathf.MoveTowards(ambient.volume, session.IsImpactTime ? 0.08f : 0.22f, 1.5f * dt);

            if (phase == GamePhase.ReadyCheck)
            {
                float meter = session.LaunchMeter;
                if (lastMeter < CrashoutGameSession.LaunchZoneMin && meter >= CrashoutGameSession.LaunchZoneMin)
                {
                    ui.PlayOneShot(tickHigh, 0.55f);
                }
                lastMeter = meter;
            }

            bool canCrashbreak = session.CanCrashbreak;
            if (canCrashbreak && !couldCrashbreak) ui.PlayOneShot(readyBeep, 0.6f);
            couldCrashbreak = canCrashbreak;

            if (phase == GamePhase.Driving && Time.unscaledTime >= nextHornScan)
            {
                nextHornScan = Time.unscaledTime + 0.15f;
                HonkAtThePlayer();
            }
        }

        // Cars the player is about to meet sometimes sound their horn, once each.
        private void HonkAtThePlayer()
        {
            if (session.Player == null) return;
            Vector3 playerPosition = session.Player.transform.position;
            IReadOnlyList<DamageableVehicle> vehicles = session.Vehicles;
            for (int i = 0; i < vehicles.Count; i++)
            {
                DamageableVehicle vehicle = vehicles[i];
                if (vehicle == null || vehicle.IsPlayerVehicle || honked.Contains(vehicle.EntityId)) continue;
                TrafficVehicle traffic = vehicle.GetComponent<TrafficVehicle>();
                if (traffic == null || !traffic.DriveEnabled) continue;
                if (Vector3.Distance(vehicle.transform.position, playerPosition) > 13f) continue;
                honked.Add(vehicle.EntityId);
                if (Random.value < 0.4f)
                {
                    PlayAt(horn, vehicle.transform.position, 0.5f, Random.Range(0.85f, 1.25f));
                }
            }
        }

        // ------------------------------------------------------------------ events

        private void OnScore(ScoreEvent scoreEvent)
        {
            if (scoreEvent.Amount <= 0 || !session.TryGetVehiclePosition(scoreEvent.EntityId, out Vector3 position)) return;
            bool heavy = false;
            IReadOnlyList<DamageableVehicle> vehicles = session.Vehicles;
            for (int i = 0; i < vehicles.Count; i++)
            {
                if (vehicles[i] != null && vehicles[i].EntityId == scoreEvent.EntityId)
                {
                    heavy = vehicles[i].Class == VehicleClass.Bus || vehicles[i].Class == VehicleClass.Tanker;
                    break;
                }
            }

            float volume = Mathf.Clamp(0.35f + scoreEvent.RelativeSpeed / 14f, 0.35f, 1f);
            PlayAt(heavy ? crunchHeavy : crunchLight, position, volume, Random.Range(heavy ? 0.85f : 0.92f, heavy ? 1f : 1.12f));
            if (scoreEvent.Amount > 1800)
            {
                PlayAt(glass, position + Vector3.up, volume * 0.8f, Random.Range(0.9f, 1.2f));
            }
        }

        private void OnExploded(Vector3 centre, float radius, bool byPlayer)
        {
            float size = Mathf.Clamp01(radius / 17f);
            PlayAt(explosion, centre, 1f, Mathf.Lerp(1.15f, 0.8f, size));
            // Always audible regardless of where the camera is.
            ui.PlayOneShot(explosion, Mathf.Lerp(0.35f, 0.6f, size));
        }

        private void OnBonus(string label, int amount, Vector3 position)
        {
            if (label == "PERFECT LAUNCH") ui.PlayOneShot(perfect, 0.8f);
            else if (label == "TARGET DOWN") ui.PlayOneShot(targetDown, 0.85f);
            else if (label == "SLOW START" || label == "BAD START") ui.PlayOneShot(fail, 0.5f);
            else if (amount > 0) ui.PlayOneShot(bonus, 0.7f);
        }

        private void OnPhase(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.ReadyCheck:
                    honked.Clear();
                    lastMeter = 0f;
                    break;
                case GamePhase.Driving:
                    ui.PlayOneShot(whoosh, 0.7f);
                    break;
                case GamePhase.Crash:
                    ui.PlayOneShot(slowMotion, 0.8f);
                    break;
                case GamePhase.Results:
                    CrashMedal medal = session.Medal;
                    ui.PlayOneShot(medal == CrashMedal.Gold ? medalGold : medal == CrashMedal.Silver ? medalSilver : medal == CrashMedal.Bronze ? medalBronze : medalNone, 0.9f);
                    break;
            }
        }

        // Plays a positional one-shot, reusing the oldest source when all are busy.
        private void PlayAt(AudioClip clip, Vector3 position, float volume, float pitch)
        {
            AudioSource chosen = null;
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].isPlaying)
                {
                    chosen = pool[i];
                    break;
                }
            }
            if (chosen == null)
            {
                chosen = pool[nextPooled];
                nextPooled = (nextPooled + 1) % pool.Count;
            }
            chosen.transform.position = position;
            chosen.pitch = pitch;
            chosen.volume = volume;
            chosen.clip = clip;
            chosen.Play();
        }
    }
}
