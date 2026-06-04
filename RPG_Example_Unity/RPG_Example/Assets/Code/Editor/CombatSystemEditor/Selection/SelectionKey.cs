using System;

public struct SelectionKey : IEquatable<SelectionKey>
{
    public string fieldName;
    public int index;

    public bool Equals(SelectionKey other) => fieldName == other.fieldName && index == other.index;
    public override bool Equals(object obj) => obj is SelectionKey other && Equals(other);
    public override int GetHashCode() => (fieldName?.GetHashCode() ?? 0) * 397 ^ index;
    public override string ToString() => $"{fieldName}:{index}";
}