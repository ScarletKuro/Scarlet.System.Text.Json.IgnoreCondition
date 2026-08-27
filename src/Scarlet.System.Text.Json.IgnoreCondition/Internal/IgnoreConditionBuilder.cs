using Scarlet.Text.Json.Serialization;
using Scarlet.Text.Json.Serialization.Metadata;

namespace Scarlet.Text.Json.Internal;

/// <summary>Collects builder calls into <see cref="MemberIgnoreSpec"/> instances.</summary>
internal sealed class IgnoreConditionBuilder : IIgnoreConditionBuilder
{
    private readonly Dictionary<string, Draft> _drafts = new(StringComparer.Ordinal);

    public void Member<TMember>(string memberName, JsonIgnoreConditionEx condition) =>
        Add(memberName, condition, MemberOps<TMember>.Instance);

    public void ValueMember<TMember>(string memberName, JsonIgnoreConditionEx condition)
        where TMember : struct =>
        Add(memberName, condition, ValueMemberOps<TMember>.Instance);

    public MemberIgnoreSpec[] Build(Type? declaringType = null)
    {
        if (_drafts.Count == 0)
        {
            return [];
        }

        var specs = new MemberIgnoreSpec[_drafts.Count];
        int index = 0;
        foreach (KeyValuePair<string, Draft> entry in _drafts)
        {
            Draft draft = entry.Value;
            specs[index++] = new MemberIgnoreSpec
            {
                MemberName = entry.Key,
                Ops = draft.Ops,
                DeclaringType = declaringType,
                ReadCondition = draft.Read,
                WriteCondition = draft.Write,
                IgnoreAlways = draft.Always,
                SuppressReadDefault = draft.SuppressRead,
                SuppressWriteDefault = draft.SuppressWrite,
            };
        }

        return specs;
    }

    internal void Add(string memberName, JsonIgnoreConditionEx condition, MemberOps ops)
    {
        ArgumentException.ThrowIfNullOrEmpty(memberName);

        if (!_drafts.TryGetValue(memberName, out Draft? draft))
        {
            draft = new Draft(ops);
            _drafts.Add(memberName, draft);
        }

        switch (condition)
        {
            case JsonIgnoreConditionEx.Always:
                draft.Always = true;
                break;

            case JsonIgnoreConditionEx.Never:
                draft.SuppressRead = true;
                draft.SuppressWrite = true;
                break;

            default:
                if (ConditionKinds.IsReadSide(condition))
                {
                    ThrowIfAlreadySet(memberName, draft.Read, condition);
                    draft.Read = condition;
                }
                else
                {
                    ThrowIfAlreadySet(memberName, draft.Write, condition);
                    draft.Write = condition;
                }

                break;
        }
    }

    private static void ThrowIfAlreadySet(string memberName, JsonIgnoreConditionEx? existing, JsonIgnoreConditionEx incoming)
    {
        if (existing is { } already && already != incoming)
        {
            throw new InvalidOperationException(
                $"Member '{memberName}' declares two conflicting conditions for the same side: " +
                $"'{already}' and '{incoming}'.");
        }
    }

    private sealed class Draft(MemberOps ops)
    {
        public MemberOps Ops { get; } = ops;

        public JsonIgnoreConditionEx? Read { get; set; }

        public JsonIgnoreConditionEx? Write { get; set; }

        public bool Always { get; set; }

        public bool SuppressRead { get; set; }

        public bool SuppressWrite { get; set; }
    }
}
