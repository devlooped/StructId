﻿// <auto-generated />

readonly partial record struct Self
{
    public static implicit operator string(Self id) => id.Value;
    public static explicit operator Self(string value) => new(value);
}