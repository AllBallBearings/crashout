using System.Collections.Generic;
using Crashout.Simulation;
using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>
    /// The approach roads. Each junction launches the player from far outside the board and high
    /// above it: the brow of a hill, a cable-stayed expressway over a river, or a mountain pass that
    /// ends in a kicker ramp. The road, barriers and ramp are colliders; the landscape is dressing.
    /// On the AR table only the road structure is built (it reaches off the board like a model).
    /// </summary>
    public sealed partial class CrashoutArt
    {
        private const float SampleStep = 1f;
        private const float RoadHalf = RunwayProfile.RoadHalfWidth;
        private const float KerbOuter = 5.7f;

        private Mesh[] conifers;

        public Transform BuildRunway(Transform parent, RunwayProfile runway, bool tabletop)
        {
            Transform root = new GameObject("Runway " + runway.Title).transform;
            root.SetParent(parent, false);
            if (runway.DistanceBeyondBoard < 1f) return root;

            List<float> zs = Samples(runway);
            BuildRoadDeck(root, runway, zs, tabletop);
            BuildBarriers(root, runway, zs);
            BuildStartLine(root, runway);
            BuildGantry(root, runway, -58f);
            if (runway.HasRamp) BuildRamp(root, runway);

            switch (runway.Style)
            {
                case RunwayStyle.Overpass:
                    BuildViaduct(root, runway, tabletop);
                    break;
                case RunwayStyle.MountainPass:
                    BuildTunnel(root, runway);
                    break;
            }

            if (!tabletop)
            {
                BuildTerrain(root, runway);
                BuildLandscapeDressing(root, runway);
            }
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

        private void BuildRoadDeck(Transform root, RunwayProfile runway, List<float> zs, bool tabletop)
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
                    if (tabletop && runway.Style != RunwayStyle.Overpass) depth = runway.HeightAt(z) + 0.2f;
                    bottomOuter.Add(RoadPoint(runway, side * KerbOuter, z, -Mathf.Max(0.4f, depth)));
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
                if (tabletop && runway.Style != RunwayStyle.Overpass) depth = runway.HeightAt(z) + 0.2f;
                underside.Add(RoadPoint(runway, -KerbOuter, z, -Mathf.Max(0.4f, depth)));
                undersideRight.Add(RoadPoint(runway, KerbOuter, z, -Mathf.Max(0.4f, depth)));
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
        private const float RiverWater = -2.4f;

        private void BuildViaduct(Transform root, RunwayProfile runway, bool tabletop)
        {
            var piers = new MeshKit();
            for (float z = runway.FarZ + 6f; z < RunwayProfile.BoardEdgeZ - 6f; z += 16f)
            {
                float h = runway.HeightAt(z);
                if (h < 2.4f) continue;
                if (Mathf.Abs(z - RiverCentreZ) < RiverHalfWidth + 2f) continue;
                float bottom = tabletop ? 0f : TerrainHeight(runway, 0f, z) - 0.5f;
                float capY = RunwayProfile.RoadSurfaceY + h - 1.4f;
                for (int side = -1; side <= 1; side += 2)
                {
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
            float baseY = tabletop ? 0f : RiverWater - 1.5f;
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
            face.Box(portal + new Vector3(-9.5f, 5f, 0f), new Vector3(7f, 12f, 1.4f), Quaternion.identity);
            face.Box(portal + new Vector3(9.5f, 5f, 0f), new Vector3(7f, 12f, 1.4f), Quaternion.identity);
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

        // ------------------------------------------------------------------ landscape

        private float TerrainHeight(RunwayProfile runway, float x, float z)
        {
            float ax = Mathf.Abs(x);
            float roadZ = Mathf.Max(z, runway.FarZ);
            float road = runway.HeightAt(Mathf.Min(roadZ, RunwayProfile.BoardEdgeZ));
            float noise = ProceduralTextures.Value(x / 38f + 100f, z / 38f + 100f, 0, 5) - 0.5f;
            float detail = ProceduralTextures.Value(x / 11f + 50f, z / 11f + 50f, 0, 9) - 0.5f;
            float natural;
            float carve = 1f;
            switch (runway.Style)
            {
                case RunwayStyle.Overpass:
                {
                    natural = noise * 0.8f + detail * 0.3f;
                    float distanceFromRiver = Mathf.Abs(z - RiverCentreZ);
                    natural = Mathf.Lerp(-4.2f, natural, ProceduralTextures.SmoothStep(RiverHalfWidth - 0.5f, RiverHalfWidth + 0.5f, distanceFromRiver));
                    natural += ProceduralTextures.SmoothStep(-170f, -300f, z) * (18f + noise * 30f) * ProceduralTextures.SmoothStep(20f, 90f, ax);
                    carve = ProceduralTextures.SmoothStep(3f, 0.5f, road) * ProceduralTextures.SmoothStep(-60f, -45f, z);
                    break;
                }
                case RunwayStyle.MountainPass:
                {
                    float valley = Mathf.Lerp(9f, 46f, ProceduralTextures.SmoothStep(-110f, -40f, z));
                    float cliff = Mathf.Min(Mathf.Max(0f, ax - valley) * 1.25f, 48f + noise * 18f);
                    natural = road + cliff + noise * 6f + detail * 2.5f;
                    float portalZ = runway.StartZ - 16f;
                    natural += ProceduralTextures.SmoothStep(portalZ + 1f, portalZ - 8f, z) * 26f * (1f - ProceduralTextures.SmoothStep(25f, 70f, ax));
                    natural += ProceduralTextures.SmoothStep(-140f, -320f, z) * (40f + noise * 40f);
                    carve = ProceduralTextures.SmoothStep(portalZ - 1f, portalZ + 2f, z);
                    break;
                }
                default:
                {
                    float shoulders = road * (1f - 0.55f * ProceduralTextures.SmoothStep(30f, 150f, ax));
                    natural = shoulders + noise * 9f * ProceduralTextures.SmoothStep(12f, 50f, ax) + detail * 1.5f;
                    natural += ProceduralTextures.SmoothStep(-150f, -320f, z) * (22f + noise * 20f);
                    natural += ProceduralTextures.SmoothStep(60f, 220f, ax) * ProceduralTextures.SmoothStep(-30f, -120f, z) * 14f;
                    break;
                }
            }
            float corridor = ProceduralTextures.SmoothStep(6.4f, 18f, ax);
            float carved = Mathf.Lerp(road - 0.9f, natural, corridor);
            float height = Mathf.Lerp(natural, carved, carve);
            // Meet the city ground at the board edge.
            float edge = ProceduralTextures.SmoothStep(RunwayProfile.BoardEdgeZ, RunwayProfile.BoardEdgeZ - 16f, z);
            return Mathf.Lerp(-0.12f, height, edge);
        }

        private void BuildTerrain(Transform root, RunwayProfile runway)
        {
            const float step = 3.5f;
            float x0 = -260f, x1 = 260f;
            float z0 = Mathf.Min(runway.FarZ - 140f, -320f), z1 = RunwayProfile.BoardEdgeZ;
            int nx = Mathf.CeilToInt((x1 - x0) / step) + 1;
            int nz = Mathf.CeilToInt((z1 - z0) / step) + 1;
            var heights = new float[nx * nz];
            System.Threading.Tasks.Parallel.For(0, nz, j =>
            {
                float z = Mathf.Min(z1, z0 + j * step);
                for (int i = 0; i < nx; i++) heights[j * nx + i] = TerrainHeight(runway, x0 + i * step, z);
            });

            var kit = new MeshKit();
            for (int j = 0; j < nz; j++)
            {
                float z = Mathf.Min(z1, z0 + j * step);
                for (int i = 0; i < nx; i++)
                {
                    float x = x0 + i * step;
                    kit.Vertex(new Vector3(x, heights[j * nx + i], z), Vector3.up, new Vector2((x - x0) / (x1 - x0), (z - z0) / (z1 - z0)));
                }
            }
            kit.Grid(0, nz, nx);
            Mesh mesh = kit.ToMesh("Terrain", true);

            // Colour map from height, slope and noise.
            const int size = 512;
            var pixels = new Color32[size * size];
            RunwayStyle style = runway.Style;
            System.Threading.Tasks.Parallel.For(0, size, py =>
            {
                for (int px = 0; px < size; px++)
                {
                    float u = px / (float)(size - 1), v = py / (float)(size - 1);
                    float fx = u * (nx - 1), fz = v * (nz - 1);
                    int i = Mathf.Min(nx - 2, (int)fx), j = Mathf.Min(nz - 2, (int)fz);
                    float h00 = heights[j * nx + i], h10 = heights[j * nx + i + 1], h01 = heights[(j + 1) * nx + i];
                    float slope = Mathf.Sqrt((h10 - h00) * (h10 - h00) + (h01 - h00) * (h01 - h00)) / step;
                    float x = x0 + u * (x1 - x0), z = z0 + v * (z1 - z0);
                    float n = ProceduralTextures.Value(x / 23f, z / 23f, 0, 77);
                    float n2 = ProceduralTextures.Value(x / 6f, z / 6f, 0, 79);
                    Color grass = Color.Lerp(new Color(0.19f, 0.24f, 0.1f), new Color(0.32f, 0.34f, 0.17f), n);
                    if (style == RunwayStyle.MountainPass) grass = Color.Lerp(new Color(0.16f, 0.22f, 0.1f), new Color(0.3f, 0.3f, 0.16f), n);
                    Color dry = new Color(0.46f, 0.42f, 0.26f);
                    Color dirt = new Color(0.34f, 0.27f, 0.19f);
                    Color stone = Color.Lerp(new Color(0.36f, 0.34f, 0.31f), new Color(0.5f, 0.47f, 0.42f), n2);
                    Color c = Color.Lerp(grass, dry, ProceduralTextures.SmoothStep(0.6f, 0.95f, n) * 0.6f);
                    // Only genuinely steep ground loses its grass; rolling hills stay green.
                    c = Color.Lerp(c, dirt, ProceduralTextures.SmoothStep(0.55f, 0.9f, slope) * 0.7f);
                    c = Color.Lerp(c, stone, ProceduralTextures.SmoothStep(0.9f, 1.4f, slope));
                    float corridor = ProceduralTextures.SmoothStep(9f, 6f, Mathf.Abs(x));
                    c = Color.Lerp(c, new Color(0.32f, 0.3f, 0.27f), corridor * 0.8f);
                    if (style == RunwayStyle.Overpass && Mathf.Abs(z - RiverCentreZ) < RiverHalfWidth + 1f) c = new Color(0.17f, 0.15f, 0.12f);
                    c *= Mathf.Lerp(0.88f, 1.08f, n2);
                    c.a = 1f;
                    pixels[py * size + px] = c;
                }
            });
            var colourMap = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { name = "Terrain colour", wrapMode = TextureWrapMode.Clamp, anisoLevel = 8 };
            colourMap.SetPixels32(pixels);
            colourMap.Apply(true, true);

            Material material = Lit("Terrain " + runway.Title, Color.white, 0f, 0.12f);
            material.SetTexture("_BaseMap", colourMap);
            Detail(material, 3.5f, x1 - x0);
            GameObject terrain = MeshKit.Spawn(root, "Terrain", mesh, material, true, true);
            terrain.isStatic = true;
        }

        private void BuildLandscapeDressing(Transform root, RunwayProfile runway)
        {
            var batch = new BatchedMeshes();
            EnsureTrees();
            EnsureConifers();
            bool mountain = runway.Style == RunwayStyle.MountainPass;
            bool overpass = runway.Style == RunwayStyle.Overpass;
            float zFar = Mathf.Min(runway.FarZ - 80f, -260f);
            for (float z = RunwayProfile.BoardEdgeZ - 8f; z > zFar; z -= 7f)
            {
                for (float x = -230f; x <= 230f; x += 7f)
                {
                    int hx = Mathf.RoundToInt(x), hz = Mathf.RoundToInt(z);
                    float jx = x + (Hash(hx, hz) - 0.5f) * 6f;
                    float jz = z + (Hash(hz, hx) - 0.5f) * 6f;
                    float ax = Mathf.Abs(jx);
                    if (ax < 10f) continue;
                    if (overpass && Mathf.Abs(jz - RiverCentreZ) < RiverHalfWidth + 3f) continue;
                    float h = TerrainHeight(runway, jx, jz);
                    float slope = Mathf.Abs(TerrainHeight(runway, jx + 2f, jz) - h) + Mathf.Abs(TerrainHeight(runway, jx, jz + 2f) - h);
                    if (slope > 3.2f) continue;
                    float density = ProceduralTextures.Value(jx / 45f, jz / 45f, 0, 13);
                    float chance = mountain ? 0.35f + density * 0.55f : overpass ? 0.12f + density * 0.25f : 0.1f + density * 0.45f;
                    if (ax < 18f) chance *= 0.4f;
                    if (Hash(hx * 3 + 1, hz * 5 + 2) > chance) continue;
                    float scale = Mathf.Lerp(0.8f, 1.5f, Hash(hx + 7, hz + 9));
                    Matrix4x4 m = Matrix4x4.TRS(new Vector3(jx, h - 0.2f, jz), Quaternion.Euler(0f, Hash(hx, hz + 3) * 360f, 0f), Vector3.one * scale);
                    bool pine = mountain || (Hash(hx + 2, hz) < 0.25f && !overpass);
                    if (pine)
                    {
                        int variant = (hx + hz * 3) & 1;
                        batch.For(bark).Append(conifers[variant * 2], m);
                        batch.For(conifer).Append(conifers[variant * 2 + 1], m);
                    }
                    else
                    {
                        int variant = Mathf.Abs(hx * 7 + hz) % treeBark.Length;
                        batch.For(bark).Append(treeBark[variant], m);
                        batch.For(foliage).Append(treeLeaves[variant], m);
                    }
                }
            }

            // Boulders scattered on slopes and in the mountain valley.
            int boulders = mountain ? 70 : 24;
            for (int b = 0; b < boulders; b++)
            {
                float x = (Hash(b, 91) - 0.5f) * 160f;
                if (Mathf.Abs(x) < 8f) x += Mathf.Sign(x) * 8f;
                float z = Mathf.Lerp(-34f, -200f, Hash(b, 93));
                if (overpass && Mathf.Abs(z - RiverCentreZ) < RiverHalfWidth + 2f) continue;
                float h = TerrainHeight(runway, x, z);
                float size = Mathf.Lerp(0.6f, mountain ? 3.6f : 1.8f, Hash(b, 95) * Hash(b, 97));
                var rockKit = new MeshKit();
                rockKit.Blob(Vector3.zero, new Vector3(size * 1.2f, size * 0.7f, size), 2, 0.7f, b);
                batch.For(rock).Append(rockKit.ToMesh("Boulder"), Matrix4x4.TRS(new Vector3(x, h, z), Quaternion.Euler(0f, b * 37f, 0f), Vector3.one));
            }

            if (overpass) BuildRiver(root, batch);
            else BuildHouses(root, runway, batch, mountain ? 4 : 12);
            batch.Emit(root, "Landscape");
        }

        private void BuildRiver(Transform root, BatchedMeshes batch)
        {
            var surface = new MeshKit();
            surface.Panel(new Vector3(0f, RiverWater, RiverCentreZ), Vector3.up, Vector3.forward, 560f, RiverHalfWidth * 2f + 1f);
            Mesh water = surface.ToMesh("River");
            // Panel UVs are 0..1; scale to metres for the ripple normal map.
            var uv = new List<Vector2>();
            water.GetUVs(0, uv);
            for (int i = 0; i < uv.Count; i++) uv[i] = new Vector2(uv[i].x * 560f, uv[i].y * 21f);
            water.SetUVs(0, uv);
            GameObject river = MeshKit.Spawn(root, "River", water, this.water, false);
            river.GetComponent<MeshRenderer>().receiveShadows = true;
            river.AddComponent<WaterScroller>();

            for (int bank = -1; bank <= 1; bank += 2)
            {
                float z = RiverCentreZ + bank * (RiverHalfWidth + 0.3f);
                batch.For(concrete).Box(new Vector3(0f, -2.1f, z), new Vector3(520f, 4.4f, 0.6f), Quaternion.identity);
                batch.For(darkMetal).Box(new Vector3(0f, 0.55f, z), new Vector3(520f, 0.06f, 0.06f), Quaternion.identity);
                for (float x = -250f; x < 250f; x += 2.5f)
                {
                    batch.For(darkMetal).Box(new Vector3(x, 0.3f, z), new Vector3(0.05f, 0.6f, 0.05f), Quaternion.identity);
                }
            }

            // Warehouses and depots along the far banks.
            for (int i = 0; i < 14; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float x = side * (20f + Hash(i, 1) * 140f);
                float z = RiverCentreZ + (Hash(i, 2) > 0.5f ? -1f : 1f) * (RiverHalfWidth + 14f + Hash(i, 3) * 40f);
                if (z > -40f) continue;
                Vector3 size = new Vector3(14f + Hash(i, 4) * 16f, 7f + Hash(i, 5) * 9f, 12f + Hash(i, 6) * 10f);
                Material facade = facades[i % facades.Length];
                Slab(root, "Warehouse", new Vector3(x, size.y * 0.5f - 0.1f, z), size, 0.08f, facade, false);
                Building(root, new Vector3(x, size.y * 0.5f - 0.1f, z), size, i + 40, false);
            }
        }

        /// <summary>Suburban houses on the hillsides, for scale.</summary>
        private void BuildHouses(Transform root, RunwayProfile runway, BatchedMeshes batch, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float x = side * (22f + Hash(i, 11) * 70f);
                float z = Mathf.Lerp(-45f, -175f, Hash(i, 12));
                float h = TerrainHeight(runway, x, z);
                float w = 7f + Hash(i, 13) * 3f, d = 8f + Hash(i, 14) * 4f, wall = 3.2f + Hash(i, 15) * 2.6f;
                Quaternion turn = Quaternion.Euler(0f, side > 0 ? -90f : 90f, 0f);
                Vector3 centre = new Vector3(x, h + wall * 0.5f - 0.4f, z);
                GameObject house = Slab(root, "House", centre, new Vector3(w, wall + 0.8f, d), 0.04f, facades[(i * 3 + 1) % facades.Length], false);
                house.transform.rotation = turn;
                // Pitched roof.
                var roof = new MeshKit();
                float ridge = wall * 0.5f + 0.4f + w * 0.32f;
                Vector3 a = new Vector3(-w * 0.55f, wall * 0.5f + 0.4f, -d * 0.52f), b = new Vector3(w * 0.55f, wall * 0.5f + 0.4f, -d * 0.52f);
                Vector3 c = new Vector3(0f, ridge, -d * 0.52f);
                Vector3 a2 = a + Vector3.forward * d * 1.04f, b2 = b + Vector3.forward * d * 1.04f, c2 = c + Vector3.forward * d * 1.04f;
                int ia = roof.Vertex(a, (Vector3.left + Vector3.up).normalized, new Vector2(0f, 0f));
                int ic = roof.Vertex(c, (Vector3.left + Vector3.up).normalized, new Vector2(0f, w * 0.6f));
                int ic2 = roof.Vertex(c2, (Vector3.left + Vector3.up).normalized, new Vector2(d, w * 0.6f));
                int ia2 = roof.Vertex(a2, (Vector3.left + Vector3.up).normalized, new Vector2(d, 0f));
                roof.Quad(ia, ic, ic2, ia2);
                int ib = roof.Vertex(b, (Vector3.right + Vector3.up).normalized, new Vector2(0f, 0f));
                int jc = roof.Vertex(c, (Vector3.right + Vector3.up).normalized, new Vector2(0f, w * 0.6f));
                int jc2 = roof.Vertex(c2, (Vector3.right + Vector3.up).normalized, new Vector2(d, w * 0.6f));
                int ib2 = roof.Vertex(b2, (Vector3.right + Vector3.up).normalized, new Vector2(d, 0f));
                roof.Quad(ib, jc, jc2, ib2);
                int ga = roof.Vertex(a, Vector3.back, Vector2.zero), gb = roof.Vertex(b, Vector3.back, Vector2.right), gc = roof.Vertex(c, Vector3.back, Vector2.up);
                roof.Triangle(ga, gb, gc);
                int ha = roof.Vertex(a2, Vector3.forward, Vector2.zero), hb = roof.Vertex(b2, Vector3.forward, Vector2.right), hc = roof.Vertex(c2, Vector3.forward, Vector2.up);
                roof.Triangle(ha, hb, hc);
                batch.For(awningMaterial).Append(roof.ToMesh("Roof"), Matrix4x4.TRS(centre, turn, Vector3.one));
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
