namespace OpenSimCity.Simulation.Economy
{
    /// <summary>
    /// Aggregate demand sector for the single-commodity market and RCI model.
    /// The numeric values are part of the runtime/save contract and are aligned
    /// with the low-density R/C/I zoning categories, but presentation colours and
    /// graphs are intentionally not part of this enum.
    /// </summary>
    public enum EconomicSector : byte
    {
        Residential = 1,
        Commercial = 2,
        Industrial = 3
    }
}
