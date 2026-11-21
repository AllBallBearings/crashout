using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Crashout.Presentation
{
    /// <summary>
    /// Tyre marks for one wheel: while the wheel slides it lays a flat strip on the road, and each
    /// slide starts a new strip. The strips are an ordinary world-space mesh, which draws reliably
    /// from any camera (a TrailRenderer under the car did not draw at all) and which the turn replay
    /// records like any other mesh.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class SkidMarks : MonoBehaviour
    {
        /// <summary>Further than any car moves in one frame: the car was placed, not driven.</summary>
        private const float TeleportDistance = 8f;
        private const float MinimumSpacing = 0.25f;
        private const float Lift = 0.015f;
        private const int MaximumPoints = 1600;

        private readonly List<List<Vector3>> strips = new List<List<Vector3>>();
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<Color> colours = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        private List<Vector3> current;
        private MeshFilter filter;
        private Mesh mesh;
        private float halfWidth;
        private Color tint;
        private bool hasLast;
        private Vector3 last;
        private int pointCount;

        public int StripCount => strips.Count;
        public int PointCount => pointCount;

        public static SkidMarks Create(Transform parent, string name, Material material, float width, Color tint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            SkidMarks marks = go.AddComponent<SkidMarks>();
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            marks.halfWidth = width * 0.5f;
            marks.tint = tint;
            return marks;
        }

        private void Awake()
        {
            filter = GetComponent<MeshFilter>();
            mesh = new Mesh { name = "Skid marks" };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        /// <summary>Feeds the wheel's contact point each frame; marks are laid only while sliding.</summary>
        public void Sample(Vector3 contact, bool sliding)
        {
            bool teleported = hasLast && (contact - last).sqrMagnitude > TeleportDistance * TeleportDistance;
            last = contact;
            hasLast = true;
            if (!sliding || teleported)
            {
                current = null;
                return;
            }
            if (current == null)
            {
                current = new List<Vector3> { contact };
                strips.Add(current);
                pointCount++;
                return;
            }
            if ((contact - current[current.Count - 1]).sqrMagnitude < MinimumSpacing * MinimumSpacing) return;
            current.Add(contact);
            pointCount++;
            while (pointCount > MaximumPoints && strips.Count > 1)
            {
                pointCount -= strips[0].Count;
                strips.RemoveAt(0);
            }
            Rebuild();
        }

        public void Clear()
        {
            strips.Clear();
            current = null;
            hasLast = false;
            pointCount = 0;
            Rebuild();
        }

        private void Rebuild()
        {
            vertices.Clear();
            uvs.Clear();
            colours.Clear();
            triangles.Clear();
            foreach (List<Vector3> strip in strips)
            {
                if (strip.Count < 2) continue;
                int first = vertices.Count;
                float along = 0f;
                for (int i = 0; i < strip.Count; i++)
                {
                    Vector3 previous = strip[Mathf.Max(0, i - 1)];
                    Vector3 next = strip[Mathf.Min(strip.Count - 1, i + 1)];
                    Vector3 direction = Vector3.ProjectOnPlane(next - previous, Vector3.up);
                    if (direction.sqrMagnitude < 1e-8f) direction = Vector3.forward;
                    Vector3 side = Vector3.Cross(Vector3.up, direction.normalized) * halfWidth;
                    if (i > 0) along += Vector3.Distance(strip[i], strip[i - 1]);
                    Vector3 centre = strip[i] + Vector3.up * Lift;
                    vertices.Add(centre - side);
                    vertices.Add(centre + side);
                    // The texture's soft edges and tread run across it (u), and it tiles once a metre along the strip (v).
                    uvs.Add(new Vector2(0f, along));
                    uvs.Add(new Vector2(1f, along));
                    float ends = Mathf.Min(1f, Mathf.Min(i, strip.Count - 1 - i) / 3f);
                    Color colour = new Color(tint.r, tint.g, tint.b, tint.a * Mathf.Lerp(0.35f, 1f, ends));
                    colours.Add(colour);
                    colours.Add(colour);
                    if (i == 0) continue;
                    int a = first + (i - 1) * 2, b = a + 1, c = a + 2, d = a + 3;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colours);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            ReplayMeshRevision.Changed(filter);
        }
    }
}
