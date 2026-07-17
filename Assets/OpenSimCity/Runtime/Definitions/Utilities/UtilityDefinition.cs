using OpenSimCity.Application.Utilities;
using OpenSimCity.Core;
using UnityEngine;

namespace OpenSimCity.Definitions.Utilities
{
    [CreateAssetMenu(fileName = "UtilityDefinition", menuName = "OpenSimCity/Definitions/Utility Definition")]
    public sealed class UtilityDefinition : ScriptableObject
    {
        [SerializeField] private ulong definitionId;
        [SerializeField, Min(1)] private int powerCapacity = 100;
        [SerializeField] private string presentationKey = string.Empty;
        public StableId DefinitionId => definitionId == 0 ? StableId.Invalid : new StableId(definitionId);
        public int PowerCapacity => powerCapacity;
        public string PresentationKey => presentationKey ?? string.Empty;
        public UtilityDefinitionData ToData() => new UtilityDefinitionData(new StableId(definitionId), powerCapacity, presentationKey);
    }
}
