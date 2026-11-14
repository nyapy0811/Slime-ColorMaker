using NUnit.Framework;
using UnityEngine;

namespace SlimeColorMaker
{
    public class SurfaceFinishTests
    {
        [Test]
        public void ApplyTo_SetsValuesAndKeywords()
        {
            var finish = ScriptableObject.CreateInstance<SurfaceFinish>();
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                finish.smoothness = 0.8f;
                finish.metallic = 0.6f;
                finish.specularHighlights = false;
                finish.environmentReflections = true;
                Assert.IsFalse(finish.Matches(material));
                finish.ApplyTo(material);
                Assert.AreEqual(0.8f, material.GetFloat("_Smoothness"), 1e-5f);
                Assert.AreEqual(0.6f, material.GetFloat("_Metallic"), 1e-5f);
                Assert.IsTrue(material.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"));
                Assert.IsFalse(material.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"));
                Assert.IsTrue(finish.Matches(material));

                finish.specularHighlights = true;
                finish.environmentReflections = false;
                Assert.IsFalse(finish.Matches(material));
                finish.ApplyTo(material);
                Assert.IsFalse(material.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF"));
                Assert.IsTrue(material.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"));
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(finish);
            }
        }
    }
}
