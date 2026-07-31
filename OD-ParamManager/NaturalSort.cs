using System.Collections;
using System.ComponentModel;
using OpenDefinery;

namespace OD_ParamManager
{
    /// <summary>Natural (alphanumeric) sort for the Collections-tab parameter grid columns.</summary>
    internal class NaturalDefineryComparer : IComparer
    {
        private readonly string _path;
        private readonly ListSortDirection _direction;

        public NaturalDefineryComparer(string path, ListSortDirection direction)
        {
            _path = path;
            _direction = direction;
        }

        public int Compare(object x, object y)
        {
            var a = x as DefineryParameter;
            var b = y as DefineryParameter;

            int c;
            switch (_path)
            {
                case "DataType":
                    c = NaturalSort.Compare(a?.DataType, b?.DataType);
                    break;
                default:
                    c = NaturalSort.Compare(a?.Name, b?.Name);
                    break;
            }

            return _direction == ListSortDirection.Ascending ? c : -c;
        }
    }

    /// <summary>
    /// Alphanumeric ("natural") string ordering so numbered names sort as a human expects:
    /// Parameter #2 before Parameter #10, not after it. Digit runs are compared as numbers,
    /// everything else case-insensitively character by character.
    /// </summary>
    public static class NaturalSort
    {
        public static int Compare(string a, string b)
        {
            a = a ?? string.Empty;
            b = b ?? string.Empty;

            int i = 0, j = 0;

            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    int si = i, sj = j;
                    while (i < a.Length && char.IsDigit(a[i])) i++;
                    while (j < b.Length && char.IsDigit(b[j])) j++;

                    // Compare digit runs numerically (ignore leading zeros; shorter run is smaller).
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
                    i++;
                    j++;
                }
            }

            // Whichever string still has characters left sorts after the exhausted one.
            return (a.Length - i) - (b.Length - j);
        }
    }
}
