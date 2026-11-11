using System.Text;
using Universe3D.Core.Objects;

namespace Universe3D.Core.Text;

/// <summary>
/// Finds objects by name or other name. Names are compared normalized: case-insensitive, spaces and underscores
/// ignored, leading zeros of numbers dropped ("NGC0224" = "ngc 224"). Exact matches come first, then prefix matches
/// (shortest name first), then names that contain the query.
/// </summary>
public sealed class NameSearch
{
    private readonly CosmicObject[] _objects;
    private readonly string[] _keys;
    private readonly int[] _owner;

    public NameSearch(IEnumerable<CosmicObject> objects)
    {
        _objects = objects.ToArray();
        var keys = new List<string>(_objects.Length * 2);
        var owner = new List<int>(_objects.Length * 2);
        for (int i = 0; i < _objects.Length; i++)
        {
            var o = _objects[i];
            foreach (string name in o.AltNames.Prepend(o.Name))
            {
                string key = Normalize(name);
                if (key.Length == 0) continue;
                keys.Add(key);
                owner.Add(i);
            }
        }
        _keys = keys.ToArray();
        _owner = owner.ToArray();
    }

    /// <summary>At most <paramref name="max"/> objects, best match first, each once.</summary>
    public IReadOnlyList<CosmicObject> Find(string query, int max = 50)
    {
        string q = Normalize(query ?? "");
        if (q.Length == 0 || max <= 0) return Array.Empty<CosmicObject>();

        // Rank per object: 0 exact, 1 prefix, 2 contains; then the key length, then the object's order.
        var best = new Dictionary<int, (int Rank, int Length)>();
        for (int k = 0; k < _keys.Length; k++)
        {
            string key = _keys[k];
            int rank = key.Length == q.Length && key == q ? 0
                     : key.StartsWith(q, StringComparison.Ordinal) ? 1
                     : key.Contains(q, StringComparison.Ordinal) ? 2
                     : -1;
            if (rank < 0) continue;
            int i = _owner[k];
            if (!best.TryGetValue(i, out var b) || (rank, key.Length).CompareTo(b) < 0) best[i] = (rank, key.Length);
        }
        return best.OrderBy(kv => kv.Value.Rank).ThenBy(kv => kv.Value.Length).ThenBy(kv => kv.Key)
                   .Take(max).Select(kv => _objects[kv.Key]).ToList();
    }

    /// <summary>Lower case without spaces or underscores, leading zeros of numbers dropped ("NGC 0224" → "ngc224").</summary>
    public static string Normalize(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
            if (c != ' ' && c != '_' && !char.IsWhiteSpace(c)) sb.Append(char.ToLowerInvariant(c));
        // Drop a zero that starts a number (not a decimal fraction) and is followed by another digit.
        for (int i = 0; i < sb.Length - 1; i++)
        {
            bool starts = i == 0 || (!char.IsDigit(sb[i - 1]) && sb[i - 1] != '.');
            while (starts && sb[i] == '0' && i + 1 < sb.Length && char.IsDigit(sb[i + 1])) sb.Remove(i, 1);
        }
        return sb.ToString();
    }
}
