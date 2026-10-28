namespace Scarlet.System.Text.Json.IgnoreCondition;

// Shared by the runtime and analyzer without referencing the runtime assembly from Roslyn.
internal static class IgnoreConditionRules
{
    public static bool NativeConflictsWithExtended(int nativeCondition) => nativeCondition == 5;

    public static bool NativeMasksExtended(int nativeCondition) => nativeCondition == 1;

    public static bool NativeSuppressesReadDefault(int nativeCondition) => nativeCondition is 0 or 1 or 5;
}
