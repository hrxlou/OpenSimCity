using UnityEngine;

namespace OpenSimCity.Presentation
{
    /// <summary>
    /// Static P3-08 benchmark metadata. IDs and footprint candidates are owned by
    /// OpenSimCity and remain independent from vendor filenames.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RepresentativeArtDefinition",
        menuName = "OpenSimCity/Presentation/Representative Art Definition")]
    public sealed class RepresentativeArtDefinition : ScriptableObject
    {
        [SerializeField] private string assetId = string.Empty;
        [SerializeField] private string prefabKey = string.Empty;
        [SerializeField] private RepresentativeArtRole role;
        [SerializeField] private Vector2Int footprintCells = Vector2Int.one;
        [SerializeField] private string sourceProvenanceKey = string.Empty;

        public string AssetId => assetId ?? string.Empty;
        public string PrefabKey => prefabKey ?? string.Empty;
        public RepresentativeArtRole Role => role;
        public Vector2Int FootprintCells => footprintCells;
        public string SourceProvenanceKey => sourceProvenanceKey ?? string.Empty;
    }
}
