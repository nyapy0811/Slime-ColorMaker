using NUnit.Framework;
using UnityEngine;

namespace ColorMaze
{
    public class ColorRulesTests
    {
        static readonly ColorValue Limit = new(5, 5, 5);

        [Test]
        public void IsWithin_ChecksEachChannelAgainstItsLimit()
        {
            Assert.IsTrue(ColorRules.IsWithin(new ColorValue(5, 0, 3), Limit));
            Assert.IsFalse(ColorRules.IsWithin(new ColorValue(6, 0, 0), Limit));
            Assert.IsFalse(ColorRules.IsWithin(new ColorValue(-1, 0, 0), Limit));
            Assert.IsTrue(ColorRules.IsWithin(new ColorValue(8, 7, 9), new ColorValue(8, 7, 9)));
        }

        [Test]
        public void Clamp_NegativeBecomesZeroAndOverflowBecomesLimit()
        {
            Assert.AreEqual(new ColorValue(0, 5, 3), ColorRules.Clamp(new ColorValue(-2, 9, 3), Limit));
            Assert.AreEqual(new ColorValue(8, 7, 9), ColorRules.Clamp(new ColorValue(10, 10, 10), new ColorValue(8, 7, 9)));
        }

        [Test]
        public void IsAtLeast_NeedsEveryChannelAtLeastCost()
        {
            Assert.IsTrue(ColorRules.IsAtLeast(new ColorValue(2, 1, 1), new ColorValue(1, 0, 1)));
            Assert.IsTrue(ColorRules.IsAtLeast(new ColorValue(1, 0, 1), new ColorValue(1, 0, 1)));
            Assert.IsFalse(ColorRules.IsAtLeast(new ColorValue(2, 1, 0), new ColorValue(1, 0, 1)));
        }

        [Test]
        public void IsValidRequired_NeedsEveryChannelAtLeastOneNotAllEqualAndWithinLimit()
        {
            Assert.IsTrue(ColorRules.IsValidRequired(new ColorValue(1, 2, 3), Limit));
            Assert.IsFalse(ColorRules.IsValidRequired(new ColorValue(2, 0, 0), Limit));
            Assert.IsFalse(ColorRules.IsValidRequired(new ColorValue(2, 2, 2), Limit));
            Assert.IsFalse(ColorRules.IsValidRequired(new ColorValue(6, 1, 1), Limit), "한도를 넘으면 지금 색이 닿을 수 없다");
        }

        [Test]
        public void IsValidSourceValue_RejectsZeroAndAllEqual()
        {
            Assert.IsTrue(ColorRules.IsValidSourceValue(new ColorValue(2, 0, 0)));
            Assert.IsFalse(ColorRules.IsValidSourceValue(ColorValue.Zero));
            Assert.IsFalse(ColorRules.IsValidSourceValue(new ColorValue(1, 1, 1)));
        }

        [Test]
        public void ToColor_IsValueOverChannelMax()
        {
            Assert.AreEqual(new Color(1f, 0.2f, 0f), ColorRules.ToColor(new ColorValue(15, 3, 0)));
            Assert.AreEqual(Color.black, ColorRules.ToColor(ColorValue.Zero));
            Assert.AreEqual(Color.white, ColorRules.ToColor(ColorRules.Limit));
            Assert.AreNotEqual(ColorRules.ToColor(new ColorValue(1, 0, 0)), ColorRules.ToColor(new ColorValue(2, 0, 0)));
        }

        [Test]
        public void ToGrayscale_UsesChannelAverage()
        {
            Assert.AreEqual(ToGray(1f / 3f), ColorRules.ToGrayscale(Color.red));
            Assert.AreEqual(ColorRules.ToGrayscale(Color.red), ColorRules.ToGrayscale(Color.blue));
            Assert.AreEqual(ToGray(0.5f), ColorRules.ToGrayscale(new Color(1f, 0.5f, 0f)));
        }

        static Color ToGray(float value) => new(value, value, value, 1f);
    }
}
