using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class FastenerStatisticsWorkbookWriter
{
    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly Lazy<FastenerCatalog> Catalog = new(FastenerCatalog.LoadEmbedded);

    public static void Write(
        string path,
        string documentName,
        DateTimeOffset exportedAt,
        FastenerStatisticsReport report)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("必须指定 Excel 文件路径。", nameof(path));
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("无法确定 Excel 文件夹。");
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException(directory);
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, false, Encoding.UTF8))
            {
                WriteXml(archive, "[Content_Types].xml", WriteContentTypes);
                WriteXml(archive, "_rels/.rels", WritePackageRelationships);
                WriteXml(archive, "xl/workbook.xml", WriteWorkbook);
                WriteXml(archive, "xl/_rels/workbook.xml.rels", WriteWorkbookRelationships);
                WriteXml(archive, "xl/styles.xml", WriteStyles);
                WriteXml(archive, "xl/worksheets/sheet1.xml", writer =>
                    WriteSummarySheet(writer, documentName, exportedAt, report));
                WriteXml(archive, "xl/worksheets/sheet2.xml", writer =>
                    WriteDetailSheet(writer, report));
            }
            File.Move(temporaryPath, fullPath, true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
            throw;
        }
    }

    private static void WriteContentTypes(XmlWriter writer)
    {
        writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
        const string contentTypeNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";
        WriteElement(writer, contentTypeNamespace, "Default", ("Extension", "rels"), ("ContentType", "application/vnd.openxmlformats-package.relationships+xml"));
        WriteElement(writer, contentTypeNamespace, "Default", ("Extension", "xml"), ("ContentType", "application/xml"));
        WriteElement(writer, contentTypeNamespace, "Override", ("PartName", "/xl/workbook.xml"), ("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"));
        WriteElement(writer, contentTypeNamespace, "Override", ("PartName", "/xl/styles.xml"), ("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"));
        WriteElement(writer, contentTypeNamespace, "Override", ("PartName", "/xl/worksheets/sheet1.xml"), ("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"));
        WriteElement(writer, contentTypeNamespace, "Override", ("PartName", "/xl/worksheets/sheet2.xml"), ("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"));
        writer.WriteEndElement();
    }

    private static void WritePackageRelationships(XmlWriter writer)
    {
        writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
        WriteElement(
            writer,
            "http://schemas.openxmlformats.org/package/2006/relationships",
            "Relationship",
            ("Id", "rId1"),
            ("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
            ("Target", "xl/workbook.xml"));
        writer.WriteEndElement();
    }

    private static void WriteWorkbook(XmlWriter writer)
    {
        writer.WriteStartElement("workbook", SpreadsheetNamespace);
        writer.WriteAttributeString("xmlns", "r", null, RelationshipNamespace);
        writer.WriteStartElement("sheets", SpreadsheetNamespace);
        WriteSheet(writer, "汇总", 1, "rId1");
        WriteSheet(writer, "明细", 2, "rId2");
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteWorkbookRelationships(XmlWriter writer)
    {
        writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
        const string packageRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
        WriteElement(writer, packageRelationshipNamespace, "Relationship", ("Id", "rId1"), ("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"), ("Target", "worksheets/sheet1.xml"));
        WriteElement(writer, packageRelationshipNamespace, "Relationship", ("Id", "rId2"), ("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"), ("Target", "worksheets/sheet2.xml"));
        WriteElement(writer, packageRelationshipNamespace, "Relationship", ("Id", "rId3"), ("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"), ("Target", "styles.xml"));
        writer.WriteEndElement();
    }

    private static void WriteStyles(XmlWriter writer)
    {
        writer.WriteStartElement("styleSheet", SpreadsheetNamespace);
        writer.WriteStartElement("fonts", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "2");
        WriteFont(writer, false);
        WriteFont(writer, true);
        writer.WriteEndElement();
        writer.WriteStartElement("fills", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "2");
        WritePatternFill(writer, "none");
        WritePatternFill(writer, "gray125");
        writer.WriteEndElement();
        writer.WriteStartElement("borders", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "1");
        writer.WriteStartElement("border", SpreadsheetNamespace);
        foreach (var side in new[] { "left", "right", "top", "bottom", "diagonal" })
            writer.WriteElementString(side, SpreadsheetNamespace, string.Empty);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("cellStyleXfs", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "1");
        WriteXf(writer, 0);
        writer.WriteEndElement();
        writer.WriteStartElement("cellXfs", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "2");
        WriteXf(writer, 0);
        WriteXf(writer, 1);
        writer.WriteEndElement();
        writer.WriteStartElement("cellStyles", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "1");
        WriteElement(writer, SpreadsheetNamespace, "cellStyle", ("name", "Normal"), ("xfId", "0"), ("builtinId", "0"));
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteSummarySheet(
        XmlWriter writer,
        string documentName,
        DateTimeOffset exportedAt,
        FastenerStatisticsReport report)
    {
        StartWorksheet(writer, 8, 9);
        WriteColumns(writer, [28, 16, 14, 14, 12]);
        writer.WriteStartElement("sheetData", SpreadsheetNamespace);
        WriteRow(writer, 1, [Text("紧固件统计汇总", true)]);
        WriteRow(writer, 2, [Text("文档"), Text(documentName)]);
        WriteRow(writer, 3, [Text("统计范围"), Text(ScopeLabel(report.Scope))]);
        WriteRow(writer, 4, [Text("导出时间"), Text(exportedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))]);
        WriteRow(writer, 5, [Text("总数"), Number(report.TotalCount), Text("螺丝"), Number(report.ScrewCount), Text("螺母"), Number(report.NutCount)]);
        WriteRow(writer, 8, [Text("类型", true), Text("规格", true), Text("长度 mm", true), Text("外径 mm", true), Text("数量", true)]);
        var rowIndex = 9;
        foreach (var row in report.SummaryRows)
        {
            WriteRow(writer, rowIndex++,
            [
                Text(row.Kind == FastenerKind.HexNut && row.NutStyle.HasValue
                    ? FastenerLabels.NutStyle(row.NutStyle.Value)
                    : FastenerLabels.Kind(row.Kind)),
                Text(row.Size),
                row.Length.HasValue ? Number(row.Length.Value) : Blank(),
                row.OuterDiameter.HasValue ? Number(row.OuterDiameter.Value) : Blank(),
                Number(row.Quantity)
            ]);
        }
        writer.WriteEndElement();
        WriteAutoFilter(writer, $"A8:E{Math.Max(8, rowIndex - 1)}");
        writer.WriteEndElement();
    }

    private static void WriteDetailSheet(XmlWriter writer, FastenerStatisticsReport report)
    {
        StartWorksheet(writer, 1, 2);
        WriteColumns(writer, [8, 24, 14, 18, 22, 10, 12, 12, 14, 14, 14, 14, 14, 14, 14, 12, 12, 24, 16, 14, 14, 14, 14, 14, 38]);
        writer.WriteStartElement("sheetData", SpreadsheetNamespace);
        var headers = new[]
        {
            "序号", "类型", "装配角色", "螺母样式", "尺寸标准", "规格", "长度 mm", "外径 mm", "孔径补偿 mm", "深度补偿 mm", "嵌入深度 mm", "孔径修正 mm", "通孔最终直径 mm", "咬合缩减 mm", "咬合最终直径 mm",
            "通孔宿主数", "咬合宿主数", "咬合深度模式", "装配方式", "末端露出量 mm", "螺母槽补偿 mm", "控制点 X", "控制点 Y", "控制点 Z", "组件 ID"
        };
        WriteRow(writer, 1, headers.Select(value => Text(value, true)).ToArray());
        var rowIndex = 2;
        foreach (var component in report.Components)
        {
            WriteRow(writer, rowIndex, BuildDetailCells(component, false, rowIndex - 1));
            rowIndex++;
            if (FastenerKindTraits.IsScrew(component.Kind)
                && component.AssemblyMode == ScrewAssemblyMode.NutFastened)
            {
                WriteRow(writer, rowIndex, BuildDetailCells(component, true, rowIndex - 1));
                rowIndex++;
            }
        }
        writer.WriteEndElement();
        WriteAutoFilter(writer, $"A1:Y{Math.Max(1, rowIndex - 1)}");
        writer.WriteEndElement();
    }

    private static CellValue[] BuildDetailCells(
        FastenerComponentData component,
        bool pairedNut,
        int sequence)
    {
        var clearance = component.Bindings.FirstOrDefault(binding => binding.Role == ShaftFitRole.Clearance);
        var engagement = component.Bindings.FirstOrDefault(binding => binding.Role == ShaftFitRole.ThreadEngagement);
        var depthModes = string.Join(", ", component.Bindings
            .Where(binding => binding.Role == ShaftFitRole.ThreadEngagement)
            .Select(binding => FastenerLabels.Depth(binding.DepthMode))
            .Distinct());
        var spec = Catalog.Value.Sizes.FirstOrDefault(item =>
            string.Equals(item.Designation, component.Size, StringComparison.OrdinalIgnoreCase));
        var clearanceDiameter = spec is not null && clearance is not null
            ? HoleDiameterCalculator.Calculate(component, spec, clearance).FinalDiameter
            : (double?)null;
        var engagementDiameter = spec is not null && engagement is not null
            ? HoleDiameterCalculator.Calculate(component, spec, engagement).FinalDiameter
            : (double?)null;
        var role = pairedNut ? "配套螺母" : FastenerKindTraits.IsScrew(component.Kind) ? "螺丝" : "独立螺母";
        var type = pairedNut
            ? FastenerLabels.NutStyle(component.PairedNutStyle)
            : FastenerLabels.Kind(component.Kind);
        var nutStyle = pairedNut
            ? component.PairedNutStyle
            : component.Kind == FastenerKind.HexNut ? component.HexNutStyle : (HexNutStyle?)null;
        var position = (X: component.Placement.OriginX, Y: component.Placement.OriginY, Z: component.Placement.OriginZ);
        if (pairedNut && spec is not null)
        {
            var range = PairedNutAssemblyCalculator.AxialRange(component, spec);
            var distance = (range.InnerFace + range.OuterFace) / 2;
            position = (
                component.Placement.OriginX + component.Placement.ZAxisX * distance,
                component.Placement.OriginY + component.Placement.ZAxisY * distance,
                component.Placement.OriginZ + component.Placement.ZAxisZ * distance);
        }
        return
        [
            Number(sequence),
            Text(type),
            Text(role),
            nutStyle.HasValue ? Text(HexNutDimensions.StyleLabel(nutStyle.Value)) : Blank(),
            nutStyle.HasValue ? Text(FastenerLabels.NutStandard(nutStyle.Value, component.Size)) : Blank(),
            Text(component.Size),
            !pairedNut && FastenerKindTraits.UsesLengthInStatistics(component.Kind) ? Number(component.Length) : Blank(),
            !pairedNut && component.Kind == FastenerKind.HeatSetInsert ? Number(component.InsertOuterDiameter) : Blank(),
            !pairedNut && component.Kind == FastenerKind.HeatSetInsert ? Number(component.InsertDiameterCompensation) : Blank(),
            !pairedNut && component.Kind == FastenerKind.HeatSetInsert ? Number(component.InsertDepthCompensation) : Blank(),
            !pairedNut ? Number(component.HeadEmbedDepth) : Blank(),
            !pairedNut ? Number(component.PrintProfile.HoleDiameterCorrection) : Blank(),
            !pairedNut && clearanceDiameter.HasValue ? Number(clearanceDiameter.Value) : Blank(),
            !pairedNut && engagement is not null ? Number(engagement.BiteReduction) : Blank(),
            !pairedNut && engagementDiameter.HasValue ? Number(engagementDiameter.Value) : Blank(),
            !pairedNut ? Number(component.Bindings.Count(binding => binding.Role == ShaftFitRole.Clearance)) : Blank(),
            !pairedNut ? Number(component.Bindings.Count(binding => binding.Role == ShaftFitRole.ThreadEngagement)) : Blank(),
            !pairedNut ? Text(depthModes) : Blank(),
            Text(FastenerLabels.AssemblyMode(component.AssemblyMode)),
            component.AssemblyMode == ScrewAssemblyMode.NutFastened ? Number(component.NutTipProtrusion) : Blank(),
            component.AssemblyMode == ScrewAssemblyMode.NutFastened ? Number(component.NutPocketCompensation) : Blank(),
            Number(position.X),
            Number(position.Y),
            Number(position.Z),
            Text(component.ComponentId.ToString("D"))
        ];
    }

    private static void StartWorksheet(XmlWriter writer, int frozenRows, int topRow)
    {
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        writer.WriteStartElement("sheetViews", SpreadsheetNamespace);
        writer.WriteStartElement("sheetView", SpreadsheetNamespace);
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane", SpreadsheetNamespace);
        writer.WriteAttributeString("ySplit", frozenRows.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("topLeftCell", $"A{topRow}");
        writer.WriteAttributeString("activePane", "bottomLeft");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteColumns(XmlWriter writer, IReadOnlyList<double> widths)
    {
        writer.WriteStartElement("cols", SpreadsheetNamespace);
        for (var index = 0; index < widths.Count; index++)
        {
            writer.WriteStartElement("col", SpreadsheetNamespace);
            writer.WriteAttributeString("min", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("max", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("width", widths[index].ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customWidth", "1");
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static void WriteRow(XmlWriter writer, int rowIndex, IReadOnlyList<CellValue> cells)
    {
        writer.WriteStartElement("row", SpreadsheetNamespace);
        writer.WriteAttributeString("r", rowIndex.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < cells.Count; index++)
            WriteCell(writer, CellReference(index + 1, rowIndex), cells[index]);
        writer.WriteEndElement();
    }

    private static void WriteCell(XmlWriter writer, string reference, CellValue cell)
    {
        writer.WriteStartElement("c", SpreadsheetNamespace);
        writer.WriteAttributeString("r", reference);
        if (cell.IsHeader)
            writer.WriteAttributeString("s", "1");
        if (cell.Kind == CellKind.Blank)
        {
            writer.WriteEndElement();
            return;
        }
        if (cell.Kind == CellKind.Number)
        {
            writer.WriteAttributeString("t", "n");
            writer.WriteElementString("v", SpreadsheetNamespace, cell.Value);
        }
        else
        {
            writer.WriteAttributeString("t", "inlineStr");
            writer.WriteStartElement("is", SpreadsheetNamespace);
            writer.WriteStartElement("t", SpreadsheetNamespace);
            if (cell.Value.Length > 0 && (char.IsWhiteSpace(cell.Value[0]) || char.IsWhiteSpace(cell.Value[^1])))
                writer.WriteAttributeString("xml", "space", null, "preserve");
            writer.WriteString(cell.Value);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
    }

    private static void WriteAutoFilter(XmlWriter writer, string reference)
    {
        writer.WriteStartElement("autoFilter", SpreadsheetNamespace);
        writer.WriteAttributeString("ref", reference);
        writer.WriteEndElement();
    }

    private static void WriteSheet(XmlWriter writer, string name, int id, string relationshipId)
    {
        writer.WriteStartElement("sheet", SpreadsheetNamespace);
        writer.WriteAttributeString("name", name);
        writer.WriteAttributeString("sheetId", id.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("r", "id", RelationshipNamespace, relationshipId);
        writer.WriteEndElement();
    }

    private static void WriteFont(XmlWriter writer, bool bold)
    {
        writer.WriteStartElement("font", SpreadsheetNamespace);
        if (bold)
            writer.WriteElementString("b", SpreadsheetNamespace, string.Empty);
        writer.WriteStartElement("sz", SpreadsheetNamespace);
        writer.WriteAttributeString("val", "11");
        writer.WriteEndElement();
        writer.WriteStartElement("name", SpreadsheetNamespace);
        writer.WriteAttributeString("val", "Calibri");
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WritePatternFill(XmlWriter writer, string pattern)
    {
        writer.WriteStartElement("fill", SpreadsheetNamespace);
        writer.WriteStartElement("patternFill", SpreadsheetNamespace);
        writer.WriteAttributeString("patternType", pattern);
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteXf(XmlWriter writer, int fontId)
    {
        writer.WriteStartElement("xf", SpreadsheetNamespace);
        writer.WriteAttributeString("numFmtId", "0");
        writer.WriteAttributeString("fontId", fontId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("fillId", "0");
        writer.WriteAttributeString("borderId", "0");
        writer.WriteAttributeString("xfId", "0");
        if (fontId > 0)
            writer.WriteAttributeString("applyFont", "1");
        writer.WriteEndElement();
    }

    private static void WriteElement(
        XmlWriter writer,
        string namespaceUri,
        string name,
        params (string Name, string Value)[] attributes)
    {
        writer.WriteStartElement(name, namespaceUri);
        foreach (var attribute in attributes)
            writer.WriteAttributeString(attribute.Name, attribute.Value);
        writer.WriteEndElement();
    }

    private static void WriteXml(ZipArchive archive, string entryName, Action<XmlWriter> write)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            CloseOutput = false
        });
        writer.WriteStartDocument();
        write(writer);
        writer.WriteEndDocument();
    }

    private static string CellReference(int column, int row)
    {
        var letters = string.Empty;
        while (column > 0)
        {
            column--;
            letters = (char)('A' + column % 26) + letters;
            column /= 26;
        }
        return letters + row.ToString(CultureInfo.InvariantCulture);
    }

    private static string ScopeLabel(FastenerStatisticsScope scope) =>
        scope == FastenerStatisticsScope.Selected ? "当前选择" : "全部组件";

    private static CellValue Text(string value, bool header = false) => new(CellKind.Text, value ?? string.Empty, header);
    private static CellValue Number(double value) => new(CellKind.Number, value.ToString("0.###############", CultureInfo.InvariantCulture), false);
    private static CellValue Blank() => new(CellKind.Blank, string.Empty, false);

    private enum CellKind { Blank, Text, Number }
    private sealed record CellValue(CellKind Kind, string Value, bool IsHeader);
}
