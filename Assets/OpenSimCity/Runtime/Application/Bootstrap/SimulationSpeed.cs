namespace OpenSimCity.Application.Bootstrap
{
    /// <summary>
    /// Supported simulation rates. These are application controls, not city commands.
    /// </summary>
    public enum SimulationSpeed
    {
        Paused = 0,
        One = 1,
        Two = 2,
        Four = 4,

        Pause = Paused,
        OneX = One,
        TwoX = Two,
        FourX = Four
    }
}
