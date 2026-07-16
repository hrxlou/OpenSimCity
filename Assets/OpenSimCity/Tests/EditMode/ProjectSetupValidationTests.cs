using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Editor;

namespace OpenSimCity.Tests.EditMode
{
    /// <summary>
    /// 플랫폼에 관계없이 유지되어야 하는 ProjectSetup 구조 검증을 회귀 검사한다.
    /// Git index의 정확한 ProjectSettings baseline은 Tools/CI guard가 별도로 검사한다.
    /// EditMode에서 아무것도 변경하지 않으므로 Windows·Mac 양쪽에서 동일하게 실행된다.
    /// </summary>
    public sealed class ProjectSetupValidationTests
    {
        [Test]
        public void ProjectSetupPassesPlatformNeutralValidation()
        {
            IReadOnlyList<string> issues = ProjectSetup.Validate();

            Assert.That(
                issues,
                Is.Empty,
                "ProjectSetup.Validate() reported configuration issues:\n" + string.Join("\n", issues));
        }

        [Test]
        public void ReleaseBuildScenesExcludeDevelopmentFixtures()
        {
            IReadOnlyList<string> scenes = BuildScript.ReleaseScenePaths();

            Assert.That(scenes, Does.Contain("Assets/OpenSimCity/Scenes/Bootstrap.unity"));
            Assert.That(scenes, Does.Contain("Assets/OpenSimCity/Scenes/MainMenu.unity"));
            Assert.That(scenes, Does.Contain("Assets/OpenSimCity/Scenes/CityPrototype.unity"));
            Assert.That(scenes.Any(path => path.Contains("Benchmark")), Is.False);
        }
    }
}
