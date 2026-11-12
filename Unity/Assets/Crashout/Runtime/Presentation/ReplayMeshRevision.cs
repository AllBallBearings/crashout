using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>Marks in-place mesh edits so replay stores a new mesh only when it changes.</summary>
    public sealed class ReplayMeshRevision : MonoBehaviour
    {
        public int Version { get; private set; }

        public static void Changed(MeshFilter filter)
        {
            var revision = filter.GetComponent<ReplayMeshRevision>();
            if (revision == null) revision = filter.gameObject.AddComponent<ReplayMeshRevision>();
            revision.Version++;
        }
    }
}
