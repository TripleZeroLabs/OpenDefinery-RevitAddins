using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace OpenDefinery
{
    /// <summary>
    /// Maps between Revit data-type specs (<see cref="ForgeTypeId"/>) and the
    /// OpenDefinery uppercase data-type tokens (e.g. "LENGTH", "YESNO").
    ///
    /// Revit 2024 removed the legacy <c>ParameterType</c> enum, so a parameter's
    /// data type is now a <see cref="ForgeTypeId"/> obtained from
    /// <c>Definition.GetDataType()</c>. This table provides an explicit, reviewable
    /// mapping for the common discipline-neutral specs; anything not listed falls
    /// back to <see cref="ParseToken"/>, which derives a best-effort token from the
    /// ForgeTypeId string.
    ///
    /// TODO (needs a Revit install to validate): extend the table to the full
    /// MEP/structural spec set so disciplined tokens (HVAC_/ELECTRICAL_/PIPING_)
    /// round-trip exactly. The discipline prefix is not recoverable from the
    /// ForgeTypeId string alone, so those must be enumerated explicitly.
    /// </summary>
    public static class RvtSpecMap
    {
        // OpenDefinery token -> Revit spec. High-confidence, discipline-neutral
        // specs available across Revit 2022-2027.
        private static readonly Dictionary<string, ForgeTypeId> TokenToSpec =
            new Dictionary<string, ForgeTypeId>
            {
                { "LENGTH", SpecTypeId.Length },
                { "AREA", SpecTypeId.Area },
                { "VOLUME", SpecTypeId.Volume },
                { "ANGLE", SpecTypeId.Angle },
                { "NUMBER", SpecTypeId.Number },
                { "MASS", SpecTypeId.Mass },
                { "MASS_DENSITY", SpecTypeId.MassDensity },
                { "CURRENCY", SpecTypeId.Currency },
                { "SLOPE", SpecTypeId.Slope },
                { "FORCE", SpecTypeId.Force },
                { "INTEGER", SpecTypeId.Int.Integer },
                { "YESNO", SpecTypeId.Boolean.YesNo },
                { "TEXT", SpecTypeId.String.Text },
                { "MULTILINETEXT", SpecTypeId.String.MultilineText },
                { "URL", SpecTypeId.String.Url },
                { "MATERIAL", SpecTypeId.Reference.Material },
                { "IMAGE", SpecTypeId.Reference.Image },
                // NOTE: FAMILYTYPE has no SpecTypeId.Reference member; it relies on the
                // ParseToken fallback. Validate/extend with a Revit install.
                { "LOADCLASSIFICATION", SpecTypeId.Reference.LoadClassification },
            };

        /// <summary>
        /// Return the OpenDefinery token for a parameter <see cref="Definition"/>
        /// (e.g. "LENGTH", "YESNO"). Falls back to a parsed token for specs not in
        /// the explicit table.
        /// </summary>
        public static string GetToken(Definition definition)
        {
            return GetToken(definition.GetDataType());
        }

        /// <summary>Return the OpenDefinery token for a spec <see cref="ForgeTypeId"/>.</summary>
        public static string GetToken(ForgeTypeId spec)
        {
            if (spec == null || string.IsNullOrEmpty(spec.TypeId))
            {
                return string.Empty;
            }

            foreach (var pair in TokenToSpec)
            {
                if (pair.Value.Equals(spec))
                {
                    return pair.Key;
                }
            }

            return ParseToken(spec);
        }

        /// <summary>True if the spec is the boolean Yes/No type.</summary>
        public static bool IsYesNo(Definition definition)
        {
            return definition.GetDataType().Equals(SpecTypeId.Boolean.YesNo);
        }

        /// <summary>
        /// Best-effort token derived from the ForgeTypeId string when the spec is not
        /// in the explicit table. TypeId looks like "autodesk.spec.aec:length-2.0.0":
        /// take the spec-name segment ("length") and uppercase it. Works for common
        /// single-word specs; disciplined MEP specs may not round-trip.
        /// </summary>
        private static string ParseToken(ForgeTypeId spec)
        {
            var typeId = spec.TypeId;
            var afterColon = typeId.Contains(":") ? typeId.Substring(typeId.IndexOf(':') + 1) : typeId;
            var name = afterColon.Split('-')[0];
            var lastSegment = name.Split('.').Last();

            return lastSegment.ToUpperInvariant();
        }
    }
}
