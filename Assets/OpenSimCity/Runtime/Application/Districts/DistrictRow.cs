namespace OpenSimCity.Application.Districts
{
    public sealed class DistrictRow
    {
        public ulong Id { get; set; }
        public string Name { get; set; }
        public uint Color { get; set; }
        public DistrictCellDto[] Cells { get; set; }
    }
}
