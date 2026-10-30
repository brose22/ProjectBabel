using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Exports every gameplay data asset to an Excel workbook and imports the edited workbook back.
// Each data type gets a sheet with one row per asset. A list of plain values sits in one cell,
// separated by "; ". A list of records (event choices, waves and their spawn groups, layout props)
// gets its own sheet with one row per record, tied to its asset by the Asset and # columns.
// Columns come from the serialized fields, so a new field shows up without touching this file.
public static class DataWorkbook
{
    const string AssetColumn = "Asset";
    const string ListSeparator = "; ";
    const string NoReference = "None";
    const string LastPathKey = "Babel.DataWorkbook.LastPath";
    const string DefaultFileName = "BabelData.xlsx";

    // One sheet: a data type, or a list of records inside one.
    class Table
    {
        public string Sheet;
        public string ListField;
        public readonly List<string> Columns = new();
        public readonly List<Table> Lists = new();
        public string IndexColumn => $"{ObjectNames.NicifyVariableName(ListField)} #";
    }

    class DataType
    {
        public Type AssetType;
        public Table Root;
        public Dictionary<string, Object> Assets;
    }

    // A row read from a sheet, with its values by column.
    class SheetRow
    {
        public int Number;
        public string Key;
        public int[] Indexes;
        public Dictionary<string, string> Cells;
        public bool Used;
    }

    class SheetRows
    {
        public string Name;
        public List<string> Headers;
        public readonly List<SheetRow> Rows = new();
    }

    class Report
    {
        public readonly List<string> Problems = new();
        public readonly List<string> Changes = new();
        public void Problem(string message) => Problems.Add(message);
    }

    // ---------- Menu and buttons ----------

    [MenuItem("Babel/Data/Export to Excel...", priority = 40)]
    public static void ExportWithPrompt()
    {
        string path = EditorUtility.SaveFilePanel("Export data to Excel", LastFolder(), LastFileName(), "xlsx");
        if (string.IsNullOrEmpty(path)) return;
        EditorPrefs.SetString(LastPathKey, path);
        Export(path);
    }

    [MenuItem("Babel/Data/Import from Excel...", priority = 41)]
    public static void ImportWithPrompt()
    {
        string path = EditorUtility.OpenFilePanel("Import data from Excel", LastFolder(), "xlsx");
        if (string.IsNullOrEmpty(path)) return;
        EditorPrefs.SetString(LastPathKey, path);
        Import(path);
    }

    static string LastFolder()
    {
        string last = EditorPrefs.GetString(LastPathKey, "");
        return last.Length > 0 ? Path.GetDirectoryName(last) : Path.GetDirectoryName(Application.dataPath);
    }

    static string LastFileName()
    {
        string last = EditorPrefs.GetString(LastPathKey, "");
        return last.Length > 0 ? Path.GetFileName(last) : DefaultFileName;
    }

    // ---------- Data types ----------

    static readonly (Type type, string sheet)[] Sheets =
    {
        (typeof(CaptainData), "Captains"),
        (typeof(AbilityData), "Abilities"),
        (typeof(EnemyData), "Enemies"),
        (typeof(SoldierClassData), "Soldiers"),
        (typeof(ElementData), "Elements"),
        (typeof(EventData), "Events"),
        (typeof(GiftData), "Gifts"),
        (typeof(MatchData), "Matches"),
        (typeof(CityLayout), "Layouts"),
    };

