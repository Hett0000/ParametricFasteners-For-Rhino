using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class UserFastenerDefinitionValidator
{
    public static ValidationResult Validate(UserFastenerDefinition definition)
    {
        var result = new ValidationResult();
        var spec = definition.SizeSpec;
        CheckText(result, definition.Name, "CUSTOM_NAME", "自定义标准件名称不能为空。");
        CheckText(result, definition.ThreadDesignation, "CUSTOM_THREAD", "螺纹规格不能为空。");
        CheckPositive(result, spec.NominalDiameter, "CUSTOM_DIAMETER", "公称直径必须大于 0。");
        CheckPositive(result, spec.CoarsePitch, "CUSTOM_PITCH", "螺距必须大于 0。");
        CheckPositive(result, spec.Head.SocketDiameter, "CUSTOM_SOCKET_DIAMETER", "杯头直径必须大于 0。");
        CheckPositive(result, spec.Head.SocketHeight, "CUSTOM_SOCKET_HEIGHT", "杯头高度必须大于 0。");
        CheckPositive(result, spec.Head.CountersunkDiameter, "CUSTOM_CS_DIAMETER", "沉头直径必须大于 0。");
        CheckPositive(result, spec.Head.CountersunkAngle, "CUSTOM_CS_ANGLE", "沉头角度必须大于 0。");
        CheckPositive(result, spec.Head.HexAcrossFlats, "CUSTOM_HEX_FLATS", "六角头对边必须大于 0。");
        CheckPositive(result, spec.Head.HexHeight, "CUSTOM_HEX_HEIGHT", "六角头高度必须大于 0。");
        CheckPositive(result, spec.Head.NutAcrossFlats, "CUSTOM_NUT_FLATS", "螺母对边必须大于 0。");
        CheckPositive(result, spec.Head.NutThickness, "CUSTOM_NUT_HEIGHT", "螺母厚度必须大于 0。");
        if (spec.Head.SocketDiameter < spec.NominalDiameter)
            result.Issues.Add(new ValidationIssue("CUSTOM_HEAD_RELATION", "杯头直径不得小于公称直径。"));
        if (spec.Head.CountersunkDiameter < spec.NominalDiameter)
            result.Issues.Add(new ValidationIssue("CUSTOM_CS_RELATION", "沉头大端直径不得小于公称直径。"));
        if (spec.Head.NutAcrossFlats < spec.NominalDiameter)
            result.Issues.Add(new ValidationIssue("CUSTOM_NUT_RELATION", "螺母对边不得小于公称直径。"));
        if (definition.LockingNutSpec is { } locking)
        {
            CheckPositive(result, locking.AcrossFlats, "CUSTOM_LOCK_FLATS", "防松螺母对边必须大于 0。");
            CheckPositive(result, locking.TotalHeight, "CUSTOM_LOCK_HEIGHT", "防松螺母总高必须大于 0。");
        }
        if (definition.Kind == FastenerKind.HeatSetInsert)
        {
            CheckPositive(result, definition.DefaultLength, "CUSTOM_INSERT_LENGTH", "热熔螺母默认长度必须大于 0。");
            CheckPositive(result, definition.DefaultInsertOuterDiameter, "CUSTOM_INSERT_OD", "热熔螺母外径必须大于 0。");
            if (definition.DefaultInsertOuterDiameter <= spec.NominalDiameter)
                result.Issues.Add(new ValidationIssue("CUSTOM_INSERT_RELATION", "热熔螺母外径必须大于公称直径。"));
            if (!double.IsFinite(definition.DefaultInsertDiameterCompensation)
                || !double.IsFinite(definition.DefaultInsertDepthCompensation)
                || definition.DefaultInsertDepthCompensation < 0)
                result.Issues.Add(new ValidationIssue("CUSTOM_INSERT_COMPENSATION", "热熔螺母补偿参数无效。"));
        }
        return result;
    }

    private static void CheckText(ValidationResult result, string? value, string code, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
            result.Issues.Add(new ValidationIssue(code, message));
    }

    private static void CheckPositive(ValidationResult result, double value, string code, string message)
    {
        if (!double.IsFinite(value) || value <= 0)
            result.Issues.Add(new ValidationIssue(code, message));
    }
}
