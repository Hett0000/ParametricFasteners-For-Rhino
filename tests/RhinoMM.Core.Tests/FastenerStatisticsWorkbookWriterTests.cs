using System.IO.Compression;
using System.Xml.Linq;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class FastenerStatisticsWorkbookWriterTests
{
    [Fact]
    public void Write_CreatesTwoValidWorksheetPartsWithTextAndNumbers()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "项目_紧固件统计.xlsx");
            var component = new FastenerComponentData
            {
                ComponentId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
                Kind = FastenerKind.SocketCap,
                Size = "M3<&>",
                Length = 12,
                Placement = PlacementFrame.WorldXY,
                Bindings =
                [
                    new HoleTargetBinding { Role = ShaftFitRole.Clearance },
                    new HoleTargetBinding
                    {
                        Role = ShaftFitRole.ThreadEngagement,
                        BiteReduction = 0.35,
                        DepthMode = DepthMode.FastenerLengthPlusOneDiameter
                    }
                ]
            };
            var report = FastenerStatisticsBuilder.Build([component], FastenerStatisticsScope.Selected);

            FastenerStatisticsWorkbookWriter.Write(
                path,
                "项目<&>.3dm",
                new DateTimeOffset(2026, 7, 17, 15, 30, 0, TimeSpan.FromHours(8)),
                report);

            Assert.True(File.Exists(path));
            using var archive = ZipFile.OpenRead(path);
            var expected = new[]
            {
                "[Content_Types].xml",
                "_rels/.rels",
                "xl/workbook.xml",
                "xl/_rels/workbook.xml.rels",
                "xl/styles.xml",
                "xl/worksheets/sheet1.xml",
                "xl/worksheets/sheet2.xml"
            };
            foreach (var entryName in expected)
            {
                var entry = Assert.Single(archive.Entries, item => item.FullName == entryName);
                using var stream = entry.Open();
                _ = XDocument.Load(stream);
            }

            var workbook = ReadEntry(archive, "xl/workbook.xml");
            Assert.Contains("汇总", workbook, StringComparison.Ordinal);
            Assert.Contains("明细", workbook, StringComparison.Ordinal);
            var summary = ReadEntry(archive, "xl/worksheets/sheet1.xml");
            Assert.Contains("项目&lt;&amp;&gt;.3dm", summary, StringComparison.Ordinal);
            Assert.Contains("M3&lt;&amp;&gt;", summary, StringComparison.Ordinal);
            Assert.Contains("外径 mm", summary, StringComparison.Ordinal);
            var detail = ReadEntry(archive, "xl/worksheets/sheet2.xml");
            Assert.Contains("r=\"D2\" t=\"n\"><v>12</v>", detail, StringComparison.Ordinal);
            Assert.Contains("切割补偿 mm", detail, StringComparison.Ordinal);
            Assert.Contains("11111111-2222-3333-4444-555555555555", detail, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Write_OverwritesExistingWorkbookAndSupportsEmptyReport()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "empty.xlsx");
            File.WriteAllText(path, "old");
            var report = FastenerStatisticsBuilder.Build([], FastenerStatisticsScope.All);

            FastenerStatisticsWorkbookWriter.Write(path, "Untitled", DateTimeOffset.UtcNow, report);

            using var archive = ZipFile.OpenRead(path);
            Assert.NotNull(archive.GetEntry("xl/worksheets/sheet1.xml"));
            Assert.NotNull(archive.GetEntry("xl/worksheets/sheet2.xml"));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Write_RemovesTemporaryFileWhenFinalMoveFails()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var report = FastenerStatisticsBuilder.Build([], FastenerStatisticsScope.All);

            Assert.ThrowsAny<Exception>(() => FastenerStatisticsWorkbookWriter.Write(
                directory,
                "Untitled",
                DateTimeOffset.UtcNow,
                report));

            var parent = Directory.GetParent(directory)!.FullName;
            var prefix = $".{Path.GetFileName(directory)}.";
            Assert.DoesNotContain(
                Directory.EnumerateFiles(parent),
                file => Path.GetFileName(file).StartsWith(prefix, StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string ReadEntry(ZipArchive archive, string name)
    {
        using var reader = new StreamReader(archive.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"RhinoMM-Statistics-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