    static List<DataType> LoadDataTypes(Report report)
    {
        var types = new List<DataType>();
        foreach ((Type type, string sheet) in Sheets)
        {
            var assets = new List<Object>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{type.Name}", new[] { "Assets" }))
            {
                Object asset = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), type);
                if (asset != null) assets.Add(asset);
            }
            types.Add(new DataType { AssetType = type, Root = BuildTable(type, sheet, report), Assets = KeyAssets(assets) });
        }
        return types;
    }

    // Assets go by file name, or by path when two of a type share one.
    static Dictionary<string, Object> KeyAssets(List<Object> assets)
    {
        var keyed = new Dictionary<string, Object>(StringComparer.OrdinalIgnoreCase);
        foreach (IGrouping<string, Object> group in assets.GroupBy(a => a.name, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            foreach (Object asset in group)
                keyed[group.Count() == 1 ? asset.name : AssetDatabase.GetAssetPath(asset)] = asset;
        return keyed;
    }

    // Reads the sheet layout off a throwaway instance, growing its lists so that empty lists still
    // show what their records hold.
    static Table BuildTable(Type type, string sheet, Report report)
    {
        ScriptableObject instance = ScriptableObject.CreateInstance(type);
        try
        {
            var so = new SerializedObject(instance);
            var table = new Table { Sheet = sheet };
            SerializedProperty property = so.GetIterator();
            for (bool more = property.Next(true); more; more = property.Next(false))
                if (!property.name.StartsWith("m_"))
                    AddField(table, property.Copy(), property.name, report);
            return table;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    static void AddField(Table table, SerializedProperty property, string column, Report report)
    {
        if (IsPlainValue(property))
        {
            table.Columns.Add(column);
        }
        else if (property.isArray)
        {
            property.arraySize = 1;
            SerializedProperty element = property.GetArrayElementAtIndex(0);
            if (IsPlainValue(element))
            {
                table.Columns.Add(column);
                return;
            }
            var list = new Table { ListField = column, Sheet = $"{table.Sheet} {ObjectNames.NicifyVariableName(column)}" };
            foreach (SerializedProperty child in Children(element))
                AddField(list, child, child.name, report);
            table.Lists.Add(list);
        }
        else if (property.propertyType == SerializedPropertyType.Generic)
        {
            foreach (SerializedProperty child in Children(property))
                AddField(table, child, $"{column}.{child.name}", report);
        }
        else
        {
            report.Problem($"{table.Sheet}: field {column} is a {property.propertyType}, which the workbook can't hold, so it is left out.");
        }
    }

    static List<SerializedProperty> Children(SerializedProperty parent)
    {
        var children = new List<SerializedProperty>();
        SerializedProperty child = parent.Copy();
        SerializedProperty end = parent.GetEndProperty();
        for (bool more = child.Next(true); more && !SerializedProperty.EqualContents(child, end); more = child.Next(false))
            children.Add(child.Copy());
        return children;
    }

    static bool IsPlainValue(SerializedProperty property) => property.propertyType switch
    {
        SerializedPropertyType.Integer or SerializedPropertyType.Float or SerializedPropertyType.Boolean or SerializedPropertyType.String
            or SerializedPropertyType.Enum or SerializedPropertyType.Color or SerializedPropertyType.ObjectReference
            or SerializedPropertyType.Vector2Int => true,
        _ => false,
    };

    // ---------- Export ----------

    public static bool Export(string path)
    {
        var report = new Report();
        List<DataType> types = LoadDataTypes(report);
        var sheets = new List<XlsxSheet>();
        int assetCount = 0;
        foreach (DataType type in types)
        {
            var rootSheet = new XlsxSheet(type.Root.Sheet);
            rootSheet.Rows.Add(new object[] { AssetColumn }.Concat(type.Root.Columns).ToArray());
            sheets.Add(rootSheet);
            var listSheets = new Dictionary<Table, XlsxSheet>();
            AddListSheets(type.Root, new List<string>(), sheets, listSheets);

            foreach ((string key, Object asset) in type.Assets.OrderBy(a => a.Key, StringComparer.OrdinalIgnoreCase))
            {
                using var so = new SerializedObject(asset);
                rootSheet.Rows.Add(new object[] { key }.Concat(type.Root.Columns.Select(c => CellValue(so.FindProperty(c), types, report, $"{type.Root.Sheet}/{key}/{c}"))).ToArray());
                foreach (Table list in type.Root.Lists)
                    ExportList(list, so.FindProperty(list.ListField), new object[] { key }, listSheets, types, report, $"{type.Root.Sheet}/{key}");
                assetCount++;
            }
        }

        try
        {
            XlsxFile.Write(path, sheets);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Debug.LogError($"Data export failed, nothing was written to {path} (is it open in Excel?): {e.Message}");
            return false;
        }
        LogProblems("Data export", report);
        Debug.Log($"Data export: wrote {assetCount} assets to {sheets.Count} sheets in {path}.");
        return true;
    }

    static void AddListSheets(Table table, List<string> indexColumns, List<XlsxSheet> sheets, Dictionary<Table, XlsxSheet> listSheets)
    {
        foreach (Table list in table.Lists)
        {
            List<string> columns = indexColumns.Append(list.IndexColumn).ToList();
            var sheet = new XlsxSheet(list.Sheet);
            sheet.Rows.Add(new object[] { AssetColumn }.Concat(columns).Concat(list.Columns).ToArray());
            sheets.Add(sheet);
            listSheets[list] = sheet;
            AddListSheets(list, columns, sheets, listSheets);
        }
    }

    static void ExportList(Table list, SerializedProperty property, object[] prefix, Dictionary<Table, XlsxSheet> listSheets, List<DataType> types, Report report, string context)
    {
        for (int i = 0; i < property.arraySize; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            object[] key = prefix.Append((double)(i + 1)).ToArray();
            string where = $"{context}/{list.ListField} {i + 1}";
            listSheets[list].Rows.Add(key.Concat(list.Columns.Select(c => CellValue(element.FindPropertyRelative(c), types, report, $"{where}/{c}"))).ToArray());
            foreach (Table nested in list.Lists)
                ExportList(nested, element.FindPropertyRelative(nested.ListField), key, listSheets, types, report, where);
        }
    }

    static object CellValue(SerializedProperty property, List<DataType> types, Report report, string where)
    {
        if (property.isArray && property.propertyType != SerializedPropertyType.String)
        {
            var parts = new List<string>();
            for (int i = 0; i < property.arraySize; i++)
            {
                string part = Convert.ToString(CellValue(property.GetArrayElementAtIndex(i), types, report, where), CultureInfo.InvariantCulture);
                if (part.Contains(';')) report.Problem($"{where}: \"{part}\" holds a ';' and won't import as one list entry.");
                parts.Add(part);
            }
            return string.Join(ListSeparator, parts);
        }
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer: return (double)property.intValue;
            case SerializedPropertyType.Float: return double.Parse(FloatText(property.floatValue), CultureInfo.InvariantCulture);
            case SerializedPropertyType.Boolean: return property.boolValue;
            case SerializedPropertyType.String: return property.stringValue;
            case SerializedPropertyType.Enum:
                return property.enumValueIndex >= 0 ? property.enumNames[property.enumValueIndex] : property.intValue.ToString(CultureInfo.InvariantCulture);
            case SerializedPropertyType.Color:
                Color color = property.colorValue;
                return string.Join(", ", new[] { color.r, color.g, color.b, color.a }.Select(FloatText));
            case SerializedPropertyType.Vector2Int:
                return string.Format(CultureInfo.InvariantCulture, "{0}, {1}", property.vector2IntValue.x, property.vector2IntValue.y);
            case SerializedPropertyType.ObjectReference:
                return ReferenceText(property, types, report, where);
            default:
                return null;
        }
    }

    static string FloatText(float value) => value.ToString("R", CultureInfo.InvariantCulture);

    static string ReferenceText(SerializedProperty property, List<DataType> types, Report report, string where)
    {
        Object target = property.objectReferenceValue;
        if (target == null)
        {
            if (!property.objectReferenceEntityIdValue.Equals(default(EntityId)))
                report.Problem($"{where}: the reference points at a missing asset. It is written as {NoReference}, and importing leaves it as it is.");
            return NoReference;
        }
        string text = null;
        foreach (DataType type in types)
            if (type.AssetType.IsInstanceOfType(target))
                text = type.Assets.FirstOrDefault(a => a.Value == target).Key;
        if (text == null)
        {
            text = AssetDatabase.GetAssetPath(target);
            if (!AssetDatabase.IsMainAsset(target)) text += "#" + target.name;
        }
        if (!TryResolve(text, property, types, out Object resolved, out string error) || resolved != target)
            report.Problem($"{where}: {text} won't import as the same asset ({error ?? resolved?.name}).");
        return text;
    }

    // ---------- Import ----------

    public static bool Import(string path)
    {
        List<XlsxSheet> sheets;
        try
        {
            sheets = XlsxFile.Read(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or System.Xml.XmlException)
        {
            Debug.LogError($"Data import failed, no assets were changed. Couldn't read {path}: {e.Message}");
            return false;
        }

        var report = new Report();
        List<DataType> types = LoadDataTypes(report);
        Dictionary<string, XlsxSheet> byName = sheets.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Import data from Excel");
        int undoGroup = Undo.GetCurrentGroup();
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int changedAssets = 0;
        foreach (DataType type in types)
            changedAssets += ImportType(type, byName, types, known, report);
        Undo.CollapseUndoOperations(undoGroup);

        foreach (XlsxSheet sheet in sheets.Where(s => !known.Contains(s.Name)))
            report.Problem($"Sheet \"{sheet.Name}\" doesn't match any data type, so it was skipped.");

        foreach (string change in report.Changes) Debug.Log($"Data import: {change}");
        LogProblems("Data import", report);
        Debug.Log($"Data import: changed {changedAssets} assets from {path}, {report.Problems.Count} problems.");
        return report.Problems.Count == 0;
    }

    static int ImportType(DataType type, Dictionary<string, XlsxSheet> sheets, List<DataType> types, HashSet<string> known, Report report)
    {
        Table root = type.Root;
        known.Add(root.Sheet);
        var lists = new Dictionary<Table, SheetRows>();
        CollectListSheets(root, new List<string>(), sheets, lists, known, report);
        if (!sheets.TryGetValue(root.Sheet, out XlsxSheet sheet))
        {
            report.Problem($"Sheet \"{root.Sheet}\" is missing, so those assets were left as they are.");
            return 0;
        }
        SheetRows rows = ParseSheet(sheet, new List<string>(), root.Columns, report);

        int changed = 0;
        var seen = new Dictionary<Object, SheetRow>();
        foreach (SheetRow row in rows.Rows)
        {
            if (!type.Assets.TryGetValue(row.Key, out Object asset))
            {
                report.Problem($"{Where(rows, row)}: no {type.AssetType.Name} asset is called \"{row.Key}\", so the row was skipped. Importing doesn't create assets.");
                continue;
            }
            if (seen.ContainsKey(asset))
            {
                report.Problem($"{Where(rows, row)}: \"{row.Key}\" already appears on row {seen[asset].Number}, so this row was skipped.");
                continue;
            }
            seen[asset] = row;
            row.Used = true;

            using var so = new SerializedObject(asset);
            var changes = new List<string>();
            ApplyRow(row, rows, root.Columns, null, so, types, changes, report);
            foreach (Table list in root.Lists)
                ImportList(list, so.FindProperty(list.ListField), row.Key, Array.Empty<int>(), lists, types, changes, report);
            if (changes.Count == 0) continue;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            report.Changes.Add($"{row.Key}: {string.Join(", ", changes)}");
            changed++;
        }

        foreach ((string key, Object asset) in type.Assets.Where(a => !seen.ContainsKey(a.Value)))
            report.Problem($"{root.Sheet}: {key} has no row, so it was left as it is.");
        foreach (SheetRows listRows in lists.Values)
            foreach (SheetRow row in listRows.Rows.Where(r => !r.Used))
                report.Problem($"{Where(listRows, row)}: nothing matches this row, so it was skipped.");
        return changed;
    }

    static void CollectListSheets(Table table, List<string> indexColumns, Dictionary<string, XlsxSheet> sheets, Dictionary<Table, SheetRows> lists, HashSet<string> known, Report report)
    {
        foreach (Table list in table.Lists)
        {
            known.Add(list.Sheet);
            List<string> columns = indexColumns.Append(list.IndexColumn).ToList();
            if (sheets.TryGetValue(list.Sheet, out XlsxSheet sheet))
                lists[list] = ParseSheet(sheet, columns, list.Columns, report);
            else
                report.Problem($"Sheet \"{list.Sheet}\" is missing, so those lists were left as they are.");
            CollectListSheets(list, columns, sheets, lists, known, report);
        }
    }

    static SheetRows ParseSheet(XlsxSheet sheet, List<string> indexColumns, List<string> columns, Report report)
    {
        var parsed = new SheetRows { Name = sheet.Name, Headers = new List<string>() };
        if (sheet.Rows.Count == 0)
        {
            report.Problem($"{sheet.Name}: the sheet is empty, so the header row is missing.");
            return parsed;
        }

        var headers = sheet.Rows[0].Select(h => ((string)h)?.Trim()).ToList();
        var columnOf = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int c = 0; c < headers.Count; c++)
        {
            if (string.IsNullOrEmpty(headers[c])) continue;
            if (columnOf.ContainsKey(headers[c])) report.Problem($"{sheet.Name}: column \"{headers[c]}\" appears twice, so the later one was skipped.");
            else columnOf[headers[c]] = c;
        }
        foreach (string header in columnOf.Keys.Except(columns.Concat(indexColumns).Append(AssetColumn), StringComparer.OrdinalIgnoreCase))
            report.Problem($"{sheet.Name}: column \"{header}\" doesn't match a field, so it was skipped.");
        foreach (string column in columns.Where(c => !columnOf.ContainsKey(c)))
            report.Problem($"{sheet.Name}: column \"{column}\" is missing, so that field was left as it is.");
        foreach (string column in indexColumns.Append(AssetColumn).Where(c => !columnOf.ContainsKey(c)))
        {
            report.Problem($"{sheet.Name}: column \"{column}\" is missing, so the sheet was skipped.");
            return parsed;
        }

        parsed.Headers = columns.Where(columnOf.ContainsKey).ToList();
        for (int r = 1; r < sheet.Rows.Count; r++)
        {
            object[] cells = sheet.Rows[r];
            if (cells.All(c => string.IsNullOrWhiteSpace((string)c))) continue;
            string Text(string column) => columnOf[column] < cells.Length ? (string)cells[columnOf[column]] ?? "" : "";
            var row = new SheetRow { Number = sheet.RowNumbers[r], Key = Text(AssetColumn).Trim(), Cells = parsed.Headers.ToDictionary(c => c, Text) };

            row.Indexes = new int[indexColumns.Count];
            bool valid = row.Key.Length > 0;
            if (!valid) report.Problem($"{Where(parsed, row)}: the Asset cell is empty, so the row was skipped.");
            for (int i = 0; i < indexColumns.Count && valid; i++)
            {
                valid = ParseWhole(Text(indexColumns[i]), out row.Indexes[i]) && row.Indexes[i] >= 1;
                if (!valid) report.Problem($"{Where(parsed, row)}: \"{Text(indexColumns[i])}\" isn't a position from 1 up in column {indexColumns[i]}, so the row was skipped.");
            }
            if (valid) parsed.Rows.Add(row);
        }
        return parsed;
    }

    static string Where(SheetRows sheet, SheetRow row) => $"{sheet.Name} row {row.Number}";

    // Rebuilds a list from its rows, so adding, removing and reordering rows all work. Positions have
    // to run 1, 2, 3 with no repeats: anything else leaves the list as it is rather than guessing.
    static void ImportList(Table list, SerializedProperty property, string key, int[] parent, Dictionary<Table, SheetRows> lists, List<DataType> types, List<string> changes, Report report)
    {
        if (!lists.TryGetValue(list, out SheetRows rows)) return;
        List<SheetRow> mine = rows.Rows.Where(r => !r.Used && string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase)
            && r.Indexes.Take(parent.Length).SequenceEqual(parent)).OrderBy(r => r.Indexes[parent.Length]).ToList();

        for (int i = 0; i < mine.Count; i++)
        {
            if (mine[i].Indexes[parent.Length] == i + 1) continue;
            report.Problem($"{rows.Name}: {key} {PositionText(parent)}has positions {string.Join(", ", mine.Select(r => r.Indexes[parent.Length]))}, which should run 1 to {mine.Count}, so the list was left as it is.");
            foreach (SheetRow row in mine) row.Used = true;
            return;
        }

        if (property.arraySize != mine.Count)
        {
            changes.Add($"{list.ListField} count {property.arraySize} -> {mine.Count}");
            if (mine.Count < property.arraySize)
                report.Problem($"{rows.Name}: {key} {PositionText(parent)}had {property.arraySize} entries and now has {mine.Count}. The removed rows were deleted, Undo restores them.");
            property.arraySize = mine.Count;
        }
        for (int i = 0; i < mine.Count; i++)
        {
            SheetRow row = mine[i];
            row.Used = true;
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            ApplyRow(row, rows, list.Columns, element, null, types, changes, report);
            int[] position = parent.Append(i + 1).ToArray();
            foreach (Table nested in list.Lists)
                ImportList(nested, element.FindPropertyRelative(nested.ListField), key, position, lists, types, changes, report);
        }
    }

    static string PositionText(int[] parent) => parent.Length > 0 ? $"entry {string.Join(".", parent)} " : "";

    static void ApplyRow(SheetRow row, SheetRows sheet, List<string> columns, SerializedProperty element, SerializedObject so, List<DataType> types, List<string> changes, Report report)
    {
        foreach (string column in columns)
        {
            if (!row.Cells.TryGetValue(column, out string text)) continue;
            SerializedProperty property = element != null ? element.FindPropertyRelative(column) : so.FindProperty(column);
            if (!TryApply(property, text, types, out bool changed, out string error))
                report.Problem($"{Where(sheet, row)}, {column}: {error} The value was left as it is.");
            else if (changed)
                changes.Add(column);
        }
    }

    static bool TryApply(SerializedProperty property, string text, List<DataType> types, out bool changed, out string error)
    {
        changed = false;
        if (property.isArray && property.propertyType != SerializedPropertyType.String)
            return TryApplyArray(property, text, types, out changed, out error);
        if (!TryParse(property, text, types, out object value, out error)) return false;
        if (Equals(CurrentValue(property), value)) return true;
        SetValue(property, value);
        changed = true;
        return true;
    }

    // Parses every entry before touching the list, so a bad entry leaves the whole list alone.
    static bool TryApplyArray(SerializedProperty property, string text, List<DataType> types, out bool changed, out string error)
    {
        changed = false;
        string[] parts = text.Trim().Length == 0 ? Array.Empty<string>() : text.Split(';').Select(p => p.Trim()).ToArray();
        int size = property.arraySize;
        if (parts.Length != size) property.arraySize = parts.Length;
        var values = new object[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (TryParse(property.GetArrayElementAtIndex(i), parts[i], types, out values[i], out error)) continue;
            property.arraySize = size;
            error = $"Entry {i + 1}: {error}";
            return false;
        }
        changed = parts.Length != size;
        for (int i = 0; i < parts.Length; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            if (i < size && Equals(CurrentValue(element), values[i])) continue;
            SetValue(element, values[i]);
            changed = true;
        }
        error = null;
        return true;
    }

    static object CurrentValue(SerializedProperty property) => property.propertyType switch
    {
        SerializedPropertyType.Integer => property.intValue,
        SerializedPropertyType.Enum => property.enumValueIndex,
        SerializedPropertyType.Float => property.floatValue,
        SerializedPropertyType.Boolean => property.boolValue,
        SerializedPropertyType.String => property.stringValue,
        SerializedPropertyType.Color => property.colorValue,
        SerializedPropertyType.Vector2Int => property.vector2IntValue,
        SerializedPropertyType.ObjectReference => property.objectReferenceValue != null ? property.objectReferenceValue : null,
        _ => null,
    };

    static void SetValue(SerializedProperty property, object value)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer: property.intValue = (int)value; break;
            case SerializedPropertyType.Enum: property.enumValueIndex = (int)value; break;
            case SerializedPropertyType.Float: property.floatValue = (float)value; break;
            case SerializedPropertyType.Boolean: property.boolValue = (bool)value; break;
            case SerializedPropertyType.String: property.stringValue = (string)value; break;
            case SerializedPropertyType.Color: property.colorValue = (Color)value; break;
            case SerializedPropertyType.Vector2Int: property.vector2IntValue = (Vector2Int)value; break;
            case SerializedPropertyType.ObjectReference: property.objectReferenceValue = (Object)value; break;
        }
    }

    static bool TryParse(SerializedProperty property, string text, List<DataType> types, out object value, out string error)
    {
        value = null;
        error = null;
        string trimmed = text.Trim();
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                if (ParseWhole(trimmed, out int whole) && InRange(property, whole, out error)) { value = whole; return true; }
                error ??= $"\"{text}\" isn't a whole number.";
                return false;
            case SerializedPropertyType.Float:
                if (ParseFloat(trimmed, out float number) && InRange(property, number, out error)) { value = number; return true; }
                error ??= $"\"{text}\" isn't a number.";
                return false;
            case SerializedPropertyType.Boolean:
                if (trimmed.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || trimmed == "1") { value = true; return true; }
                if (trimmed.Equals("FALSE", StringComparison.OrdinalIgnoreCase) || trimmed == "0") { value = false; return true; }
                error = $"\"{text}\" isn't TRUE or FALSE.";
                return false;
            case SerializedPropertyType.String:
                value = text;
                return true;
            case SerializedPropertyType.Enum:
                int index = Array.FindIndex(property.enumNames, n => n.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
                if (index >= 0) { value = index; return true; }
                // A stored number outside the enum is exported as that number and stays as it is.
                if (trimmed == property.intValue.ToString(CultureInfo.InvariantCulture)) { value = property.enumValueIndex; return true; }
                error = $"\"{text}\" isn't one of {string.Join(", ", property.enumNames)}.";
                return false;
            case SerializedPropertyType.Color:
                string[] channels = trimmed.Split(',');
                var rgba = new float[4];
                if (channels.Length == 4 && channels.Select((c, i) => ParseFloat(c.Trim(), out rgba[i])).All(ok => ok))
                {
                    value = new Color(rgba[0], rgba[1], rgba[2], rgba[3]);
                    return true;
                }
                error = $"\"{text}\" isn't a colour written as r, g, b, a.";
                return false;
            case SerializedPropertyType.Vector2Int:
                string[] axes = trimmed.Split(',');
                if (axes.Length == 2 && ParseWhole(axes[0].Trim(), out int x) && ParseWhole(axes[1].Trim(), out int y))
                {
                    value = new Vector2Int(x, y);
                    return true;
                }
                error = $"\"{text}\" isn't a cell written as x, y.";
                return false;
            case SerializedPropertyType.ObjectReference:
                if (!TryResolve(trimmed, property, types, out Object target, out error)) return false;
                value = target;
                return true;
            default:
                error = $"{property.propertyType} fields can't be imported.";
                return false;
        }
    }

    // Excel can store a whole number as 1E+3 or 7.0, so whole numbers are read through a double.
    static bool ParseWhole(string text, out int value)
    {
        value = 0;
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) return false;
        if (number != Math.Floor(number) || number < int.MinValue || number > int.MaxValue) return false;
        value = (int)number;
        return true;
    }

    static bool ParseFloat(string text, out float value) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);

    // Honours a [Range] on the field, which the Inspector would have enforced.
    static bool InRange(SerializedProperty property, float value, out string error)
    {
        error = null;
        FieldInfo field = property.serializedObject.targetObject.GetType().GetField(property.propertyPath);
        if (field?.GetCustomAttribute<RangeAttribute>() is not { } range || (value >= range.min && value <= range.max)) return true;
        error = $"{value.ToString(CultureInfo.InvariantCulture)} is outside {range.min.ToString(CultureInfo.InvariantCulture)} to {range.max.ToString(CultureInfo.InvariantCulture)}.";
        return false;
    }

    // A reference is None or empty, a data asset's name, or an asset path, with #Name for a sprite
    // or other object inside a file.
    static bool TryResolve(string text, SerializedProperty property, List<DataType> types, out Object target, out string error)
    {
        target = null;
        error = null;
        if (text.Length == 0 || text.Equals(NoReference, StringComparison.OrdinalIgnoreCase)) return true;

        string typeName = property.type.Replace("PPtr<$", "").TrimEnd('>');
        if (!text.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) && !text.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
        {
            foreach (DataType type in types)
                if (IsOrDerives(type.AssetType, typeName) && type.Assets.TryGetValue(text, out target))
                    return true;
            error = $"No {typeName} is called \"{text}\".";
            return false;
        }

        int hash = text.LastIndexOf('#');
        string path = hash >= 0 ? text.Substring(0, hash) : text;
        string subName = hash >= 0 ? text.Substring(hash + 1) : null;
        Object[] matches = subName != null
            ? AssetDatabase.LoadAllAssetsAtPath(path).Where(o => o != null && o.name == subName && IsOrDerives(o.GetType(), typeName)).ToArray()
            : new[] { AssetDatabase.LoadMainAssetAtPath(path) }.Where(o => o != null && IsOrDerives(o.GetType(), typeName)).ToArray();
        if (matches.Length == 1)
        {
            target = matches[0];
            return true;
        }
        error = matches.Length == 0 ? $"No {typeName} found at \"{text}\"." : $"\"{text}\" matches {matches.Length} objects.";
        return false;
    }

    static bool IsOrDerives(Type type, string typeName)
    {
        for (; type != null; type = type.BaseType)
            if (type.Name == typeName)
                return true;
        return false;
    }

    static void LogProblems(string title, Report report)
    {
        foreach (string problem in report.Problems)
            Debug.LogWarning($"{title}: {problem}");
    }
}
