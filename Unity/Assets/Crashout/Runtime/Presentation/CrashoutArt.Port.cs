using System.Collections.Generic;
using Crashout.Simulation;
using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>
    /// The harbor board and its pier. A low-rise alternative to the downtown block, built so the
    /// junction stays visible from every seat round the table: nothing solid near the roads stands
    /// much above a car, the container stacks step up away from the main road, and the tall pieces
    /// (cranes, the lighthouse) are open frames or stand out on the pier. Containers, stalls, crates
    /// and drums are knock-down props; the pier, ferry and cranes replace the terrain runway arm.
    /// </summary>
    public sealed partial class CrashoutArt
    {
        /// <summary>The quay wall along the board's south side; open water lies beyond it.</summary>
        internal const float QuayEdgeZ = -18f;
        private const float HarborSurface = -0.33f;
        private const float PavementTop = 0.1f;
        private const float ContainerLength = 12.19f;
        private const float ContainerWidth = 2.44f;
        private const float ContainerHeight = 2.59f;
        private const float GateCentre = 23.6f;

        private Material[] containerPaints;
        private Material craneYellow, craneBlue, shedMetal, hullPaint, bootPaint, shipWhite, funnelPaint, deckPaint, tankPaint, timber;
        private Mesh containerMesh, drumMesh, stallMesh, stallAwningMesh, crateMesh;

        private void EnsurePortMaterials()
        {
            if (containerPaints != null) return;
            Color[] colours =
            {
                new Color(0.5f, 0.13f, 0.08f),   // oxide red
                new Color(0.07f, 0.2f, 0.4f),    // line blue
                new Color(0.1f, 0.3f, 0.22f),    // green
                new Color(0.78f, 0.36f, 0.06f),  // orange
                new Color(0.62f, 0.62f, 0.58f),  // weathered grey
                new Color(0.72f, 0.56f, 0.1f)    // ochre
            };
            containerPaints = new Material[colours.Length];
            for (int i = 0; i < colours.Length; i++) containerPaints[i] = Lit("Container paint " + i, colours[i], 0.15f, 0.32f);
            craneYellow = Lit("Crane yellow", new Color(0.86f, 0.58f, 0.06f), 0.2f, 0.4f);
            craneBlue = Lit("Crane blue", new Color(0.1f, 0.26f, 0.45f), 0.2f, 0.4f);
            shedMetal = Lit("Shed cladding", new Color(0.36f, 0.45f, 0.5f), 0.35f, 0.38f);
            hullPaint = Lit("Hull", new Color(0.04f, 0.07f, 0.13f), 0.1f, 0.45f);
            bootPaint = Lit("Boot topping", new Color(0.5f, 0.07f, 0.05f), 0.05f, 0.35f);
            shipWhite = Lit("Ship white", new Color(0.88f, 0.88f, 0.84f), 0.05f, 0.45f);
            funnelPaint = Lit("Funnel", new Color(0.85f, 0.32f, 0.05f), 0.1f, 0.4f);
            deckPaint = Lit("Deck paint", new Color(0.16f, 0.27f, 0.23f), 0.1f, 0.25f);
            tankPaint = Lit("Tank white", new Color(0.82f, 0.82f, 0.78f), 0.3f, 0.5f);
            timber = benchWood;
        }

        // ------------------------------------------------------------------ the board

        private void BuildPortBoard(Transform root)
        {
            EnsurePortMaterials();
            var detail = new BatchedMeshes();
            BuildHarborEdge(root, detail);
            BuildContainerYard(root, detail);
            BuildYardCrane(root, detail);
            BuildShed(root, detail);
            BuildMarket(root, detail);
            BuildFuelDepot(root, detail);
            PortGate(root, detail, Vector3.forward);
            PortGate(root, detail, Vector3.right);
            PortGate(root, detail, Vector3.left);

            // One lamp either side of each arm, as on the downtown board.
            for (int arm = 0; arm < 4; arm++)
            {
                Vector3 forward = arm == 0 ? Vector3.forward : arm == 1 ? Vector3.back : arm == 2 ? Vector3.right : Vector3.left;
                Vector3 side = Vector3.Cross(Vector3.up, forward);
                for (int s = -1; s <= 1; s += 2) Lamp(root, forward * 15.2f + side * (s * 5.9f), -side * s);
            }
            detail.Emit(root, "Port detail");
        }

        /// <summary>Water either side of the pier road, the quay wall's coping, bollards and fenders.</summary>
        private void BuildHarborEdge(Transform root, BatchedMeshes detail)
        {
            float reach = RunwayProfile.BoardHalfExtent;
            float bandDepth = reach + QuayEdgeZ;
            float bandCentreZ = (QuayEdgeZ - reach) * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                // The verge that carries the pier road across the water to the board edge.
                Slab(root, "Sidewalk", new Vector3(side * 5.75f, 0f, bandCentreZ), new Vector3(1.5f, 0.2f, bandDepth), 0.05f, paving, true);
                Slab(root, "Kerb", new Vector3(side * 5.1f, 0.03f, bandCentreZ), new Vector3(0.24f, 0.2f, bandDepth), 0.04f, concrete, false);

                float width = reach - 6.5f;
                float centreX = side * (6.5f + width * 0.5f);
                var surface = new MeshKit();
                surface.Panel(new Vector3(centreX, HarborSurface, bandCentreZ), Vector3.up, Vector3.forward, width, bandDepth);
                GameObject harbor = MeshKit.Spawn(root, "Harbor water", ScaleUv(surface.ToMesh("Harbor water"), width, bandDepth), water, false);
                harbor.GetComponent<MeshRenderer>().receiveShadows = true;
                harbor.AddComponent<WaterScroller>();
                // The seabed: anything knocked off the quay sinks to its sills instead of falling forever.
                GameObject seabed = new GameObject("Harbor bed");
                seabed.transform.SetParent(root, false);
                seabed.transform.position = new Vector3(centreX, -1f, bandCentreZ);
                seabed.AddComponent<BoxCollider>().size = new Vector3(width, 0.5f, bandDepth);

                detail.For(concrete).Box(new Vector3(centreX, 0.17f, QuayEdgeZ + 0.25f), new Vector3(width, 0.16f, 0.5f), Quaternion.identity);
                for (float x = 8.2f; x < reach - 1f; x += 3.9f)
                {
                    Vector3 foot = new Vector3(side * x, 0.25f, QuayEdgeZ + 0.3f);
                    detail.For(darkMetal).Cylinder(foot, foot + Vector3.up * 0.42f, 0.16f, 0.13f, 10);
                    detail.For(darkMetal).Cylinder(foot + Vector3.up * 0.42f, foot + Vector3.up * 0.52f, 0.22f, 0.2f, 10);
                    Vector3 fender = new Vector3(side * (x + 1.9f), -0.02f, QuayEdgeZ - 0.02f);
                    detail.For(rubber).Cylinder(fender, fender + Vector3.back * 0.26f, 0.34f, 0.34f, 14);
                }
            }
        }

        /// <summary>North-west: stacks that step up away from the main road, one high to three.</summary>
        private void BuildContainerYard(Transform root, BatchedMeshes detail)
        {
            float[] rowZ = { 12.5f, 15.3f, 18.5f, 21.3f, 24.3f };
            int[] rowHigh = { 1, 1, 2, 2, 3 };
            Quaternion alongX = Quaternion.Euler(0f, 90f, 0f);
            int paint = 0;
            for (int row = 0; row < rowZ.Length; row++)
            {
                for (int level = 0; level < rowHigh[row]; level++)
                {
                    Vector3 centre = new Vector3(-18f, PavementTop + ContainerHeight * (level + 0.5f), rowZ[row]);
                    Container(root, centre, alongX, paint++);
                }
                // Painted bay lines on the apron.
                detail.For(lanePaint, false).Box(new Vector3(-18f, PavementTop + 0.006f, rowZ[row] - 1.36f), new Vector3(13f, 0.01f, 0.1f), Quaternion.identity);
            }
            // Two set down beside the harbor road, within reach of a tumbling wreck.
            Container(root, new Vector3(-9.3f, PavementTop + ContainerHeight * 0.5f, 19.6f), Quaternion.Euler(0f, 4f, 0f), 4);
            Container(root, new Vector3(-8.6f, PavementTop + ContainerHeight * 0.5f, 9.2f), Quaternion.Euler(0f, 78f, 0f), 3, 6.06f);
        }

        /// <summary>A shipping container the wrecks can shove about and topple.</summary>
        private GameObject Container(Transform root, Vector3 centre, Quaternion rotation, int paint, float length = ContainerLength)
        {
            bool full = length > 10f;
            if (full && containerMesh == null) containerMesh = ContainerMesh(ContainerLength);
            Mesh mesh = full ? containerMesh : ContainerMesh(length);
            GameObject box = MeshKit.Spawn(root, "Container", mesh, containerPaints[paint % containerPaints.Length]);
            box.transform.SetPositionAndRotation(centre, rotation);
            box.AddComponent<BoxCollider>().size = new Vector3(ContainerWidth, ContainerHeight, length);
            box.AddComponent<Rigidbody>();
            box.AddComponent<BreakableProp>().Configure(StructureKind.Loose, full ? 2600f : 1700f, 150000f, Vector3.zero);
            return box;
        }

        /// <summary>Corrugated sides, corner posts, rails and door gear; long axis along z.</summary>
        private static Mesh ContainerMesh(float length)
        {
            var kit = new MeshKit();
            float halfW = ContainerWidth * 0.5f, halfH = ContainerHeight * 0.5f, halfL = length * 0.5f;
            kit.Box(Vector3.zero, new Vector3(ContainerWidth - 0.08f, ContainerHeight - 0.08f, length - 0.08f), Quaternion.identity);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    kit.Box(new Vector3(sx * (halfW - 0.07f), 0f, sz * (halfL - 0.07f)), new Vector3(0.14f, ContainerHeight, 0.14f), Quaternion.identity);
                }
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    kit.Box(new Vector3(sx * (halfW - 0.06f), sy * (halfH - 0.07f), 0f), new Vector3(0.12f, 0.14f, length), Quaternion.identity);
                }
                for (float z = -halfL + 0.36f; z < halfL - 0.3f; z += 0.3f)
                {
                    kit.Box(new Vector3(sx * (halfW - 0.02f), 0f, z), new Vector3(0.05f, ContainerHeight - 0.34f, 0.15f), Quaternion.identity);
                }
            }
            for (float x = -halfW + 0.3f; x < halfW - 0.2f; x += 0.3f)
            {
                kit.Box(new Vector3(x, 0f, -halfL + 0.02f), new Vector3(0.15f, ContainerHeight - 0.34f, 0.05f), Quaternion.identity);
            }
            // Door end: a centre seam and four lock rods.
            kit.Box(new Vector3(0f, 0f, halfL - 0.01f), new Vector3(0.05f, ContainerHeight - 0.3f, 0.04f), Quaternion.identity);
            for (int rod = 0; rod < 4; rod++)
            {
                float x = -0.9f + rod * 0.6f;
                kit.Cylinder(new Vector3(x, -halfH + 0.12f, halfL + 0.02f), new Vector3(x, halfH - 0.12f, halfL + 0.02f), 0.025f, 0.025f, 6);
                kit.Box(new Vector3(x + 0.08f, -0.2f, halfL + 0.03f), new Vector3(0.2f, 0.06f, 0.04f), Quaternion.identity);
            }
            return kit.ToMesh("Container");
        }

        /// <summary>North-east: an open portal crane reaching out over the harbor road with a box on the hook.</summary>
        private void BuildYardCrane(Transform root, BatchedMeshes detail)
        {
            const float height = 20f, nearX = 9f, farX = 21f, z = 13f, halfZ = 3f;
            MeshKit frame = detail.For(craneYellow);
            foreach (float x in new[] { nearX, farX })
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 foot = new Vector3(x, PavementTop, z + sz * halfZ);
                    frame.Box(foot + Vector3.up * (height * 0.5f + 0.9f), new Vector3(0.8f, height - 1.8f, 0.8f), Quaternion.identity);
                    detail.For(darkMetal).Box(foot + Vector3.up * 0.55f, new Vector3(1.1f, 0.9f, 2.4f), Quaternion.identity);
                    detail.For(hazard).Box(foot + Vector3.up * 1.7f, new Vector3(0.84f, 1.2f, 0.84f), Quaternion.identity);

                    GameObject leg = new GameObject("Crane leg collider");
                    leg.transform.SetParent(root, false);
                    leg.transform.position = foot + Vector3.up * 3f;
                    leg.AddComponent<BoxCollider>().size = new Vector3(1.1f, 6f, 2.4f);
                }
                frame.Box(new Vector3(x, PavementTop + 1.5f, z), new Vector3(0.55f, 0.55f, halfZ * 2f), Quaternion.identity);
                frame.Box(new Vector3(x, PavementTop + height, z), new Vector3(0.9f, 0.9f, halfZ * 2f + 0.8f), Quaternion.identity);
                frame.Cylinder(new Vector3(x, PavementTop + 1.6f, z - halfZ), new Vector3(x, PavementTop + height - 0.4f, z + halfZ), 0.14f, 0.14f, 8, false);
            }
            const float girderFrom = -1.5f, girderTo = 25f;
            float girderY = PavementTop + height + 0.9f;
            for (int sz = -1; sz <= 1; sz += 2)
            {
                frame.Box(new Vector3((girderFrom + girderTo) * 0.5f, girderY, z + sz * halfZ), new Vector3(girderTo - girderFrom, 1.2f, 0.8f), Quaternion.identity);
                // Walkway rail along each girder.
                detail.For(steel).Box(new Vector3((girderFrom + girderTo) * 0.5f, girderY + 1.3f, z + sz * (halfZ + 0.45f)), new Vector3(girderTo - girderFrom, 0.05f, 0.05f), Quaternion.identity);
            }
            detail.For(shipWhite).Box(new Vector3(farX, girderY + 2f, z), new Vector3(4.2f, 2.6f, 5f), Quaternion.identity);
            detail.For(signalRed, false).Blob(new Vector3(farX, girderY + 3.6f, z), Vector3.one * 0.2f, 1, 0f, 5);

            // Trolley, cab and a container hanging over the road.
            const float trolleyX = 2.4f;
            detail.For(darkMetal).Box(new Vector3(trolleyX, girderY - 0.2f, z), new Vector3(2.8f, 0.9f, halfZ * 2f - 1f), Quaternion.identity);
            detail.For(shipWhite).Box(new Vector3(trolleyX + 2.3f, girderY - 1.8f, z), new Vector3(1.6f, 1.9f, 1.7f), Quaternion.identity);
            detail.For(windowGlass).Box(new Vector3(trolleyX + 2.3f, girderY - 1.7f, z), new Vector3(1.64f, 0.8f, 1.74f), Quaternion.identity);
            const float hookY = 12.4f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 top = new Vector3(trolleyX + sx * 0.9f, girderY - 0.6f, z + sz * 1.6f);
                    detail.For(steel).Cylinder(top, new Vector3(top.x, hookY, z + sz * 4.6f), 0.03f, 0.03f, 5, false);
                }
            }
            detail.For(craneYellow).Box(new Vector3(trolleyX, hookY - 0.15f, z), new Vector3(2.5f, 0.3f, ContainerLength + 0.1f), Quaternion.identity);
            if (containerMesh == null) containerMesh = ContainerMesh(ContainerLength);
            GameObject load = MeshKit.Spawn(root, "Hanging container", containerMesh, containerPaints[1]);
            load.transform.position = new Vector3(trolleyX, hookY - 0.3f - ContainerHeight * 0.5f, z);
        }

        /// <summary>A low transit shed in the far corner, well back from both roads.</summary>
        private void BuildShed(Transform root, BatchedMeshes detail)
        {
            Vector3 size = new Vector3(11f, 4.2f, 6.5f);
            Vector3 centre = new Vector3(19.5f, PavementTop + size.y * 0.5f, 22f);
            Structure(Slab(root, "Shed", centre, size, 0.05f, shedMetal, true), shedMetal);
            float eaves = PavementTop + size.y;
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Slab(root, "Shed roof", new Vector3(centre.x, eaves + 0.55f, centre.z + sz * 1.68f), new Vector3(size.x + 0.5f, 0.12f, 3.7f), 0.02f, roofMaterial, false, new Vector3(sz * 19f, 0f, 0f));
            }
            for (int sx = -1; sx <= 1; sx += 2)
            {
                detail.For(shedMetal).Box(new Vector3(centre.x + sx * (size.x * 0.5f - 0.05f), eaves + 0.45f, centre.z), new Vector3(0.1f, 1f, 3.2f), Quaternion.identity);
            }
            detail.For(steel).Box(new Vector3(centre.x, eaves + 1.2f, centre.z), new Vector3(size.x + 0.5f, 0.14f, 0.3f), Quaternion.identity);
            for (int vent = -1; vent <= 1; vent++)
            {
                detail.For(steel).Cylinder(new Vector3(centre.x + vent * 3.2f, eaves + 1.2f, centre.z), new Vector3(centre.x + vent * 3.2f, eaves + 1.7f, centre.z), 0.35f, 0.42f, 10);
            }
            // Cladding ribs and two roller doors on the face that looks at the junction.
            float southFace = centre.z - size.z * 0.5f;
            for (float x = centre.x - size.x * 0.5f + 0.3f; x < centre.x + size.x * 0.5f; x += 0.55f)
            {
                detail.For(shedMetal).Box(new Vector3(x, centre.y, southFace - 0.03f), new Vector3(0.12f, size.y - 0.1f, 0.06f), Quaternion.identity);
            }
            float westFace = centre.x - size.x * 0.5f;
            for (float zz = southFace + 0.3f; zz < centre.z + size.z * 0.5f; zz += 0.55f)
            {
                detail.For(shedMetal).Box(new Vector3(westFace - 0.03f, centre.y, zz), new Vector3(0.06f, size.y - 0.1f, 0.12f), Quaternion.identity);
            }
            for (int door = -1; door <= 1; door += 2)
            {
                Vector3 at = new Vector3(centre.x + door * 2.6f, PavementTop + 1.7f, southFace - 0.08f);
                detail.For(darkMetal).Box(at, new Vector3(3.4f, 3.4f, 0.08f), Quaternion.identity);
                detail.For(hazard).Box(at + Vector3.up * 1.85f, new Vector3(3.6f, 0.3f, 0.1f), Quaternion.identity);
            }
        }

        /// <summary>South-west: market stalls on the quay edge, an open plaza by the road, and a tug alongside.</summary>
        private void BuildMarket(Transform root, BatchedMeshes detail)
        {
            if (stallMesh == null)
            {
                var frame = new MeshKit();
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2) frame.Box(new Vector3(sx * 1.15f, 1.2f, sz * 1f), new Vector3(0.09f, 2.4f, 0.09f), Quaternion.identity);
                    frame.Box(new Vector3(sx * 1.15f, 2.38f, 0f), new Vector3(0.07f, 0.07f, 2.1f), Quaternion.identity);
                }
                frame.Box(new Vector3(0f, 0.45f, 0.85f), new Vector3(2.4f, 0.9f, 0.6f), Quaternion.identity);
                frame.Box(new Vector3(0f, 0.92f, 0.85f), new Vector3(2.5f, 0.05f, 0.7f), Quaternion.identity);
                frame.Box(new Vector3(0f, 1.1f, -0.95f), new Vector3(2.3f, 2.1f, 0.06f), Quaternion.identity);
                for (int crate = -1; crate <= 1; crate++) frame.Box(new Vector3(crate * 0.7f, 1.08f, 0.85f), new Vector3(0.5f, 0.26f, 0.4f), Quaternion.Euler(12f, 0f, 0f));
                stallMesh = frame.ToMesh("Market stall");
                var awning = new MeshKit();
                awning.Box(new Vector3(0f, 2.55f, 0.25f), new Vector3(2.9f, 0.05f, 3.1f), Quaternion.Euler(8f, 0f, 0f));
                awning.Box(new Vector3(0f, 2.2f, 1.78f), new Vector3(2.9f, 0.28f, 0.04f), Quaternion.identity);
                stallAwningMesh = awning.ToMesh("Stall awning");
                crateMesh = ProceduralMeshes.RoundedBox(new Vector3(0.62f, 0.5f, 0.46f), 0.03f, 2);
            }
            int[] awningPaint = { 0, 5, 2, 3, 1 };
            for (int i = 0; i < 5; i++)
            {
                GameObject stall = MeshKit.Spawn(root, "Market stall", stallMesh, timber);
                stall.transform.position = new Vector3(-8.6f - i * 3.5f, PavementTop, -14.9f);
                MeshKit.Spawn(stall.transform, "Awning", stallAwningMesh, containerPaints[awningPaint[i]]);
                BoxCollider box = stall.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 1.25f, 0f);
                box.size = new Vector3(2.5f, 2.5f, 2.3f);
                stall.AddComponent<Rigidbody>();
                stall.AddComponent<BreakableProp>().Configure(StructureKind.Loose, 170f, 16000f, new Vector3(0f, 0.6f, 0f));
            }
            Vector3[] crates =
            {
                new Vector3(-7.4f, 0.35f, -12.3f), new Vector3(-8.1f, 0.35f, -12.2f), new Vector3(-7.7f, 0.85f, -12.25f),
                new Vector3(-13.6f, 0.35f, -12.6f), new Vector3(-14.3f, 0.35f, -12.4f), new Vector3(-20.4f, 0.35f, -12.5f),
                new Vector3(-21.1f, 0.35f, -12.7f), new Vector3(-20.7f, 0.85f, -12.6f)
            };
            for (int i = 0; i < crates.Length; i++)
            {
                GameObject crate = MeshKit.Spawn(root, "Crate", crateMesh, timber);
                crate.transform.SetPositionAndRotation(crates[i], Quaternion.Euler(0f, i * 23f, 0f));
                crate.AddComponent<BoxCollider>().size = new Vector3(0.62f, 0.5f, 0.46f);
                crate.AddComponent<Rigidbody>();
                crate.AddComponent<BreakableProp>().Configure(StructureKind.Loose, 18f, 0f, Vector3.zero);
            }

            // The tug, moored against the quay.
            Vector3 hull = new Vector3(-15f, HarborSurface + 0.4f, -22.3f);
            detail.For(hullPaint).Box(hull, new Vector3(8.6f, 1.5f, 3.1f), Quaternion.identity);
            detail.For(hullPaint).Box(hull + new Vector3(4.7f, 0.1f, 0f), new Vector3(2.2f, 1.3f, 2.2f), Quaternion.Euler(0f, 45f, 0f));
            detail.For(bootPaint).Box(hull + Vector3.up * 0.72f, new Vector3(8.7f, 0.14f, 3.2f), Quaternion.identity);
            detail.For(deckPaint).Box(hull + Vector3.up * 0.77f, new Vector3(8.4f, 0.04f, 2.9f), Quaternion.identity);
            detail.For(shipWhite).Box(hull + new Vector3(-0.6f, 1.65f, 0f), new Vector3(3.2f, 1.8f, 2.2f), Quaternion.identity);
            detail.For(windowGlass).Box(hull + new Vector3(-0.6f, 2f, 0f), new Vector3(3.24f, 0.6f, 2.24f), Quaternion.identity);
            detail.For(shipWhite).Box(hull + new Vector3(-0.6f, 2.6f, 0f), new Vector3(3.5f, 0.1f, 2.5f), Quaternion.identity);
            detail.For(funnelPaint).Cylinder(hull + new Vector3(-2.9f, 0.75f, 0f), hull + new Vector3(-2.9f, 2.9f, 0f), 0.42f, 0.36f, 12);
            detail.For(darkMetal).Cylinder(hull + new Vector3(-2.9f, 2.9f, 0f), hull + new Vector3(-2.9f, 3.2f, 0f), 0.37f, 0.37f, 12);
            detail.For(steel).Cylinder(hull + new Vector3(0.4f, 2.65f, 0f), hull + new Vector3(0.4f, 4.4f, 0f), 0.05f, 0.03f, 6);
            for (float x = -3.4f; x <= 3.5f; x += 1.7f)
            {
                Vector3 tyre = hull + new Vector3(x, 0.45f, 1.56f);
                detail.For(rubber).Cylinder(tyre, tyre + Vector3.forward * 0.22f, 0.36f, 0.36f, 12);
            }
            GameObject tug = new GameObject("Tug collider");
            tug.transform.SetParent(root, false);
            tug.transform.position = hull + Vector3.up * 0.6f;
            tug.AddComponent<BoxCollider>().size = new Vector3(8.6f, 2.8f, 3.1f);
        }

        /// <summary>South-east: two low tanks inside a bund, loose drums on the plaza and a harbor beacon.</summary>
        private void BuildFuelDepot(Transform root, BatchedMeshes detail)
        {
            const float radius = 2.5f, shell = 3f, tankZ = -13.4f;
            float[] tankX = { 13.2f, 19.8f };
            foreach (float x in tankX)
            {
                var tank = new MeshKit();
                Vector3 foot = new Vector3(x, PavementTop, tankZ);
                tank.Cylinder(foot, foot + Vector3.up * shell, radius, radius, 28);
                tank.Cylinder(foot + Vector3.up * shell, foot + Vector3.up * (shell + 0.35f), radius, radius * 0.55f, 28);
                GameObject body = MeshKit.Spawn(root, "Fuel tank", tank.ToMesh("Fuel tank"), tankPaint, true, true);
                body.isStatic = true;
                detail.For(bootPaint).Cylinder(foot + Vector3.up * 1.5f, foot + Vector3.up * 1.95f, radius + 0.02f, radius + 0.02f, 28, false);
                detail.For(steel).Cylinder(foot + Vector3.up * (shell + 0.35f), foot + Vector3.up * (shell + 0.7f), 0.2f, 0.2f, 8);
                // Access ladder with a cage, on the side facing the road.
                detail.For(darkMetal).Box(foot + new Vector3(0f, shell * 0.5f, radius + 0.08f), new Vector3(0.5f, shell, 0.06f), Quaternion.identity);
                detail.For(hazard).Box(foot + new Vector3(1.4f, 2.4f, radius * 0.86f), new Vector3(0.8f, 0.8f, 0.04f), Quaternion.Euler(0f, 28f, 45f));
            }
            // Pipework between the tanks and out to the quay.
            detail.For(steel).Cylinder(new Vector3(tankX[0], 0.55f, tankZ - radius - 0.4f), new Vector3(tankX[1], 0.55f, tankZ - radius - 0.4f), 0.14f, 0.14f, 10);
            foreach (float x in tankX)
            {
                detail.For(steel).Cylinder(new Vector3(x, 0.55f, tankZ - radius + 0.1f), new Vector3(x, 0.55f, tankZ - radius - 0.4f), 0.14f, 0.14f, 10);
                detail.For(bootPaint).Cylinder(new Vector3(x, 0.55f, tankZ - radius - 0.1f), new Vector3(x, 0.95f, tankZ - radius - 0.1f), 0.05f, 0.05f, 6);
                detail.For(bootPaint).Cylinder(new Vector3(x, 0.95f, tankZ - radius - 0.1f), new Vector3(x, 1f, tankZ - radius - 0.1f), 0.22f, 0.22f, 10);
            }
            // The bund: a low wall round both tanks.
            const float west = 9.8f, east = 23.2f, south = -17f, north = -10.2f, wall = 0.55f;
            float midX = (west + east) * 0.5f, midZ = (south + north) * 0.5f;
            Slab(root, "Bund wall", new Vector3(midX, PavementTop + wall * 0.5f, north), new Vector3(east - west, wall, 0.25f), 0.03f, concrete, true);
            Slab(root, "Bund wall", new Vector3(midX, PavementTop + wall * 0.5f, south), new Vector3(east - west, wall, 0.25f), 0.03f, concrete, true);
            Slab(root, "Bund wall", new Vector3(west, PavementTop + wall * 0.5f, midZ), new Vector3(0.25f, wall, north - south), 0.03f, concrete, true);
            Slab(root, "Bund wall", new Vector3(east, PavementTop + wall * 0.5f, midZ), new Vector3(0.25f, wall, north - south), 0.03f, concrete, true);

            if (drumMesh == null)
            {
                var drum = new MeshKit();
                drum.Cylinder(Vector3.zero, Vector3.up * 0.88f, 0.29f, 0.29f, 14);
                drum.Cylinder(Vector3.up * 0.28f, Vector3.up * 0.31f, 0.3f, 0.3f, 14, false);
                drum.Cylinder(Vector3.up * 0.58f, Vector3.up * 0.61f, 0.3f, 0.3f, 14, false);
                drumMesh = drum.ToMesh("Drum");
            }
            Vector3[] drums = { new Vector3(10.2f, 0.1f, -8.6f), new Vector3(10.85f, 0.1f, -8.7f), new Vector3(10.5f, 0.1f, -9.25f), new Vector3(11.5f, 0.1f, -8.5f), new Vector3(16.4f, 0.1f, -8.8f), new Vector3(17.05f, 0.1f, -8.9f) };
            for (int i = 0; i < drums.Length; i++)
            {
                GameObject drum = MeshKit.Spawn(root, "Drum", drumMesh, containerPaints[i % 2 == 0 ? 1 : 0]);
                drum.transform.position = drums[i];
                CapsuleCollider capsule = drum.AddComponent<CapsuleCollider>();
                capsule.radius = 0.29f;
                capsule.height = 0.88f;
                capsule.center = new Vector3(0f, 0.44f, 0f);
                drum.AddComponent<Rigidbody>();
                drum.AddComponent<BreakableProp>().Configure(StructureKind.Plastic, 28f, 0f, new Vector3(0f, 0.36f, 0f));
            }

            // A short beacon in the corner; the lighthouse itself stands out on the pier.
            Vector3 beacon = new Vector3(24.5f, PavementTop, -8f);
            detail.For(shipWhite).Cylinder(beacon, beacon + Vector3.up * 2.6f, 0.42f, 0.3f, 14);
            detail.For(deckPaint).Cylinder(beacon + Vector3.up * 1f, beacon + Vector3.up * 1.5f, 0.4f, 0.37f, 14, false);
            detail.For(signalGreen, false).Cylinder(beacon + Vector3.up * 2.6f, beacon + Vector3.up * 3f, 0.24f, 0.24f, 12);
            detail.For(darkMetal).Cylinder(beacon + Vector3.up * 3f, beacon + Vector3.up * 3.25f, 0.3f, 0.04f, 12);
        }

        /// <summary>
        /// A customs post where a road leaves the board: a one-storey booth either side and barrier
        /// arms raised clear. Nothing spans the road; traffic drives off the board edge and vanishes
        /// there, as through an invisible portal.
        /// </summary>
        private void PortGate(Transform root, BatchedMeshes detail, Vector3 outward)
        {
            Vector3 side = Vector3.Cross(Vector3.up, outward);
            Quaternion turn = Quaternion.LookRotation(outward, Vector3.up);
            Vector3 euler = turn.eulerAngles;
            Vector3 middle = outward * GateCentre;
            const float boothHeight = 3f;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 at = middle + side * (s * 7.6f) + Vector3.up * (PavementTop + boothHeight * 0.5f);
                Structure(Slab(root, "Gatehouse", at, new Vector3(2.6f, boothHeight, 3.6f), 0.05f, shipWhite, true, euler), shipWhite);
                detail.For(windowGlass).Box(at + side * (-s * 1.31f) + Vector3.up * 0.35f, new Vector3(0.04f, 1.1f, 2.6f), turn);
                detail.For(roofMaterial).Box(at + Vector3.up * (boothHeight * 0.5f + 0.12f), new Vector3(3.2f, 0.24f, 4.2f), turn);
                detail.For(hazard).Box(at + Vector3.down * (boothHeight * 0.5f - 0.25f), new Vector3(2.66f, 0.5f, 3.66f), turn);
                detail.For(steel).Box(at + Vector3.up * (boothHeight * 0.5f + 0.5f), new Vector3(1.2f, 0.55f, 1f), turn);

                // A barrier arm, raised upright so it never crosses the road.
                Vector3 pivot = middle + side * (s * 5.6f) + outward * -1.4f + Vector3.up * PavementTop;
                detail.For(darkMetal).Box(pivot + Vector3.up * 0.5f, new Vector3(0.4f, 1f, 0.4f), turn);
                detail.For(hazard).Box(pivot + Vector3.up * 2.9f, new Vector3(0.14f, 3.8f, 0.14f), turn);
                detail.For(s < 0 ? signalGreen : signalRed, false).Box(pivot + Vector3.up * 1.15f + outward * -0.22f, new Vector3(0.26f, 0.26f, 0.06f), turn);
            }
        }

        // ------------------------------------------------------------------ the pier

        private const float PierHalfWidth = 8f;
        private const float PierHeadZ = -98f;

        /// <summary>
        /// The harbor's panhandle: water in place of terrain, a pier as wide as the road and its
        /// aprons, a car ferry at the far end with the start line on its deck, the linkspan down to
        /// the pier, two ship-to-shore cranes to drive under, the bridge towers and the lighthouse.
        /// </summary>
        private void BuildPier(Transform root, RunwayProfile runway)
        {
            EnsurePortMaterials();
            var batch = new BatchedMeshes();
            float edge = RunwayProfile.BoardEdgeZ;
            float sternZ = runway.FarZ - 4f;
            float farZ = sternZ - 4f;
            float length = edge - farZ;
            float midZ = (edge + farZ) * 0.5f;

            // A block of water the width of the other arms, cut square to show its depth.
            var surface = new MeshKit();
            surface.Panel(new Vector3(0f, HarborSurface, midZ), Vector3.up, Vector3.forward, ArmHalfWidth * 2f, length);
            GameObject sea = MeshKit.Spawn(root, "Pier water", ScaleUv(surface.ToMesh("Pier water"), ArmHalfWidth * 2f, length), water, false);
            sea.GetComponent<MeshRenderer>().receiveShadows = true;
            sea.AddComponent<WaterScroller>();
            var cut = new MeshKit();
            float depth = HarborSurface - DioramaBase;
            for (int side = -1; side <= 1; side += 2)
            {
                cut.Panel(new Vector3(side * ArmHalfWidth, (HarborSurface + DioramaBase) * 0.5f, midZ), Vector3.right * side, Vector3.up, length, depth);
            }
            cut.Panel(new Vector3(0f, (HarborSurface + DioramaBase) * 0.5f, farZ), Vector3.back, Vector3.up, ArmHalfWidth * 2f, depth);
            MeshKit.Spawn(root, "Pier water cut", ScaleUv(cut.ToMesh("Pier water cut"), 6f, 1f), water, false);
            Slab(root, "Arm plinth", new Vector3(0f, (PlinthTop + DioramaBase) * 0.5f, midZ - 0.3f),
                new Vector3(ArmHalfWidth * 2f + 1.2f, PlinthTop - DioramaBase, length + 0.6f), 0.4f, darkMetal, false);
            GameObject bed = new GameObject("Pier seabed");
            bed.transform.SetParent(root, false);
            bed.transform.position = new Vector3(0f, -1.1f, midZ);
            bed.AddComponent<BoxCollider>().size = new Vector3(ArmHalfWidth * 2f, 0.5f, length);

            // Concrete aprons either side of the road, on timber fender piles.
            float apronLength = edge - PierHeadZ;
            float apronZ = (edge + PierHeadZ) * 0.5f;
            float apronWidth = PierHalfWidth - KerbOuter;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (KerbOuter + apronWidth * 0.5f);
                batch.For(concrete).Box(new Vector3(x, -0.4f, apronZ), new Vector3(apronWidth, 1.1f, apronLength), Quaternion.identity);
                for (float z = edge - 3f; z > PierHeadZ; z -= 4f)
                {
                    Vector3 pile = new Vector3(side * (PierHalfWidth + 0.2f), HarborSurface - 0.5f, z);
                    batch.For(timber).Cylinder(pile, new Vector3(pile.x, 0.5f, z), 0.2f, 0.18f, 8);
                    if (Mathf.Repeat(z, 8f) < 4f)
                    {
                        Vector3 foot = new Vector3(side * (PierHalfWidth - 0.6f), 0.15f, z + 1.4f);
                        batch.For(darkMetal).Cylinder(foot, foot + Vector3.up * 0.4f, 0.16f, 0.13f, 10);
                        batch.For(darkMetal).Cylinder(foot + Vector3.up * 0.4f, foot + Vector3.up * 0.5f, 0.22f, 0.2f, 10);
                    }
                }
            }
            batch.For(concrete).Box(new Vector3(0f, -0.4f, PierHeadZ - 0.25f), new Vector3(PierHalfWidth * 2f, 1.1f, 0.5f), Quaternion.identity);

            BuildFerry(batch, runway, sternZ);
            BuildLinkspan(batch, runway);
            ShipCrane(root, batch, -74f, true);
            ShipCrane(root, batch, -86f, false);
            if (runway.HasRamp) BuildBridgeTowers(batch, runway);
            BuildLighthouse(batch, new Vector3(-(PierHalfWidth - 0.6f), 0.15f, -40f));
            batch.Emit(root, "Pier detail");
        }

        /// <summary>A roll-on roll-off ferry, bow to the linkspan, with the road running down its car deck.</summary>
        private void BuildFerry(BatchedMeshes batch, RunwayProfile runway, float sternZ)
        {
            float deckY = RunwayProfile.RoadSurfaceY + runway.StartHeight - 0.14f;
            float bowZ = runway.StartZ + 9f;
            float hullLength = bowZ - sternZ;
            float hullZ = (bowZ + sternZ) * 0.5f;
            const float beam = 18f;
            float keel = HarborSurface - 0.6f;
            batch.For(hullPaint).Box(new Vector3(0f, (deckY + keel) * 0.5f, hullZ), new Vector3(beam, deckY - keel, hullLength), Quaternion.identity);
            batch.For(hullPaint).Box(new Vector3(0f, (deckY + keel) * 0.5f, bowZ + 0.9f), new Vector3(beam - 5f, deckY - keel, 2.6f), Quaternion.identity);
            batch.For(bootPaint).Box(new Vector3(0f, HarborSurface + 0.45f, hullZ), new Vector3(beam + 0.08f, 1.1f, hullLength + 0.08f), Quaternion.identity);
            batch.For(shipWhite).Box(new Vector3(0f, deckY - 0.9f, hullZ), new Vector3(beam + 0.1f, 1.5f, hullLength + 0.1f), Quaternion.identity);
            batch.For(deckPaint).Box(new Vector3(0f, deckY + 0.02f, hullZ), new Vector3(beam - 0.4f, 0.05f, hullLength - 0.4f), Quaternion.identity);
            for (int side = -1; side <= 1; side += 2)
            {
                // Bulwarks, and a row of portholes along the hull.
                batch.For(shipWhite).Box(new Vector3(side * (beam * 0.5f - 0.15f), deckY + 0.65f, hullZ), new Vector3(0.3f, 1.3f, hullLength), Quaternion.identity);
                for (float z = sternZ + 3f; z < bowZ - 2f; z += 3.2f)
                {
                    batch.For(windowGlass).Box(new Vector3(side * (beam * 0.5f + 0.06f), deckY - 3.2f, z), new Vector3(0.04f, 0.55f, 1.1f), Quaternion.identity);
                }
                // Orange lifeboats in davits.
                batch.For(funnelPaint).Cylinder(new Vector3(side * (beam * 0.5f + 0.5f), deckY + 5.2f, sternZ + 5f), new Vector3(side * (beam * 0.5f + 0.5f), deckY + 5.2f, sternZ + 10f), 0.75f, 0.75f, 10);
            }

            // The accommodation block bridges the car deck aft; the road runs out of it like a garage.
            float blockFront = sternZ + 14f;
            float blockZ = (sternZ + blockFront) * 0.5f;
            const float headroom = 4.6f, blockDepth = 14f;
            for (int side = -1; side <= 1; side += 2)
            {
                batch.For(shipWhite).Box(new Vector3(side * 7.6f, deckY + headroom * 0.5f, blockZ), new Vector3(2.8f, headroom, blockDepth), Quaternion.identity);
            }
            batch.For(shipWhite).Box(new Vector3(0f, deckY + headroom + 1.5f, blockZ), new Vector3(beam, 3f, blockDepth), Quaternion.identity);
            batch.For(windowGlass).Box(new Vector3(0f, deckY + headroom + 1.7f, blockZ), new Vector3(beam + 0.06f, 0.8f, blockDepth - 1.2f), Quaternion.identity);
            batch.For(hazard).Box(new Vector3(0f, deckY + headroom - 0.25f, blockFront + 0.06f), new Vector3(12.4f, 0.5f, 0.1f), Quaternion.identity);
            float bridgeY = deckY + headroom + 3f;
            batch.For(shipWhite).Box(new Vector3(0f, bridgeY + 1.3f, blockZ + 1.5f), new Vector3(13f, 2.6f, 6f), Quaternion.identity);
            batch.For(windowGlass).Box(new Vector3(0f, bridgeY + 1.6f, blockZ + 1.5f), new Vector3(13.06f, 0.9f, 6.06f), Quaternion.identity);
            batch.For(shipWhite).Box(new Vector3(0f, bridgeY + 2.7f, blockZ + 1.5f), new Vector3(13.6f, 0.14f, 6.6f), Quaternion.identity);
            Vector3 funnel = new Vector3(0f, bridgeY, blockZ - 4f);
            batch.For(funnelPaint).Cylinder(funnel, funnel + Vector3.up * 4.4f, 1.5f, 1.25f, 16);
            batch.For(darkMetal).Cylinder(funnel + Vector3.up * 4.4f, funnel + Vector3.up * 5f, 1.27f, 1.2f, 16);
            batch.For(steel).Cylinder(new Vector3(0f, bridgeY + 2.7f, blockZ + 2.5f), new Vector3(0f, bridgeY + 6.5f, blockZ + 2.5f), 0.09f, 0.05f, 6);
            batch.For(steel).Box(new Vector3(0f, bridgeY + 5.4f, blockZ + 2.5f), new Vector3(2.4f, 0.06f, 0.06f), Quaternion.identity);
            // The bow door, folded open either side of the linkspan.
            for (int side = -1; side <= 1; side += 2)
            {
                batch.For(hazard).Box(new Vector3(side * 6.6f, deckY + 1.6f, bowZ + 2f), new Vector3(0.3f, 3.2f, 3f), Quaternion.Euler(0f, side * 24f, 0f));
            }
        }

        /// <summary>The lifting towers that carry the linkspan between the ferry's bow and the pier.</summary>
        private void BuildLinkspan(BatchedMeshes batch, RunwayProfile runway)
        {
            float z = (runway.StartZ + 8f + PierHeadZ) * 0.5f;
            float deck = RunwayProfile.RoadSurfaceY + runway.HeightAt(z);
            float top = deck + 7f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 7f;
                batch.For(craneBlue).Box(new Vector3(x, (top + HarborSurface - 0.6f) * 0.5f, z), new Vector3(0.9f, top - HarborSurface + 0.6f, 0.9f), Quaternion.identity);
                batch.For(concrete).Cylinder(new Vector3(x, HarborSurface - 0.6f, z), new Vector3(x, HarborSurface + 0.5f, z), 1.1f, 1.1f, 14);
                batch.For(hazard).Box(new Vector3(x, deck + 1.2f, z), new Vector3(0.94f, 1.2f, 0.94f), Quaternion.identity);
                for (int along = -1; along <= 1; along += 2)
                {
                    float az = z + along * 6f;
                    Vector3 anchor = new Vector3(side * 5.6f, RunwayProfile.RoadSurfaceY + runway.HeightAt(az) + 0.2f, az);
                    batch.For(steel).Cylinder(new Vector3(x, top - 0.3f, z), anchor, 0.05f, 0.05f, 6, false);
                }
            }
            batch.For(craneBlue).Box(new Vector3(0f, top, z), new Vector3(15f, 0.8f, 0.9f), Quaternion.identity);
            batch.For(signalRed, false).Blob(new Vector3(0f, top + 0.6f, z), Vector3.one * 0.18f, 1, 0f, 9);
        }

        /// <summary>A ship-to-shore gantry astride the pier: the player drives between its legs.</summary>
        private void ShipCrane(Transform root, BatchedMeshes batch, float z, bool loaded)
        {
            const float height = 17f, legX = 7.25f, legZ = 2.4f;
            MeshKit frame = batch.For(craneBlue);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 foot = new Vector3(sx * legX, 0.15f, z + sz * legZ);
                    frame.Box(foot + Vector3.up * (height * 0.5f), new Vector3(0.7f, height, 0.7f), Quaternion.identity);
                    batch.For(hazard).Box(foot + Vector3.up * 0.9f, new Vector3(0.74f, 1.4f, 0.74f), Quaternion.identity);
                }
                frame.Box(new Vector3(sx * legX, 1.9f, z), new Vector3(0.5f, 0.5f, legZ * 2f), Quaternion.identity);
                frame.Box(new Vector3(sx * legX, 9f, z), new Vector3(0.5f, 0.5f, legZ * 2f), Quaternion.identity);
                frame.Cylinder(new Vector3(sx * legX, 2.1f, z - legZ), new Vector3(sx * legX, 8.8f, z + legZ), 0.11f, 0.11f, 8, false);
            }
            const float boomFrom = -9.5f, boomTo = 22f;
            float boomY = height + 0.6f;
            for (int sz = -1; sz <= 1; sz += 2)
            {
                frame.Box(new Vector3(0f, height + 0.1f, z + sz * legZ), new Vector3(legX * 2f + 0.7f, 0.7f, 0.7f), Quaternion.identity);
                frame.Box(new Vector3((boomFrom + boomTo) * 0.5f, boomY + 0.55f, z + sz * 1.5f), new Vector3(boomTo - boomFrom, 0.9f, 0.5f), Quaternion.identity);
            }
            // A-frame and stays.
            Vector3 apex = new Vector3(-2.5f, height + 9f, z);
            for (int sz = -1; sz <= 1; sz += 2)
            {
                frame.Cylinder(new Vector3(-7f, boomY + 1f, z + sz * 1.5f), apex, 0.2f, 0.16f, 8);
                frame.Cylinder(new Vector3(2.5f, boomY + 1f, z + sz * 1.5f), apex, 0.2f, 0.16f, 8);
                batch.For(steel).Cylinder(apex, new Vector3(boomTo - 1f, boomY + 1f, z + sz * 1.5f), 0.06f, 0.06f, 6, false);
                batch.For(steel).Cylinder(apex, new Vector3(boomFrom + 0.5f, boomY + 1f, z + sz * 1.5f), 0.06f, 0.06f, 6, false);
            }
            batch.For(signalRed, false).Blob(apex + Vector3.up * 0.3f, Vector3.one * 0.2f, 1, 0f, 7);
            batch.For(shipWhite).Box(new Vector3(-5.5f, boomY + 2.3f, z), new Vector3(4.6f, 2.5f, 3.6f), Quaternion.identity);

            // Trolley and cab; one crane carries a box out over the water, the other is parked over the road.
            float trolleyX = loaded ? 14.5f : 3f;
            batch.For(darkMetal).Box(new Vector3(trolleyX, boomY - 0.1f, z), new Vector3(2.4f, 0.7f, 3.2f), Quaternion.identity);
            batch.For(shipWhite).Box(new Vector3(trolleyX - 2f, boomY - 1.5f, z), new Vector3(1.5f, 1.7f, 1.5f), Quaternion.identity);
            batch.For(windowGlass).Box(new Vector3(trolleyX - 2f, boomY - 1.5f, z), new Vector3(1.54f, 0.7f, 1.54f), Quaternion.identity);
            float hookY = loaded ? 9.6f : 13.5f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    batch.For(steel).Cylinder(new Vector3(trolleyX + sx * 0.8f, boomY - 0.4f, z + sz * 1.2f), new Vector3(trolleyX + sx * 2.6f, hookY, z + sz * 1.1f), 0.03f, 0.03f, 5, false);
                }
            }
            Quaternion acrossPier = Quaternion.Euler(0f, 90f, 0f);
            batch.For(craneYellow).Box(new Vector3(trolleyX, hookY - 0.15f, z), new Vector3(6.2f, 0.3f, 2.5f), Quaternion.identity);
            if (loaded)
            {
                GameObject load = MeshKit.Spawn(root, "Hanging container", ContainerMesh(6.06f), containerPaints[3]);
                load.transform.SetPositionAndRotation(new Vector3(trolleyX, hookY - 0.3f - ContainerHeight * 0.5f, z), acrossPier);
            }
        }

        /// <summary>The bascule bridge's machinery towers, standing where the raised leaf ends.</summary>
        private void BuildBridgeTowers(BatchedMeshes batch, RunwayProfile runway)
        {
            float z = runway.RampEndZ + 1.2f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (PierHalfWidth - 0.9f);
                batch.For(concrete).Box(new Vector3(x, 2.75f, z), new Vector3(1.6f, 5.2f, 2.6f), Quaternion.identity);
                batch.For(shipWhite).Box(new Vector3(x, 6.05f, z), new Vector3(2f, 1.4f, 3f), Quaternion.identity);
                batch.For(windowGlass).Box(new Vector3(x, 6.15f, z), new Vector3(2.04f, 0.6f, 3.04f), Quaternion.identity);
                batch.For(roofMaterial).Box(new Vector3(x, 6.85f, z), new Vector3(2.3f, 0.14f, 3.3f), Quaternion.identity);
                batch.For(signalRed, false).Blob(new Vector3(x, 7.2f, z), Vector3.one * 0.2f, 1, 0f, 11);
                // The counterweight arm, swung down as the leaf lifts.
                batch.For(hazard).Box(new Vector3(side * (PierHalfWidth - 1.9f), 2.4f, z - 4.4f), new Vector3(0.35f, 0.5f, 9f), Quaternion.Euler(-16f, 0f, 0f));
                batch.For(darkMetal).Box(new Vector3(side * (PierHalfWidth - 1.9f), 1.1f, z + 0.2f), new Vector3(0.9f, 1.6f, 1.6f), Quaternion.identity);
            }
        }

        private void BuildLighthouse(BatchedMeshes batch, Vector3 foot)
        {
            batch.For(concrete).Cylinder(foot + Vector3.down * 0.9f, foot + Vector3.up * 0.25f, 2.1f, 2.1f, 20);
            Vector3 baseTop = foot + Vector3.up * 0.25f;
            const float towerHeight = 10.4f;
            batch.For(shipWhite).Cylinder(baseTop, baseTop + Vector3.up * towerHeight, 1.15f, 0.72f, 24);
            for (int band = 0; band < 2; band++)
            {
                float y0 = 2.2f + band * 4.2f, y1 = y0 + 1.7f;
                float r0 = Mathf.Lerp(1.15f, 0.72f, y0 / towerHeight) + 0.015f, r1 = Mathf.Lerp(1.15f, 0.72f, y1 / towerHeight) + 0.015f;
                batch.For(bootPaint).Cylinder(baseTop + Vector3.up * y0, baseTop + Vector3.up * y1, r0, r1, 24, false);
            }
            Vector3 gallery = baseTop + Vector3.up * towerHeight;
            batch.For(darkMetal).Cylinder(gallery, gallery + Vector3.up * 0.14f, 1.3f, 1.3f, 20);
            for (int post = 0; post < 10; post++)
            {
                float a = post / 10f * Mathf.PI * 2f;
                Vector3 p = gallery + new Vector3(Mathf.Cos(a) * 1.22f, 0.14f, Mathf.Sin(a) * 1.22f);
                batch.For(darkMetal).Cylinder(p, p + Vector3.up * 0.75f, 0.025f, 0.025f, 5, false);
            }
            batch.For(darkMetal).Cylinder(gallery + Vector3.up * 0.86f, gallery + Vector3.up * 0.9f, 1.24f, 1.24f, 20, false);
            batch.For(lampGlow, false).Cylinder(gallery + Vector3.up * 0.14f, gallery + Vector3.up * 1.7f, 0.68f, 0.68f, 16);
            batch.For(bootPaint).Cylinder(gallery + Vector3.up * 1.7f, gallery + Vector3.up * 2.5f, 0.86f, 0.06f, 16);
            batch.For(darkMetal).Cylinder(gallery + Vector3.up * 2.5f, gallery + Vector3.up * 3f, 0.03f, 0.03f, 5);
        }

        /// <summary>Puts a panel mesh's UVs in metres so the water's ripple map tiles instead of stretching.</summary>
        private static Mesh ScaleUv(Mesh mesh, float u, float v)
        {
            var uv = new List<Vector2>();
            mesh.GetUVs(0, uv);
            for (int i = 0; i < uv.Count; i++) uv[i] = new Vector2(uv[i].x * u, uv[i].y * v);
            mesh.SetUVs(0, uv);
            return mesh;
        }
    }
}
