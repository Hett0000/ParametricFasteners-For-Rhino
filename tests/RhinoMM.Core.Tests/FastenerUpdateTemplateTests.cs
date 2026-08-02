using RhinoMM.Core.Domain;
using RhinoMM.Core.Services;
using Xunit;

namespace RhinoMM.Core.Tests;

public sealed class FastenerUpdateTemplateTests
{
    [Fact]
    public void ApplyTo_UpdatesAllEngagementBindingsAndPreservesUnspecifiedSwitches()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var component = Component(
            new HoleTargetBinding
            {
                BindingId = firstId,
                TargetObjectId = Guid.NewGuid(),
                Role = ShaftFitRole.ThreadEngagement,
                DepthMode = DepthMode.ThroughTarget,
                IsPreviewVisible = false,
                IsBooleanEnabled = true
            },
            new HoleTargetBinding
            {
                BindingId = secondId,
                TargetObjectId = Guid.NewGuid(),
                Role = ShaftFitRole.ThreadEngagement,
                DepthMode = DepthMode.Blind,
                BlindDepth = 4,
                IsPreviewVisible = true,
                IsBooleanEnabled = false
            });

        var result = Template().ApplyTo(component);

        Assert.All(result.Bindings, binding =>
        {
            Assert.Equal(DepthMode.FastenerLengthPlusCustom, binding.DepthMode);
            Assert.Equal(6, binding.BlindDepth);
            Assert.Equal(0.4, binding.BiteReduction);
        });
        Assert.False(result.Bindings[0].IsPreviewVisible);
        Assert.True(result.Bindings[0].IsBooleanEnabled);
        Assert.True(result.Bindings[1].IsPreviewVisible);
        Assert.False(result.Bindings[1].IsBooleanEnabled);
        Assert.Equal(HoleDiameterFormula.NominalIndependent, result.HoleDiameterFormula);
    }

    [Fact]
    public void ApplyTo_OverridesSwitchesOnlyWhenUserChangedTemplateSwitches()
    {
        var component = Component(
            new HoleTargetBinding
            {
                TargetObjectId = Guid.NewGuid(),
                Role = ShaftFitRole.Clearance,
                IsPreviewVisible = false,
                IsBooleanEnabled = true
            },
            new HoleTargetBinding
            {
                TargetObjectId = Guid.NewGuid(),
                Role = ShaftFitRole.ThreadEngagement,
                IsPreviewVisible = true,
                IsBooleanEnabled = false
            });
        var template = Template() with
        {
            ClearancePreviewVisible = true,
            EngagementBooleanEnabled = true
        };

        var result = template.ApplyTo(component);

        Assert.True(result.Bindings[0].IsPreviewVisible);
        Assert.True(result.Bindings[0].IsBooleanEnabled);
        Assert.True(result.Bindings[1].IsPreviewVisible);
        Assert.True(result.Bindings[1].IsBooleanEnabled);
    }

    [Fact]
    public void ApplyTo_UpdatesSmartRecognitionTemplateForFutureHosts()
    {
        var component = Component() with
        {
            AutoRecognizeHosts = true,
            SmartBindingProfile = null
        };

        var result = Template().ApplyTo(component);

        Assert.NotNull(result.SmartBindingProfile);
        Assert.Equal(ClearanceFitClass.Loose, result.SmartBindingProfile!.ClearanceFit);
        Assert.Equal(0.4, result.SmartBindingProfile.BiteReduction);
        Assert.Equal(
            DepthMode.FastenerLengthPlusCustom,
            result.SmartBindingProfile.EngagementDepthMode);
    }

    [Fact]
    public void ApplyTo_UpdatesCounterboreBridgeForSocketCapOnly()
    {
        var template = Template() with
        {
            CounterboreBridgeEnabled = true,
            CounterboreBridgeLayerHeight = 0.16
        };

        var socketCap = template.ApplyTo(Component());
        var countersunk = (template with
        {
            Kind = FastenerKind.Countersunk
        }).ApplyTo(Component());

        Assert.True(socketCap.CounterboreBridgeEnabled);
        Assert.Equal(0.16, socketCap.CounterboreBridgeLayerHeight, 6);
        Assert.False(countersunk.CounterboreBridgeEnabled);
    }

    [Fact]
    public void ApplyTo_EngagementOnlyKeepsHeadSeatHostAsSingleEngagementBinding()
    {
        var headHost = Guid.NewGuid();
        var otherHost = Guid.NewGuid();
        var component = Component(
            new HoleTargetBinding
            {
                TargetObjectId = headHost,
                Role = ShaftFitRole.Clearance,
                IncludeHeadSeat = true
            },
            new HoleTargetBinding
            {
                TargetObjectId = otherHost,
                Role = ShaftFitRole.ThreadEngagement
            });

        var result = (Template() with { EngagementOnly = true }).ApplyTo(component);

        Assert.True(result.EngagementOnly);
        var binding = Assert.Single(result.Bindings);
        Assert.Equal(headHost, binding.TargetObjectId);
        Assert.Equal(ShaftFitRole.ThreadEngagement, binding.Role);
        Assert.True(binding.IncludeHeadSeat);
        Assert.Equal(DepthMode.FastenerLengthPlusCustom, binding.DepthMode);
    }

    [Fact]
    public void ApplyTo_EngagementOnlyUsesEngagementSwitchesInsteadOfHeadSeatClearanceSwitches()
    {
        var headHost = Guid.NewGuid();
        var component = Component(
            new HoleTargetBinding
            {
                TargetObjectId = headHost,
                Role = ShaftFitRole.Clearance,
                IncludeHeadSeat = true,
                IsPreviewVisible = false,
                IsBooleanEnabled = false
            },
            new HoleTargetBinding
            {
                TargetObjectId = Guid.NewGuid(),
                Role = ShaftFitRole.ThreadEngagement,
                IsPreviewVisible = true,
                IsBooleanEnabled = true
            });

        var result = (Template() with { EngagementOnly = true }).ApplyTo(component);

        var binding = Assert.Single(result.Bindings);
        Assert.Equal(headHost, binding.TargetObjectId);
        Assert.Equal(ShaftFitRole.ThreadEngagement, binding.Role);
        Assert.True(binding.IsPreviewVisible);
        Assert.True(binding.IsBooleanEnabled);
    }

    [Fact]
    public void ApplyTo_DisablesEngagementOnlyForNutKinds()
    {
        var result = (Template() with
        {
            Kind = FastenerKind.HexNut,
            EngagementOnly = true
        }).ApplyTo(Component());

        Assert.False(result.EngagementOnly);
    }

    [Fact]
    public void ApplyTo_UpdatesHexNutStyleWithoutChangingComponentIdentity()
    {
        var component = Component(new HoleTargetBinding
        {
            TargetObjectId = Guid.NewGuid(),
            Role = ShaftFitRole.InstallationPocket,
            DepthMode = DepthMode.Blind,
            BlindDepth = 6
        }) with
        {
            Kind = FastenerKind.HexNut,
            HexNutStyle = HexNutStyle.Standard,
            HeadEmbedDepth = 6
        };

        var result = (Template() with
        {
            Kind = FastenerKind.HexNut,
            NutStyle = HexNutStyle.NylonInsertLocking,
            HeadEmbedDepth = 6
        }).ApplyTo(component);

        Assert.Equal(component.ComponentId, result.ComponentId);
        Assert.Equal(HexNutStyle.NylonInsertLocking, result.HexNutStyle);
    }

    [Fact]
    public void ApplyTo_UsesPerHostDepthOverrideOnlyForMatchingBinding()
    {
        var first = new HoleTargetBinding
        {
            BindingId = Guid.NewGuid(),
            TargetObjectId = Guid.NewGuid(),
            Role = ShaftFitRole.ThreadEngagement
        };
        var second = first with
        {
            BindingId = Guid.NewGuid(),
            TargetObjectId = Guid.NewGuid()
        };
        var local = first with
        {
            DepthMode = DepthMode.Blind,
            BlindDepth = 7.5,
            IsPreviewVisible = false
        };

        var result = Template().ApplyTo(Component(first, second), [local]);

        Assert.Equal(DepthMode.Blind, result.Bindings[0].DepthMode);
        Assert.Equal(7.5, result.Bindings[0].BlindDepth);
        Assert.False(result.Bindings[0].IsPreviewVisible);
        Assert.Equal(
            DepthMode.FastenerLengthPlusCustom,
            result.Bindings[1].DepthMode);
    }

    private static FastenerUpdateTemplate Template() => new(
        FastenerKind.SocketCap,
        HexNutStyle.Standard,
        "M4",
        20,
        1.5,
        0,
        0,
        0,
        0.25,
        ClearanceFitClass.Loose,
        0.4,
        DepthMode.FastenerLengthPlusCustom,
        6,
        70,
        35);

    private static FastenerComponentData Component(
        params HoleTargetBinding[] bindings) => new()
    {
        Kind = FastenerKind.Countersunk,
        Size = "M3",
        Length = 12,
        PrintProfile = new PrintProfileSnapshot("test", 0.2),
        Bindings = bindings
    };
}
