using Autodesk.Revit.DB;
using OpenDefinery;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace OD_FamEditor
{
    /// <summary>A family type shown in the left sidebar.</summary>
    public class FamTypeItem
    {
        public FamilyType Type { get; set; }
        public string Name { get; set; }
    }

    /// <summary>
    /// One parameter row in the editor grid. The value is per-selected-type and updates when the
    /// user picks a different type in the sidebar, so it raises change notifications.
    /// </summary>
    public class FamParamRow : INotifyPropertyChanged
    {
        public FamilyParameter Param { get; set; }
        public string Name { get; set; }
        public string GroupLabel { get; set; }
        public string DataType { get; set; }

        /// <summary>Instance or Type.</summary>
        public string Binding { get; set; }

        /// <summary>Shared, Family, or Built-in.</summary>
        private string _kind;
        public string Kind
        {
            get => _kind;
            set { if (_kind != value) { _kind = value; Raise(nameof(Kind)); } }
        }

        public bool IsShared { get; set; }
        public bool IsBuiltIn { get; set; }
        public bool IsYesNo { get; set; }
        public Guid Guid { get; set; }

        // Tri-state value for Yes/No parameters: null = unset (Revit shows a greyed, still-tickable
        // box), true = Yes, false = No.
        private bool? _boolValue;
        public bool? BoolValue
        {
            get => _boolValue;
            set { if (_boolValue != value) { _boolValue = value; Raise(nameof(BoolValue)); } }
        }

        private string _formula;
        public string Formula
        {
            get => _formula;
            set { if (_formula != value) { _formula = value; Raise(nameof(Formula)); Raise(nameof(HasFormula)); Raise(nameof(CanEditValue)); } }
        }
        public bool HasFormula => !string.IsNullOrEmpty(Formula);

        private string _value;
        public string Value
        {
            get => _value;
            set { if (_value != value) { _value = value; Raise(nameof(Value)); Raise(nameof(CalculatedValue)); } }
        }

        /// <summary>Formula-driven display: the computed value with a &lt;CALCULATED&gt; suffix.</summary>
        public string CalculatedValue => (Value ?? string.Empty) + "   <CALCULATED>";

        private bool _inSelectedCollection;
        /// <summary>True when this shared parameter already exists in the Collection chosen at the top.</summary>
        public bool InSelectedCollection
        {
            get => _inSelectedCollection;
            set { if (_inSelectedCollection != value) { _inSelectedCollection = value; Raise(nameof(InSelectedCollection)); } }
        }

        // What the grid allows editing. Built-in parameters are locked; a value driven by a
        // formula is read-only (Revit computes it).
        public bool CanEditName => !IsBuiltIn;
        public bool CanEditFormula => !IsBuiltIn;
        public bool CanEditValue => !HasFormula;
        public bool CanEditKind => !IsBuiltIn;

        /// <summary>Show the GUID line only for shared parameters.</summary>
        public System.Windows.Visibility GuidVisibility =>
            IsShared ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    /// <summary>Reads family types and parameters from the active family document (read-only).</summary>
    public static class FamEditorReader
    {
        public static List<FamTypeItem> ReadTypes(Document doc)
        {
            var items = new List<FamTypeItem>();
            if (doc == null || !doc.IsFamilyDocument) return items;

            var all = doc.FamilyManager.Types.Cast<FamilyType>().ToList();
            var named = all.Where(t => !string.IsNullOrWhiteSpace(t.Name)).ToList();

            if (named.Count > 0)
            {
                // Real types exist - show only those, never the blank placeholder.
                foreach (var t in named) items.Add(new FamTypeItem { Type = t, Name = t.Name });
            }
            else if (all.Count > 0)
            {
                // Only the single unnamed type: label it so values stay editable.
                items.Add(new FamTypeItem { Type = all[0], Name = "(default)" });
            }

            return items.OrderBy(i => i.Name, Comparer<string>.Create(NaturalCompare)).ToList();
        }

        public static List<FamParamRow> ReadParams(Document doc)
        {
            var rows = new List<FamParamRow>();
            if (doc == null || !doc.IsFamilyDocument) return rows;

            var fm = doc.FamilyManager;

            foreach (FamilyParameter fp in fm.GetParameters())
            {
                var def = fp.Definition;
                var internalDef = def as InternalDefinition;
                var isBuiltIn = internalDef != null && internalDef.BuiltInParameter != BuiltInParameter.INVALID;

                var dataType = RvtCompat.GetDataTypeToken(def);

                rows.Add(new FamParamRow
                {
                    Param = fp,
                    Name = def.Name,
                    GroupLabel = SafeGroupLabel(def),
                    DataType = dataType,
                    Binding = fp.IsInstance ? "Instance" : "Type",
                    IsShared = fp.IsShared,
                    IsBuiltIn = isBuiltIn,
                    IsYesNo = fp.StorageType == StorageType.Integer && RvtCompat.IsYesNo(def),
                    Kind = isBuiltIn ? "Built-in" : (fp.IsShared ? "Shared" : "Family"),
                    Guid = fp.IsShared ? fp.GUID : Guid.Empty,
                    Formula = fp.Formula
                });
            }

            return rows;
        }

        /// <summary>Refresh each row's Value to the given family type's value.</summary>
        public static void ApplyTypeValues(Document doc, FamilyType type, IEnumerable<FamParamRow> rows)
        {
            foreach (var row in rows)
            {
                if (type != null && type.HasValue(row.Param))
                {
                    if (row.IsYesNo)
                    {
                        var iv = type.AsInteger(row.Param);
                        row.BoolValue = iv.HasValue ? (bool?)(iv.Value != 0) : null;
                        row.Value = row.BoolValue == true ? "Yes" : row.BoolValue == false ? "No" : string.Empty;
                    }
                    else
                    {
                        row.Value = ValueDisplay(type, row.Param);
                    }
                }
                else
                {
                    row.BoolValue = null;
                    row.Value = string.Empty;
                }
            }
        }

        private static string SafeGroupLabel(Definition def)
        {
            try { return RvtCompat.GetParamGroupLabel(def); }
            catch { return "Other"; }
        }

        /// <summary>
        /// A parameter's value formatted in the project's display units (AsValueString) - e.g. a
        /// length shows as 24.5" or feet-and-fractional-inches with the project's rounding. Text
        /// uses the raw string; integers fall back to the number if no value string exists.
        /// </summary>
        private static string ValueDisplay(FamilyType type, FamilyParameter fp)
        {
            try
            {
                switch (fp.StorageType)
                {
                    case StorageType.String:
                        return type.AsString(fp) ?? string.Empty;
                    case StorageType.Integer:
                        return type.AsValueString(fp) ?? type.AsInteger(fp)?.ToString() ?? string.Empty;
                    default: // Double / unit-based / ElementId
                        return type.AsValueString(fp) ?? string.Empty;
                }
            }
            catch { return string.Empty; }
        }

        // Natural order so Type 2 sorts before Type 10.
        private static int NaturalCompare(string a, string b)
        {
            a = a ?? string.Empty; b = b ?? string.Empty;
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    int si = i, sj = j;
                    while (i < a.Length && char.IsDigit(a[i])) i++;
                    while (j < b.Length && char.IsDigit(b[j])) j++;
                    var na = a.Substring(si, i - si).TrimStart('0');
                    var nb = b.Substring(sj, j - sj).TrimStart('0');
                    if (na.Length != nb.Length) return na.Length - nb.Length;
                    int cmp = string.CompareOrdinal(na, nb);
                    if (cmp != 0) return cmp;
                }
                else
                {
                    int cmp = char.ToLowerInvariant(a[i]).CompareTo(char.ToLowerInvariant(b[j]));
                    if (cmp != 0) return cmp;
                    i++; j++;
                }
            }
            return (a.Length - i) - (b.Length - j);
        }
    }

    /// <summary>Transaction-backed edits to the family. Each is its own Revit undo step.</summary>
    public static class FamEditorOps
    {
        public static bool Rename(Document doc, FamilyParameter fp, string newName, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(newName)) { error = "Name cannot be empty."; return false; }

            try
            {
                using (var t = new Transaction(doc, "Rename Parameter"))
                {
                    t.Start();
                    doc.FamilyManager.RenameParameter(fp, newName.Trim());
                    t.Commit();
                }
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public static bool SetValue(Document doc, FamilyType type, FamilyParameter fp, string valueString, out string error)
        {
            error = null;
            valueString = valueString ?? string.Empty;

            try
            {
                using (var t = new Transaction(doc, "Set Parameter Value"))
                {
                    t.Start();
                    var fm = doc.FamilyManager;

                    // Fall back to the family's current type when none is selected (a family may
                    // have a single, unnamed type - Revit still stores values against it).
                    var target = type ?? fm.CurrentType;
                    if (target != null) fm.CurrentType = target;

                    // Set by storage type: SetValueString is unit-aware but only valid for
                    // "value" (double) parameters - text/int must use Set().
                    switch (fp.StorageType)
                    {
                        case StorageType.String:
                            fm.Set(fp, valueString);
                            break;

                        case StorageType.Integer:
                            int iv;
                            if (RvtCompat.IsYesNo(fp.Definition))
                            {
                                var v = valueString.Trim().ToLowerInvariant();
                                iv = (v == "1" || v == "yes" || v == "true") ? 1 : 0;
                            }
                            else if (!int.TryParse(valueString.Trim(), out iv))
                            {
                                error = "Enter a whole number.";
                                t.RollBack();
                                return false;
                            }
                            fm.Set(fp, iv);
                            break;

                        case StorageType.ElementId:
                            error = "This value type can't be edited here.";
                            t.RollBack();
                            return false;

                        default: // Double and unit-based types
                            fm.SetValueString(fp, valueString);
                            break;
                    }

                    t.Commit();
                }
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        public static bool SetFormula(Document doc, FamilyParameter fp, string formula, out string error)
        {
            error = null;
            try
            {
                using (var t = new Transaction(doc, "Set Formula"))
                {
                    t.Start();
                    // Empty string clears the formula.
                    doc.FamilyManager.SetFormula(fp, string.IsNullOrWhiteSpace(formula) ? null : formula.Trim());
                    t.Commit();
                }
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        /// <summary>Set a Yes/No parameter for the current (or given) type. A null value is left as-is.</summary>
        public static bool SetYesNo(Document doc, FamilyType type, FamilyParameter fp, bool? value, out string error)
        {
            error = null;
            if (!value.HasValue) return true; // Revit has no direct "clear to unset" via the API.

            try
            {
                using (var t = new Transaction(doc, "Set Yes/No"))
                {
                    t.Start();
                    var fm = doc.FamilyManager;
                    var target = type ?? fm.CurrentType;
                    if (target != null) fm.CurrentType = target;
                    fm.Set(fp, value.Value ? 1 : 0);
                    t.Commit();
                }
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        /// <summary>
        /// Replace a parameter with a different OpenDefinery SHARED parameter of the same data
        /// type, in place. Revit's ReplaceParameter preserves the existing values, formulas, and
        /// every formula/schedule reference automatically - no manual copy or formula rewriting.
        /// Used by both Swap (shared -> shared) and the Kind change (family -> shared).
        /// </summary>
        public static bool ReplaceWithShared(Document doc, Autodesk.Revit.ApplicationServices.Application app,
            FamilyParameter source, DefineryParameter target, out string error)
        {
            error = null;

            string previousSpFile = app.SharedParametersFilename;
            var tempFile = System.IO.Path.GetTempPath() + "OdSwap_" + Guid.NewGuid().ToString() + ".txt";

            try
            {
                // Prepare the target's ExternalDefinition from a temp shared-parameter file
                // (must happen outside the transaction).
                System.IO.File.WriteAllText(tempFile, DefineryParameter.CreateParamTable(new List<DefineryParameter> { target }));
                app.SharedParametersFilename = tempFile;

                var spFile = app.OpenSharedParameterFile();
                var extDef = spFile?.Groups
                    .Cast<DefinitionGroup>()
                    .SelectMany(g => g.Definitions.Cast<ExternalDefinition>())
                    .FirstOrDefault(d => d.GUID == target.Guid);

                if (extDef == null) { error = "Could not prepare the target parameter definition."; return false; }

                using (var t = new Transaction(doc, "Swap Parameter"))
                {
                    t.Start();
                    var fm = doc.FamilyManager;
                    var group = source.Definition.GetGroupTypeId();
                    fm.ReplaceParameter(source, extDef, group, source.IsInstance);
                    t.Commit();
                }

                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
            finally
            {
                try { app.SharedParametersFilename = previousSpFile; } catch { }
                try { System.IO.File.Delete(tempFile); } catch { }
            }
        }

        private class StoredValue
        {
            public StorageType Type;
            public double D; public int I; public string S; public ElementId E;

            public static StoredValue Capture(FamilyType ft, FamilyParameter fp)
            {
                var sv = new StoredValue { Type = fp.StorageType };
                switch (fp.StorageType)
                {
                    case StorageType.Double: sv.D = ft.AsDouble(fp) ?? 0; break;
                    case StorageType.Integer: sv.I = ft.AsInteger(fp) ?? 0; break;
                    case StorageType.String: sv.S = ft.AsString(fp); break;
                    case StorageType.ElementId: sv.E = ft.AsElementId(fp); break;
                }
                return sv;
            }

            public void Restore(FamilyManager fm, FamilyParameter fp)
            {
                switch (Type)
                {
                    case StorageType.Double: fm.Set(fp, D); break;
                    case StorageType.Integer: fm.Set(fp, I); break;
                    case StorageType.String: if (S != null) fm.Set(fp, S); break;
                    case StorageType.ElementId: if (E != null) fm.Set(fp, E); break;
                }
            }
        }

        /// <summary>
        /// Flip a family parameter between Instance and Type. Revit exposes no direct toggle, so
        /// this removes and re-adds the parameter under the new binding, restoring its formula (or
        /// per-type values) and any other formulas that referenced it. One undoable transaction.
        ///
        /// Note: Revit forbids a Type parameter's formula from referencing an Instance parameter
        /// (and vice versa), so a dependent formula may become invalid after the flip - those are
        /// reported rather than silently dropped.
        /// </summary>
        public static bool ToggleBinding(Document doc, Autodesk.Revit.ApplicationServices.Application app,
            FamilyParameter source, string dataTypeToken, out string error, out string warning)
        {
            error = null;
            warning = null;

            var fm = doc.FamilyManager;
            var name = source.Definition.Name;
            var group = source.Definition.GetGroupTypeId();
            var spec = source.Definition.GetDataType();
            var newIsInstance = !source.IsInstance;
            var isShared = source.IsShared;
            var guid = isShared ? source.GUID : Guid.Empty;
            var formula = source.Formula;

            // Capture per-type values (only when no formula drives them).
            var values = new List<KeyValuePair<FamilyType, StoredValue>>();
            if (string.IsNullOrEmpty(formula))
            {
                foreach (FamilyType ft in fm.Types)
                {
                    if (string.IsNullOrWhiteSpace(ft.Name) || !ft.HasValue(source)) continue;
                    values.Add(new KeyValuePair<FamilyType, StoredValue>(ft, StoredValue.Capture(ft, source)));
                }
            }

            // Capture other parameters' formulas that reference this one (whole-token) so they can
            // be cleared before removal and restored afterward.
            var referencing = new List<KeyValuePair<FamilyParameter, string>>();
            var pattern = "(?<![\\w])" + System.Text.RegularExpressions.Regex.Escape(name) + "(?![\\w])";
            foreach (FamilyParameter fp in fm.GetParameters())
            {
                if (fp.Id == source.Id || string.IsNullOrEmpty(fp.Formula)) continue;
                if (System.Text.RegularExpressions.Regex.IsMatch(fp.Formula, pattern))
                    referencing.Add(new KeyValuePair<FamilyParameter, string>(fp, fp.Formula));
            }

            // Removing/re-adding can't carry a formula across a type/instance change, so rather than
            // risk silently dropping one, block the toggle entirely when any formula is involved and
            // report it. Nothing has been mutated at this point, so the caller just reverts the UI.
            if (!string.IsNullOrEmpty(formula))
            {
                error = "\"" + name + "\" has a formula, so its type/instance binding can't be changed without losing the formula.\n\n" +
                        "Remove the formula first, change the binding, then re-add the formula.";
                return false;
            }
            if (referencing.Count > 0)
            {
                error = "\"" + name + "\" is used in the formula of: " +
                        string.Join(", ", referencing.Select(kv => "\"" + kv.Key.Definition.Name + "\"")) + ".\n\n" +
                        "Changing its type/instance binding would break those formulas, so it's blocked. Remove the referencing formulas first.";
                return false;
            }

            // Prepare the shared ExternalDefinition (re-adding a shared param needs one).
            ExternalDefinition extDef = null;
            string previousSp = app.SharedParametersFilename;
            string tempFile = null;
            if (isShared)
            {
                tempFile = System.IO.Path.GetTempPath() + "OdBind_" + Guid.NewGuid().ToString() + ".txt";
                var dp = new DefineryParameter
                {
                    Guid = guid, Name = name, DataType = dataTypeToken,
                    Visible = "1", UserModifiable = "1", Description = string.Empty
                };
                System.IO.File.WriteAllText(tempFile, DefineryParameter.CreateParamTable(new List<DefineryParameter> { dp }));
                app.SharedParametersFilename = tempFile;
                var spf = app.OpenSharedParameterFile();
                extDef = spf?.Groups.Cast<DefinitionGroup>()
                    .SelectMany(g => g.Definitions.Cast<ExternalDefinition>())
                    .FirstOrDefault(d => d.GUID == guid);

                if (extDef == null)
                {
                    error = "Could not prepare the shared parameter definition.";
                    try { app.SharedParametersFilename = previousSp; } catch { }
                    try { System.IO.File.Delete(tempFile); } catch { }
                    return false;
                }
            }

            var failedFormulas = new List<string>();

            try
            {
                using (var t = new Transaction(doc, "Toggle Parameter Binding"))
                {
                    t.Start();

                    // Clear referencing formulas so the parameter can be removed.
                    foreach (var kv in referencing) { try { fm.SetFormula(kv.Key, null); } catch { } }

                    fm.RemoveParameter(source);

                    var np = isShared
                        ? fm.AddParameter(extDef, group, newIsInstance)
                        : fm.AddParameter(name, group, spec, newIsInstance);

                    if (!string.IsNullOrEmpty(formula))
                    {
                        fm.SetFormula(np, formula);
                    }
                    else
                    {
                        foreach (var kv in values) { fm.CurrentType = kv.Key; kv.Value.Restore(fm, np); }
                    }

                    // Restore referencing formulas; a binding-incompatibility makes some invalid.
                    foreach (var kv in referencing)
                    {
                        try { fm.SetFormula(kv.Key, kv.Value); }
                        catch { failedFormulas.Add(kv.Key.Definition.Name); }
                    }

                    t.Commit();
                }

                if (failedFormulas.Count > 0)
                {
                    warning = "The binding change left these formulas invalid (Revit forbids mixing type/instance references); they were cleared:\n  " +
                              string.Join("\n  ", failedFormulas);
                }

                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
            finally
            {
                if (isShared)
                {
                    try { app.SharedParametersFilename = previousSp; } catch { }
                    try { System.IO.File.Delete(tempFile); } catch { }
                }
            }
        }
    }

    /// <summary>OpenDefinery Collection helpers for the family editor's collection column.</summary>
    public static class FamEditorCollections
    {
        public static HashSet<Guid> MembershipGuids(Definery session, Collection collection)
        {
            var set = new HashSet<Guid>();
            if (session == null || collection == null) return set;

            try
            {
                var lite = Collection.GetIds(session, collection);
                if (lite != null)
                {
                    foreach (var lp in lite) set.Add(lp.Guid);
                }
            }
            catch { }

            return set;
        }

        /// <summary>Add a family shared parameter to the Collection, creating it in OpenDefinery if needed.</summary>
        public static bool AddToCollection(Definery session, FamParamRow row, Collection collection, out string error)
        {
            error = null;

            try
            {
                var existing = DefineryParameter.FromGuid(session, row.Guid);

                if (existing != null && existing.Count > 0)
                {
                    var od = existing.First();
                    var resp = DefineryParameter.AddToCollection(session, od, collection.Id);
                    var code = (int)resp.StatusCode;
                    if (code >= 200 && code < 300) return true;

                    error = "OpenDefinery could not add it to the Collection.";
                    return false;
                }

                var dt = ResolveDataType(session, row.DataType);
                if (dt == null)
                {
                    error = string.Format("Data type \"{0}\" is not available in OpenDefinery.", row.DataType);
                    return false;
                }

                var np = new DefineryParameter
                {
                    Guid = row.Guid,
                    Name = row.Name,
                    DataType = row.DataType,
                    Description = string.Empty,
                    Visible = "1",
                    UserModifiable = "1"
                };

                var created = DefineryParameter.Create(session, np, collection.Id, null, row.Name, string.Empty);
                if (created != null) return true;

                error = "OpenDefinery rejected the request.";
                return false;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        /// <summary>Remove a parameter (by GUID) from a Collection - the Undo for AddToCollection.</summary>
        public static bool RemoveFromCollection(Definery session, Guid guid, int collectionId)
        {
            try
            {
                var existing = DefineryParameter.FromGuid(session, guid);
                if (existing == null || existing.Count == 0) return false;
                return DefineryParameter.RemoveCollection(session, existing.First(), collectionId);
            }
            catch { return false; }
        }

        /// <summary>
        /// Resolve any representation of a data type - the family's shared-parameter code ("TEXT"),
        /// an OpenDefinery numeric id, a display Name, or a Revit ParameterType name - to the single
        /// OpenDefinery <see cref="DataType"/>, so two representations can be compared reliably.
        /// </summary>
        public static DataType ResolveDataType(Definery session, string token)
        {
            if (session?.DataTypes == null || string.IsNullOrWhiteSpace(token)) return null;
            var t = token.Trim();

            Func<string, string> norm = s => (s ?? string.Empty).Replace("_", string.Empty).Trim();

            return session.DataTypes.FirstOrDefault(d => d.Id.ToString() == t)
                ?? session.DataTypes.FirstOrDefault(d => string.Equals(d.Name, t, StringComparison.OrdinalIgnoreCase))
                ?? session.DataTypes.FirstOrDefault(d => string.Equals(d.ParameterTypeName, t, StringComparison.OrdinalIgnoreCase))
                ?? session.DataTypes.FirstOrDefault(d => string.Equals(norm(d.Name), norm(t), StringComparison.OrdinalIgnoreCase))
                ?? session.DataTypes.FirstOrDefault(d => string.Equals(norm(d.ParameterTypeName), norm(t), StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Parameters in the Collection that match a data type and aren't already in the family.</summary>
        public static List<DefineryParameter> SwapCandidates(
            Definery session, Collection collection, string dataType, HashSet<Guid> familyGuids)
        {
            var result = new List<DefineryParameter>();
            if (session == null || collection == null) return result;

            // Resolve the family's data-type token to the canonical OpenDefinery DataType so we can
            // compare it against a collection parameter's DataType (which comes back as a numeric id).
            var famDt = ResolveDataType(session, dataType);

            try
            {
                var all = Collection.GetParameters(session, collection);
                foreach (var p in all)
                {
                    if (p == null) continue;

                    var pdt = (p.DataType ?? string.Empty).Trim();
                    var pDt = ResolveDataType(session, pdt);

                    var typeMatches =
                        (famDt != null && pDt != null && famDt.Id == pDt.Id) ||
                        string.Equals(pdt, dataType, StringComparison.OrdinalIgnoreCase);

                    if (!typeMatches) continue;
                    if (familyGuids.Contains(p.Guid)) continue;

                    result.Add(p);
                }
            }
            catch { }

            return result.OrderBy(p => p.Name).ToList();
        }
    }
}
