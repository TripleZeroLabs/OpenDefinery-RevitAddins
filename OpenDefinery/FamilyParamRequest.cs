using Autodesk.Revit.DB;

namespace OpenDefinery
{
    /// <summary>
    /// A request to add one OpenDefinery parameter to a family under a specific parameter
    /// group and instance/type binding. Consumed by <see cref="RvtConnector.CreateParamsWithBinding"/>.
    /// </summary>
    public class FamilyParamRequest
    {
        public DefineryParameter Param { get; set; }

        /// <summary>Target built-in parameter group. Null falls back to Identity Data.</summary>
        public ForgeTypeId Group { get; set; }

        /// <summary>True = Instance binding, false = Type binding.</summary>
        public bool IsInstance { get; set; }
    }
}
