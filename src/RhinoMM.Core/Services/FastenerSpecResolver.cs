using RhinoMM.Core.Domain;

namespace RhinoMM.Core.Services;

public static class FastenerSpecResolver
{
    public static FastenerSizeSpec Resolve(
        FastenerComponentData component,
        FastenerCatalog catalog) => component.CustomDefinitionSnapshot?.SizeSpec
        ?? catalog.Get(component.Size);

    public static FastenerSizeSpec Resolve(
        FastenerTemplateData template,
        FastenerCatalog catalog) => template.CustomDefinitionSnapshot?.SizeSpec
        ?? catalog.Get(template.Size);

    public static LockingNutSizeSpec? ResolveLockingNut(
        FastenerComponentData component) => component.CustomDefinitionSnapshot?.LockingNutSpec;
}
