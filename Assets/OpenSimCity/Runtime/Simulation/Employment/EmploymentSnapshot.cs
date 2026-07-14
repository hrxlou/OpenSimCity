using System;

namespace OpenSimCity.Simulation.Employment
{
    [Serializable]
    public readonly struct EmploymentSnapshot
    {
        public EmploymentSnapshot(int laborForce, int employedWorkers, int availableJobs)
        {
            if (laborForce < 0)
                throw new ArgumentOutOfRangeException(nameof(laborForce));
            if (employedWorkers < 0 || employedWorkers > laborForce)
                throw new ArgumentOutOfRangeException(nameof(employedWorkers));
            if (availableJobs < 0 || employedWorkers > availableJobs)
                throw new ArgumentOutOfRangeException(nameof(availableJobs));

            LaborForce = laborForce;
            EmployedWorkers = employedWorkers;
            AvailableJobs = availableJobs;
        }

        public int LaborForce { get; }

        public int EmployedWorkers { get; }

        public int AvailableJobs { get; }

        public int UnemployedWorkers => LaborForce - EmployedWorkers;

        public int VacantJobs => AvailableJobs - EmployedWorkers;

        public double UnemploymentRate => LaborForce == 0 ? 0d : (double)UnemployedWorkers / LaborForce;

        public double JobVacancyRate => AvailableJobs == 0 ? 0d : (double)VacantJobs / AvailableJobs;
    }
}
