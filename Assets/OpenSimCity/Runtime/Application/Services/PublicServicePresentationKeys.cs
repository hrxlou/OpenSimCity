namespace OpenSimCity.Application.Services
{
    /// <summary>
    /// Presentation-only stable keys for the five implemented public-service definitions.
    /// These keys are not save identifiers; Simulation and persistence continue to use
    /// <see cref="PublicServiceDefinitionIds"/> stable IDs.
    /// </summary>
    public static class PublicServicePresentationKeys
    {
        public const string Police = "service.police.facility";
        public const string Fire = "service.fire.facility";
        public const string School = "service.school.facility";
        public const string Healthcare = "service.healthcare.facility";
        public const string Waste = "service.waste.facility";

        public static string For(ServiceKindView kind)
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
