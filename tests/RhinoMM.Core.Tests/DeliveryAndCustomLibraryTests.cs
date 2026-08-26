using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using System.IO.Compression;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class DeliveryAndCustomLibraryTests
{
    [Fact]
    public void SchemaSixteenMigratesWithEmptyDeliveryAndNoCustomDefinition()
    {
        var source = new FastenerComponentData
        {
            SchemaVersion = 16,
            Delivery = new DeliveryMetadata("OLD", "OLD", "OLD"),
            CustomDefinitionId = Guid.NewGuid(),
            CustomDefinitionName = "OLD"
        };
        var migrated = ComponentJson.Migrate(source);
        Assert.Equal(FastenerComponentData.CurrentSchemaVersion, migrated.SchemaVersion);
        Assert.Equal(new DeliveryMetadata(), migrated.Delivery);
        Assert.Null(migrated.CustomDefinitionId);
        Assert.Null(migrated.CustomDefinitionSnapshot);
    }

    [Fact]
    public void CustomSnapshotIsAuthoritativeForSpecResolution()
    {
        var builtIn = FastenerCatalog.LoadEmbedded();
        var custom = builtIn.Get("M3") with { NominalDiameter = 3.25 };
        var component = new FastenerComponentData
        {
            Size = "M3",
            CustomDefinitionId = Guid.NewGuid(),
            CustomDefinitionName = "M3 特制",
            CustomDefinitionSnapshot = new FastenerDefinitionSnapshot(
                Guid.NewGuid(), "M3 特制", FastenerKind.SocketCap, "M3", "test", "2", "", custom)
        };
        Assert.Equal(3.25, FastenerSpecResolver.Resolve(component, builtIn).NominalDiameter, 6);
    }

    [Fact]
    public void UserDefinitionRejectsInvalidHeatSetOuterDiameter()
    {
        var spec = FastenerCatalog.LoadEmbedded().Get("M3");
        var definition = new UserFastenerDefinition
        {
            Kind = FastenerKind.HeatSetInsert,
            Name = "invalid",
            ThreadDesignation = "M3",
            SizeSpec = spec,
            DefaultLength = 5,
            DefaultInsertOuterDiameter = 3
        };
        var result = UserFastenerDefinitionValidator.Validate(definition);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "CUSTOM_INSERT_RELATION");
    }

    [Fact]
    public void TemplateSchemaTwoKeepsCustomSnapshot()
    {
        var spec = FastenerCatalog.LoadEmbedded().Get("M4");
        var id = Guid.NewGuid();
        var template = new FastenerTemplateData
        {
            CustomDefinitionId = id,
            CustomDefinitionName = "M4 custom",
            CustomDefinitionSnapshot = new FastenerDefinitionSnapshot(
                id, "M4 custom", FastenerKind.SocketCap, "M4", "user", "1", "", spec)
        }.Normalize();
        Assert.Equal(FastenerTemplateData.CurrentSchemaVersion, template.SchemaVersion);
        Assert.Equal(id, template.CustomDefinitionId);
        Assert.NotNull(template.CustomDefinitionSnapshot);
    }

    [Fact]
    public void CsvBomExpandsPairedNutAndWritesDeliveryAndHostFields()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"RhinoMM-csv-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var id = Guid.NewGuid();
            var component = new FastenerComponentData
            {
                ComponentId = id,
                Kind = FastenerKind.SocketCap,
                Size = "M3",
                Length = 20,
                AssemblyMode = ScrewAssemblyMode.NutFastened,
                Delivery = new DeliveryMetadata("A-007", "机架", "现场配装")
            };
            var report = FastenerStatisticsBuilder.Build([component], FastenerStatisticsScope.All);
            var path = Path.Combine(directory, "bom.csv");
            FastenerStatisticsCsvWriter.Write(path, report, new Dictionary<Guid, FastenerDeliveryHostInfo>
            {
                [id] = new("前板 / 后板", "结构::板件")
            });

            var csv = File.ReadAllText(path);
            Assert.Contains("装配编号", csv, StringComparison.Ordinal);
            Assert.Contains("A-007", csv, StringComparison.Ordinal);
            Assert.Contains("前板 / 后板", csv, StringComparison.Ordinal);
            Assert.Contains("配套螺母", csv, StringComparison.Ordinal);
            Assert.Equal(3, File.ReadAllLines(path).Length);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ExcelBomWritesDeliveryAndHostFields()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"RhinoMM-xlsx-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var id = Guid.NewGuid();
            var component = new FastenerComponentData
            {
                ComponentId = id,
                Kind = FastenerKind.HexNut,
                Size = "M4",
                Delivery = new DeliveryMetadata("N-01", "外壳", "保留备件")
            };
            var report = FastenerStatisticsBuilder.Build([component], FastenerStatisticsScope.All);
            var path = Path.Combine(directory, "bom.xlsx");
            FastenerStatisticsWorkbookWriter.Write(path, "Project.3dm", DateTimeOffset.UtcNow, report,
                new Dictionary<Guid, FastenerDeliveryHostInfo> { [id] = new("底座", "零件::底座") });

            using var archive = ZipFile.OpenRead(path);
            var entry = Assert.Single(archive.Entries, item => item.FullName == "xl/worksheets/sheet2.xml");
            using var reader = new StreamReader(entry.Open());
            var xml = reader.ReadToEnd();
            Assert.Contains("装配编号", xml, StringComparison.Ordinal);
            Assert.Contains("N-01", xml, StringComparison.Ordinal);
            Assert.Contains("项目分组", xml, StringComparison.Ordinal);
            Assert.Contains("底座", xml, StringComparison.Ordinal);
            Assert.Contains("零件::底座", xml, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
