using NUnit.Framework;

namespace SlimeColorMaker
{
    public class CurrentColorTests
    {
        static CurrentColor Make(ColorValue start)
        {
            var color = new CurrentColor(new ColorValue(5, 5, 5));
            color.Absorb(start);
            return color;
        }

        [Test]
        public void StartsAtZero()
        {
            Assert.AreEqual(ColorValue.Zero, new CurrentColor(new ColorValue(5, 5, 5)).Value);
        }

        [Test]
        public void Absorb_AddsAndDropsOverflow()
        {
            var color = Make(new ColorValue(4, 1, 0));
            color.Absorb(new ColorValue(3, 0, 2));
            Assert.AreEqual(new ColorValue(5, 1, 2), color.Value);
        }

        [Test]
        public void TrySpend_ExactMatchEmptiesToZero()
        {
            var color = Make(new ColorValue(3, 1, 1));
            Assert.IsTrue(color.TrySpend(new ColorValue(3, 1, 1)));
            Assert.AreEqual(ColorValue.Zero, color.Value);
        }

        [Test]
        public void TrySpend_DifferentValueKeepsColor()
        {
            var color = Make(new ColorValue(4, 1, 1));
            Assert.IsFalse(color.TrySpend(new ColorValue(3, 1, 1)));
            Assert.AreEqual(new ColorValue(4, 1, 1), color.Value);
        }

        [Test]
        public void Pass_SubtractsCost()
        {
            var color = Make(new ColorValue(2, 1, 1));
            color.Pass(new ColorValue(1, 0, 1));
            Assert.AreEqual(new ColorValue(1, 1, 0), color.Value);
        }

        [Test]
        public void Changed_FiresOnlyWhenValueChanges()
        {
            var color = Make(new ColorValue(5, 0, 0));
            int count = 0;
            color.Changed += () => count++;
            color.Absorb(new ColorValue(1, 0, 0));
            Assert.AreEqual(0, count, "한도에서 잘려 그대로면 알리지 않는다");
            color.Absorb(new ColorValue(0, 1, 0));
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Restore_SetsSavedValue()
        {
            var color = Make(new ColorValue(3, 1, 1));
            int count = 0;
            color.Changed += () => count++;
            color.Restore(new ColorValue(1, 0, 2));
            Assert.AreEqual(new ColorValue(1, 0, 2), color.Value);
            Assert.AreEqual(1, count);
        }
    }
}
