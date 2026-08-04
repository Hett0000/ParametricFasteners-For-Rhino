using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class FastenerComponentValidator
{
    public static ValidationResult Validate(FastenerComponentData component, FastenerSizeSpec spec)
    {
        var result = new ValidationResult();

        if (FastenerKindTraits.UsesLengthInStatistics(component.Kind) && component.Length <= 0)
            result.Issues.Add(new("length", "紧固件长度必须大于 0。"));

        if (component.HeadEmbedDepth < 0)
            result.Issues.Add(new("head-embed-negative", "螺丝头嵌入深度不能小于 0。"));

        if (!FastenerKindTraits.SupportsEmbedDepth(component.Kind) && component.HeadEmbedDepth != 0)
            result.Issues.Add(new("nut-head-embed", "螺母不支持螺丝头嵌入深度。"));

        var assemblyMode = FastenerKindTraits.IsScrew(component.Kind)
            ? component.AssemblyMode
            : ScrewAssemblyMode.ThreadEngagement;
        if (assemblyMode == ScrewAssemblyMode.EngagementOnly)
        {
            if (component.Bindings.Count != 1
                || component.Bindings[0].Role != ShaftFitRole.ThreadEngagement)
                result.Issues.Add(new(
                    "engagement-only-binding",
                    "“只咬合”模式必须且只能绑定一个咬合宿主。"));
        }

        if (assemblyMode == ScrewAssemblyMode.NutFastened)
        {
            if (!double.IsFinite(component.NutTipProtrusion)
                || component.NutTipProtrusion < 0)
                result.Issues.Add(new("nut-tip-protrusion", "螺杆末端露出量必须是非负数。"));
            if (!double.IsFinite(component.NutPocketCompensation))
                result.Issues.Add(new("nut-pocket-compensation", "螺母槽补偿必须是有效数值。"));
            if (!HexNutDimensions.Supports(component.PairedNutStyle, component.Size))
                result.Issues.Add(new(
                    "paired-locking-nut-size",
                    $"尼龙防松配套螺母不支持规格 {component.Size}；请选择 M2–M12。"));
            try
            {
                var range = PairedNutAssemblyCalculator.AxialRange(component, spec);
                if (range.InnerFace < component.HeadEmbedDepth - 1e-6)
                    result.Issues.Add(new(
                        "paired-nut-outside-shaft",
                        "当前螺杆长度无法同时容纳配套螺母和末端露出量。"));
            }
            catch (InvalidOperationException ex)
            {
                result.Issues.Add(new("paired-nut-size", ex.Message));
            }
            if (component.Bindings.Count(binding => binding.Role == ShaftFitRole.Clearance) == 0)
                result.Issues.Add(new("nut-fastened-clearance", "螺母固定模式至少需要一个正补偿通孔宿主。"));
            if (component.Bindings.Count(binding => binding.Role == ShaftFitRole.NutPocket) != 1)
                result.Issues.Add(new("nut-pocket-count", "螺母固定模式必须且只能包含一个配套螺母槽。"));
            if (component.Bindings.Any(binding => binding.Role == ShaftFitRole.ThreadEngagement))
                result.Issues.Add(new("nut-fastened-engagement", "螺母固定模式不能包含螺纹咬合孔。"));
        }

        if (component.CounterboreBridgeEnabled)
        {
            if (component.Kind != FastenerKind.SocketCap)
                result.Issues.Add(new(
                    "counterbore-bridge-kind",
                    "悬垂沉孔架桥仅支持内六角杯头螺丝。"));
            if (component.HeadEmbedDepth <= 0)
                result.Issues.Add(new(
                    "counterbore-bridge-embed",
                    "启用悬垂沉孔架桥时，杯头螺丝嵌入深度必须大于 0。"));
            if (!double.IsFinite(component.CounterboreBridgeLayerHeight)
                || component.CounterboreBridgeLayerHeight is < 0.05 or > 1.0)
                result.Issues.Add(new(
                    "counterbore-bridge-height",
                    "悬垂沉孔架桥层高必须在 0.05–1.00 mm 之间。"));
        }

        if (FastenerKindTraits.SupportsHeadEmbed(component.Kind)
            && component.HeadEmbedDepth > 0
            && component.Bindings.All(binding => !binding.IncludeHeadSeat))
            result.Issues.Add(new("head-seat-required", "螺丝头嵌入时必须指定一个头部切割宿主。"));

        if (component.FastenerOpacityPercent is < 0 or > 100)
            result.Issues.Add(new("fastener-opacity", "紧固件不透明度必须在 0–100% 之间。"));

        if (component.CutterOpacityPercent is < 0 or > 100)
            result.Issues.Add(new("cutter-opacity", "切割模块不透明度必须在 0–100% 之间。"));

        if (FastenerKindTraits.IsNut(component.Kind) && component.Bindings.Count != 1)
            result.Issues.Add(new("nut-binding-count", "螺母必须绑定且只能绑定一个安装宿主。"));

        if (component.AutoRecognizeHosts && FastenerKindTraits.IsNut(component.Kind))
            result.Issues.Add(new("smart-host-nut", "螺母类组件不能启用螺杆宿主重识别。"));

        if (component.AutoRecognizeHosts && component.SmartBindingProfile is null)
            result.Issues.Add(new("smart-host-profile", "智能组件缺少宿主识别工艺模板。"));

        if (component.Kind == FastenerKind.HeatSetInsert)
        {
            if (component.InsertOuterDiameter <= 0)
                result.Issues.Add(new("insert-outer-diameter", "热熔螺母外径必须大于 0。"));
            if (component.InsertDepthCompensation is < 0 or > 1000)
                result.Issues.Add(new("insert-depth-compensation", "热熔螺母深度补偿必须在 0–1000 mm 之间。"));
            var finalDiameter = InstallationPocketCalculator.HeatSetFinalDiameter(component);
            if (finalDiameter <= spec.NominalDiameter)
                result.Issues.Add(new(
                    "insert-final-diameter",
                    $"热熔安装孔最终直径 {finalDiameter:0.###} mm 必须大于螺纹公称直径 {spec.NominalDiameter:0.###} mm。"));
        }

        if (component.Kind == FastenerKind.HexNut)
        {
            if (!HexNutDimensions.Supports(component.HexNutStyle, component.Size))
            {
                result.Issues.Add(new(
                    "locking-nut-size",
                    $"尼龙防松螺母不支持规格 {component.Size}；请选择 M2–M12。"));
            }
            var binding = component.Bindings.FirstOrDefault() ?? new HoleTargetBinding();
            try
            {
                var finalAcrossFlats = InstallationPocketCalculator.HexNutAcrossFlats(component, spec, binding);
                if (finalAcrossFlats <= 0)
                    result.Issues.Add(new("nut-pocket-size", "六角螺母槽最终对边尺寸必须大于 0。"));
            }
            catch (InvalidOperationException ex)
            {
                result.Issues.Add(new("nut-pocket-size", ex.Message));
            }
        }

        if (component.Bindings.Count(x => x.IncludeHeadSeat) > 1)
            result.Issues.Add(new("head-seat", "一个组件只能在一个被切割体上生成头部沉孔。"));

        foreach (var binding in component.Bindings)
        {
            if (binding.TargetObjectId == Guid.Empty)
                result.Issues.Add(new("target", "被切割体引用不能为空。"));

            var zeroDepthHexNutPocket = component.Kind == FastenerKind.HexNut
                && binding.Role == ShaftFitRole.InstallationPocket
                && component.HeadEmbedDepth == 0;
            if (binding.DepthMode == DepthMode.Blind
                && binding.BlindDepth <= 0
                && !zeroDepthHexNutPocket)
                result.Issues.Add(new("blind-depth", "盲孔深度必须大于 0。"));
            if (binding.Role == ShaftFitRole.ThreadEngagement
                && binding.DepthMode == DepthMode.FastenerLengthPlusCustom)
            {
                var finalDepth = component.HeadEmbedDepth + component.Length + binding.BlindDepth;
                if (!double.IsFinite(binding.BlindDepth) || finalDepth <= 0)
                    result.Issues.Add(new(
                        "engagement-custom-depth",
                        "螺杆长度与自定追加深度计算后的孔底必须大于放置面。"));
            }

            if (FastenerKindTraits.IsNut(component.Kind))
            {
                if (binding.Role != ShaftFitRole.InstallationPocket)
                    result.Issues.Add(new("nut-binding-role", "螺母宿主必须使用安装槽/孔绑定。"));
                if (binding.DepthMode != DepthMode.Blind)
                    result.Issues.Add(new("nut-depth-mode", "螺母安装槽/孔必须使用盲孔深度。"));
                if (component.Kind == FastenerKind.HexNut
                    && Math.Abs(binding.BlindDepth - component.HeadEmbedDepth) > 1e-6)
                    result.Issues.Add(new(
                        "nut-embed-depth",
                        "六角螺母安装槽深度必须与嵌入深度一致。"));
                continue;
            }

            if (binding.Role == ShaftFitRole.InstallationPocket)
            {
                result.Issues.Add(new("screw-installation-pocket", "螺丝组件不能使用螺母安装槽绑定。"));
                continue;
            }

            if (binding.Role == ShaftFitRole.NutPocket)
            {
                if (assemblyMode != ScrewAssemblyMode.NutFastened)
                    result.Issues.Add(new("nut-pocket-mode", "配套螺母槽只能用于螺母固定模式。"));
                if (binding.DepthMode != DepthMode.ThroughTarget)
                    result.Issues.Add(new("nut-pocket-depth", "配套螺母槽必须向宿主背面贯穿。"));
                try
                {
                    if (PairedNutAssemblyCalculator.PocketAcrossFlats(component, spec, binding) <= 0)
                        result.Issues.Add(new("nut-pocket-size", "配套螺母槽最终对边必须大于 0。"));
                }
                catch (InvalidOperationException ex)
                {
                    result.Issues.Add(new("nut-pocket-size", ex.Message));
                }
                continue;
            }

            if (binding.Role == ShaftFitRole.Clearance && binding.DepthMode != DepthMode.ThroughTarget)
                result.Issues.Add(new("clearance-depth", "穿过通孔只能使用贯穿宿主深度模式。"));

            if (binding.Role == ShaftFitRole.ThreadEngagement && binding.BiteReduction <= 0)
                result.Issues.Add(new("bite-required", "咬合孔必须输入大于 0 的咬合缩减量，不能使用未校准默认值。"));

            var diameter = HoleDiameterCalculator.Calculate(component, spec, binding).FinalDiameter;
            if (diameter <= 0)
                result.Issues.Add(new("diameter-positive", "最终孔径必须大于 0。"));

            if (binding.Role == ShaftFitRole.Clearance && diameter <= spec.NominalDiameter)
                result.Issues.Add(new("clearance-range", $"通孔最终直径 {diameter:0.###} mm 必须大于公称直径 {spec.NominalDiameter:0.###} mm。"));

            if (binding.Role == ShaftFitRole.ThreadEngagement && diameter >= spec.NominalDiameter)
                result.Issues.Add(new("engagement-range", $"咬合孔最终直径 {diameter:0.###} mm 必须小于公称直径 {spec.NominalDiameter:0.###} mm。"));
        }

        return result;
    }
}
