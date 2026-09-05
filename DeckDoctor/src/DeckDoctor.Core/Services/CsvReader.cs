namespace DeckDoctor.Core.Services;

/// <summary>
/// Small hand-rolled CSV parser (handles quoted fields, embedded commas, escaped quotes).
/// The plan called for CsvHelper, but nuget.org isn't reachable from this build environment,
/// and the format we need to parse (Archidekt's export) is simple enough not to need a library —
/// this keeps the project buildable with zero package restore. Swap in CsvHelper later if you
/// want fancier attribute-based mapping; the row-list shape below is deliberately easy to adapt.
/// </summary>
public static class CsvReader
{
    public static List<Dictionary<string, string>> ReadWithHeader(string text)
    {
        var rows = ParseRows(text);
        if (rows.Count == 0) return new List<Dictionary<string, string>>();

        var header = rows[0];
        var result = new List<Dictionary<string, string>>();
        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Count == 1 && row[0].Length == 0) continue; // skip blank trailing line
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int col = 0; col < header.Count; col++)
                dict[header[col].Trim()] = col < row.Count ? row[col] : "";
            result.Add(dict);
        }
        return result;
    }

    private static List<List<string>> ParseRows(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new System.Text.StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { row.Add(field.ToString()); field.Clear(); }
                else if (c == '\r') { /* skip, \n handles the line break */ }
                else if (c == '\n')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                }
                else field.Append(c);
            }
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }
}
