using Crashout.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Crashout.Tests
{
    public sealed class ArBoardCoordinatorTests
    {
        [Test]
        public void CanonicalCarUsesOneToThirtyTwoTabletopScale()
        {
            GameObject root = new GameObject("Board");
            try
            {
                ArBoardCoordinator board = root.AddComponent<ArBoardCoordinator>();
                board.Place(new Pose(Vector3.zero, Quaternion.identity), ArBoardCoordinator.DefaultPresentationScale);
                Vector3 tabletopCarNose = board.CanonicalToTabletopPosition(new Vector3(0f, 0f, 4.5f));
                Assert.That(tabletopCarNose.z, Is.EqualTo(0.140625f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
