using Xunit;
using Verify = Scarlet.System.Text.Json.IgnoreCondition.Analyzers.Tests.Verifiers.CSharpCodeFixVerifier<Scarlet.System.Text.Json.IgnoreCondition.Analyzers.IgnoreConditionAnalyzer, Scarlet.System.Text.Json.IgnoreCondition.Analyzers.IgnoreConditionCodeFixProvider>;

namespace Scarlet.System.Text.Json.IgnoreCondition.Analyzers.Tests;

public sealed class IgnoreConditionCodeFixTests
{
    [Theory]
    [InlineData("RemoveCurrentCondition", "JsonIgnore(Condition = JsonIgnoreCondition.WhenReading)")]
    [InlineData("RemovePreviousCondition", "JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)")]
    public Task Conflicts_offer_both_choices(string key, string kept) =>
        Verify.VerifyAsync(IgnoreConditionAnalyzerTests.Imports + """
            class Model
            {
                [JsonIgnore(Condition = JsonIgnoreCondition.WhenReading), {|SJI0001:JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)|}]
                public string Value { get; set; }
            }
            """, IgnoreConditionAnalyzerTests.Imports + $$"""
            class Model
            {
                [{{kept}}]
                public string Value { get; set; }
            }
            """, key);

    [Fact]
    public Task Removal_keeps_unrelated_attributes() =>
        Verify.VerifyAsync(IgnoreConditionAnalyzerTests.Imports + """
            class Model
            {
                [JsonIgnore, {|SJI0002:JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)|}, System.Obsolete]
                public string Value { get; set; }
            }
            """, IgnoreConditionAnalyzerTests.Imports + """
            class Model
            {
                [JsonIgnore, System.Obsolete]
                public string Value { get; set; }
            }
            """, "RemoveCurrentCondition");

    [Fact]
    public Task Ineffective_condition_is_removed_and_comments_are_kept() =>
        Verify.VerifyAsync(IgnoreConditionAnalyzerTests.Imports + """
            class Model
            {
                [JsonIgnore]
                // Intentional native ignore.
                [{|SJI0002:JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)|}]
                public string Value { get; set; }
            }
            """, IgnoreConditionAnalyzerTests.Imports + """
            class Model
            {
                [JsonIgnore]
                // Intentional native ignore.
                public string Value { get; set; }
            }
            """, "RemoveCurrentCondition");

    [Fact]
    public Task Fix_all_removes_conflicting_extended_conditions() =>
        Verify.VerifyAsync(IgnoreConditionAnalyzerTests.Imports + """
            class Model
            {
                [JsonIgnore(Condition = JsonIgnoreCondition.WhenReading), {|SJI0001:JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)|}]
                public string First { get; set; }

                [JsonIgnore(Condition = JsonIgnoreCondition.WhenReading), {|SJI0001:JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingDefault)|}]
                public string Second { get; set; }
            }
            """, IgnoreConditionAnalyzerTests.Imports + """
            class Model
            {
                [JsonIgnore(Condition = JsonIgnoreCondition.WhenReading)]
                public string First { get; set; }

                [JsonIgnore(Condition = JsonIgnoreCondition.WhenReading)]
                public string Second { get; set; }
            }
            """, "RemoveCurrentCondition");

    [Fact]
    public Task Record_property_target_is_kept() =>
        Verify.VerifyAsync(IgnoreConditionAnalyzerTests.Imports + """
            public record Model([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenReading), {|SJI0001:JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)|}] string Value);
            """, IgnoreConditionAnalyzerTests.Imports + """
            public record Model([property: JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)] string Value);
            """, "RemovePreviousCondition");

    [Fact]
    public Task Native_attribute_can_appear_after_the_extended_attribute() =>
        Verify.VerifyAsync(IgnoreConditionAnalyzerTests.Imports + """
            class Model
            {
                [{|SJI0001:JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)|}, JsonIgnore(Condition = JsonIgnoreCondition.WhenReading)]
                public string Value { get; set; }
            }
            """, IgnoreConditionAnalyzerTests.Imports + """
            class Model
            {
                [JsonIgnoreEx(JsonIgnoreConditionEx.WhenReadingNull)]
                public string Value { get; set; }
            }
            """, "RemovePreviousCondition");
}
