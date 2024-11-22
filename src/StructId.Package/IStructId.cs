namespace StructId;

partial interface IStructId<T> where T : struct
{
    T Value { get; }
}