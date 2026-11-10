using NUnit.Framework;
using UnityEngine;

namespace SlimeColorMaker
{
    public class MapSnapshotTests
    {
        [Test]
        public void JsonRoundTrip_KeepsCurrentColorAndFixtureStates()
        {
            var snapshot = new MapSnapshot
            {
                mapId = "test_core_loop",
                mapVersion = 1,
                playerPosition = new Vector3(1f, 0f, 2f),
                playerYaw = 90f,
                playerPitch = -10f,
                currentColor = new ColorValue(3, 1, 0),
                playTime = 12.5f,
            };
            snapshot.fixtures.Add(new FixtureStateEntry { fixtureId = 3, state = "{\"open\":true}" });
            snapshot.activatedCheckpointIds.Add(7);

            var loaded = JsonUtility.FromJson<MapSnapshot>(JsonUtility.ToJson(snapshot));

            Assert.AreEqual(new ColorValue(3, 1, 0), loaded.currentColor);
            Assert.AreEqual("test_core_loop", loaded.mapId);
            Assert.AreEqual(snapshot.playerPosition, loaded.playerPosition);
            Assert.AreEqual(1, loaded.fixtures.Count);
            Assert.AreEqual(3, loaded.fixtures[0].fixtureId);
            Assert.AreEqual(snapshot.fixtures[0].state, loaded.fixtures[0].state);
            CollectionAssert.AreEqual(new[] { 7 }, loaded.activatedCheckpointIds);
        }
    }
}
