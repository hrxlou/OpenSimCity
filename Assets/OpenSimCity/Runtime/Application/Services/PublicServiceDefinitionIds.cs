using OpenSimCity.Core;

namespace OpenSimCity.Application.Services
{
    /// <summary>Application-facing stable service definition IDs.</summary>
    public static class PublicServiceDefinitionIds
    {
        private const ulong Namespace = 0xF701_0000_0000_0000UL;
        public static readonly StableId Police = new StableId(Namespace | 0x01UL);
        public static readonly StableId Fire = new StableId(Namespace | 0x02UL);
        public static readonly StableId School = new StableId(Namespace | 0x03UL);
        public static readonly StableId Healthcare = new StableId(Namespace | 0x04UL);
        public static readonly StableId Waste = new StableId(Namespace | 0x05UL);

        public static StableId For(ServiceKindView kind)
        {
            switch (kind)
            {
                case ServiceKindView.Police: return Police;
                case ServiceKindView.Fire: return Fire;
                case ServiceKindView.School: return School;
                case ServiceKindView.Healthcare: return Healthcare;
                default: return Waste;
            }
        }
    }
}
