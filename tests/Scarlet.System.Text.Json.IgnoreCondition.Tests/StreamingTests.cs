using System.Text;
using System.Text.Json;
using Xunit;

namespace Scarlet.System.Text.Json.IgnoreCondition.Tests;

/// <summary>
/// Streaming and concurrency, which is where a mechanism that carried state between reading a value
/// and assigning it would come apart.
/// </summary>
public sealed class StreamingTests
{
    public static TheoryData<SerializerConfiguration> Configurations => ReadConditionTests.Configurations;

    [Theory]
    [MemberData(nameof(Configurations))]
    public async Task A_buffered_stream_read_produces_the_same_result(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);
        string json = BuildNestedJson(depth: 12, padding: 4096);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        NestedModel model = (await Serializers.DeserializeAsync<NestedModel>(
            stream,
            new JsonSerializerOptions(options) { DefaultBufferSize = 16 },
            TestContext.Current.CancellationToken));

        AssertNestedDefaultsPreserved(model, depth: 12);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_synchronous_read_of_the_same_graph_agrees(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        NestedModel model = Serializers.Deserialize<NestedModel>(
            BuildNestedJson(depth: 12, padding: 4096),
            options);

        AssertNestedDefaultsPreserved(model, depth: 12);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void Parallel_reads_on_shared_options_stay_correct(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);
        const string skipped = """{"Name":null,"Age":null}""";
        const string assigned = """{"Name":"given","Age":9}""";

        Parallel.For(0, 512, index =>
        {
            if (index % 2 == 0)
            {
                NestedModel model = Serializers.Deserialize<NestedModel>(skipped, options);
                Assert.Equal("initial", model.Name);
                Assert.Equal(41, model.Age);
            }
            else
            {
                NestedModel model = Serializers.Deserialize<NestedModel>(assigned, options);
                Assert.Equal("given", model.Name);
                Assert.Equal(9, model.Age);
            }
        });
    }

    /// <summary>
    /// A null that is skipped must not bleed into the sibling that is read straight after it, in
    /// either order.
    /// </summary>
    [Theory]
    [MemberData(nameof(Configurations))]
    public void Skipping_one_member_does_not_affect_its_siblings(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        ValueModel nullFirst = Serializers.Deserialize<ValueModel>("""{"Age":null,"Amount":3.5}""", options);
        Assert.Equal(41, nullFirst.Age);
        Assert.Equal(3.5m, nullFirst.Amount);

        ValueModel nullSecond = Serializers.Deserialize<ValueModel>("""{"Amount":3.5,"Age":null}""", options);
        Assert.Equal(41, nullSecond.Age);
        Assert.Equal(3.5m, nullSecond.Amount);
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void A_duplicate_member_takes_the_last_non_null_value(SerializerConfiguration configuration)
    {
        JsonSerializerOptions options = Serializers.Create(configuration);

        Assert.Equal(5, Serializers.Deserialize<ValueModel>("""{"Age":null,"Age":5}""", options).Age);
        Assert.Equal(5, Serializers.Deserialize<ValueModel>("""{"Age":5,"Age":null}""", options).Age);
    }

    private static string BuildNestedJson(int depth, int padding)
    {
        var builder = new StringBuilder();
        string pad = new('p', padding);

        for (int level = 0; level < depth; level++)
        {
            builder.Append($$"""{"Name":null,"Age":null,"Padding":"{{pad}}","Children":[""");
        }

        builder.Append("""{"Name":null,"Age":null}""");

        for (int level = 0; level < depth; level++)
        {
            builder.Append("]}");
        }

        return builder.ToString();
    }

    private static void AssertNestedDefaultsPreserved(NestedModel model, int depth)
    {
        NestedModel? current = model;
        for (int level = 0; level <= depth; level++)
        {
            Assert.NotNull(current);
            Assert.Equal("initial", current.Name);
            Assert.Equal(41, current.Age);
            current = current.Children.Count > 0 ? current.Children[0] : null;
        }
    }
}
