using StructId;

partial record struct TValue
{
    public static TValue Parse(string _) => new();
    public static bool TryParse(string? s, out TValue value)
    {
        if (s is not null)
        {
            value = new();
            return true;
        }
        value = default;
        return false;
    }
}

readonly partial record struct SStruct(string Value) : IStructId
{
}

readonly partial record struct TStruct(TValue Value) : IStructId<TValue>
{
}
