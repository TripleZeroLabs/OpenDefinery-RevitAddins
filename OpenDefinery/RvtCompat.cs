using System.Linq;
using Autodesk.Revit.DB;

namespace OpenDefinery
{
    /// <summary>
    /// Version-bridging helpers that isolate Revit API differences across Revit
    /// 2022-2027. Revit 2024 removed <c>Definition.ParameterType</c> and the
    /// data-type members of the <c>ParameterType</c> enum; Revit 2025 removed
    /// <c>BuiltInParameterGroup</c>. These helpers use the modern
    /// <c>ForgeTypeId</c>/<c>SpecTypeId</c>/<c>GroupTypeId</c> API, which is
    /// available across the entire 2022-2027 range, so #if is only needed where a
    /// signature genuinely differs (e.g. <c>ElementId(long)</c>, added in 2024).
    /// </summary>
    public static class RvtCompat
    {
        /// <summary>
        /// Build an <see cref="ElementId"/> from a 64-bit id. Revit 2024 introduced
        /// <c>ElementId(long)</c> and deprecated <c>ElementId(int)</c>; 2022/2023 only
        /// have the int constructor.
        /// </summary>
        public static ElementId NewElementId(long id)
        {
#if REVIT2022 || REVIT2023
            return new ElementId((int)id);
#else
            return new ElementId(id);
#endif
        }

        /// <summary>
        /// Read an <see cref="ElementId"/>'s underlying value as a 64-bit integer.
        /// Revit 2024 introduced <c>ElementId.Value</c> (long) and deprecated
        /// <c>ElementId.IntegerValue</c> (int), which is all 2022/2023 expose.
        /// </summary>
        public static long GetIdValue(ElementId id)
        {
#if REVIT2022 || REVIT2023
            return id.IntegerValue;
#else
            return id.Value;
#endif
        }

        /// <summary>
        /// Return the OpenDefinery-style uppercase data-type token for a parameter
        /// <see cref="Definition"/> (e.g. "LENGTH", "YESNO"). See <see cref="RvtSpecMap"/>.
        /// </summary>
        public static string GetDataTypeToken(Definition definition)
        {
            return RvtSpecMap.GetToken(definition);
        }

        /// <summary>True if the parameter <see cref="Definition"/> is a Yes/No (boolean) type.</summary>
        public static bool IsYesNo(Definition definition)
        {
            return RvtSpecMap.IsYesNo(definition);
        }

        /// <summary>
        /// Add a shared parameter to a family under the Identity Data group, using the
        /// ForgeTypeId-based <c>AddParameter</c> overload available in all supported versions.
        /// </summary>
        public static FamilyParameter AddIdentityParameter(FamilyManager familyManager, ExternalDefinition externalDefinition)
        {
            return familyManager.AddParameter(externalDefinition, GroupTypeId.IdentityData, false);
        }

        /// <summary>
        /// Localized label for a parameter's group. Revit 2024 removed
        /// <c>Definition.ParameterGroup</c>/<c>LabelUtils.GetLabelFor(BuiltInParameterGroup)</c>;
        /// the ForgeTypeId-based <c>GetGroupTypeId()</c>/<c>GetLabelForGroup()</c> work across 2022-2027.
        /// </summary>
        public static string GetParamGroupLabel(Definition definition)
        {
            return LabelUtils.GetLabelForGroup(definition.GetGroupTypeId());
        }
    }
}
