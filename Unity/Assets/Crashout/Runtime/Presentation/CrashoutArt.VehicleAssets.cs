using System.Collections.Generic;
using UnityEngine;

namespace Crashout.Presentation
{
    public sealed partial class CrashoutArt
    {
        private void BuildVehicleAsset(Transform root, VehicleBodyAsset asset, Material paint, Material tail,
            Material indicator, Material plate, List<GameObject> detachables, List<MeshFilter> deformables)
        {
            var groups = new Dictionary<string, Transform> { ["Body"] = root };
            Material Resolve(string role) => role switch
            {
                "paint" => paint, "framePaint" => paint, "glass" => glass, "trim" => trim, "underbody" => underbody,
                "interior" => interior, "chrome" => chrome, "steel" => steel,
                "headlight" => headlight, "tail" => tail, "indicator" => indicator, "plate" => plate,
                _ => underbody
            };
            foreach (VehicleBodyAsset.Section section in asset.Sections)
            {
                if (!groups.TryGetValue(section.Part, out Transform parent))
                {
                    GameObject panel = new GameObject(section.Part); parent = panel.transform; parent.SetParent(root, false);
                    groups.Add(section.Part, parent); detachables.Add(panel);
                }
                Material material = Resolve(section.Material);
                GameObject piece = MeshKit.Spawn(parent, section.Part + " " + section.Material, section.CreateMesh(), material,
                    section.Material != "glass" && section.Material != "headlight" && section.Material != "tail");
                deformables.Add(piece.GetComponent<MeshFilter>());
            }
            if (asset.CabinSize.y > 0f)
            {
                var cabin = new GameObject("Cabin contact"); cabin.transform.SetParent(root, false); cabin.layer = 8;
                var collider = cabin.AddComponent<BoxCollider>();
                collider.center = asset.CabinCentre + Vector3.up * asset.Ground; collider.size = asset.CabinSize;
            }
        }
    }
}
