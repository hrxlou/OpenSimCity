using OpenSimCity.Core.Time;

namespace OpenSimCity.Simulation.Economy
{
    /// <summary>
    /// A simulation system that advances aggregate city state exactly once per
    /// month. The application scheduler owns the run order and drives each system
    /// on the month boundary; a system never schedules itself. Every implementation
    /// requires a strictly increasing month.
    /// </summary>
    public interface IMonthlySimulationSystem
    {
        void RunMonth(SimulationDate month);
    }
}
