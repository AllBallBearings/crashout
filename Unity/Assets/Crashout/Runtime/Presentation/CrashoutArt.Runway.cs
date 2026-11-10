using System.Collections.Generic;
using Crashout.Simulation;
using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>
    /// The approach roads. Each junction launches the player from far outside the board and high
    /// above it: the brow of a hill, a cable-stayed expressway over a river, or a mountain pass that
    /// ends in a kicker ramp. The runway is a panhandle: a narrow diorama arm reaching off the south
    /// edge of the board, cut square at its sides and end and standing on the same plinth. The road,
    /// barriers and ramp are colliders; the arm's ground is dressing.
    /// </summary>
    public sealed partial class CrashoutArt
    {
        private const float SampleStep = 1f;
        private const float RoadHalf = RunwayProfile.RoadHalfWidth;
        private const float KerbOuter = 5.7f;

        private Mesh[] conifers;

        public Transform BuildRunway(Transform parent, RunwayProfile runway)
        {
            Transform root = new GameObject("Runway " + runway.Title).transform;
            root.SetParent(parent, false);
            if (runway.DistanceBeyondBoard < 1f) return root;

            List<float> zs = Samples(runway);
            BuildRoadDeck(root, runway, zs);
            BuildBarriers(root, runway, zs);
            BuildStartLine(root, runway);
            BuildGantry(root, runway, -58f);
            if (runway.HasRamp) BuildRamp(root, runway);

            switch (runway.Style)
            {
                case RunwayStyle.Overpass:
                    BuildViaduct(root, runway);
                    break;
                case RunwayStyle.MountainPass:
                    BuildTunnel(root, runway);
                    break;
            }

            BuildArm(root, runway);
            BuildArmDressing(root, runway);
            return root;
        }

        private static List<float> Samples(RunwayProfile runway)
        {
            var zs = new List<float>();
            for (float z = runway.FarZ; z < RunwayProfile.BoardEdgeZ; z += SampleStep) zs.Add(z);
            zs.Add(RunwayProfile.BoardEdgeZ);
            return zs;
        }

        private static Vector3 RoadPoint(RunwayProfile runway, float x, float z, float lift = 0f)
        {
            return new Vector3(x, RunwayProfile.RoadSurfaceY + runway.HeightAt(z) + lift, z);
        }

        // ------------------------------------------------------------------ road

        private void BuildRoadDeck(Transform root, RunwayProfile runway, List<float> zs)
        {
            var left = new List<Vector3>();
            var right = new List<Vector3>();
            foreach (float z in zs)
            {
                left.Add(RoadPoint(runway, -RoadHalf, z));
                right.Add(RoadPoint(runway, RoadHalf, z));
            }
            var surface = new MeshKit();
            surface.Strip(left, right, Vector3.up, 1f / 14.4f);
            Mesh roadMesh = surface.ToMesh("Runway surface");
            GameObject road = MeshKit.Spawn(root, "Runway surface", roadMesh, roadPainted, false, true);
            road.isStatic = true;

            // Drivable shoulders under the kerbs: without them a wheel hugging the barrier drops into the
            // carved verge and the tilted car snags the city kerb at the board edge.
            var shoulders = new MeshKit();
            for (int side = -1; side <= 1; side += 2)
            {
                var inner = new List<Vector3>();
                var outer = new List<Vector3>();
                foreach (float z in zs)
                {
                    inner.Add(RoadPoint(runway, side * RoadHalf, z, -0.01f));
                    outer.Add(RoadPoint(runway, side * (KerbOuter + 0.3f), z, 0.1f));
                }
                shoulders.Strip(inner, outer, Vector3.up, 0.25f);
            }
            GameObject shoulderCollider = new GameObject("Runway shoulders");
            shoulderCollider.transform.SetParent(root, false);
            shoulderCollider.AddComponent<MeshCollider>().sharedMesh = shoulders.ToMesh("Runway shoulders");
            shoulderCollider.isStatic = true;

            // Kerbs, deck fascia and underside.
            var kerbs = new MeshKit();
            var fascia = new MeshKit();
            for (int side = -1; side <= 1; side += 2)
            {
                var inner = new List<Vector3>();
                var outer = new List<Vector3>();
                var topOuter = new List<Vector3>();
                var bottomOuter = new List<Vector3>();
                foreach (float z in zs)
                {
                    inner.Add(RoadPoint(runway, side * RoadHalf, z, 0.12f));
                    outer.Add(RoadPoint(runway, side * KerbOuter, z, 0.12f));
                    topOuter.Add(RoadPoint(runway, side * KerbOuter, z, 0.12f));
                    float depth = runway.Style == RunwayStyle.Overpass ? 1.4f : 1.2f;
                    bottomOuter.Add(RoadPoint(runway, side * KerbOuter, z, -depth));
                }
                kerbs.Strip(inner, outer, Vector3.up, 0.25f, 0.7f);
                // Kerb face down to the asphalt.
                var kerbLow = new List<Vector3>();
                foreach (float z in zs) kerbLow.Add(RoadPoint(runway, side * RoadHalf, z, 0f));
                kerbs.Strip(kerbLow, inner, Vector3.right * -side, 0.25f, 0.12f);
                fascia.Strip(bottomOuter, topOuter, Vector3.right * side, 0.25f, 1.4f);
            }
            var underside = new List<Vector3>();
            var undersideRight = new List<Vector3>();
            foreach (float z in zs)
            {
                float depth = runway.Style == RunwayStyle.Overpass ? 1.4f : 1.2f;
                underside.Add(RoadPoint(runway, -KerbOuter, z, -depth));
                undersideRight.Add(RoadPoint(runway, KerbOuter, z, -depth));
            }
            fascia.Strip(underside, undersideRight, Vector3.down, 0.25f, 11.4f);
            MeshKit.Spawn(root, "Runway kerbs", kerbs.ToMesh("Runway kerbs"), concrete);
            MeshKit.Spawn(root, "Runway deck", fascia.ToMesh("Runway deck"), concreteDark);
        }

        private void BuildBarriers(Transform root, RunwayProfile runway, List<float> zs)
        {
            bool concreteParapet = runway.Style == RunwayStyle.Overpass;
            // Run the barriers right up to the city kerb so the board edge never presents a bare step.
            float endZ = RunwayProfile.BoardEdgeZ;
            var rails = new MeshKit();
            var posts = new MeshKit();
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 5.45f;
                var samples = new List<float>();
                foreach (float z in zs) if (z <= endZ) samples.Add(z);
                if (samples.Count < 2) continue;

                if (concreteParapet)
                {
                    // Jersey barrier section, extruded along the deck.
                    Vector2[] section = { new Vector2(-0.3f, 0f), new Vector2(-0.2f, 0.25f), new Vector2(-0.1f, 1.05f), new Vector2(0.1f, 1.05f), new Vector2(0.2f, 0.25f), new Vector2(0.3f, 0f) };
                    rails.Loft(samples.Count, section.Length, (s, r) =>
                    {
                        Vector3 p = RoadPoint(runway, x, samples[s], 0.12f);
                        return p + new Vector3(section[r].x, section[r].y, 0f);
                    }, true, true, 0.5f);
                }
                else
                {
                    // Galvanised W-beam on posts.
                    Vector2[] beam = { new Vector2(0f, 0.5f), new Vector2(0.08f, 0.58f), new Vector2(0.03f, 0.65f), new Vector2(0.08f, 0.72f), new Vector2(0f, 0.8f), new Vector2(-0.02f, 0.65f) };
                    rails.Loft(samples.Count, beam.Length, (s, r) =>
                    {
                        Vector3 p = RoadPoint(runway, x, samples[s], 0.12f);
                        return p + new Vector3(beam[r].x * -side, beam[r].y, 0f);
                    }, true, true, 0.5f);
                    for (float z = samples[0] + 1f; z < endZ; z += 2f)
                    {
                        Vector3 p = RoadPoint(runway, x + side * 0.12f, z, 0.12f);
                        posts.Box(p + Vector3.up * 0.33f, new Vector3(0.1f, 0.75f, 0.14f), Quaternion.identity);
                    }
                }

                // Box colliders every few metres, pitched with the grade.
                for (int i = 0; i + 1 < samples.Count; i += 4)
                {
                    int j = Mathf.Min(samples.Count - 1, i + 4);
                    Vector3 a = RoadPoint(runway, x, samples[i], 0.12f);
                    Vector3 b = RoadPoint(runway, x, samples[j], 0.12f);
                    GameObject collider = new GameObject("Barrier collider");
                    collider.transform.SetParent(root, false);
                    collider.transform.SetPositionAndRotation((a + b) * 0.5f + Vector3.up * 0.5f, Quaternion.LookRotation(b - a, Vector3.up));
                    BoxCollider box = collider.AddComponent<BoxCollider>();
                    box.size = new Vector3(0.35f, 1.1f, Vector3.Distance(a, b) + 0.3f);
                }
            }
            MeshKit.Spawn(root, concreteParapet ? "Parapets" : "Guardrails", rails.ToMesh("Barriers"), concreteParapet ? concrete : steel);
            if (posts.VertexCount > 0) MeshKit.Spawn(root, "Guardrail posts", posts.ToMesh("Guardrail posts"), steel);
        }

        private void BuildStartLine(Transform root, RunwayProfile runway)
        {
            var kit = new MeshKit();
            float z = runway.StartZ + 2.6f;
            // A chequered grid across the player's side of the road.
            for (int i = 0; i < 10; i++)
            {
                for (int row = 0; row < 2; row++)
                {
                    if ((i + row) % 2 != 0) continue;
                    float x = 0.25f + i * 0.475f;
                    kit.Box(RoadPoint(runway, x, z + row * 0.45f, 0.006f), new Vector3(0.47f, 0.01f, 0.44f), Quaternion.identity);
                }
            }
            MeshKit.Spawn(root, "Start grid", kit.ToMesh("Start grid"), whitePaint, false);
        }

        /// <summary>An overhead sign gantry: two masts, a truss and green panels.</summary>
        private void BuildGantry(Transform root, RunwayProfile runway, float z)
        {
            var steelKit = new MeshKit();
            var panels = new MeshKit();
            var lettering = new MeshKit();
            Vector3 baseLeft = RoadPoint(runway, -6.6f, z, 0.12f);
            Vector3 baseRight = RoadPoint(runway, 6.6f, z, 0.12f);
            float top = 6.6f;
            steelKit.Cylinder(baseLeft, baseLeft + Vector3.up * top, 0.18f, 0.16f, 12);
            steelKit.Cylinder(baseRight, baseRight + Vector3.up * top, 0.18f, 0.16f, 12);
            for (int chord = 0; chord < 2; chord++)
            {
                float y = top - 0.15f - chord * 0.75f;
                steelKit.Cylinder(baseLeft + Vector3.up * y, baseRight + Vector3.up * y, 0.06f, 0.06f, 8);
            }
            for (float x = -6.4f; x < 6.4f; x += 0.8f)
            {
                Vector3 a = baseLeft + new Vector3(x + 6.6f, top - 0.15f, 0f);
                Vector3 b = baseLeft + new Vector3(x + 7.0f, top - 0.9f, 0f);
                steelKit.Cylinder(a, b, 0.025f, 0.025f, 5, false);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 centre = baseLeft + new Vector3(6.6f + s * 2.5f, top - 1.7f, -0.12f);
                panels.Box(centre, new Vector3(4.2f, 1.7f, 0.08f), Quaternion.identity);
                lettering.Box(centre + new Vector3(0f, 0f, -0.045f), new Vector3(4.05f, 1.55f, 0.005f), Quaternion.identity);
                // White "text" lines and an arrow.
                for (int line = 0; line < 2; line++)
                {
                    float width = line == 0 ? 2.8f : 1.9f;
                    panels.Box(centre + new Vector3(-0.4f, 0.35f - line * 0.45f, -0.05f), new Vector3(width, 0.22f, 0.005f), Quaternion.identity);
                }
                panels.Box(centre + new Vector3(1.55f, -0.1f, -0.05f), new Vector3(0.14f, 0.9f, 0.005f), Quaternion.identity);
            }
            MeshKit.Spawn(root, "Gantry", steelKit.ToMesh("Gantry"), steel);
            MeshKit.Spawn(root, "Gantry signs", lettering.ToMesh("Gantry signs"), signGreen);
            MeshKit.Spawn(root, "Gantry lettering", panels.ToMesh("Gantry lettering"), whitePaint);
        }

        // ------------------------------------------------------------------ ramp

        private void BuildRamp(Transform root, RunwayProfile runway)
        {
            const int steps = 24;
            const float halfWidth = 4.2f;
            var top = new MeshKit();
            var sides = new MeshKit();
            var leftEdge = new List<Vector3>();
            var rightEdge = new List<Vector3>();
            var leftBase = new List<Vector3>();
            var rightBase = new List<Vector3>();
            for (int i = 0; i <= steps; i++)
            {
                float z = Mathf.Lerp(runway.RampStartZ, runway.RampEndZ, i / (float)steps);
                float rise = runway.RampHeightAt(z);
                leftEdge.Add(RoadPoint(runway, -halfWidth, z, rise + 0.005f));
                rightEdge.Add(RoadPoint(runway, halfWidth, z, rise + 0.005f));
                leftBase.Add(RoadPoint(runway, -halfWidth, z, 0f));
                rightBase.Add(RoadPoint(runway, halfWidth, z, 0f));
            }
            top.Strip(leftEdge, rightEdge, Vector3.up, 0.5f, 8.4f);
            sides.Strip(leftBase, leftEdge, Vector3.left, 0.5f, 1f);
            sides.Strip(rightBase, rightEdge, Vector3.right, 0.5f, 1f);
            var lipLow = new List<Vector3> { leftBase[steps], rightBase[steps] };
            var lipHigh = new List<Vector3> { leftEdge[steps], rightEdge[steps] };
            sides.Strip(lipLow, lipHigh, Vector3.forward, 1f, 1f);

            GameObject deck = MeshKit.Spawn(root, "Kicker ramp", top.ToMesh("Kicker ramp"), darkMetal, true, true);
            deck.isStatic = true;
            MeshKit.Spawn(root, "Kicker ramp sides", sides.ToMesh("Kicker ramp sides"), hazard);

            // Side rails and chevron boards warning of the jump.
            var rails = new MeshKit();
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 previous = Vector3.zero;
                for (int i = 0; i <= steps; i += 3)
                {
                    Vector3 p = (side < 0 ? leftEdge[i] : rightEdge[i]) + Vector3.up * 0.55f;
                    rails.Cylinder((side < 0 ? leftEdge[i] : rightEdge[i]), p, 0.035f, 0.035f, 6, false);
                    if (i > 0) rails.Cylinder(previous, p, 0.04f, 0.04f, 6, false);
                    previous = p;
                }
            }
            var boards = new MeshKit();
            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = 0; k < 3; k++)
                {
                    float z = runway.RampStartZ - 12f - k * 9f;
                    Vector3 p = RoadPoint(runway, side * 6.2f, z, 0.12f);
                    rails.Cylinder(p, p + Vector3.up * 1.9f, 0.04f, 0.04f, 6);
                    boards.Panel(p + Vector3.up * 1.6f, Vector3.back, Vector3.up, 0.9f, 0.7f);
                }
            }
            MeshKit.Spawn(root, "Ramp rails", rails.ToMesh("Ramp rails"), steel);
            MeshKit.Spawn(root, "Jump warning boards", boards.ToMesh("Jump warning boards"), hazard);
        }

        // ------------------------------------------------------------------ expressway structure

        private const float RiverCentreZ = -102f;
        private const float RiverHalfWidth = 10f;
        private const float RiverSurface = -0.7f;
        private const float RiverBed = -1.15f;
        /// <summary>Half the width of the runway arm: the road, its verges and nothing more.</summary>
        private const float ArmHalfWidth = 11f;

        private void BuildViaduct(Transform root, RunwayProfile runway)
        {
            var piers = new MeshKit();
            for (float z = runway.FarZ + 6f; z < RunwayProfile.BoardEdgeZ - 6f; z += 16f)
            {
                float h = runway.HeightAt(z);
                if (h < 2.4f) continue;
                if (Mathf.Abs(z - RiverCentreZ) < RiverHalfWidth + 2f) continue;
                float capY = RunwayProfile.RoadSurfaceY + h - 1.4f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float bottom = ArmHeight(runway, side * 3.2f, z) - 0.3f;
                    piers.Cylinder(new Vector3(side * 3.2f, bottom, z), new Vector3(side * 3.2f, capY - 0.6f, z), 0.65f, 0.55f, 18);
                }
                piers.Box(new Vector3(0f, capY - 0.35f, z), new Vector3(10.4f, 0.8f, 1.6f), Quaternion.identity);
            }
            MeshKit.Spawn(root, "Viaduct piers", piers.ToMesh("Viaduct piers"), concrete);

            // Retaining walls where the ramp is too low for piers.
            var walls = new MeshKit();
            for (int side = -1; side <= 1; side += 2)
            {
                var top = new List<Vector3>();
                var bottom = new List<Vector3>();
                for (float z = -60f; z <= RunwayProfile.BoardEdgeZ; z += 1f)
                {
                    float h = runway.HeightAt(z);
                    if (h > 3f) continue;
                    top.Add(RoadPoint(runway, side * KerbOuter, z, -0.2f));
                    bottom.Add(new Vector3(side * KerbOuter, -0.3f, z));
                }
                walls.Strip(bottom, top, Vector3.right * side, 0.25f, 3f);
            }
            MeshKit.Spawn(root, "Ramp walls", walls.ToMesh("Ramp walls"), concrete);

            // Deck lighting.
            var lamps = new MeshKit();
            var glow = new MeshKit();
            for (float z = runway.FarZ + 10f; z < -36f; z += 22f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 basePoint = RoadPoint(runway, side * 5.45f, z, 1.15f);
                    Vector3 top = basePoint + Vector3.up * 7.5f;
                    lamps.Cylinder(basePoint, top, 0.1f, 0.07f, 10);
                    Vector3 head = top + new Vector3(-side * 1.3f, 0.2f, 0f);
                    lamps.Cylinder(top, head, 0.05f, 0.05f, 6);
                    lamps.Box(head, new Vector3(0.8f, 0.12f, 0.32f), Quaternion.identity);
                    glow.Box(head + Vector3.down * 0.07f, new Vector3(0.66f, 0.02f, 0.24f), Quaternion.identity);
                }
            }
            MeshKit.Spawn(root, "Deck lamps", lamps.ToMesh("Deck lamps"), darkMetal);
            MeshKit.Spawn(root, "Deck lamp glow", glow.ToMesh("Deck lamp glow"), lampGlow, false);

            // A cable-stayed main span over the river.
            float deckY = RunwayProfile.RoadSurfaceY + runway.HeightAt(RiverCentreZ);
            float baseY = RiverBed;
            var pylon = new MeshKit();
            var cables = new MeshKit();
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 foot = new Vector3(side * 7.4f, baseY, RiverCentreZ);
                Vector3 head = new Vector3(side * 6.4f, deckY + 30f, RiverCentreZ);
                pylon.Cylinder(foot, head, 1.0f, 0.62f, 6);
                for (int k = 1; k <= 5; k++)
                {
                    for (int dir = -1; dir <= 1; dir += 2)
                    {
                        Vector3 anchor = new Vector3(side * 5.75f, deckY + 0.9f, RiverCentreZ + dir * k * 7.5f);
                        Vector3 saddle = new Vector3(side * 6.4f, deckY + 18f + k * 2.2f, RiverCentreZ + dir * 0.4f);
                        cables.Cylinder(anchor, saddle, 0.07f, 0.07f, 6, false);
                    }
                }
            }
            pylon.Box(new Vector3(0f, deckY - 1.8f, RiverCentreZ), new Vector3(15.4f, 1.4f, 2.4f), Quaternion.identity);
            pylon.Box(new Vector3(0f, deckY + 26f, RiverCentreZ), new Vector3(13.4f, 1.6f, 1.8f), Quaternion.identity);
            pylon.Box(new Vector3(0f, deckY + 30.4f, RiverCentreZ), new Vector3(13.4f, 0.8f, 1.4f), Quaternion.identity);
            MeshKit.Spawn(root, "Bridge pylon", pylon.ToMesh("Bridge pylon"), concrete);
            MeshKit.Spawn(root, "Stay cables", cables.ToMesh("Stay cables"), steel);
            // Aircraft warning lights on the pylon tops.
            var beacons = new MeshKit();
            for (int side = -1; side <= 1; side += 2) beacons.Blob(new Vector3(side * 6.4f, deckY + 30.8f, RiverCentreZ), Vector3.one * 0.22f, 1, 0f, 3);
            MeshKit.Spawn(root, "Pylon beacons", beacons.ToMesh("Pylon beacons"), signalRed, false);
        }

        private void BuildTunnel(Transform root, RunwayProfile runway)
        {
            float portalZ = runway.StartZ - 16f;
            Vector3 portal = RoadPoint(runway, 0f, portalZ, 0f);
            var face = new MeshKit();
            // Portal wall with a horseshoe opening framed by a thick concrete arch.
            face.Box(portal + new Vector3(-8.6f, 5f, 0f), new Vector3(4.8f, 12f, 1.4f), Quaternion.identity);
            face.Box(portal + new Vector3(8.6f, 5f, 0f), new Vector3(4.8f, 12f, 1.4f), Quaternion.identity);
            face.Box(portal + new Vector3(0f, 9.5f, 0f), new Vector3(12f, 5f, 1.4f), Quaternion.identity);
            var arch = new MeshKit();
            for (int i = 0; i < 16; i++)
            {
                float a0 = i / 16f * Mathf.PI, a1 = (i + 1) / 16f * Mathf.PI;
                Vector3 p0 = portal + new Vector3(Mathf.Cos(a0) * 6.2f, 3.2f + Mathf.Sin(a0) * 4.6f, 0.5f);
                Vector3 p1 = portal + new Vector3(Mathf.Cos(a1) * 6.2f, 3.2f + Mathf.Sin(a1) * 4.6f, 0.5f);
                arch.Cylinder(p0, p1, 0.55f, 0.55f, 8);
            }
            arch.Box(portal + new Vector3(-6.2f, 1.6f, 0.5f), new Vector3(1.1f, 3.2f, 1.1f), Quaternion.identity);
            arch.Box(portal + new Vector3(6.2f, 1.6f, 0.5f), new Vector3(1.1f, 3.2f, 1.1f), Quaternion.identity);
            MeshKit.Spawn(root, "Tunnel portal", face.ToMesh("Tunnel portal"), concreteDark);
            MeshKit.Spawn(root, "Tunnel arch", arch.ToMesh("Tunnel arch"), concrete);

            // The bore: dark walls and a row of sodium lights receding into the mountain.
            var bore = new MeshKit();
            var lights = new MeshKit();
            for (float z = portalZ - 0.5f; z > runway.FarZ; z -= 1f)
            {
                Vector3 p = RoadPoint(runway, 0f, z, 0f);
                bore.Box(p + new Vector3(-6.4f, 3.5f, 0f), new Vector3(0.4f, 7f, 1.02f), Quaternion.identity);
                bore.Box(p + new Vector3(6.4f, 3.5f, 0f), new Vector3(0.4f, 7f, 1.02f), Quaternion.identity);
                bore.Box(p + new Vector3(0f, 7.6f, 0f), new Vector3(13.2f, 0.4f, 1.02f), Quaternion.identity);
                if (Mathf.Repeat(z, 6f) < 1f)
                {
                    lights.Box(p + new Vector3(-5.9f, 5.6f, 0f), new Vector3(0.15f, 0.12f, 0.9f), Quaternion.identity);
                    lights.Box(p + new Vector3(5.9f, 5.6f, 0f), new Vector3(0.15f, 0.12f, 0.9f), Quaternion.identity);
                }
            }
            MeshKit.Spawn(root, "Tunnel bore", bore.ToMesh("Tunnel bore"), underbody);
            Material sodium = Lit("Sodium lamp", new Color(1f, 0.6f, 0.2f), 0f, 0.5f);
            Emissive(sodium, new Color(7f, 3.4f, 0.8f));
            MeshKit.Spawn(root, "Tunnel lights", lights.ToMesh("Tunnel lights"), sodium, false);
        }

        // ------------------------------------------------------------------ the arm

        /// <summary>Ground height of the runway arm at (x, z). The arm only spans |x| &lt;= ArmHalfWidth.</summary>
        private float ArmHeight(RunwayProfile runway, float x, float z)
        {
            float ax = Mathf.Abs(x);
            float road = RunwayProfile.RoadSurfaceY + runway.HeightAt(Mathf.Clamp(z, runway.FarZ, RunwayProfile.BoardEdgeZ));
            float noise = ProceduralTextures.Value(x / 6f + 100f, z / 9f + 100f, 0, 5) - 0.5f;
            float verge = ProceduralTextures.SmoothStep(KerbOuter + 0.3f, ArmHalfWidth, ax);
            // The last few metres settle to board level so the arm meets the board flush.
            float away = ProceduralTextures.SmoothStep(RunwayProfile.BoardEdgeZ, RunwayProfile.BoardEdgeZ - 10f, z);
            float h;
            switch (runway.Style)
            {
                case RunwayStyle.Overpass:
                {
                    // Water meadow at board level under the viaduct, cut by the river.
                    h = -0.06f + noise * 0.3f * verge;
                    float channel = ProceduralTextures.SmoothStep(RiverHalfWidth + 0.6f, RiverHalfWidth - 0.4f, Mathf.Abs(z - RiverCentreZ));
                    h = Mathf.Lerp(h, RiverBed, channel);
                    break;
                }
                case RunwayStyle.MountainPass:
                {
                    // A rock cutting: walls rise from the verges, and the mountain closes over the tunnel.
                    float walls = ProceduralTextures.SmoothStep(KerbOuter + 0.8f, ArmHalfWidth - 0.4f, ax) * (8f + noise * 6f);
                    h = road - 0.16f - verge * 0.3f + walls * away;
                    float portalZ = runway.StartZ - 16f;
                    float mass = ProceduralTextures.SmoothStep(portalZ + 0.6f, portalZ - 1.2f, z);
                    h = Mathf.Lerp(h, Mathf.Max(h, road + 9.6f + noise * 1.5f), mass);
                    break;
                }
                default:
                    // The hill itself: the road runs along its crest and the shoulders roll off to the cut.
                    h = road - 0.16f - verge * (0.5f + noise * 0.9f);
                    break;
            }
            return Mathf.Lerp(-0.06f, h, away);
        }

        /// <summary>
        /// The panhandle: a terrain strip as wide as the road and its verges, cut square at the sides
        /// and far end to show the strata, standing on a plinth that matches the board's.
        /// </summary>
        private void BuildArm(Transform root, RunwayProfile runway)
        {
            float z0 = runway.FarZ, z1 = RunwayProfile.BoardEdgeZ;
            const float step = 1f;
            int nx = Mathf.RoundToInt(ArmHalfWidth * 2f / step) + 1;
            int nz = Mathf.CeilToInt((z1 - z0) / step) + 1;
            var heights = new float[nx * nz];
            System.Threading.Tasks.Parallel.For(0, nz, j =>
            {
                float z = Mathf.Min(z1, z0 + j * step);
                for (int i = 0; i < nx; i++) heights[j * nx + i] = ArmHeight(runway, -ArmHalfWidth + i * step, z);
            });

            var top = new MeshKit();
            for (int j = 0; j < nz; j++)
            {
                float z = Mathf.Min(z1, z0 + j * step);
                for (int i = 0; i < nx; i++)
                {
                    float x = -ArmHalfWidth + i * step;
                    top.Vertex(new Vector3(x, heights[j * nx + i], z), Vector3.up, new Vector2((float)i / (nx - 1), (z - z0) / (z1 - z0)));
                }
            }
            top.Grid(0, nz, nx);
            GameObject ground = MeshKit.Spawn(root, "Arm ground", top.ToMesh("Arm ground", true), ArmGroundMaterial(runway), true, true);
            ground.isStatic = true;

            // Cut faces: both sides and the far end, from the ground down to the base.
            var cut = new MeshKit();
            for (int side = -1; side <= 1; side += 2)
            {
                int column = side < 0 ? 0 : nx - 1;
                Vector3 normal = Vector3.right * side;
                for (int j = 0; j + 1 < nz; j++)
                {
                    float za = Mathf.Min(z1, z0 + j * step), zb = Mathf.Min(z1, z0 + (j + 1) * step);
                    float x = side * ArmHalfWidth;
                    float ha = heights[j * nx + column], hb = heights[(j + 1) * nx + column];
                    int a = cut.Vertex(new Vector3(x, DioramaBase, za), normal, new Vector2(za, DioramaBase));
                    int b = cut.Vertex(new Vector3(x, ha, za), normal, new Vector2(za, ha));
                    int c = cut.Vertex(new Vector3(x, hb, zb), normal, new Vector2(zb, hb));
                    int d = cut.Vertex(new Vector3(x, DioramaBase, zb), normal, new Vector2(zb, DioramaBase));
                    cut.Quad(a, b, c, d);
                }
            }
            for (int i = 0; i + 1 < nx; i++)
            {
                float xa = -ArmHalfWidth + i * step, xb = xa + step;
                float ha = heights[i], hb = heights[i + 1];
                int a = cut.Vertex(new Vector3(xa, DioramaBase, z0), Vector3.back, new Vector2(xa, DioramaBase));
                int b = cut.Vertex(new Vector3(xa, ha, z0), Vector3.back, new Vector2(xa, ha));
                int c = cut.Vertex(new Vector3(xb, hb, z0), Vector3.back, new Vector2(xb, hb));
                int d = cut.Vertex(new Vector3(xb, DioramaBase, z0), Vector3.back, new Vector2(xb, DioramaBase));
                cut.Quad(a, b, c, d);
            }
            MeshKit.Spawn(root, "Arm strata", cut.ToMesh("Arm strata"), strata);

            // The plinth band, proud of the cut faces exactly as the board's is.
            float length = z1 - z0;
            Slab(root, "Arm plinth", new Vector3(0f, (PlinthTop + DioramaBase) * 0.5f, z0 + length * 0.5f - 0.3f),
                new Vector3(ArmHalfWidth * 2f + 1.2f, PlinthTop - DioramaBase, length + 0.6f), 0.4f, darkMetal, false);
        }

        private Material ArmGroundMaterial(RunwayProfile runway)
        {
            const int width = 96, height = 1024;
            float z0 = runway.FarZ, z1 = RunwayProfile.BoardEdgeZ;
            RunwayStyle style = runway.Style;
            var pixels = new Color32[width * height];
            System.Threading.Tasks.Parallel.For(0, height, py =>
            {
                for (int px = 0; px < width; px++)
                {
                    float x = Mathf.Lerp(-ArmHalfWidth, ArmHalfWidth, px / (float)(width - 1));
                    float z = Mathf.Lerp(z0, z1, py / (float)(height - 1));
                    float h = ArmHeight(runway, x, z);
                    float slope = (Mathf.Abs(ArmHeight(runway, x + 0.5f, z) - h) + Mathf.Abs(ArmHeight(runway, x, z + 0.5f) - h)) * 2f;
                    float n = ProceduralTextures.Value(x / 5f, z / 9f, 0, 77);
                    float n2 = ProceduralTextures.Value(x / 1.7f, z / 1.7f, 0, 79);
                    Color grass = Color.Lerp(new Color(0.19f, 0.24f, 0.1f), new Color(0.32f, 0.34f, 0.17f), n);
                    if (style == RunwayStyle.MountainPass) grass = Color.Lerp(new Color(0.16f, 0.21f, 0.1f), new Color(0.28f, 0.29f, 0.17f), n);
                    Color dry = new Color(0.46f, 0.42f, 0.26f);
                    Color dirt = new Color(0.34f, 0.27f, 0.19f);
                    Color stone = Color.Lerp(new Color(0.36f, 0.34f, 0.31f), new Color(0.5f, 0.47f, 0.42f), n2);
                    Color c = Color.Lerp(grass, dry, ProceduralTextures.SmoothStep(0.6f, 0.95f, n) * 0.6f);
                    c = Color.Lerp(c, dirt, ProceduralTextures.SmoothStep(0.55f, 0.9f, slope) * 0.7f);
                    c = Color.Lerp(c, stone, ProceduralTextures.SmoothStep(0.9f, 1.4f, slope));
                    // Gravel verge beside the kerbs.
                    c = Color.Lerp(c, new Color(0.33f, 0.31f, 0.28f), ProceduralTextures.SmoothStep(7.2f, 6f, Mathf.Abs(x)) * 0.85f);
                    if (style == RunwayStyle.Overpass && Mathf.Abs(z - RiverCentreZ) < RiverHalfWidth + 0.5f) c = new Color(0.16f, 0.14f, 0.11f);
                    c *= Mathf.Lerp(0.88f, 1.08f, n2);
                    c.a = 1f;
                    pixels[py * width + px] = c;
                }
            });
            var colourMap = new Texture2D(width, height, TextureFormat.RGBA32, true, false) { name = "Arm colour", wrapMode = TextureWrapMode.Clamp, anisoLevel = 8 };
            colourMap.SetPixels32(pixels);
            colourMap.Apply(true, true);

            Material material = Lit("Arm ground " + runway.Title, Color.white, 0f, 0.12f);
            material.SetTexture("_BaseMap", colourMap);
            Detail(material, 3.5f, 1f);
            material.SetTextureScale("_DetailAlbedoMap", new Vector2(ArmHalfWidth * 2f / 3.5f, (z1 - z0) / 3.5f));
            return material;
        }

        private void BuildArmDressing(Transform root, RunwayProfile runway)
        {
            var batch = new BatchedMeshes();
            EnsureTrees();
            EnsureConifers();
            bool mountain = runway.Style == RunwayStyle.MountainPass;
            bool overpass = runway.Style == RunwayStyle.Overpass;
            float portalZ = runway.StartZ - 16f;
            for (float z = RunwayProfile.BoardEdgeZ - 5f; z > runway.FarZ + 2.5f; z -= 4.5f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    int hz = Mathf.RoundToInt(z * 3f) + side * 101;
                    if (Hash(hz, 7) > (mountain ? 0.6f : 0.45f)) continue;
                    if (overpass && Mathf.Abs(z - RiverCentreZ) < RiverHalfWidth + 2f) continue;
                    if (mountain && z < portalZ + 3f) continue;
                    float x = side * Mathf.Lerp(7.4f, mountain ? 8.4f : ArmHalfWidth - 1.6f, Hash(hz, 9));
                    float jz = z + (Hash(hz, 11) - 0.5f) * 2.5f;
                    float h = ArmHeight(runway, x, jz);
                    float scale = Mathf.Lerp(0.65f, 1.05f, Hash(hz, 13));
                    Matrix4x4 m = Matrix4x4.TRS(new Vector3(x, h - 0.15f, jz), Quaternion.Euler(0f, Hash(hz, 15) * 360f, 0f), Vector3.one * scale);
                    bool pine = mountain || (!overpass && Hash(hz, 17) < 0.3f);
                    if (pine)
                    {
                        int variant = hz & 1;
                        batch.For(bark).Append(conifers[variant * 2], m);
                        batch.For(conifer).Append(conifers[variant * 2 + 1], m);
                    }
                    else
                    {
                        int variant = Mathf.Abs(hz) % treeBark.Length;
                        batch.For(bark).Append(treeBark[variant], m);
                        batch.For(foliage).Append(treeLeaves[variant], m);
                    }
                }
            }

            // A few boulders on the verges.
            int boulders = mountain ? 22 : 8;
            for (int b = 0; b < boulders; b++)
            {
                float side = Hash(b, 91) > 0.5f ? 1f : -1f;
                float x = side * Mathf.Lerp(6.6f, mountain ? 8.6f : ArmHalfWidth - 0.8f, Hash(b, 92));
                float z = Mathf.Lerp(RunwayProfile.BoardEdgeZ - 6f, runway.FarZ + 4f, Hash(b, 93));
                if (overpass && Mathf.Abs(z - RiverCentreZ) < RiverHalfWidth + 2f) continue;
                if (mountain && z < portalZ + 2f) continue;
                float size = Mathf.Lerp(0.4f, mountain ? 1.6f : 1f, Hash(b, 95) * Hash(b, 97));
                var rockKit = new MeshKit();
                rockKit.Blob(Vector3.zero, new Vector3(size * 1.2f, size * 0.7f, size), 2, 0.7f, b);
                batch.For(rock).Append(rockKit.ToMesh("Boulder"), Matrix4x4.TRS(new Vector3(x, ArmHeight(runway, x, z), z), Quaternion.Euler(0f, b * 37f, 0f), Vector3.one));
            }

            if (overpass) BuildArmRiver(root, batch);
            batch.Emit(root, "Arm dressing");
        }

        /// <summary>The river crosses the arm in a channel; the cut faces show it like resin water in a model.</summary>
        private void BuildArmRiver(Transform root, BatchedMeshes batch)
        {
            float width = ArmHalfWidth * 2f, span = RiverHalfWidth * 2f + 1f;
            var surface = new MeshKit();
            surface.Panel(new Vector3(0f, RiverSurface, RiverCentreZ), Vector3.up, Vector3.forward, width, span);
            for (int side = -1; side <= 1; side += 2)
            {
                // Water seen through the cut, slightly proud of the strata.
                surface.Panel(new Vector3(side * (ArmHalfWidth + 0.01f), (RiverSurface + RiverBed) * 0.5f, RiverCentreZ), Vector3.right * side, Vector3.up, span, RiverSurface - RiverBed);
            }
            Mesh mesh = surface.ToMesh("River");
            var uv = new List<Vector2>();
            mesh.GetUVs(0, uv);
            for (int i = 0; i < uv.Count; i++) uv[i] = new Vector2(uv[i].x * width, uv[i].y * span);
            mesh.SetUVs(0, uv);
            GameObject river = MeshKit.Spawn(root, "River", mesh, water, false);
            river.GetComponent<MeshRenderer>().receiveShadows = true;
            river.AddComponent<WaterScroller>();

            for (int bank = -1; bank <= 1; bank += 2)
            {
                float z = RiverCentreZ + bank * (RiverHalfWidth + 0.3f);
                batch.For(concrete).Box(new Vector3(0f, (RiverBed + 0.05f) * 0.5f, z), new Vector3(width, 0.05f - RiverBed, 0.6f), Quaternion.identity);
                batch.For(darkMetal).Box(new Vector3(0f, 0.85f, z), new Vector3(width, 0.06f, 0.06f), Quaternion.identity);
                for (float x = -ArmHalfWidth + 0.5f; x < ArmHalfWidth; x += 2.5f)
                {
                    batch.For(darkMetal).Box(new Vector3(x, 0.45f, z), new Vector3(0.05f, 0.8f, 0.05f), Quaternion.identity);
                }
            }
        }

        private void EnsureConifers()
        {
            if (conifers != null) return;
            conifers = new Mesh[4];
            for (int v = 0; v < 2; v++)
            {
                var trunk = new MeshKit();
                var needles = new MeshKit();
                float height = 7f + v * 2.5f;
                trunk.Cylinder(Vector3.zero, Vector3.up * height * 0.4f, 0.22f, 0.12f, 8);
                int tiers = 5 + v;
                for (int t = 0; t < tiers; t++)
                {
                    float f = t / (float)tiers;
                    float y = height * (0.18f + f * 0.72f);
                    float r = Mathf.Lerp(2.1f, 0.5f, f) * (0.9f + v * 0.1f);
                    needles.Cylinder(new Vector3(0f, y, 0f), new Vector3(0f, y + height * 0.28f, 0f), r, 0.05f, 9);
                }
                conifers[v * 2] = trunk.ToMesh("Conifer trunk " + v);
                conifers[v * 2 + 1] = needles.ToMesh("Conifer needles " + v);
            }
        }
    }

    /// <summary>Slowly drifts the river's ripple normals.</summary>
    public sealed class WaterScroller : MonoBehaviour
    {
        private Material material;

        private void Start()
        {
            material = GetComponent<Renderer>().sharedMaterial;
        }

        private void Update()
        {
            if (material == null) return;
            material.SetTextureOffset("_BaseMap", new Vector2(Time.time * 0.01f, Time.time * 0.02f));
        }
    }
}
