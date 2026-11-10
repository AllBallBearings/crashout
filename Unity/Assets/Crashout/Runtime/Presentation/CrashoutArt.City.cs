using Crashout.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashout.Presentation
{
    /// <summary>The junction board and the city around it.</summary>
    public sealed partial class CrashoutArt
    {
        private const float CornerBlockCentre = 9.4f;
        private const float CornerBlockSize = 6f;

        private Mesh[] treeBark;
        private Mesh[] treeLeaves;
        private Mesh lampMesh;
        private Mesh lampHeadMesh;

        /// <summary>Elevation of the underside of the board plinth; the runway arms share it.</summary>
        internal const float DioramaBase = -1.45f;
        /// <summary>Top of the plinth band the board and runway arms sit on.</summary>
        internal const float PlinthTop = -0.35f;

        /// <summary>
        /// Builds the board: one bounded city block diorama on a plinth. Nothing extends past the board
        /// except each junction's runway arm. On the AR table the real table is the floor; elsewhere a
        /// dark display floor stands in for it.
        /// </summary>
        public Transform BuildCity(Transform parent, bool tabletop = false)
        {
            float reach = RunwayProfile.BoardHalfExtent;
            float south = RunwayProfile.BoardHalfExtent;
            Transform root = new GameObject("City").transform;
            root.SetParent(parent, false);

            Slab(root, "Ground", new Vector3(0f, -0.2f, 0f), new Vector3(52f, 0.3f, 52f), 0.08f, ground, true);
            // A bevelled plinth so the board reads as a premium diorama.
            Slab(root, "Board plinth", new Vector3(0f, (PlinthTop + DioramaBase) * 0.5f, 0f), new Vector3(53.2f, PlinthTop - DioramaBase, 53.2f), 0.4f, darkMetal, false);
            if (!tabletop)
            {
                GameObject floor = Slab(root, "Display floor", new Vector3(0f, DioramaBase - 0.25f, -60f), new Vector3(1600f, 0.5f, 1600f), 0.02f, displayFloor, true, null, false);
                floor.isStatic = true;
            }

            float northLength = reach + south;
            Slab(root, "North / South road", new Vector3(0f, 0f, (reach - south) * 0.5f), new Vector3(10f, 0.08f, northLength), 0.02f, asphalt, true);
            Slab(root, "East / West road", new Vector3(0f, 0.01f, 0f), new Vector3(reach * 2f, 0.08f, 10f), 0.02f, asphalt, true);

            float sidewalkFar = reach;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    float zFar = sz < 0 ? south : sidewalkFar;
                    float sizeX = sidewalkFar - 5f, sizeZ = zFar - 5f;
                    Slab(root, "Sidewalk", new Vector3(sx * (5f + sizeX * 0.5f), 0f, sz * (5f + sizeZ * 0.5f)),
                        new Vector3(sizeX, 0.2f, sizeZ), 0.05f, paving, true);
                    // Granite kerb stones along both road edges.
                    Slab(root, "Kerb", new Vector3(sx * 5.1f, 0.03f, sz * (5f + sizeZ * 0.5f)), new Vector3(0.24f, 0.2f, sizeZ), 0.04f, concrete, false);
                    Slab(root, "Kerb", new Vector3(sx * (5f + sizeX * 0.5f), 0.03f, sz * 5.1f), new Vector3(sizeX, 0.2f, 0.24f), 0.04f, concrete, false);
                }
            }

            BuildRoadMarkings(root, reach, south);
            BuildCornerBlocks(root);
            BuildBoardBlocks(root);
            BuildStreetFurniture(root);
            BuildSignals(root);

            // The orange construction barrier on the south-east corner (a collider the player can hit).
            GameObject barrier = Slab(root, "Impact target", new Vector3(7f, 0.6f, -5.7f), new Vector3(3.8f, 1.2f, 0.35f), 0.08f, PlayerPaint, true);
            Slab(root, "Barrier stripes", new Vector3(7f, 0.95f, -5.86f), new Vector3(3.6f, 0.3f, 0.04f), 0.01f, hazard, false);
            Slab(root, "Barrier stripes", new Vector3(7f, 0.95f, -5.54f), new Vector3(3.6f, 0.3f, 0.04f), 0.01f, hazard, false);
            barrier.isStatic = true;
            return root;
        }

        private void BuildRoadMarkings(Transform root, float reach, float south)
        {
            // Dashed white lane dividers between the two lanes in each direction.
            for (float d = -reach; d <= reach; d += 3.6f)
            {
                if (Mathf.Abs(d) < 10.8f) continue;
                if (d > -south + 1f)
                {
                    Decal(root, "Lane mark", new Vector3(-2.5f, 0.052f, d), new Vector3(0.12f, 0.012f, 1.8f), whitePaint);
                    Decal(root, "Lane mark", new Vector3(2.5f, 0.052f, d), new Vector3(0.12f, 0.012f, 1.8f), whitePaint);
                }
                Decal(root, "Lane mark", new Vector3(d, 0.062f, -2.5f), new Vector3(1.8f, 0.012f, 0.12f), whitePaint);
                Decal(root, "Lane mark", new Vector3(d, 0.062f, 2.5f), new Vector3(1.8f, 0.012f, 0.12f), whitePaint);
            }
            // Solid double yellow centre lines on every arm.
            for (int arm = -1; arm <= 1; arm += 2)
            {
                float farNorthSouth = arm < 0 ? south : reach;
                float lengthNS = farNorthSouth - 10.8f;
                float lengthEW = reach - 10.8f;
                for (int line = -1; line <= 1; line += 2)
                {
                    Decal(root, "Centre line", new Vector3(line * 0.12f, 0.052f, arm * (10.8f + lengthNS * 0.5f)), new Vector3(0.11f, 0.012f, lengthNS), lanePaint);
                    Decal(root, "Centre line", new Vector3(arm * (10.8f + lengthEW * 0.5f), 0.062f, line * 0.12f), new Vector3(lengthEW, 0.012f, 0.11f), lanePaint);
                }
            }

            for (int side = -1; side <= 1; side += 2)
            {
                for (int arm = -1; arm <= 1; arm += 2)
                {
                    float far = arm < 0 ? south : reach;
                    float length = far - 11f;
                    float centre = 11f + length * 0.5f;
                    Decal(root, "Edge line", new Vector3(side * 4.55f, 0.052f, arm * centre), new Vector3(0.14f, 0.012f, length), whitePaint);
                    Decal(root, "Edge line", new Vector3(arm * (11f + (reach - 11f) * 0.5f), 0.062f, side * 4.55f), new Vector3(reach - 11f, 0.012f, 0.14f), whitePaint);
                }
            }

            for (int arm = -1; arm <= 1; arm += 2)
            {
                for (float offset = -4f; offset <= 4.01f; offset += 1f)
                {
                    Decal(root, "Crosswalk", new Vector3(offset, 0.052f, arm * 7.6f), new Vector3(0.5f, 0.012f, 2.4f), whitePaint);
                    Decal(root, "Crosswalk", new Vector3(arm * 7.6f, 0.062f, offset), new Vector3(2.4f, 0.012f, 0.5f), whitePaint);
                }
                Decal(root, "Stop line", new Vector3(-arm * 2.5f, 0.052f, arm * 9.6f), new Vector3(4.6f, 0.012f, 0.3f), whitePaint);
                Decal(root, "Stop line", new Vector3(arm * 9.6f, 0.062f, arm * 2.5f), new Vector3(0.3f, 0.012f, 4.6f), whitePaint);
            }

            // Manholes and drains break up the asphalt.
            Decal(root, "Manhole", new Vector3(2.2f, 0.05f, -14f), new Vector3(0.75f, 0.012f, 0.75f), darkMetal);
            Decal(root, "Manhole", new Vector3(-13f, 0.06f, 1.8f), new Vector3(0.75f, 0.012f, 0.75f), darkMetal);
            Decal(root, "Manhole", new Vector3(12f, 0.06f, -2.8f), new Vector3(0.75f, 0.012f, 0.75f), darkMetal);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f;
                Vector3 p = Quaternion.Euler(0f, a, 0f) * new Vector3(4.6f, 0.055f, -16f);
                Decal(root, "Drain", p, new Vector3(0.4f, 0.012f, 0.9f), darkMetal).transform.rotation = Quaternion.Euler(0f, a, 0f);
            }
        }

        private void BuildCornerBlocks(Transform root)
        {
            float[] heights = { 14f, 19f, 12.5f, 16.5f };
            int index = 0;
            var shopWindows = new MeshKit();
            for (int sz = -1; sz <= 1; sz += 2)
            {
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    float h = heights[index];
                    Material facade = facades[(index * 3 + 1) % facades.Length];
                    Vector3 centre = new Vector3(sx * CornerBlockCentre, h * 0.5f, sz * CornerBlockCentre);
                    float s = CornerBlockSize;
                    Slab(root, "Corner block " + (index + 1), centre, new Vector3(s, h, s), 0.05f, facade, true);
                    Building(root, centre, new Vector3(s, h, s), index * 7 + 3, true);

                    // Shopfronts on the two faces that look onto the junction.
                    Vector3 faceX = new Vector3(sx * (CornerBlockCentre - s * 0.5f - 0.04f), 1.6f, sz * CornerBlockCentre);
                    Vector3 faceZ = new Vector3(sx * CornerBlockCentre, 1.6f, sz * (CornerBlockCentre - s * 0.5f - 0.04f));
                    shopWindows.Panel(faceX, new Vector3(-sx, 0f, 0f), Vector3.up, s - 1.2f, 2.6f);
                    shopWindows.Panel(faceZ, new Vector3(0f, 0f, -sz), Vector3.up, s - 1.2f, 2.6f);
                    Slab(root, "Shop frame", faceX + new Vector3(-sx * 0.02f, 1.45f, 0f), new Vector3(0.14f, 0.3f, s - 0.8f), 0.02f, darkMetal, false);
                    Slab(root, "Shop frame", faceZ + new Vector3(0f, 1.45f, -sz * 0.02f), new Vector3(s - 0.8f, 0.3f, 0.14f), 0.02f, darkMetal, false);
                    Material awning = index % 2 == 0 ? awningMaterial : signGreen;
                    Slab(root, "Awning", faceX + new Vector3(sx * -0.7f, 1.85f, 0f), new Vector3(1.4f, 0.08f, s - 1f), 0.03f, awning, false, new Vector3(0f, 0f, 14f * sx));
                    Slab(root, "Awning", faceZ + new Vector3(0f, 1.85f, sz * -0.7f), new Vector3(s - 1f, 0.08f, 1.4f), 0.03f, awning, false, new Vector3(-14f * sz, 0f, 0f));
                    index++;
                }
            }
            MeshKit.Spawn(root, "Shop windows", shopWindows.ToMesh("Shop windows"), shopGlass, false);
        }

        /// <summary>Cornice, parapet and rooftop plant for a block whose footprint is <paramref name="size"/>.</summary>
        private void Building(Transform root, Vector3 centre, Vector3 size, int seed, bool detailed)
        {
            float top = centre.y + size.y * 0.5f;
            Slab(root, "Cornice", new Vector3(centre.x, top - 0.25f, centre.z), new Vector3(size.x + 0.35f, 0.3f, size.z + 0.35f), 0.05f, concrete, false);
            Slab(root, "Parapet", new Vector3(centre.x, top + 0.35f, centre.z), new Vector3(size.x, 0.7f, size.z), 0.03f, roofMaterial, false);
            Slab(root, "Roof", new Vector3(centre.x, top + 0.42f, centre.z), new Vector3(size.x - 0.5f, 0.7f, size.z - 0.5f), 0.02f, roofMaterial, false);
            if (!detailed) return;
            float r1 = Hash(seed, 1), r2 = Hash(seed, 2);
            Slab(root, "Rooftop plant", new Vector3(centre.x + (r1 - 0.5f) * size.x * 0.4f, top + 1.2f, centre.z + (r2 - 0.5f) * size.z * 0.4f), new Vector3(1.8f, 1f, 1.2f), 0.06f, steel, false);
            Slab(root, "Rooftop plant", new Vector3(centre.x - (r2 - 0.5f) * size.x * 0.5f, top + 1.05f, centre.z + (r1 - 0.5f) * size.z * 0.5f), new Vector3(1f, 0.7f, 1f), 0.05f, concreteDark, false);
            if (r1 > 0.5f)
            {
                var kit = new MeshKit();
                Vector3 tank = new Vector3(centre.x - size.x * 0.22f, top + 0.8f, centre.z - size.z * 0.22f);
                for (int leg = 0; leg < 4; leg++)
                {
                    Vector3 offset = Quaternion.Euler(0f, leg * 90f + 45f, 0f) * Vector3.forward * 0.6f;
                    kit.Cylinder(tank + offset, tank + offset + Vector3.up * 1.6f, 0.05f, 0.05f, 6);
                }
                MeshKit.Spawn(root, "Water tower legs", kit.ToMesh("Water tower legs"), darkMetal);
                var barrel = new MeshKit();
                barrel.Cylinder(tank + Vector3.up * 1.6f, tank + Vector3.up * 3.3f, 0.95f, 0.95f, 16);
                barrel.Cylinder(tank + Vector3.up * 3.3f, tank + Vector3.up * 3.9f, 0.98f, 0.05f, 16);
                MeshKit.Spawn(root, "Water tower", barrel.ToMesh("Water tower"), bark);
            }
        }

        // Footprints on the board, in metres from the junction centre. Corner blocks sit at 6.4..12.4.
        private const float FrontageCentre = 10.1f;
        private const float FrontageWidth = 5.4f;
        private const float FrontageAlong = 16.6f;
        private const float FrontageDepth = 6f;
        private const float InnerCentre = 19.9f;
        private const float InnerSize = 11.4f;
        private const float EdgeCentre = 23.1f;
        private const float EdgeDepth = 5.8f;
        private const float PortalHalfWidth = 5.3f;
        private const float PortalHeight = 5.8f;

        /// <summary>
        /// Fills the board out to its edges: street frontages, an inner block in each quadrant, portal
        /// buildings where the roads leave the board (looping traffic disappears inside them) and a
        /// gateway with a skybridge where the runway arrives from the south.
        /// </summary>
        private void BuildBoardBlocks(Transform root)
        {
            var detail = new BatchedMeshes();
            float[] frontHeights = { 15f, 11.5f, 18f, 13f, 12f, 16.5f, 14f, 10.5f };
            float[] innerHeights = { 22f, 36f, 27f, 18f };
            int f = 0, quadrant = 0;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Block(root, detail, new Vector3(sx * FrontageCentre, 0f, sz * FrontageAlong), new Vector3(FrontageWidth, frontHeights[f], FrontageDepth), 200 + f, false);
                    f++;
                    Block(root, detail, new Vector3(sx * FrontageAlong, 0f, sz * FrontageCentre), new Vector3(FrontageDepth, frontHeights[f], FrontageWidth), 200 + f, false);
                    f++;
                    bool glass = quadrant == 1 || quadrant == 2;
                    Block(root, detail, new Vector3(sx * InnerCentre, 0f, sz * InnerCentre), new Vector3(InnerSize, innerHeights[quadrant], InnerSize), 240 + quadrant, glass);
                    quadrant++;
                }
            }
            Portal(root, detail, Vector3.forward, 19f, 260);
            Portal(root, detail, Vector3.right, 16f, 261);
            Portal(root, detail, Vector3.left, 21f, 262);
            Gateway(root, detail);
            detail.Emit(root, "Board block detail");
        }

        private void Block(Transform root, BatchedMeshes detail, Vector3 footprint, Vector3 size, int seed, bool glass)
        {
            Vector3 centre = new Vector3(footprint.x, size.y * 0.5f, footprint.z);
            Material facade = glass ? glassTower : facades[seed % facades.Length];
            Slab(root, glass ? "Glass tower" : "Building", centre, size, 0.05f, facade, true);
            if (glass) CurtainWall(detail, centre, size);
            Building(root, centre, size, seed, true);
        }

        /// <summary>Floor plates and mullions so a glass block reads as a curtain wall.</summary>
        private void CurtainWall(BatchedMeshes detail, Vector3 centre, Vector3 size)
        {
            float bottom = centre.y - size.y * 0.5f;
            for (float y = 3.6f; y < size.y - 1f; y += 3.6f)
            {
                detail.For(concreteDark).Box(new Vector3(centre.x, bottom + y, centre.z), new Vector3(size.x + 0.08f, 0.32f, size.z + 0.08f), Quaternion.identity);
            }
            for (int face = -1; face <= 1; face += 2)
            {
                for (float x = -size.x * 0.5f + 1.5f; x < size.x * 0.5f; x += 1.5f)
                {
                    detail.For(darkMetal).Box(new Vector3(centre.x + x, centre.y, centre.z + face * (size.z * 0.5f + 0.02f)), new Vector3(0.08f, size.y, 0.06f), Quaternion.identity);
                }
                for (float z = -size.z * 0.5f + 1.5f; z < size.z * 0.5f; z += 1.5f)
                {
                    detail.For(darkMetal).Box(new Vector3(centre.x + face * (size.x * 0.5f + 0.02f), centre.y, centre.z + z), new Vector3(0.06f, size.y, 0.08f), Quaternion.identity);
                }
            }
        }

        /// <summary>
        /// A building straddling a road where it leaves the board. Traffic loops at 24 m, inside the
        /// dark tunnel, so cars drive into the city beyond rather than popping out of existence.
        /// </summary>
        private void Portal(Transform root, BatchedMeshes detail, Vector3 outward, float height, int seed)
        {
            Vector3 side = Vector3.Cross(Vector3.up, outward);
            Quaternion turn = Quaternion.LookRotation(outward, Vector3.up);
            Vector3 euler = turn.eulerAngles;
            Vector3 middle = outward * EdgeCentre;
            float outer = FrontageCentre + FrontageWidth * 0.5f;
            float pierWidth = outer - PortalHalfWidth;
            Material facade = facades[seed % facades.Length];
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 pier = middle + side * (s * (PortalHalfWidth + pierWidth * 0.5f)) + Vector3.up * (height * 0.5f);
                Slab(root, "Portal pier", pier, new Vector3(pierWidth, height, EdgeDepth), 0.05f, facade, true, euler);
            }
            float lintelHeight = height - PortalHeight;
            Slab(root, "Portal lintel", middle + Vector3.up * (PortalHeight + lintelHeight * 0.5f), new Vector3(PortalHalfWidth * 2f, lintelHeight, EdgeDepth), 0.05f, facade, true, euler);
            Vector3 worldSize = new Vector3(Mathf.Abs(outward.x) > 0.5f ? EdgeDepth : outer * 2f, height, Mathf.Abs(outward.x) > 0.5f ? outer * 2f : EdgeDepth);
            Building(root, middle + Vector3.up * (height * 0.5f), worldSize, seed, true);

            // Concrete frame around the opening, a black lining and a row of tunnel lights.
            float near = EdgeCentre - EdgeDepth * 0.5f;
            Vector3 mouth = outward * (near - 0.15f);
            detail.For(concrete).Box(mouth + Vector3.up * (PortalHeight + 0.3f), Abs(turn * new Vector3(PortalHalfWidth * 2f + 1f, 0.6f, 0.4f)), Quaternion.identity);
            for (int s = -1; s <= 1; s += 2)
            {
                detail.For(concrete).Box(mouth + side * (s * (PortalHalfWidth + 0.25f)) + Vector3.up * (PortalHeight * 0.5f), Abs(turn * new Vector3(0.5f, PortalHeight, 0.4f)), Quaternion.identity);
                detail.For(underbody).Box(middle + side * (s * (PortalHalfWidth - 0.05f)) + Vector3.up * (PortalHeight * 0.5f), Abs(turn * new Vector3(0.1f, PortalHeight, EdgeDepth)), Quaternion.identity);
            }
            detail.For(underbody).Box(middle + Vector3.up * (PortalHeight - 0.05f), Abs(turn * new Vector3(PortalHalfWidth * 2f, 0.1f, EdgeDepth)), Quaternion.identity);
            detail.For(underbody).Box(outward * (RunwayProfile.BoardHalfExtent - 0.05f) + Vector3.up * (PortalHeight * 0.5f), Abs(turn * new Vector3(PortalHalfWidth * 2f, PortalHeight, 0.1f)), Quaternion.identity);
            for (float a = near + 0.6f; a < RunwayProfile.BoardHalfExtent - 0.5f; a += 1.6f)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    detail.For(lampGlow).Box(outward * a + side * (s * (PortalHalfWidth - 0.6f)) + Vector3.up * (PortalHeight - 0.14f), Abs(turn * new Vector3(0.12f, 0.06f, 0.9f)), Quaternion.identity);
                }
            }
        }

        /// <summary>Twin towers and a glazed skybridge framing the runway's arrival on the south edge.</summary>
        private void Gateway(Transform root, BatchedMeshes detail)
        {
            float[] heights = { 24f, 20.5f };
            for (int s = -1; s <= 1; s += 2)
            {
                Vector3 footprint = new Vector3(s * FrontageCentre, 0f, -EdgeCentre);
                Block(root, detail, footprint, new Vector3(FrontageWidth, heights[(s + 1) / 2], EdgeDepth), 270 + s, false);
            }
            float span = (FrontageCentre - FrontageWidth * 0.5f) * 2f + 0.4f;
            Vector3 bridge = new Vector3(0f, 9.9f, -EdgeCentre);
            Slab(root, "Skybridge", bridge, new Vector3(span, 3.4f, 3f), 0.05f, glassTower, true);
            detail.For(concreteDark).Box(bridge + Vector3.down * 1.62f, new Vector3(span, 0.35f, 3.2f), Quaternion.identity);
            detail.For(concreteDark).Box(bridge + Vector3.up * 1.62f, new Vector3(span, 0.35f, 3.2f), Quaternion.identity);
            for (float x = -span * 0.5f + 1.2f; x < span * 0.5f; x += 1.2f)
            {
                for (int face = -1; face <= 1; face += 2)
                {
                    detail.For(darkMetal).Box(bridge + new Vector3(x, 0f, face * 1.52f), new Vector3(0.07f, 3.4f, 0.05f), Quaternion.identity);
                }
            }
            for (float x = -span * 0.5f + 1f; x < span * 0.5f; x += 2f)
            {
                detail.For(lampGlow).Box(bridge + new Vector3(x, -1.82f, 0f), new Vector3(0.6f, 0.04f, 0.2f), Quaternion.identity);
            }
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        private void BuildStreetFurniture(Transform root)
        {
            float[] along = { 15.2f, 18.6f };
            for (int arm = 0; arm < 4; arm++)
            {
                Vector3 forward = arm == 0 ? Vector3.forward : arm == 1 ? Vector3.back : arm == 2 ? Vector3.right : Vector3.left;
                Vector3 side = Vector3.Cross(Vector3.up, forward);
                for (int s = -1; s <= 1; s += 2)
                {
                    for (int i = 0; i < along.Length; i++)
                    {
                        Vector3 position = forward * along[i] + side * (s * 5.9f);
                        if (i % 2 == 0) Lamp(root, position, -side * s);
                        else Tree(root, position + side * (s * 0.35f), Hash(arm * 7 + i, s + 3));
                    }
                }
                // Hydrant and bollards near each corner.
                Vector3 corner = forward * 12.5f + side * 5.7f;
                Hydrant(root, corner);
                for (int b = 0; b < 3; b++)
                {
                    Vector3 p = forward * (6.2f + b * 0.9f) - side * 5.45f;
                    var bollard = new MeshKit();
                    bollard.Cylinder(p + Vector3.up * 0.1f, p + Vector3.up * 0.95f, 0.11f, 0.1f, 10);
                    MeshKit.Spawn(root, "Bollard", bollard.ToMesh("Bollard"), darkMetal);
                }
            }
        }

        private void Hydrant(Transform root, Vector3 position)
        {
            Material red = signalRed;
            var kit = new MeshKit();
            kit.Cylinder(position + Vector3.up * 0.1f, position + Vector3.up * 0.75f, 0.13f, 0.12f, 12);
            kit.Cylinder(position + Vector3.up * 0.75f, position + Vector3.up * 0.9f, 0.14f, 0.06f, 12);
            kit.Cylinder(position + new Vector3(-0.2f, 0.5f, 0f), position + new Vector3(0.2f, 0.5f, 0f), 0.06f, 0.06f, 8);
            MeshKit.Spawn(root, "Hydrant", kit.ToMesh("Hydrant"), Paint(new Color(0.7f, 0.08f, 0.05f), false, "Hydrant paint"));
        }

        private void Lamp(Transform root, Vector3 position, Vector3 towardRoad)
        {
            if (lampMesh == null)
            {
                var kit = new MeshKit();
                kit.Cylinder(new Vector3(0f, 0.1f, 0f), new Vector3(0f, 0.6f, 0f), 0.16f, 0.12f, 12);
                kit.Cylinder(new Vector3(0f, 0.6f, 0f), new Vector3(0f, 5.6f, 0f), 0.085f, 0.06f, 12);
                // Swan-neck arm.
                Vector3 previous = new Vector3(0f, 5.6f, 0f);
                for (int i = 1; i <= 6; i++)
                {
                    float t = i / 6f;
                    Vector3 next = new Vector3(0f, 5.6f + Mathf.Sin(t * Mathf.PI * 0.5f) * 0.45f, t * 1.5f);
                    kit.Cylinder(previous, next, 0.055f, 0.05f, 8, false);
                    previous = next;
                }
                kit.Box(new Vector3(0f, 6.0f, 1.55f), new Vector3(0.36f, 0.14f, 0.7f), Quaternion.identity);
                lampMesh = kit.ToMesh("Street lamp");
                var head = new MeshKit();
                head.Box(new Vector3(0f, 5.92f, 1.55f), new Vector3(0.28f, 0.03f, 0.58f), Quaternion.identity);
                lampHeadMesh = head.ToMesh("Street lamp glow");
            }
            Quaternion facing = Quaternion.LookRotation(towardRoad, Vector3.up);
            GameObject pole = MeshKit.Spawn(root, "Lamp", lampMesh, darkMetal);
            pole.transform.SetPositionAndRotation(position, facing);
            GameObject glow = MeshKit.Spawn(pole.transform, "Lamp glow", lampHeadMesh, lampGlow, false);
            CapsuleCollider collider = pole.AddComponent<CapsuleCollider>();
            collider.radius = 0.14f;
            collider.height = 5.4f;
            collider.center = new Vector3(0f, 2.8f, 0f);
        }

        private void Tree(Transform root, Vector3 position, float random)
        {
            EnsureTrees();
            int variant = Mathf.Min(treeBark.Length - 1, Mathf.FloorToInt(random * treeBark.Length));
            float scale = Mathf.Lerp(0.85f, 1.2f, Hash(Mathf.RoundToInt(position.x * 10f), Mathf.RoundToInt(position.z * 10f)));
            GameObject tree = MeshKit.Spawn(root, "Tree", treeBark[variant], bark);
            tree.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, random * 360f, 0f));
            tree.transform.localScale = Vector3.one * scale;
            MeshKit.Spawn(tree.transform, "Canopy", treeLeaves[variant], foliage);
            CapsuleCollider collider = tree.AddComponent<CapsuleCollider>();
            collider.radius = 0.2f;
            collider.height = 3f;
            collider.center = new Vector3(0f, 1.5f, 0f);
            // A tree pit grate.
            Decal(root, "Tree pit", position + Vector3.up * 0.105f, new Vector3(1.2f, 0.01f, 1.2f), darkMetal);
        }

        /// <summary>Three deciduous tree shapes shared by every street tree.</summary>
        private void EnsureTrees()
        {
            if (treeBark != null) return;
            treeBark = new Mesh[3];
            treeLeaves = new Mesh[3];
            for (int v = 0; v < 3; v++)
            {
                var trunk = new MeshKit();
                var crown = new MeshKit();
                float height = 2.4f + v * 0.35f;
                trunk.Cylinder(Vector3.zero, new Vector3(0.05f, height, 0.03f), 0.16f, 0.09f, 10);
                for (int b = 0; b < 3; b++)
                {
                    float a = (b * 120f + v * 40f) * Mathf.Deg2Rad;
                    Vector3 from = new Vector3(0.04f, height * 0.75f, 0f);
                    Vector3 to = from + new Vector3(Mathf.Cos(a) * 0.9f, 1.1f, Mathf.Sin(a) * 0.9f);
                    trunk.Cylinder(from, to, 0.07f, 0.035f, 7, false);
                }
                int lumps = 6 + v;
                for (int i = 0; i < lumps; i++)
                {
                    float a = i / (float)lumps * Mathf.PI * 2f + v;
                    float r = i == 0 ? 0f : Mathf.Lerp(0.7f, 1.2f, Hash(i, v + 30));
                    Vector3 centre = new Vector3(Mathf.Cos(a) * r, height + 0.9f + Hash(i, v + 40) * 0.9f, Mathf.Sin(a) * r);
                    float size = i == 0 ? 1.5f : Mathf.Lerp(0.85f, 1.2f, Hash(i, v + 50));
                    crown.Blob(centre, new Vector3(size, size * 0.85f, size), 2, 0.55f, v * 10 + i);
                }
                treeBark[v] = trunk.ToMesh("Tree trunk " + v);
                treeLeaves[v] = crown.ToMesh("Tree crown " + v);
            }
        }

        /// <summary>
        /// Mast-arm traffic signals on the far-right corner of each approach. The player's approach
        /// faces a red light; the cross street is green.
        /// </summary>
        private void BuildSignals(Transform root)
        {
            var poles = new MeshKit();
            var housings = new MeshKit();
            var red = new MeshKit();
            var green = new MeshKit();
            var off = new MeshKit();
            Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.right, Vector3.left };
            foreach (Vector3 travel in directions)
            {
                Vector3 right = Vector3.Cross(Vector3.up, travel);
                Vector3 pole = travel * 5.7f + right * 5.7f;
                poles.Cylinder(pole, pole + Vector3.up * 6.1f, 0.13f, 0.1f, 12);
                Vector3 armStart = pole + Vector3.up * 5.8f;
                Vector3 armEnd = armStart - right * 5.6f;
                poles.Cylinder(armStart, armEnd, 0.08f, 0.05f, 8);
                bool stop = Mathf.Abs(travel.z) > 0.5f;
                for (int h = 0; h < 2; h++)
                {
                    Vector3 head = armStart - right * (1.9f + h * 2.5f) + Vector3.down * 0.65f;
                    Quaternion face = Quaternion.LookRotation(-travel, Vector3.up);
                    housings.Box(head, new Vector3(0.36f, 1.05f, 0.26f), face);
                    housings.Box(head + Vector3.up * 0.55f, new Vector3(0.06f, 0.15f, 0.06f), Quaternion.identity);
                    for (int lamp = 0; lamp < 3; lamp++)
                    {
                        Vector3 lens = head + Vector3.up * (0.32f - lamp * 0.32f) - travel * 0.14f;
                        bool lit = (lamp == 0 && stop) || (lamp == 2 && !stop);
                        MeshKit target = lit ? (lamp == 0 ? red : green) : off;
                        target.Cylinder(lens + travel * 0.02f, lens - travel * 0.02f, 0.11f, 0.11f, 12);
                        // Visor.
                        housings.Box(lens + Vector3.up * 0.11f - travel * 0.1f, new Vector3(0.26f, 0.02f, 0.2f), face);
                    }
                }
                // Street-name blade.
                housings.Box(pole + Vector3.up * 4.2f - right * 0.6f, new Vector3(0.02f, 0.2f, 1.1f), Quaternion.LookRotation(right));
            }
            MeshKit.Spawn(root, "Signal poles", poles.ToMesh("Signal poles"), darkMetal);
            MeshKit.Spawn(root, "Signal housings", housings.ToMesh("Signal housings"), trim);
            MeshKit.Spawn(root, "Signal red", red.ToMesh("Signal red"), signalRed, false);
            MeshKit.Spawn(root, "Signal green", green.ToMesh("Signal green"), signalGreen, false);
            MeshKit.Spawn(root, "Signal off", off.ToMesh("Signal off"), signalOff, false);

            foreach (Vector3 travel in directions)
            {
                Vector3 right = Vector3.Cross(Vector3.up, travel);
                Vector3 pole = travel * 5.7f + right * 5.7f;
                GameObject collider = new GameObject("Signal pole collider");
                collider.transform.SetParent(root, false);
                collider.transform.position = pole + Vector3.up * 3f;
                CapsuleCollider capsule = collider.AddComponent<CapsuleCollider>();
                capsule.radius = 0.15f;
                capsule.height = 6f;
            }
        }
    }
}
