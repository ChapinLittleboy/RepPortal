namespace RepPortal.Models;

[AttributeUsage(AttributeTargets.Property)]
public sealed class CsiFieldAttribute : Attribute
{
    public string FieldName { get; }
    public CsiFieldAttribute(string fieldName) => FieldName = fieldName;
}

