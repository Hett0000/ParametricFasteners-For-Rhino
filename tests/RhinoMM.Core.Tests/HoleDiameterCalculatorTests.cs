using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public class HoleDiameterCalculatorTests
{
    private static readonly FastenerCatalog Catalog = FastenerCatalog.LoadEmbedded();

    [Fact]
    public void CatalogContainsM16()
    {
        var spec = Catalog.Get("M1.6");
        Assert.Equal(1.6, spec.NominalDiameter);
        Assert.Equal(0.35, spec.CoarsePitch);
    }

    [Fact]
    public void ClearanceUsesStandardPlusCorrections()
    {
        var binding = new HoleTargetBinding
        {
            TargetObjectId = Guid.NewGuid(),
            Role = ShaftFitRole.Clearance,
            ClearanceFit = ClearanceFitClass.Normal,
            BindingOverride = 0.05
        };

        var result = HoleDiameterCalculator.Calculate(
            Catalog.Get("M3"), binding, new PrintProfileSnapshot("PLA", 0.15));

        Assert.Equal(3.6, result.FinalDiameter, 6);
    }

    [Fact]
    public void EngagementUsesNominalMinusBitePlusCorrections()
    {
        var binding = new HoleTargetBinding
        {
            TargetObjectId = Guid.NewGuid(),
            Role = ShaftFitRole.ThreadEngagement,
            BiteReduction = 0.35,
            BindingOverride = -0.05
        };

        var result = HoleDiameterCalculator.Calculate(
            Catalog.Get("M3"), binding, new PrintProfileSnapshot("PETG", 0.1));

        Assert.Equal(2.7, result.FinalDiameter, 6);
    }

    [Fact]
    public void EngagementRequiresCalibratedBite()
    {
        var component = new FastenerComponentData
        {
            Size = "M3",
            Bindings = [new HoleTargetBinding
            {
                TargetObjectId = Guid.NewGuid(),
                Role = ShaftFitRole.ThreadEngagement,
                BiteReduction = 0
            }]
        };

        var validation = FastenerComponentValidator.Validate(component, Catalog.Get("M3"));
        Assert.Contains(validation.Issues, issue => issue.Code == "bite-required");
    }

    [Fact]
    public void OnlyOneHeadSeatIsAllowed()
    {
        var component = new FastenerComponentData
        {
            Size = "M4",
            Bindings =
            [
                new HoleTargetBinding { TargetObjectId = Guid.NewGuid(), IncludeHeadSeat = true },
                new HoleTargetBinding { TargetObjectId = Guid.NewGuid(), IncludeHeadSeat = true }
            ]
        };

        var validation = FastenerComponentValidator.Validate(component, Catalog.Get("M4"));
        Assert.Contains(validation.Issues, issue => issue.Code == "head-seat");
    }

    [Fact]
    public void SchemaV1MigratesAppearanceAndPreviewDefaults()
    {
        var componentId = Guid.NewGuid();
        var bindingId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var json = $$"""
        {
          "schemaVersion": 1,
          "componentId": "{{componentId}}",
          "kind": "SocketCap",
          "size": "M3",
          "length": 12,
          "bindings": [
            {
              "bindingId": "{{bindingId}}",
              "targetObjectId": "{{targetId}}",
              "role": "Clearance",
              "includeHeadSeat": true
            }
          ]
        }
        """;

        var migrated = ComponentJson.Deserialize(json);

        Assert.Equal(6, migrated.SchemaVersion);
        Assert.Equal(3.0, migrated.HeadEmbedDepth, 6);
        Assert.Equal(Guid.Empty, migrated.ControlPointObjectId);
        Assert.Equal(70, migrated.FastenerOpacityPercent);
        Assert.Equal(35, migrated.CutterOpacityPercent);
        Assert.True(migrated.Bindings.Single().IsPreviewVisible);
        Assert.True(migrated.Bindings.Single().IsBooleanEnabled);
    }

    [Fact]
    public void SchemaV2PreservesAppearanceAndEnablesBooleanByDefault()
    {
        var data = new FastenerComponentData
        {
            SchemaVersion = 2,
            FastenerOpacityPercent = 42,
            CutterOpacityPercent = 18,
            Bindings = [new HoleTargetBinding
            {
                TargetObjectId = Guid.NewGuid(),
                IsPreviewVisible = false,
                IsBooleanEnabled = false
            }]
        };

        var migrated = ComponentJson.Migrate(data);

        Assert.Equal(6, migrated.SchemaVersion);
        Assert.Equal(0.0, migrated.HeadEmbedDepth, 6);
        Assert.Equal(42, migrated.FastenerOpacityPercent);
        Assert.Equal(18, migrated.CutterOpacityPercent);
        Assert.False(migrated.Bindings.Single().IsPreviewVisible);
        Assert.True(migrated.Bindings.Single().IsBooleanEnabled);
    }

    [Fact]
    public void SchemaV3PreservesDisabledBooleanWhenMigratingControlPointAndEmbedDepth()
    {
        var data = new FastenerComponentData
        {
            SchemaVersion = 3,
            Kind = FastenerKind.SocketCap,
            Size = "M3",
            Bindings = [new HoleTargetBinding
            {
                TargetObjectId = Guid.NewGuid(),
                IncludeHeadSeat = true,
                IsPreviewVisible = true,
                IsBooleanEnabled = false
            }]
        };

        var migrated = ComponentJson.Migrate(data);

        Assert.Equal(6, migrated.SchemaVersion);
        Assert.Equal(3.0, migrated.HeadEmbedDepth, 6);
        Assert.Equal(Guid.Empty, migrated.ControlPointObjectId);
        Assert.True(migrated.Bindings.Single().IsPreviewVisible);
        Assert.False(migrated.Bindings.Single().IsBooleanEnabled);
    }

    [Fact]
    public void SchemaV4MigratesLegacyCountersunkFlushDepthToFrustumHeight()
    {
        var data = new FastenerComponentData
        {
            SchemaVersion = 4,
            Kind = FastenerKind.Countersunk,
            Size = "M3",
            HeadEmbedDepth = 2.8
        };

        var migrated = ComponentJson.Migrate(data);

        Assert.Equal(6, migrated.SchemaVersion);
        Assert.Equal(1.3, migrated.HeadEmbedDepth, 6);
    }

    [Fact]
    public void SchemaV4PreservesCustomCountersunkEmbedDepth()
    {
        var data = new FastenerComponentData
        {
            SchemaVersion = 4,
            Kind = FastenerKind.Countersunk,
            Size = "M3",
            HeadEmbedDepth = 1.0
        };

        var migrated = ComponentJson.Migrate(data);

        Assert.Equal(6, migrated.SchemaVersion);
        Assert.Equal(1.0, migrated.HeadEmbedDepth, 6);
    }

    [Theory]
    [InlineData(DepthMode.ThroughTarget, "贯穿宿主")]
    [InlineData(DepthMode.FastenerLengthPlusOneDiameter, "螺杆长度 + 1D")]
    [InlineData(DepthMode.FastenerLengthPlusTwoDiameters, "螺杆长度 + 2D")]
    [InlineData(DepthMode.Blind, "自定义深度")]
    public void DepthModesHaveChineseLabels(DepthMode mode, string expected)
    {
        Assert.Equal(expected, FastenerLabels.Depth(mode));
    }

    [Theory]
    [InlineData(FastenerKind.SocketCap, "内六角圆柱头螺钉")]
    [InlineData(FastenerKind.Countersunk, "内六角沉头螺钉")]
    [InlineData(FastenerKind.HexBolt, "六角头螺栓")]
    [InlineData(FastenerKind.HexNut, "六角螺母")]
    [InlineData(FastenerKind.HeatSetInsert, "热熔螺母")]
    public void FastenerKindsHaveChineseLabels(FastenerKind kind, string expected)
    {
        Assert.Equal(expected, FastenerLabels.Kind(kind));
    }

    [Fact]
    public void PreviewVisibilityDoesNotChangeHoleDiameter()
    {
        var visible = new HoleTargetBinding
        {
            TargetObjectId = Guid.NewGuid(),
            Role = ShaftFitRole.Clearance,
            IsPreviewVisible = true
        };
        var hidden = visible with { IsPreviewVisible = false };
        var profile = new PrintProfileSnapshot("PLA", 0.2);

        Assert.Equal(
            HoleDiameterCalculator.Calculate(Catalog.Get("M3"), visible, profile).FinalDiameter,
            HoleDiameterCalculator.Calculate(Catalog.Get("M3"), hidden, profile).FinalDiameter);
    }

    [Fact]
    public void FastenerLengthDepthUsesTwoNominalDiameters()
    {
        var component = new FastenerComponentData { Size = "M3", Length = 34 };
        var binding = new HoleTargetBinding
        {
            TargetObjectId = Guid.NewGuid(),
            Role = ShaftFitRole.ThreadEngagement,
            DepthMode = DepthMode.FastenerLengthPlusTwoDiameters,
            BiteReduction = 0.35
        };

        Assert.Equal(40, HoleDepthCalculator.GetLimit(component, Catalog.Get("M3"), binding), 6);
    }

    [Theory]
    [InlineData(FastenerKind.SocketCap, 3.0)]
    [InlineData(FastenerKind.Countersunk, 1.3)]
    [InlineData(FastenerKind.HexBolt, 2.0)]
    [InlineData(FastenerKind.HexNut, 0.0)]
    [InlineData(FastenerKind.HeatSetInsert, 0.0)]
    public void HeadHeightUsesFastenerType(FastenerKind kind, double expected)
    {
        Assert.Equal(expected, HeadGeometryCalculator.GetHeadHeight(kind, Catalog.Get("M3")), 6);
    }

    [Fact]
    public void CountersunkSeatPaddingPreservesCountersunkAngleAndClearance()
    {
        var spec = Catalog.Get("M3");
        const double clearance = 0.2;
        const double padding = 0.2;

        var profile = HeadGeometryCalculator.GetCountersunkSeatProfile(spec, clearance, padding);
        var halfAngle = spec.Head.CountersunkAngle * Math.PI / 360.0;
        var slope = Math.Tan(halfAngle);
        var headHeight = HeadGeometryCalculator.GetHeadHeight(FastenerKind.Countersunk, spec);

        Assert.Equal(slope, (profile.LargeRadius - profile.SmallRadius) / profile.Height, 6);
        Assert.Equal(
            spec.Head.CountersunkDiameter / 2 + clearance,
            profile.LargeRadius - padding * slope,
            6);
        Assert.Equal(
            spec.NominalDiameter / 2 + clearance,
            profile.SmallRadius + padding * slope,
            6);
        Assert.Equal(headHeight + padding * 2, profile.Height, 6);
    }

    [Fact]
    public void FastenerLengthDepthStartsAtEmbeddedShaftOrigin()
    {
        var component = new FastenerComponentData
        {
            Size = "M3",
            Length = 34,
            HeadEmbedDepth = 3
        };
        var binding = new HoleTargetBinding
        {
            TargetObjectId = Guid.NewGuid(),
            Role = ShaftFitRole.ThreadEngagement,
            DepthMode = DepthMode.FastenerLengthPlusTwoDiameters,
            BiteReduction = 0.35
        };

        Assert.Equal(43, HoleDepthCalculator.GetLimit(component, Catalog.Get("M3"), binding), 6);
    }

    [Theory]
    [InlineData(DepthMode.FastenerLengthPlusOneDiameter, 17)]
    [InlineData(DepthMode.FastenerLengthPlusTwoDiameters, 20)]
    public void FormulaDepthIncludesEmbedLengthAndNominalDiameter(DepthMode mode, double expected)
    {
        var component = new FastenerComponentData
        {
            Size = "M3",
            Length = 12,
            HeadEmbedDepth = 2
        };
        var binding = new HoleTargetBinding
        {
            TargetObjectId = Guid.NewGuid(),
            Role = ShaftFitRole.ThreadEngagement,
            DepthMode = mode,
            BiteReduction = 0.35
        };

        Assert.Equal(expected, HoleDepthCalculator.GetLimit(component, Catalog.Get("M3"), binding), 6);
    }
}
