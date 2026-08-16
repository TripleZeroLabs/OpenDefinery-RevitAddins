using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace OpenDefinery
{
    /// <summary>A selectable built-in parameter group: its ForgeTypeId plus a display label.</summary>
    public class ParamGroupOption
    {
        public ForgeTypeId Id { get; set; }
        public string Label { get; set; }
        public override string ToString() => Label;
    }

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
        /// Add a shared parameter to a family under a chosen group and instance/type binding.
        /// </summary>
        public static FamilyParameter AddParameter(
            FamilyManager familyManager, ExternalDefinition externalDefinition, ForgeTypeId group, bool isInstance)
        {
            return familyManager.AddParameter(externalDefinition, group, isInstance);
        }

        /// <summary>The Identity Data group - the default target when adding to a family.</summary>
        public static ForgeTypeId IdentityGroup => GroupTypeId.IdentityData;

        /// <summary>
        /// All built-in parameter groups that can host a family parameter, as (id, label)
        /// sorted by label. Uses the ForgeTypeId group API available across 2022-2027;
        /// groups with no resolvable label are skipped.
        /// </summary>
        public static List<ParamGroupOption> GetAvailableGroups()
        {
            var groups = new List<ParamGroupOption>();

            foreach (var id in ParameterUtils.GetAllBuiltInGroups())
            {
                string label;
                try { label = LabelUtils.GetLabelForGroup(id); }
                catch { continue; }

                if (string.IsNullOrWhiteSpace(label)) continue;

                groups.Add(new ParamGroupOption { Id = id, Label = label });
            }

            return groups.OrderBy(g => g.Label).ToList();
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

        /// <summary>
        /// The model categories a project parameter can be bound to (those that allow bound
        /// parameters), as (name, category) sorted by name. Matches the category list in Revit's
        /// own "Project Parameters" dialog when the "Model" category filter is active.
        /// </summary>
        public static List<ModelCategoryOption> GetBindableModelCategories(Document doc)
        {
            var list = new List<ModelCategoryOption>();
            if (doc == null) return list;

            foreach (Category c in doc.Settings.Categories)
            {
                try
                {
                    if (c != null && c.AllowsBoundParameters && c.CategoryType == CategoryType.Model)
                        list.Add(new ModelCategoryOption { Name = c.Name, Category = c });
                }
                catch { /* skip categories that throw on inspection */ }
            }

            return list.OrderBy(o => o.Name).ToList();
        }

        /// <summary>
        /// Insert a project-parameter binding under a parameter group. The ForgeTypeId
        /// (GroupTypeId) overload of <c>BindingMap.Insert</c> is available across 2022-2027, so no
        /// per-version branch is needed. Returns false if a binding for the definition already exists.
        /// </summary>
        public static bool InsertProjectBinding(Document doc, Definition def, Binding binding, ForgeTypeId group)
        {
            return doc.ParameterBindings.Insert(def, binding, group ?? IdentityGroup);
        }

        // Categories that Revit does NOT allow a TYPE project parameter to bind to. The API is a
        // trap here: Insert() returns true, but the binding is ineffective (the parameter never
        // appears on the element - e.g. Project Information has no "type"). Revit's own dialog
        // disallows Type for these, so the add-in must validate it itself. Extend as more are found.
        private static readonly BuiltInCategory[] InstanceOnlyBindingCategories =
        {
            BuiltInCategory.OST_ProjectInformation,
        };

        /// <summary>
        /// True if a TYPE project-parameter binding is allowed for the given model category. Some
        /// categories (e.g. Project Information) support Instance binding only.
        /// </summary>
        public static bool AllowsTypeBinding(Category category)
        {
            if (category == null) return true;

            foreach (var bic in InstanceOnlyBindingCategories)
            {
                if (category.Id == new ElementId(bic)) return false;
            }
            return true;
        }
    }

    /// <summary>A selectable model category for project-parameter binding: its display name and Category.</summary>
    public class ModelCategoryOption
    {
        public string Name { get; set; }
        public Category Category { get; set; }
        public override string ToString() => Name;
    }
}
