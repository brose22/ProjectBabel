using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

// One worksheet: rows of cells, each a string, or a double or bool that is written as a number or
// a TRUE/FALSE cell. A null cell is left empty.
public class XlsxSheet
{
    public string Name;
    public readonly List<object[]> Rows = new();
    // Read back from a file, every cell is text and row 1 of the sheet is Rows[0].
    public readonly List<int> RowNumbers = new();

    public XlsxSheet(string name) => Name = name;
}

// Reads and writes just enough of the .xlsx format for tables of data, so the data tools need no Excel
// library. A workbook is a zip of XML parts. Writing uses inline strings; reading takes what Excel
// saves (shared strings, formula results, skipped empty cells) as text.
public static class XlsxFile
{
    const string MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    const string ContentNs = "http://schemas.openxmlformats.org/package/2006/content-types";
    const string WorkbookType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
    const string SheetType = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";
    const string StylesType = "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml";
    const string RelBase = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
    const int HeaderStyle = 1;
    const int MaxColumnWidth = 40;

    static readonly XNamespace Main = MainNs;
    static readonly XNamespace Rel = RelNs;
    static readonly XNamespace PackageRel = PackageRelNs;
    static readonly XNamespace Content = ContentNs;
    static readonly Regex EscapeCode = new("_x([0-9A-Fa-f]{4})_");

    // ---------- Writing ----------

