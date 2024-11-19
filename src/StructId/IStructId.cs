namespace ROOTNAMESPACE;

partial interface IStructId<T> where T : struct
{
    T Value { get; }
}
