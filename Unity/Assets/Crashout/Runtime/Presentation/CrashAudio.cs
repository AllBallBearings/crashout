using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Crashout.Simulation;
using Crashout.Vehicle;
using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>
    /// The game's sound. A real-time V8 (with a six-speed gearbox, overrun crackle and supercharger
    /// whine) leads the mix; wind, tyre roar, squeal and scraping follow the car; traffic engines pass
    /// with Doppler; a low drone and heartbeat build tension down the approach. Crashes layer modal
    /// metal, glass and debris; impact time muffles the world through a low-pass and deepens hits.
    /// Driven entirely by session events and read-only state, so it never touches simulation.
    /// </summary>
    public sealed class CrashAudio : MonoBehaviour
    {
        private const int PoolSize = 18;
        private const int TrafficVoices = 5;
        private const string MutePreference = "crashout.muted";
        private static readonly float[] GearRatios = { 3.3f, 2.15f, 1.55f, 1.2f, 0.97f, 0.8f };
        private const float FinalDrive = 3.6f;
        private const float IdleRpm = 850f;
        private const float RedlineRpm = 7400f;

        private CrashoutGameSession session;
        private EngineSynth engine;
        private AudioSource boost, wind, road, squeal, scrape, ambient, tension, ui;
        private readonly List<AudioSource> pool = new List<AudioSource>();
        private readonly List<AudioSource> trafficVoices = new List<AudioSource>();
        private readonly List<DamageableVehicle> trafficAssigned = new List<DamageableVehicle>();
        private int nextPooled;
        private AudioLowPassFilter listenerFilter;
        private AudioReverbZone reverb;

        private AudioClip[] impactsLight, impactsHeavy, glassClips, debrisClips, explosions, backfires;
        private AudioClip slowMotion, click, tickHigh, readyBeep, horn, airHorn, launch, blowOff, thud, heartbeat;
        private AudioClip[] groans, concreteHits, rubbles, paneShatters, poleClangs, woodCracks, plasticKnocks, whooshes, alarmClips;
        private AudioClip subDrop, zap, tinnitus, waterLoop, fireLoop, sirenLoop, blastWide;

        private MasterBus master;
        private Destruction destruction;
        private AudioSource siren, sub;
        private readonly List<AudioSource> alarms = new List<AudioSource>();
        private readonly List<Transform> alarmOwners = new List<Transform>();
        private readonly List<float> alarmUntil = new List<float>();
        private readonly List<AudioSource> beds = new List<AudioSource>();
        private readonly List<float> bedTargets = new List<float>();
        private readonly HashSet<uint> whooshed = new HashSet<uint>();
        private float lastSubAt = -10f;
        private float lastGroanAt = -10f;
        private float deafUntil;
        private float sirenTarget;
        private float lastKnockHeard = -10f;
        private int glassFrame = -1;
        private int glassThisFrame;
        private float lastMasonryAt = -10f;
        private AudioClip perfect, bonus, targetDown, fail, medalGold, medalSilver, medalBronze, medalNone;

        private readonly HashSet<uint> honked = new HashSet<uint>();
        private float lastMeter;
        private float nextHornScan;
        private float nextTrafficScan;
        private bool couldCrashbreak;
        private int gear;
        private float shiftUntil;
        private float engineRpm = IdleRpm;
        private bool wasBoosting;
        private int lastGrounded = 4;
        private float airborneSince;
        private float nextHeartbeat;

        public static CrashAudio Instance { get; private set; }
        public static bool Muted { get; private set; }

        public static void PlayClick()
        {
            if (Instance != null && Instance.click != null) Instance.ui.PlayOneShot(Instance.click, 0.6f);
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

            AudioListener listener = FindFirstObjectByType<AudioListener>();
            if (listener != null)
            {
                listenerFilter = listener.GetComponent<AudioLowPassFilter>();
                if (listenerFilter == null) listenerFilter = listener.gameObject.AddComponent<AudioLowPassFilter>();
                listenerFilter.cutoffFrequency = 22000f;
                listenerFilter.lowpassResonanceQ = 1.1f;
                // After the low-pass in the listener's chain, so the limiter sees the final signal.
                master = listener.GetComponent<MasterBus>();
                if (master == null) master = listener.gameObject.AddComponent<MasterBus>();
            }
            ApplyReverb();
        }

        private void Awake()
        {
            Instance = this;
            Muted = PlayerPrefs.GetInt(MutePreference, 0) == 1;
            // Headless test runs still drive the speakers; keep them silent.
            AudioListener.volume = Muted || Application.isBatchMode ? 0f : 1f;
            BuildClips();

            GameObject engineObject = new GameObject("Engine");
            engineObject.transform.SetParent(transform, false);
            engineObject.AddComponent<AudioSource>();
            engine = engineObject.AddComponent<EngineSynth>();
            engine.Redline = RedlineRpm;

            boost = Loop("Boost", SynthClips.Clip("Boost loop", SynthClips.RenderBoostLoop(), true));
            wind = Loop("Wind", SynthClips.ClipStereo("Wind", SynthClips.RenderWind(29), SynthClips.RenderWind(1029), true));
            road = Loop("Road", SynthClips.Clip("Road", SynthClips.RenderRoad(), true));
            squeal = Loop("Squeal", SynthClips.Clip("Squeal", SynthClips.RenderSqueal(), true));
            scrape = Loop("Scrape", SynthClips.Clip("Scrape", SynthClips.RenderScrape(), true));
            ambient = Loop("Ambience", SynthClips.ClipStereo("Ambience", SynthClips.RenderAmbience(43), SynthClips.RenderAmbience(1043), true));
            siren = Loop("Sirens", sirenLoop);
            sub = Source("Sub", null, false);
            sub.priority = 0;
            for (int i = 0; i < 3; i++)
            {
                AudioSource alarm = Source("Car alarm " + i, alarmClips[i % alarmClips.Length], true);
                alarm.Stop();
                alarm.spatialBlend = 1f;
                alarm.rolloffMode = AudioRolloffMode.Logarithmic;
                alarm.minDistance = 5f;
                alarm.maxDistance = 160f;
                alarm.dopplerLevel = 0.4f;
                alarm.volume = 0f;
                alarm.pitch = 1f + (i - 1) * 0.07f;
                alarms.Add(alarm);
                alarmOwners.Add(null);
                alarmUntil.Add(0f);
            }
            for (int i = 0; i < 4; i++)
            {
                // Positional beds: hydrant water and burning wrecks.
                AudioSource bed = Source("Bed " + i, null, true);
                bed.spatialBlend = 1f;
                bed.rolloffMode = AudioRolloffMode.Logarithmic;
                bed.minDistance = 4f;
                bed.maxDistance = 120f;
                bed.dopplerLevel = 0f;
                bed.volume = 0f;
                beds.Add(bed);
                bedTargets.Add(0f);
            }
            tension = Loop("Tension", SynthClips.Clip("Tension", SynthClips.RenderTension(), true));
            ui = Source("Interface", null, false);
            ambient.volume = 0.22f;

            for (int i = 0; i < PoolSize; i++)
            {
                AudioSource source = Source("Spatial " + i, null, false);
                source.spatialBlend = 0.85f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 7f;
                source.maxDistance = 260f;
                source.dopplerLevel = 0f;
                source.spread = 60f;
                pool.Add(source);
            }

            AudioClip trafficEngine = SynthClips.Clip("Traffic engine", SynthClips.RenderTrafficEngine(61), true);
            for (int i = 0; i < TrafficVoices; i++)
            {
                AudioSource voice = Source("Traffic voice " + i, trafficEngine, true);
                voice.spatialBlend = 1f;
                voice.rolloffMode = AudioRolloffMode.Logarithmic;
                voice.minDistance = 3.5f;
                voice.maxDistance = 90f;
                voice.dopplerLevel = 1.3f;
                voice.volume = 0f;
                voice.time = UnityEngine.Random.Range(0f, trafficEngine.length * 0.9f);
                trafficVoices.Add(voice);
                trafficAssigned.Add(null);
            }

            GameObject reverbObject = new GameObject("Reverb");
            reverbObject.transform.SetParent(transform, false);
            reverbObject.transform.position = new Vector3(0f, 0f, -50f);
            reverb = reverbObject.AddComponent<AudioReverbZone>();
            reverb.minDistance = 120f;
            reverb.maxDistance = 400f;
            reverb.reverbPreset = AudioReverbPreset.City;
        }

        /// <summary>Renders every buffer in parallel, then wraps them as clips on the main thread.</summary>
        private void BuildClips()
        {
            var jobs = new List<Func<float[]>>();
            int Add(Func<float[]> job) { jobs.Add(job); return jobs.Count - 1; }
            int[] light = new int[5], heavy = new int[4], glass = new int[3], debris = new int[2], blasts = new int[2], pops = new int[3];
            for (int i = 0; i < light.Length; i++) { int seed = 101 + i * 13; light[i] = Add(() => SynthClips.RenderImpact(seed, false)); }
            for (int i = 0; i < heavy.Length; i++) { int seed = 202 + i * 17; heavy[i] = Add(() => SynthClips.RenderImpact(seed, true)); }
            for (int i = 0; i < glass.Length; i++) { int seed = 303 + i * 7; glass[i] = Add(() => SynthClips.RenderGlass(seed)); }
            for (int i = 0; i < debris.Length; i++) { int seed = 350 + i * 5; debris[i] = Add(() => SynthClips.RenderDebris(seed)); }
            for (int i = 0; i < blasts.Length; i++) { int seed = 404 + i * 11; blasts[i] = Add(() => SynthClips.RenderExplosion(seed)); }
            for (int i = 0; i < pops.Length; i++) { int seed = 500 + i * 3; pops[i] = Add(() => SynthClips.RenderBackfire(seed)); }
            int[] groanJobs = new int[3], concreteJobs = new int[3], rubbleJobs = new int[2], paneJobs = new int[3], poleJobs = new int[2], woodJobs = new int[2], knockJobs = new int[3], whooshJobs = new int[2], alarmJobs = new int[2];
            for (int i = 0; i < groanJobs.Length; i++) { int seed = 610 + i * 7; groanJobs[i] = Add(() => SynthClips.RenderMetalGroan(seed)); }
            for (int i = 0; i < concreteJobs.Length; i++) { int seed = 640 + i * 9; concreteJobs[i] = Add(() => SynthClips.RenderConcreteHit(seed)); }
            for (int i = 0; i < rubbleJobs.Length; i++) { int seed = 670 + i * 5; rubbleJobs[i] = Add(() => SynthClips.RenderRubble(seed)); }
            for (int i = 0; i < paneJobs.Length; i++) { int seed = 700 + i * 11; paneJobs[i] = Add(() => SynthClips.RenderGlass(seed, true)); }
            for (int i = 0; i < poleJobs.Length; i++) { int seed = 730 + i * 3; poleJobs[i] = Add(() => SynthClips.RenderPoleClang(seed)); }
            for (int i = 0; i < woodJobs.Length; i++) { int seed = 750 + i * 3; woodJobs[i] = Add(() => SynthClips.RenderWoodCrack(seed)); }
            for (int i = 0; i < knockJobs.Length; i++) { int seed = 770 + i * 3; knockJobs[i] = Add(() => SynthClips.RenderPlasticKnock(seed)); }
            for (int i = 0; i < whooshJobs.Length; i++) { int seed = 790 + i * 3; whooshJobs[i] = Add(() => SynthClips.RenderWhoosh(seed)); }
            for (int i = 0; i < alarmJobs.Length; i++) { int variant = i; alarmJobs[i] = Add(() => SynthClips.RenderCarAlarm(variant)); }
            int subJob = Add(SynthClips.RenderSubDrop);
            int zapJob = Add(SynthClips.RenderElectricZap);
            int tinnitusJob = Add(SynthClips.RenderTinnitus);
            int waterJob = Add(() => SynthClips.RenderWaterLoop(811));
            int fireJob = Add(() => SynthClips.RenderFireLoop(823));
            int sirenJob = Add(() => SynthClips.RenderSiren(829));
            int wideLeft = Add(() => SynthClips.RenderExplosion(881));
            int wideRight = Add(() => SynthClips.RenderExplosion(883));
            int slow = Add(SynthClips.RenderSlowMotion);
            int clickJob = Add(SynthClips.RenderClick);
            int tick = Add(() => SynthClips.RenderTick(1500f));
            int ready = Add(SynthClips.RenderReadyBeep);
            int hornJob = Add(() => SynthClips.RenderHorn(false));
            int airHornJob = Add(() => SynthClips.RenderHorn(true));
            int launchJob = Add(SynthClips.RenderLaunch);
            int blowJob = Add(SynthClips.RenderBlowOff);
            int thudJob = Add(SynthClips.RenderThud);
            int heartJob = Add(SynthClips.RenderHeartbeat);
            int perfectJob = Add(() => SynthClips.RenderChime(new[] { 659f, 988f }, 0.09f, 1.0f));
            int bonusJob = Add(() => SynthClips.RenderChime(new[] { 1047f, 1319f }, 0.07f, 0.7f, 6f));
            int targetJob = Add(() => SynthClips.RenderChime(new[] { 784f, 1047f, 1319f, 1568f }, 0.08f, 1.2f, 3.5f));
            int failJob = Add(() => SynthClips.RenderFail(230f, 110f, 0.5f));
            int goldJob = Add(() => SynthClips.RenderChime(new[] { 523f, 659f, 784f, 1047f, 1319f }, 0.12f, 1.9f, 2.6f));
            int silverJob = Add(() => SynthClips.RenderChime(new[] { 523f, 659f, 784f }, 0.13f, 1.4f, 3.2f));
            int bronzeJob = Add(() => SynthClips.RenderChime(new[] { 523f, 659f }, 0.14f, 1.1f, 3.8f));
            int noneJob = Add(() => SynthClips.RenderFail(300f, 170f, 0.9f));

            var buffers = new float[jobs.Count][];
            Parallel.For(0, jobs.Count, i => buffers[i] = jobs[i]());

            AudioClip Make(int index, string name) => SynthClips.Clip(name, buffers[index]);
            impactsLight = Array.ConvertAll(light, i => Make(i, "Impact light"));
            impactsHeavy = Array.ConvertAll(heavy, i => Make(i, "Impact heavy"));
            glassClips = Array.ConvertAll(glass, i => Make(i, "Glass"));
            debrisClips = Array.ConvertAll(debris, i => Make(i, "Debris"));
            explosions = Array.ConvertAll(blasts, i => Make(i, "Explosion"));
            backfires = Array.ConvertAll(pops, i => Make(i, "Backfire"));
            groans = Array.ConvertAll(groanJobs, i => Make(i, "Metal groan"));
            concreteHits = Array.ConvertAll(concreteJobs, i => Make(i, "Concrete hit"));
            rubbles = Array.ConvertAll(rubbleJobs, i => Make(i, "Rubble"));
            paneShatters = Array.ConvertAll(paneJobs, i => Make(i, "Pane shatter"));
            poleClangs = Array.ConvertAll(poleJobs, i => Make(i, "Pole clang"));
            woodCracks = Array.ConvertAll(woodJobs, i => Make(i, "Wood crack"));
            plasticKnocks = Array.ConvertAll(knockJobs, i => Make(i, "Plastic knock"));
            whooshes = Array.ConvertAll(whooshJobs, i => Make(i, "Whoosh"));
            alarmClips = Array.ConvertAll(alarmJobs, i => SynthClips.Clip("Car alarm", buffers[i], true));
            subDrop = Make(subJob, "Sub drop");
            zap = Make(zapJob, "Zap");
            tinnitus = Make(tinnitusJob, "Tinnitus");
            waterLoop = SynthClips.Clip("Hydrant water", buffers[waterJob], true);
            fireLoop = SynthClips.Clip("Fire", buffers[fireJob], true);
            sirenLoop = SynthClips.Clip("Sirens", buffers[sirenJob], true);
            blastWide = SynthClips.ClipStereo("Explosion wide", buffers[wideLeft], buffers[wideRight]);
            slowMotion = Make(slow, "Slow motion");
            click = Make(clickJob, "Click");
            tickHigh = Make(tick, "Tick");
            readyBeep = Make(ready, "Ready");
            horn = Make(hornJob, "Horn");
            airHorn = Make(airHornJob, "Air horn");
            launch = Make(launchJob, "Launch");
            blowOff = Make(blowJob, "Blow-off");
            thud = Make(thudJob, "Thud");
            heartbeat = Make(heartJob, "Heartbeat");
            perfect = Make(perfectJob, "Perfect launch");
            bonus = Make(bonusJob, "Bonus");
            targetDown = Make(targetJob, "Target down");
            fail = Make(failJob, "Bad start");
            medalGold = Make(goldJob, "Gold");
            medalSilver = Make(silverJob, "Silver");
            medalBronze = Make(bronzeJob, "Bronze");
            medalNone = Make(noneJob, "No medal");
        }

        private AudioSource Loop(string label, AudioClip clip)
        {
            AudioSource source = Source(label, clip, true);
            source.volume = 0f;
            return source;
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

        /// <summary>Gives every piece of city damage its own sound.</summary>
        public void Watch(Destruction director)
        {
            if (destruction != null) destruction.Hit -= OnStructureHit;
            destruction = director;
            if (destruction != null) destruction.Hit += OnStructureHit;
        }

        private void OnStructureHit(StructureHit hit)
        {
            float slow = session != null && session.IsImpactTime ? 0.8f : 1f;
            float vary = UnityEngine.Random.Range(0.92f, 1.08f) * slow;
            switch (hit.Kind)
            {
                case StructureKind.Masonry:
                    if (Time.unscaledTime - lastMasonryAt < 0.07f) break;
                    lastMasonryAt = Time.unscaledTime;
                    PlayAt(Pick(concreteHits), hit.Position, Mathf.Lerp(0.5f, 1f, hit.Severity), vary * Mathf.Lerp(1.1f, 0.85f, hit.Severity));
                    if (hit.Severity > 0.3f) StartCoroutine(Delayed(Pick(rubbles), hit.Position, Mathf.Lerp(0.4f, 0.9f, hit.Severity), 0.22f));
                    if (hit.Severity > 0.55f) Thump(0.55f * hit.Severity);
                    break;
                case StructureKind.Glass:
                    if (glassFrame != Time.frameCount) { glassFrame = Time.frameCount; glassThisFrame = 0; }
                    if (glassThisFrame++ >= 3) break;
                    PlayAt(Pick(paneShatters), hit.Position, Mathf.Lerp(0.45f, 0.85f, hit.Severity), vary);
                    break;
                case StructureKind.Pole:
                    PlayAt(Pick(poleClangs), hit.Position, 0.85f, vary);
                    if (hit.Severity > 0.3f) StartCoroutine(Delayed(zap, hit.Position + Vector3.up * 3f, 0.5f, 0.12f));
                    break;
                case StructureKind.Tree:
                    PlayAt(Pick(woodCracks), hit.Position, 0.95f, vary);
                    break;
                case StructureKind.Hydrant:
                    PlayAt(Pick(poleClangs), hit.Position, 0.7f, vary * 1.5f);
                    StartBed(waterLoop, hit.Position, 0.7f);
                    break;
                case StructureKind.Loose:
                    PlayAt(Pick(poleClangs), hit.Position, 0.4f, vary * 1.7f);
                    StartCoroutine(Delayed(Pick(rubbles), hit.Position, 0.45f, 0.35f));
                    break;
                default:
                    PlayAt(Pick(plasticKnocks), hit.Position, Mathf.Lerp(0.4f, 0.8f, hit.Severity), vary);
                    break;
            }
        }

        private static AudioClip Pick(AudioClip[] bank)
        {
            return bank[UnityEngine.Random.Range(0, bank.Length)];
        }

        /// <summary>The sub drop under a big moment; rate-limited so a pile-up does not turn to mud.</summary>
        private void Thump(float volume)
        {
            if (Time.unscaledTime - lastSubAt < 0.45f) return;
            lastSubAt = Time.unscaledTime;
            sub.pitch = UnityEngine.Random.Range(0.92f, 1.06f);
            sub.PlayOneShot(subDrop, Mathf.Clamp01(volume));
        }

        private void StartBed(AudioClip clip, Vector3 position, float volume)
        {
            int slot = 0;
            float quietest = float.MaxValue;
            for (int i = 0; i < beds.Count; i++)
            {
                if (bedTargets[i] < quietest) { quietest = bedTargets[i]; slot = i; }
            }
            AudioSource bed = beds[slot];
            bed.transform.position = position;
            bed.clip = clip;
            bed.time = UnityEngine.Random.Range(0f, clip.length * 0.8f);
            bed.volume = 0f;
            bed.Play();
            bedTargets[slot] = volume;
        }

        private void StartAlarm(Transform owner)
        {
            for (int i = 0; i < alarms.Count; i++)
            {
                if (alarmOwners[i] != null) continue;
                alarmOwners[i] = owner;
                alarmUntil[i] = Time.unscaledTime + UnityEngine.Random.Range(9f, 16f);
                alarms[i].transform.position = owner.position;
                alarms[i].volume = 0.5f;
                alarms[i].Play();
                return;
            }
        }

        private void SilenceAftermath()
        {
            for (int i = 0; i < alarms.Count; i++)
            {
                alarms[i].Stop();
                alarmOwners[i] = null;
            }
            for (int i = 0; i < beds.Count; i++)
            {
                beds[i].Stop();
                bedTargets[i] = 0f;
            }
            sirenTarget = 0f;
            deafUntil = 0f;
            whooshed.Clear();
        }

        private void UpdateAftermath(float dt)
        {
            for (int i = 0; i < alarms.Count; i++)
            {
                if (alarmOwners[i] == null) { if (alarms[i].isPlaying) alarms[i].Stop(); continue; }
                alarms[i].transform.position = alarmOwners[i].position;
                if (Time.unscaledTime > alarmUntil[i])
                {
                    alarms[i].volume = Mathf.MoveTowards(alarms[i].volume, 0f, dt * 0.5f);
                    if (alarms[i].volume <= 0.001f) alarmOwners[i] = null;
                }
            }
            for (int i = 0; i < beds.Count; i++)
            {
                if (!beds[i].isPlaying) continue;
                beds[i].volume = Mathf.MoveTowards(beds[i].volume, bedTargets[i], dt * 1.2f);
            }
            siren.volume = Mathf.MoveTowards(siren.volume, sirenTarget, dt * 0.06f);
        }

        private void Unsubscribe()
        {
            if (destruction != null) destruction.Hit -= OnStructureHit;
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
            if (player == null) return;
            float speed = player.SpeedMetresPerSecond;
            bool driving = phase == GamePhase.Driving;
            bool crashing = phase == GamePhase.Crash || phase == GamePhase.SettleDelay;
            bool boosting = player.IsBoosting && driving;
            bool grounded = player.WheelsGrounded > 0;
            bool impact = session.IsImpactTime;

            UpdateEngine(phase, player, speed, boosting, grounded, dt);
            UpdateAftermath(dt);

            // A survivable knock while driving: barrier, kerb, street furniture.
            if (player.LastKnockAt > lastKnockHeard)
            {
                lastKnockHeard = player.LastKnockAt;
                if (driving)
                {
                    float knock = Mathf.Clamp01(player.LastKnockSpeed / 22f);
                    PlayAt(impactsLight[UnityEngine.Random.Range(0, impactsLight.Length)], player.transform.position, Mathf.Lerp(0.3f, 0.8f, knock), UnityEngine.Random.Range(1.05f, 1.25f));
                }
            }

            float windTarget = (driving || crashing) ? Mathf.Pow(Mathf.Clamp01((speed - 6f) / 40f), 1.3f) * 0.55f : 0f;
            wind.volume = Mathf.MoveTowards(wind.volume, windTarget, dt * 2f);
            wind.pitch = 0.75f + speed / 55f;
            road.volume = Mathf.MoveTowards(road.volume, driving && grounded ? Mathf.Clamp01(speed / 32f) * 0.32f : 0f, dt * 3f);
            road.pitch = 0.6f + speed / 45f;

            float lateral = Mathf.Abs(player.LateralSlip);
            float squealTarget = driving && grounded ? Mathf.Clamp01((lateral - 2.4f) / 6f) * 0.5f + (player.AppliedBrake > 0.5f && speed > 8f ? 0.3f : 0f) : 0f;
            squeal.volume = Mathf.MoveTowards(squeal.volume, squealTarget, dt * 6f);
            squeal.pitch = 0.9f + Mathf.Clamp01(lateral / 12f) * 0.25f;

            bool sliding = crashing && speed > 2f && Physics.Raycast(player.transform.position, Vector3.down, 1.2f, 1 << 0, QueryTriggerInteraction.Ignore);
            scrape.volume = Mathf.MoveTowards(scrape.volume, sliding ? Mathf.Clamp01(speed / 12f) * 0.55f : 0f, dt * 5f);
            scrape.pitch = (impact ? 0.7f : 0.9f) + speed / 40f;

            boost.volume = Mathf.MoveTowards(boost.volume, boosting ? 0.42f : 0f, dt * 3f);
            boost.pitch = 0.9f + speed / 90f;
            if (wasBoosting && !boosting && driving) PlayAt(blowOff, player.transform.position, 0.55f, UnityEngine.Random.Range(0.95f, 1.1f));
            wasBoosting = boosting;

            ambient.volume = Mathf.MoveTowards(ambient.volume, impact ? 0.06f : 0.2f, 1.5f * dt);

            // Tension: drone and heartbeat build as the junction approaches.
            float closeness = driving ? Mathf.Clamp01(1f - session.JunctionEta / 6f) : 0f;
            float tensionTarget = phase == GamePhase.ReadyCheck ? 0.12f : driving ? Mathf.Lerp(0.12f, 0.5f, closeness) : 0f;
            tension.volume = Mathf.MoveTowards(tension.volume, tensionTarget, dt * (driving || phase == GamePhase.ReadyCheck ? 0.8f : 3f));
            tension.pitch = 1f + closeness * 0.06f;
            if (driving && closeness > 0.15f && Time.unscaledTime >= nextHeartbeat)
            {
                ui.PlayOneShot(heartbeat, Mathf.Lerp(0.25f, 0.7f, closeness));
                nextHeartbeat = Time.unscaledTime + Mathf.Lerp(0.95f, 0.4f, closeness);
            }

            // Impact time: the world goes underwater. A point-blank blast leaves the ears closed for a while.
            if (listenerFilter != null)
            {
                bool deaf = Time.unscaledTime < deafUntil;
                float cutoff = impact ? 900f : deaf ? Mathf.Lerp(22000f, 650f, Mathf.Clamp01((deafUntil - Time.unscaledTime) / 2.2f)) : 22000f;
                listenerFilter.cutoffFrequency = Mathf.Lerp(listenerFilter.cutoffFrequency, cutoff, 1f - Mathf.Exp(-(impact ? 14f : 3f) * dt));
            }

            if (driving)
            {
                int wheels = player.WheelsGrounded;
                if (wheels == 0 && lastGrounded > 0) airborneSince = Time.unscaledTime;
                if (wheels > 0 && lastGrounded == 0 && Time.unscaledTime - airborneSince > 0.25f)
                {
                    PlayAt(thud, player.transform.position, 0.9f, UnityEngine.Random.Range(0.9f, 1.05f));
                    PlayAt(impactsLight[UnityEngine.Random.Range(0, impactsLight.Length)], player.transform.position, 0.35f, 1.3f);
                }
                lastGrounded = wheels;
            }

            if (phase == GamePhase.ReadyCheck)
            {
                float meter = session.LaunchMeter;
                if (lastMeter < CrashoutGameSession.LaunchZoneMin && meter >= CrashoutGameSession.LaunchZoneMin) ui.PlayOneShot(tickHigh, 0.5f);
                lastMeter = meter;
            }

            bool canCrashbreak = session.CanCrashbreak;
            if (canCrashbreak && !couldCrashbreak) ui.PlayOneShot(readyBeep, 0.55f);
            couldCrashbreak = canCrashbreak;

            if (driving && Time.unscaledTime >= nextHornScan)
            {
                nextHornScan = Time.unscaledTime + 0.15f;
                HonkAtThePlayer();
            }
            UpdateTrafficVoices(phase, dt);
        }

        /// <summary>Six-speed gearbox model: road speed sets RPM; shifts cut the throttle and can pop.</summary>
        private void UpdateEngine(GamePhase phase, ArcadeVehicleController player, float speed, bool boosting, bool grounded, float dt)
        {
            float targetRpm, load, volume;
            if (phase == GamePhase.ReadyCheck)
            {
                // Blipping the throttle with the swing meter.
                float meter = session.LaunchMeter;
                targetRpm = 1300f + meter * meter * 5600f;
                load = 0.25f + meter * 0.75f;
                volume = 0.55f;
                gear = 0;
            }
            else if (phase == GamePhase.Driving)
            {
                float wheelRpm = speed / (2f * Mathf.PI * ArcadeVehicleController.WheelRadius) * 60f;
                float geared = wheelRpm * GearRatios[gear] * FinalDrive;
                bool shifting = Time.unscaledTime < shiftUntil;
                if (!shifting && geared > 6900f && gear < GearRatios.Length - 1)
                {
                    gear++;
                    shiftUntil = Time.unscaledTime + 0.16f;
                    if (UnityEngine.Random.value < 0.6f) PlayAt(backfires[UnityEngine.Random.Range(0, backfires.Length)], player.transform.position - player.transform.forward * 1.7f, 0.5f, UnityEngine.Random.Range(0.9f, 1.15f));
                }
                else if (!shifting && geared < 3000f && gear > 0)
                {
                    gear--;
                    shiftUntil = Time.unscaledTime + 0.12f;
                }
                geared = wheelRpm * GearRatios[gear] * FinalDrive;
                if (!grounded) geared += 1800f; // Wheels spin free in the air.
                targetRpm = Mathf.Clamp(Mathf.Max(IdleRpm + 600f, geared), IdleRpm, RedlineRpm);
                load = shifting ? 0f : Mathf.Max(player.AppliedThrottle, boosting ? 1f : 0f) * (player.AppliedBrake > 0.5f ? 0f : 1f);
                if (shifting) targetRpm = Mathf.Max(IdleRpm, engineRpm - 1600f);
                volume = 0.85f;
            }
            else if (phase == GamePhase.Crash || phase == GamePhase.SettleDelay)
            {
                targetRpm = 700f;
                load = 0f;
                volume = 0f;
            }
            else
            {
                targetRpm = IdleRpm;
                load = 0f;
                volume = 0f;
            }

            engineRpm = Mathf.Lerp(engineRpm, targetRpm, 1f - Mathf.Exp(-12f * dt));
            engine.Rpm = targetRpm;
            engine.Load = load;
            engine.Boost = boosting ? 1f : 0f;
            engine.RoadSpeed = phase == GamePhase.Driving ? speed : 0f;
            engine.Volume = Mathf.MoveTowards(engine.Volume, volume, dt * (volume > engine.Volume ? 3f : 1.6f));
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
                if (Vector3.Distance(vehicle.transform.position, playerPosition) > 16f) continue;
                honked.Add(vehicle.EntityId);
                if (UnityEngine.Random.value < 0.45f)
                {
                    bool heavy = vehicle.Class == VehicleClass.Bus || vehicle.Class == VehicleClass.Tanker;
                    PlayAt(heavy ? airHorn : horn, vehicle.transform.position, heavy ? 0.8f : 0.6f, UnityEngine.Random.Range(0.94f, 1.06f));
                }
            }
        }

        /// <summary>The nearest moving traffic gets an engine voice that follows it, so cars whoosh past with Doppler.</summary>
        private void UpdateTrafficVoices(GamePhase phase, float dt)
        {
            Transform listener = listenerFilter != null ? listenerFilter.transform : Camera.main != null ? Camera.main.transform : null;
            bool active = phase == GamePhase.Driving || phase == GamePhase.Crash || phase == GamePhase.SettleDelay;
            if (listener != null && active && Time.unscaledTime >= nextTrafficScan)
            {
                nextTrafficScan = Time.unscaledTime + 0.25f;
                var candidates = new List<DamageableVehicle>();
                foreach (DamageableVehicle vehicle in session.Vehicles)
                {
                    if (vehicle == null || vehicle.IsPlayerVehicle) continue;
                    TrafficVehicle traffic = vehicle.GetComponent<TrafficVehicle>();
                    if (traffic != null && traffic.DriveEnabled) candidates.Add(vehicle);
                }
                Vector3 ear = listener.position;
                candidates.Sort((a, b) => (a.transform.position - ear).sqrMagnitude.CompareTo((b.transform.position - ear).sqrMagnitude));
                for (int i = 0; i < trafficVoices.Count; i++) trafficAssigned[i] = i < candidates.Count ? candidates[i] : null;
            }
            for (int i = 0; i < trafficVoices.Count; i++)
            {
                AudioSource voice = trafficVoices[i];
                DamageableVehicle car = active ? trafficAssigned[i] : null;
                TrafficVehicle traffic = car != null ? car.GetComponent<TrafficVehicle>() : null;
                bool live = car != null && traffic != null && traffic.DriveEnabled;
                if (live && listener != null && phase == GamePhase.Driving && !whooshed.Contains(car.EntityId))
                {
                    Vector3 offset = car.transform.position - listener.position;
                    Vector3 closing = car.Body.linearVelocity - (session.Player != null ? session.Player.Body.linearVelocity : Vector3.zero);
                    if (offset.sqrMagnitude < 64f && closing.sqrMagnitude > 196f)
                    {
                        whooshed.Add(car.EntityId);
                        PlayAt(Pick(whooshes), car.transform.position, 0.7f, UnityEngine.Random.Range(0.9f, 1.15f));
                    }
                }
                if (live)
                {
                    voice.transform.position = car.transform.position;
                    bool heavy = car.Class == VehicleClass.Bus || car.Class == VehicleClass.Tanker;
                    voice.pitch = (heavy ? 0.55f : 0.85f) + car.Body.linearVelocity.magnitude / (heavy ? 40f : 28f);
                }
                voice.volume = Mathf.MoveTowards(voice.volume, live ? 0.45f : 0f, dt * 2f);
            }
        }

        private void ApplyReverb()
        {
            if (reverb == null || session == null || session.Runway == null) return;
            reverb.reverbPreset = session.Runway.Style == RunwayStyle.MountainPass ? AudioReverbPreset.Mountains : AudioReverbPreset.City;
        }

        // ------------------------------------------------------------------ events

        private void OnScore(ScoreEvent scoreEvent)
        {
            if (scoreEvent.Amount <= 0 || !session.TryGetVehiclePosition(scoreEvent.EntityId, out Vector3 position)) return;
            bool heavy = scoreEvent.RelativeSpeed > 12f;
            IReadOnlyList<DamageableVehicle> vehicles = session.Vehicles;
            for (int i = 0; i < vehicles.Count; i++)
            {
                if (vehicles[i] != null && vehicles[i].EntityId == scoreEvent.EntityId)
                {
                    heavy |= vehicles[i].Class == VehicleClass.Bus || vehicles[i].Class == VehicleClass.Tanker;
                    break;
                }
            }

            float slow = session.IsImpactTime ? 0.78f : 1f;
            float volume = Mathf.Clamp(0.4f + scoreEvent.RelativeSpeed / 14f, 0.4f, 1f);
            AudioClip[] bank = heavy ? impactsHeavy : impactsLight;
            PlayAt(bank[UnityEngine.Random.Range(0, bank.Length)], position, volume, UnityEngine.Random.Range(0.92f, 1.08f) * slow);
            if (scoreEvent.RelativeSpeed > 5.5f)
            {
                PlayAt(glassClips[UnityEngine.Random.Range(0, glassClips.Length)], position + Vector3.up, volume * 0.75f, UnityEngine.Random.Range(0.92f, 1.1f) * slow);
            }
            if (scoreEvent.RelativeSpeed > 9f) StartCoroutine(Delayed(debrisClips[UnityEngine.Random.Range(0, debrisClips.Length)], position, volume * 0.6f, 0.18f));

            // The cinema layers: a sub drop under anything big, twisting metal on heavy wrecks.
            DamageableVehicle struck = null;
            for (int i = 0; i < vehicles.Count; i++)
            {
                if (vehicles[i] != null && vehicles[i].EntityId == scoreEvent.EntityId) { struck = vehicles[i]; break; }
            }
            if (heavy || scoreEvent.RelativeSpeed > 10f) Thump(Mathf.Clamp01(0.35f + scoreEvent.RelativeSpeed / 30f) * (heavy ? 1f : 0.7f));
            if ((heavy || scoreEvent.RelativeSpeed > 13f) && Time.unscaledTime - lastGroanAt > 0.6f)
            {
                lastGroanAt = Time.unscaledTime;
                StartCoroutine(Delayed(Pick(groans), position, volume * 0.75f, 0.08f));
            }
            // Some wrecks start wailing.
            if (struck != null && !struck.IsPlayerVehicle && !heavy && scoreEvent.RelativeSpeed > 5f && scoreEvent.ChainCount >= 2 && UnityEngine.Random.value < 0.3f)
            {
                StartAlarm(struck.transform);
            }
        }

        private IEnumerator Delayed(AudioClip clip, Vector3 position, float volume, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            PlayAt(clip, position, volume, UnityEngine.Random.Range(0.9f, 1.1f));
        }

        private void OnExploded(Vector3 centre, float radius, bool byPlayer)
        {
            float size = Mathf.Clamp01(radius / 17f);
            AudioClip blast = explosions[UnityEngine.Random.Range(0, explosions.Length)];
            PlayAt(blast, centre, 1f, Mathf.Lerp(1.1f, 0.82f, size));
            // Always felt, wherever the camera is: a wide stereo layer and the sub.
            ui.PlayOneShot(blastWide, Mathf.Lerp(0.45f, 0.8f, size));
            lastSubAt = -10f;
            Thump(Mathf.Lerp(0.7f, 1f, size));
            if (master != null) master.Slam(Mathf.Lerp(0.25f, 0.45f, size), 0.12f);
            StartCoroutine(Delayed(debrisClips[0], centre, 0.7f, 0.5f));
            StartCoroutine(Delayed(Pick(rubbles), centre, 0.8f, 0.9f));
            StartBed(fireLoop, centre, Mathf.Lerp(0.45f, 0.8f, size));
            if (byPlayer)
            {
                // Point blank: ringing ears and a muffled world that slowly opens up again.
                deafUntil = Time.unscaledTime + 2.6f;
                ui.PlayOneShot(tinnitus, 0.16f);
            }
        }

        private void OnBonus(string label, int amount, Vector3 position)
        {
            if (label == "PERFECT LAUNCH") ui.PlayOneShot(perfect, 0.7f);
            else if (label == "TARGET DOWN") ui.PlayOneShot(targetDown, 0.8f);
            else if (label == "SLOW START" || label == "BAD START") ui.PlayOneShot(fail, 0.45f);
            else if (amount > 0) ui.PlayOneShot(bonus, 0.6f);
        }

        private void OnPhase(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.SettleDelay:
                    // Someone has called it in.
                    if (session.ChainCount >= 4) sirenTarget = 0.22f;
                    break;
                case GamePhase.ReadyCheck:
                    SilenceAftermath();
                    siren.volume = 0f;
                    honked.Clear();
                    lastMeter = 0f;
                    gear = 0;
                    lastGrounded = 4;
                    ApplyReverb();
                    break;
                case GamePhase.Driving:
                    ui.PlayOneShot(launch, 0.75f);
                    nextHeartbeat = Time.unscaledTime + 0.8f;
                    break;
                case GamePhase.Crash:
                    ui.PlayOneShot(slowMotion, 0.85f);
                    lastSubAt = -10f;
                    Thump(0.9f);
                    if (master != null) master.Slam(0.2f, 0.08f);
                    break;
                case GamePhase.Results:
                    CrashMedal medal = session.Medal;
                    ui.PlayOneShot(medal == CrashMedal.Gold ? medalGold : medal == CrashMedal.Silver ? medalSilver : medal == CrashMedal.Bronze ? medalBronze : medalNone, 0.85f);
                    break;
            }
        }

        // Plays a positional one-shot, reusing the oldest source when all are busy.
        private void PlayAt(AudioClip clip, Vector3 position, float volume, float pitch)
        {
            if (clip == null) return;
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