    // Builds the whole file in memory first, so a failure can't leave half a workbook behind.
    public static void Write(string path, IReadOnlyList<XlsxSheet> sheets)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            AddPart(zip, "[Content_Types].xml", ContentTypes(sheets.Count));
            AddPart(zip, "_rels/.rels", new XElement(PackageRel + "Relationships",
                Relationship("rId1", "officeDocument", "xl/workbook.xml")));
            AddPart(zip, "xl/workbook.xml", Workbook(sheets));
            AddPart(zip, "xl/_rels/workbook.xml.rels", WorkbookRelationships(sheets.Count));
            AddPart(zip, "xl/styles.xml", Styles());
            for (int i = 0; i < sheets.Count; i++)
                AddPart(zip, $"xl/worksheets/sheet{i + 1}.xml", Worksheet(sheets[i]));
        }
        File.WriteAllBytes(path, memory.ToArray());
    }

    static void AddPart(ZipArchive zip, string name, XElement root)
    {
        using Stream stream = zip.CreateEntry(name).Open();
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), NewLineHandling = NewLineHandling.Entitize };
        using var writer = XmlWriter.Create(stream, settings);
        new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root).Save(writer);
    }

    static XElement Relationship(string id, string type, string target) => new(PackageRel + "Relationship",
        new XAttribute("Id", id), new XAttribute("Type", RelBase + type), new XAttribute("Target", target));

    static XElement ContentTypes(int sheetCount)
    {
        var root = new XElement(Content + "Types",
            new XElement(Content + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
            new XElement(Content + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
            Override("/xl/workbook.xml", WorkbookType),
            Override("/xl/styles.xml", StylesType));
        for (int i = 1; i <= sheetCount; i++)
            root.Add(Override($"/xl/worksheets/sheet{i}.xml", SheetType));
        return root;
    }

    static XElement Override(string part, string type) => new(Content + "Override",
        new XAttribute("PartName", part), new XAttribute("ContentType", type));

    static XElement Workbook(IReadOnlyList<XlsxSheet> sheets)
    {
        var list = new XElement(Main + "sheets");
        for (int i = 0; i < sheets.Count; i++)
            list.Add(new XElement(Main + "sheet", new XAttribute("name", sheets[i].Name),
                new XAttribute("sheetId", i + 1), new XAttribute(Rel + "id", $"rId{i + 1}")));
        return new XElement(Main + "workbook", new XAttribute(XNamespace.Xmlns + "r", RelNs), list);
    }

    static XElement WorkbookRelationships(int sheetCount)
    {
        var root = new XElement(PackageRel + "Relationships");
        for (int i = 1; i <= sheetCount; i++)
            root.Add(Relationship($"rId{i}", "worksheet", $"worksheets/sheet{i}.xml"));
        root.Add(Relationship($"rId{sheetCount + 1}", "styles", "styles.xml"));
        return root;
    }

    // Style 0 is the default; HeaderStyle is bold.
    static XElement Styles() => new(Main + "styleSheet",
        new XElement(Main + "fonts", new XAttribute("count", 2),
            new XElement(Main + "font", new XElement(Main + "sz", new XAttribute("val", 11)), new XElement(Main + "name", new XAttribute("val", "Calibri"))),
            new XElement(Main + "font", new XElement(Main + "b"), new XElement(Main + "sz", new XAttribute("val", 11)), new XElement(Main + "name", new XAttribute("val", "Calibri")))),
        new XElement(Main + "fills", new XAttribute("count", 2),
            new XElement(Main + "fill", new XElement(Main + "patternFill", new XAttribute("patternType", "none"))),
            new XElement(Main + "fill", new XElement(Main + "patternFill", new XAttribute("patternType", "gray125")))),
        new XElement(Main + "borders", new XAttribute("count", 1), new XElement(Main + "border")),
        new XElement(Main + "cellStyleXfs", new XAttribute("count", 1), new XElement(Main + "xf")),
        new XElement(Main + "cellXfs", new XAttribute("count", 2),
            new XElement(Main + "xf"),
            new XElement(Main + "xf", new XAttribute("fontId", 1), new XAttribute("applyFont", 1))));

    // The first row is a bold header that stays in view and carries a filter.
    static XElement Worksheet(XlsxSheet sheet)
    {
        int columnCount = sheet.Rows.Count > 0 ? sheet.Rows.Max(r => r.Length) : 0;
        var root = new XElement(Main + "worksheet");
        if (columnCount > 0)
        {
            root.Add(new XElement(Main + "sheetViews", new XElement(Main + "sheetView", new XAttribute("workbookViewId", 0),
                new XElement(Main + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"),
                    new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")))));
            root.Add(ColumnWidths(sheet, columnCount));
        }
        var data = new XElement(Main + "sheetData");
        for (int r = 0; r < sheet.Rows.Count; r++)
        {
            var row = new XElement(Main + "row", new XAttribute("r", r + 1));
            object[] cells = sheet.Rows[r];
            for (int c = 0; c < cells.Length; c++)
                if (cells[c] != null)
                    row.Add(Cell($"{ColumnName(c)}{r + 1}", cells[c], r == 0));
            data.Add(row);
        }
        root.Add(data);
        if (columnCount > 0 && sheet.Rows.Count > 0)
            root.Add(new XElement(Main + "autoFilter", new XAttribute("ref", $"A1:{ColumnName(columnCount - 1)}{sheet.Rows.Count}")));
        return root;
    }

    static XElement ColumnWidths(XlsxSheet sheet, int columnCount)
    {
        var columns = new XElement(Main + "cols");
        for (int c = 0; c < columnCount; c++)
        {
            int longest = sheet.Rows.Where(r => c < r.Length && r[c] != null).Select(r => Convert.ToString(r[c], CultureInfo.InvariantCulture).Length).DefaultIfEmpty(0).Max();
            int width = Math.Min(longest + 4, MaxColumnWidth);
            columns.Add(new XElement(Main + "col", new XAttribute("min", c + 1), new XAttribute("max", c + 1),
                new XAttribute("width", width), new XAttribute("customWidth", 1)));
        }
        return columns;
    }

    static XElement Cell(string reference, object value, bool header)
    {
        var cell = new XElement(Main + "c", new XAttribute("r", reference));
        if (header) cell.Add(new XAttribute("s", HeaderStyle));
        switch (value)
        {
            case bool flag:
                cell.Add(new XAttribute("t", "b"), new XElement(Main + "v", flag ? "1" : "0"));
                break;
            case double number:
                cell.Add(new XElement(Main + "v", number.ToString("R", CultureInfo.InvariantCulture)));
                break;
            default:
                var text = new XElement(Main + "t", EncodeText(Convert.ToString(value, CultureInfo.InvariantCulture)));
                text.Add(new XAttribute(XNamespace.Xml + "space", "preserve"));
                cell.Add(new XAttribute("t", "inlineStr"), new XElement(Main + "is", text));
                break;
        }
        return cell;
    }

    static string ColumnName(int index)
    {
        string name = "";
        for (int i = index + 1; i > 0; i = (i - 1) / 26)
            name = (char)('A' + (i - 1) % 26) + name;
        return name;
    }

    // Spreadsheet text writes characters XML can't carry as _xHHHH_, so an underscore that would read
    // as one of those codes is escaped too.
    static string EncodeText(string text)
    {
        var encoded = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            bool code = c == '_' && EscapeCode.Match(text, i, Math.Min(7, text.Length - i)).Success;
            bool invalid = !XmlConvert.IsXmlChar(c) && !IsSurrogatePair(text, i) && !(i > 0 && IsSurrogatePair(text, i - 1));
            if (code || invalid) encoded.Append($"_x{(int)c:X4}_");
            else encoded.Append(c);
        }
        return encoded.ToString();
    }

    static bool IsSurrogatePair(string text, int index) =>
        index + 1 < text.Length && char.IsSurrogatePair(text[index], text[index + 1]);

    static string DecodeText(string text) =>
        EscapeCode.Replace(text, m => ((char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString());

    // ---------- Reading ----------

    // Excel holds a lock on an open workbook but allows reading it, so importing doesn't need it closed.
    public static List<XlsxSheet> Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        XDocument workbook = Load(zip, "xl/workbook.xml") ?? throw new InvalidDataException("It has no workbook part.");
        Dictionary<string, string> targets = (Load(zip, "xl/_rels/workbook.xml.rels")?.Root?.Elements(PackageRel + "Relationship")
            ?? Enumerable.Empty<XElement>()).ToDictionary(e => (string)e.Attribute("Id"), e => (string)e.Attribute("Target"));
        List<string> shared = SharedStrings(zip);

        var sheets = new List<XlsxSheet>();
        foreach (XElement entry in workbook.Root.Element(Main + "sheets")?.Elements(Main + "sheet") ?? Enumerable.Empty<XElement>())
        {
            string id = (string)entry.Attribute(Rel + "id");
            if (id == null || !targets.TryGetValue(id, out string target)) continue;
            string part = target.StartsWith("/") ? target.Substring(1) : "xl/" + target;
            XDocument document = Load(zip, part) ?? throw new InvalidDataException($"Sheet {(string)entry.Attribute("name")} is missing.");
            sheets.Add(ReadSheet((string)entry.Attribute("name"), document, shared));
        }
        return sheets;
    }

    static XDocument Load(ZipArchive zip, string part)
    {
        ZipArchiveEntry entry = zip.GetEntry(part);
        if (entry == null) return null;
        using Stream stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    static List<string> SharedStrings(ZipArchive zip)
    {
        XDocument document = Load(zip, "xl/sharedStrings.xml");
        if (document == null) return new List<string>();
        return document.Root.Elements(Main + "si").Select(ItemText).ToList();
    }

    // Rich text keeps its words in runs, and phonetic hints in rPh aren't part of the value.
    static string ItemText(XElement item) => DecodeText(string.Concat(item.Descendants(Main + "t")
        .Where(t => t.Parent?.Name != Main + "rPh").Select(t => t.Value)));

    static XlsxSheet ReadSheet(string name, XDocument document, List<string> shared)
    {
        var sheet = new XlsxSheet(name);
        int rowNumber = 0;
        foreach (XElement row in document.Root.Element(Main + "sheetData")?.Elements(Main + "row") ?? Enumerable.Empty<XElement>())
        {
            rowNumber = (int?)row.Attribute("r") ?? rowNumber + 1;
            var cells = new List<object>();
            int column = 0;
            foreach (XElement cell in row.Elements(Main + "c"))
            {
                string reference = (string)cell.Attribute("r");
                if (reference != null) column = ColumnIndex(reference);
                while (cells.Count <= column) cells.Add(null);
                cells[column++] = CellText(cell, shared);
            }
            sheet.Rows.Add(cells.ToArray());
            sheet.RowNumbers.Add(rowNumber);
        }
        return sheet;
    }

    static int ColumnIndex(string reference)
    {
        int index = 0;
        foreach (char c in reference)
        {
            if (!char.IsLetter(c)) break;
            index = index * 26 + char.ToUpperInvariant(c) - 'A' + 1;
        }
        return index - 1;
    }

    static string CellText(XElement cell, List<string> shared)
    {
        string value = cell.Element(Main + "v")?.Value;
        switch ((string)cell.Attribute("t"))
        {
            case "s":
                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) && index >= 0 && index < shared.Count
                    ? shared[index] : throw new InvalidDataException($"Cell {(string)cell.Attribute("r")} points at a missing shared string.");
            case "inlineStr":
                return cell.Element(Main + "is") is { } inline ? ItemText(inline) : null;
            case "str":
                return value != null ? DecodeText(value) : null;
            case "b":
                return value == "1" ? "TRUE" : "FALSE";
            default:
                return value;
        }
    }
}
