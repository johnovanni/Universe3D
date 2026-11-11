using System.Globalization;

namespace Universe3D.Core.Catalogs;

/// <summary>A VizieR ASU-TSV result: comment lines, a header row, a units row, a dashes row, then data rows.</summary>
public sealed class TsvTable
{
    private readonly Dictionary<string, int> _columns;
    private readonly List<string[]> _rows;

    private TsvTable(Dictionary<string, int> columns, List<string[]> rows)
    {
        _columns = columns;
        _rows = rows;
    }

    public int Count => _rows.Count;

    /// <summary>True when the text has at least a header row (after comments and blank lines).</summary>
    public static bool HasHeader(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith('#')) continue;
            return true;
        }
        return false;
    }

    public static TsvTable Parse(TextReader reader)
    {
        string[]? header = null;
        bool pastDashes = false;
        var rows = new List<string[]>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length > 0 && line[0] == '#') continue;
            if (line.Trim().Length == 0) continue;
            var cells = line.Split('\t').Select(c => c.Trim()).ToArray();
            if (header == null) { header = cells; continue; }
            if (!pastDashes)
            {
                // Skip the units row and everything up to and including the dashes row.
                var nonEmpty = cells.Where(c => c.Length > 0).ToArray();
                if (nonEmpty.Length > 0 && nonEmpty.All(c => c.All(ch => ch == '-'))) pastDashes = true;
                continue;
            }
            rows.Add(cells);
        }
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        if (header != null)
            for (int i = 0; i < header.Length; i++) map.TryAdd(header[i], i);
        return new TsvTable(map, rows);
    }

    public string? Get(int row, string col)
    {
        if (!_columns.TryGetValue(col, out int c)) return null;
        var cells = _rows[row];
        if (c >= cells.Length) return null;
        var v = cells[c];
        if (v.Length == 0 || v.All(ch => ch == '-')) return null;
        return v;
    }

    public double? GetDouble(int row, string col)
    {
        var s = Get(row, col);
        if (s == null) return null;
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return null;
        return double.IsFinite(d) ? d : null;
    }

    public int? GetInt(int row, string col)
    {
        var s = Get(row, col);
        if (s == null) return null;
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : null;
    }
}
