using System.Text.Json;
using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class FastenerTemplateDataTests
{
    [Fact]
    public void Signature_IgnoresFieldsThatDoNotApplyToHexNut()
    {
        var first = new FastenerTemplateData
        {
            Kind = FastenerKind.HexNut,
            NutStyle = HexNutStyle.NylonInsertLocking,
            Size = "M4",
            HeadEmbedDepth = 6,
            Length = 12,
            BiteReduction = 0.2,
            AssemblyMode = ScrewAssemblyMode.NutFastened
        };
        var second = first with
        {
            Length = 60,
            BiteReduction = 0.8,
            AssemblyMode = ScrewAssemblyMode.EngagementOnly
        };

        Assert.Equal(first.Signature(), second.Signature());
    }

    [Fact]
    public void Signature_PreservesReusableHiddenSettingsForScrewTemplates()
    {
        var first = new FastenerTemplateData
        {
            Kind = FastenerKind.SocketCap,
            Size = "M4",
            AssemblyMode = ScrewAssemblyMode.NutFastened,
            BiteReduction = 0.3,
            CounterboreBridgeLayerHeight = 0.16
        };
        var second = first with
        {
            BiteReduction = 0.45,
            CounterboreBridgeLayerHeight = 0.28
        };

        Assert.NotEqual(first.Signature(), second.Signature());
    }

    [Fact]
    public void FromComponent_CapturesReusableParametersWithoutIdentityOrPlacement()
    {
        var component = new FastenerComponentData
        {
            ComponentId = Guid.NewGuid(),
            Kind = FastenerKind.SocketCap,
            Size = "M5",
            Length = 30,
            HeadEmbedDepth = 2,
            AssemblyMode = ScrewAssemblyMode.ThreadEngagement,
            Placement = PlacementFrame.WorldXY with { OriginX = 45 },
            Bindings =
            [
                new HoleTargetBinding
                {
                    TargetObjectId = Guid.NewGuid(),
                    Role = ShaftFitRole.ThreadEngagement,
                    BiteReduction = 0.42,
                    DepthMode = DepthMode.FastenerLengthPlusCustom,
                    BlindDepth = 5,
                    IsPreviewVisible = false,
                    IsBooleanEnabled = true
                }
            ]
        };

        var template = FastenerTemplateData.FromComponent(component);

        Assert.Equal("M5", template.Size);
        Assert.Equal(30, template.Length);
        Assert.Equal(0.42, template.BiteReduction);
        Assert.Equal(DepthMode.FastenerLengthPlusCustom, template.EngagementDepthMode);
        Assert.Equal(5, template.EngagementBlindDepth);
        Assert.False(template.EngagementPreviewVisible);
        Assert.True(template.EngagementBooleanEnabled);
        Assert.DoesNotContain(component.ComponentId.ToString("D"), template.Signature(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("45", template.Signature(), StringComparison.Ordinal);
    }

    [Fact]
    public void LibraryDocument_RoundTripsStringEnumsAndLastOperations()
    {
        var data = new FastenerTemplateData
        {
            Kind = FastenerKind.HeatSetInsert,
            Size = "M3",
            Length = 5,
            InsertOuterDiameter = 4.6,
            InsertDiameterCompensation = -0.2,
            InsertDepthCompensation = 1
        }.Normalize();
        var document = new FastenerTemplateLibraryDocument
        {
            Recent = [new FastenerTemplateEntry(Guid.NewGuid(), "M3 热熔", data, DateTimeOffset.UtcNow)],
            Favorites = [new FastenerTemplateEntry(
                Guid.NewGuid(),
                "常用热熔",
                data,
                DateTimeOffset.UtcNow,
                new AssemblySchemeOptions(AdaptiveLength: true, OutputFormats: 3))],
            LastPlacement = data,
            LastUpdate = data
        };

        var json = JsonSerializer.Serialize(document, FastenerTemplateData.JsonOptions());
        var restored = JsonSerializer.Deserialize<FastenerTemplateLibraryDocument>(
            json,
            FastenerTemplateData.JsonOptions());

        Assert.NotNull(restored);
        Assert.Equal(FastenerKind.HeatSetInsert, restored!.LastPlacement!.Kind);
        Assert.Single(restored.Recent);
        Assert.Single(restored.Favorites);
        Assert.True(restored.Favorites[0].Scheme!.AdaptiveLength);
        Assert.Equal(3, restored.Favorites[0].Scheme!.OutputFormats);
        Assert.Equal(2, restored.SchemaVersion);
        Assert.Contains("HeatSetInsert", json, StringComparison.Ordinal);
    }
}
