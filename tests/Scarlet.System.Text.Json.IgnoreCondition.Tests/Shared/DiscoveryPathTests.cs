using Scarlet.Text.Json.Serialization.Metadata;
using Xunit;

namespace Scarlet.Text.Json.Tests;

/// <summary>
/// Proves which discovery path this assembly is exercising, so that the ReflectionFallback project
/// cannot silently end up re-testing the source-generated one.
/// </summary>
public sealed class DiscoveryPathTests
{
#if SCARLET_REFLECTION_FALLBACK
    [Fact]
    public void The_compile_time_registry_is_empty_in_this_assembly()
    {
        Assert.False(ExtendedIgnoreConditionRegistry.IsRegistered(typeof(ReferenceModel)));
        Assert.False(ExtendedIgnoreConditionRegistry.IsRegistered(typeof(ValueModel)));
    }
#else
    [Fact]
    public void The_generator_registered_every_annotated_type()
    {
        Assert.True(ExtendedIgnoreConditionRegistry.IsRegistered(typeof(ReferenceModel)));
        Assert.True(ExtendedIgnoreConditionRegistry.IsRegistered(typeof(ValueModel)));
        Assert.True(ExtendedIgnoreConditionRegistry.IsRegistered(typeof(FieldModel)));
        Assert.True(ExtendedIgnoreConditionRegistry.IsRegistered(typeof(StructModel)));
        Assert.True(ExtendedIgnoreConditionRegistry.IsRegistered(typeof(RecordModel)));
        Assert.True(ExtendedIgnoreConditionRegistry.IsRegistered(typeof(ParameterRecordModel)));
        Assert.True(ExtendedIgnoreConditionRegistry.IsRegistered(typeof(WriteModel)));
        Assert.True(ExtendedIgnoreConditionRegistry.IsRegistered(typeof(CombinedModel)));
        Assert.True(ExtendedIgnoreConditionRegistry.IsRegistered(typeof(BaseModel)));
        Assert.True(ExtendedIgnoreConditionRegistry.IsRegistered(typeof(RenamedModel)));
    }

    [Fact]
    public void Unannotated_types_are_not_registered()
    {
        Assert.False(ExtendedIgnoreConditionRegistry.IsRegistered(typeof(UnannotatedModel)));
        Assert.False(ExtendedIgnoreConditionRegistry.IsRegistered(typeof(DerivedModel)));
    }
#endif
}
