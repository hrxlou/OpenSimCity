using NUnit.Framework;
using OpenSimCity.Simulation.Employment;

namespace OpenSimCity.Tests.EditMode
{
    public sealed class EmploymentSnapshotTests
    {
        [Test]
        public void SnapshotExposesUnemploymentAndVacancyWithoutCreatingAgents()
        {
            var snapshot = new EmploymentSnapshot(100, 80, 120);

            Assert.That(snapshot.UnemployedWorkers, Is.EqualTo(20));
            Assert.That(snapshot.VacantJobs, Is.EqualTo(40));
            Assert.That(snapshot.UnemploymentRate, Is.EqualTo(0.2d).Within(0.0001d));
        }
    }
}
