using System.Collections.Generic;
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
    }
}
