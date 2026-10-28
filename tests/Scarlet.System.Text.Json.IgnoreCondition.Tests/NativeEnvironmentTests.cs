#if NATIVE_AOT_TESTS
using System.Runtime.CompilerServices;
using System.Text.Json;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

public sealed class NativeEnvironmentTests
{
    [Fact]
    public void Native_tests_execute_without_dynamic_code_or_serializer_reflection()
    {
        Assert.False(RuntimeFeature.IsDynamicCodeSupported);
        Assert.False(JsonSerializer.IsReflectionEnabledByDefault);
    }
}
#endif
