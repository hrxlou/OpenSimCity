using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OpenSimCity.Tests.PlayMode
{
    /// <summary>
    /// ProjectSetup이 생성한 Scene들이 실제로 로드되고, 필수 root와 카메라가 있는지
    /// PlayMode에서 확인하는 smoke test다. Configure Project 결과의 재현성을 지킨다.
    /// </summary>
    public sealed class BootstrapSceneSmokeTests
    {
        // ProjectSetup.SceneDefinitions와 같은 계약(Scene 이름 -> 필수 root)을 반영한다.
        // [UnityTest]는 [TestCaseSource]와 함께 쓰면 충돌하므로 한 테스트에서 순회한다.
        private static readonly (string SceneName, string RequiredRoot)[] Scenes =
        {
            ("Bootstrap", "OpenSimCity.Bootstrap"),
            ("MainMenu", "OpenSimCity.MainMenu"),
            ("CityPrototype", "OpenSimCity.Prototype"),
            ("Benchmark", "OpenSimCity.Benchmark")
        };

        [UnityTest]
        public IEnumerator AllScenesLoadWithRequiredRootAndCamera()
        {
            foreach (var (sceneName, requiredRoot) in Scenes)
            {
                yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

                Assert.That(
                    SceneManager.GetActiveScene().name,
                    Is.EqualTo(sceneName),
                    $"Scene '{sceneName}' did not become the active scene.");
                Assert.That(
                    GameObject.Find(requiredRoot),
                    Is.Not.Null,
                    $"Scene '{sceneName}' is missing its required root '{requiredRoot}'.");
                Assert.That(
                    Camera.main,
                    Is.Not.Null,
                    $"Scene '{sceneName}' has no active MainCamera.");
            }
        }
    }
}
