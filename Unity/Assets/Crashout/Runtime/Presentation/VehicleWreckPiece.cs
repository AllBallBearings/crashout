using Crashout.Vehicle;
using UnityEngine;

namespace Crashout.Presentation
{
    /// <summary>A detached structural section keeps the original vehicle's scoring identity.</summary>
    public sealed class VehicleWreckPiece : MonoBehaviour
    {
        public DamageableVehicle Owner { get; set; }
        private void OnCollisionEnter(Collision collision)
        {
            if (Owner == null || collision.collider.GetComponentInParent<DamageableVehicle>() == Owner) return;
            VehicleWreckPiece other = collision.collider.GetComponentInParent<VehicleWreckPiece>();
            if (other != null && other.Owner == Owner) return;
            Owner.ReportFragmentCollision(collision);
        }
    }
}
