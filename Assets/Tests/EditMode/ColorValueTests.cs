using NUnit.Framework;

namespace ColorMaze
{
    public class ColorValueTests
    {
        [Test]
        public void Add_SumsEachChannel()
        {
            Assert.AreEqual(new ColorValue(3, 2, 4), new ColorValue(1, 2, 0) + new ColorValue(2, 0, 4));
        }

        [Test]
        public void Subtract_SubtractsEachChannel()
        {
            Assert.AreEqual(new ColorValue(1, -1, 3), new ColorValue(2, 0, 4) - new ColorValue(1, 1, 1));
        }

        [Test]
        public void Equality_ComparesAllChannels()
        {
            Assert.IsTrue(new ColorValue(1, 2, 3) == new ColorValue(1, 2, 3));
            Assert.IsTrue(new ColorValue(1, 2, 3) != new ColorValue(1, 2, 4));
        }

        [Test]
        public void ToString_IsThreeNumbers()
        {
            Assert.AreEqual("2 0 0", new ColorValue(2, 0, 0).ToString());
        }
    }
}
