using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OpenSimCity.Tests.PlayMode
{
    public sealed class BootstrapSceneSmokeTests
    {
        [UnityTest]
        public IEnumerator BootstrapSceneLoadsWithRequiredRootAndCamera()
        {
            yield return SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Bootstrap"));
            Assert.That(GameObject.Find("OpenSimCity.Bootstrap"), Is.Not.Null);
            Assert.That(Camera.main, Is.Not.Null);
        }
    }
}
