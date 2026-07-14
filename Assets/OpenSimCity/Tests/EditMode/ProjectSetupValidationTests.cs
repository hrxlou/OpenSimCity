using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenSimCity.Editor;

namespace OpenSimCity.Tests.EditMode
{
    /// <summary>
    /// 커밋된 프로젝트가 ProjectSetup의 검증 규칙을 통과하는지 회귀 검사한다.
    /// EditMode에서 아무것도 변경하지 않으므로 Windows·Mac 양쪽에서 동일하게 실행된다.
    /// </summary>
    public sealed class ProjectSetupValidationTests
    {
        [Test]
        public void CommittedProjectPassesValidation()
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
