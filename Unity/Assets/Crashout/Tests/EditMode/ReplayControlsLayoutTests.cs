using System.Collections.Generic;
using Crashout.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Crashout.Tests
{
    public sealed class ReplayControlsLayoutTests
    {
        [TestCase(312f, 700f, false)]
        [TestCase(372f, 800f, false)]
        [TestCase(600f, 300f, true)]
        [TestCase(820f, 340f, true)]
        public void ControlsFitTheSafeAreaKeepTouchTargetsAndNeverOverlap(float width, float height, bool landscape)
        {
            Rect safe = new Rect(35f, 28f, width, height);
            var layout = new ReplayControlsLayout(safe, landscape);
            Assert.That(layout.Dock.height, Is.LessThanOrEqualTo(landscape ? 68f : 100f));
            Assert.That(layout.Dock.yMax, Is.EqualTo(safe.yMax));
            Assert.That(layout.Play.xMax, Is.GreaterThan(safe.xMax - 12f), "Play belongs at the right thumb.");
            Assert.That(layout.Seek.width, Is.GreaterThan(100f), "Scrubbing needs useful travel.");
            var targets = new List<Rect> { layout.Seek, layout.Restart, layout.Play };
            for (int i = 0; i < 4; i++) targets.Add(layout.SpeedOption(i));
            for (int i = 0; i < targets.Count; i++)
            {
                Rect target = targets[i];
                Assert.That(target.width, Is.GreaterThanOrEqualTo(44f));
                Assert.That(target.height, Is.GreaterThanOrEqualTo(44f));
                Assert.That(target.xMin, Is.GreaterThanOrEqualTo(safe.xMin));
                Assert.That(target.xMax, Is.LessThanOrEqualTo(safe.xMax));
                Assert.That(target.yMin, Is.GreaterThanOrEqualTo(layout.Dock.yMin));
                Assert.That(target.yMax, Is.LessThanOrEqualTo(safe.yMax));
                for (int j = i + 1; j < targets.Count; j++)
                    Assert.That(target.Overlaps(targets[j]), Is.False, $"Targets {i} and {j} overlap.");
            }
        }
    }
}
