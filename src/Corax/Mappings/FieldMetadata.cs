using System;
using System.Collections.Generic;
using Sparrow.Server;
using Voron;

namespace Corax.Mappings;

public readonly struct FieldMetadata
{
    public readonly Slice FieldName;
    public readonly int FieldId;
    public readonly FieldIndexingMode Mode;
    public readonly Analyzer Analyzer;

    private FieldMetadata(Slice fieldName, int fieldId, FieldIndexingMode mode, Analyzer analyzer)
    {
        FieldName = fieldName;
        FieldId = fieldId;
        Mode = mode;
        Analyzer = analyzer;
    }

    public FieldMetadata GetNumericFieldMetadata<T>(ByteStringContext allocator)
    {
        Slice numericTree = default;

        if (typeof(T) == typeof(Slice))
            return this;
        
        if (typeof(T) == typeof(long))
            Slice.From(allocator, $"{FieldName}-L", ByteStringType.Immutable, out numericTree);

        if (typeof(T) == typeof(double))
            Slice.From(allocator, $"{FieldName}-D", ByteStringType.Immutable, out numericTree);
        
        return new FieldMetadata(numericTree, FieldId, Mode, Analyzer);
    }

    public bool Equals(FieldMetadata other)
    {
        return FieldId == other.FieldId && SliceComparer.CompareInline(FieldName, other.FieldName) == 0;
    }

    public static FieldMetadata Build(ByteStringContext allocator, string fieldName, int fieldId, FieldIndexingMode mode, Analyzer analyzer)
    {
        Slice.From(allocator, fieldName, ByteStringType.Immutable, out var fieldNameAsSlice);
        return new(fieldNameAsSlice, fieldId, mode, analyzer);
    }

    public static FieldMetadata Build(Slice fieldName, int fieldId, FieldIndexingMode mode, Analyzer analyzer) => new(fieldName, fieldId, mode, analyzer);

    public FieldMetadata ChangeAnalyzer(FieldIndexingMode mode, Analyzer analyzer = null)
    {
        return new FieldMetadata(FieldName, FieldId, mode, analyzer ?? Analyzer);
    }
    
    public override string ToString()
    {
        return $"Field name: '{FieldName}' | Field id: {FieldId} | Indexing mode: {Mode} | Analyzer {Analyzer?.GetType()}";
    }
}


public sealed class FieldMetadataComparer : IEqualityComparer<FieldMetadata>
{
    public static readonly FieldMetadataComparer Instance = new();
    
    public bool Equals(FieldMetadata x, FieldMetadata y)
    {
        return SliceComparer.Equals(x.FieldName, y.FieldName) && x.FieldId == y.FieldId && x.Mode == y.Mode && x.Analyzer == y.Analyzer;
    }

    public int GetHashCode(FieldMetadata obj)
    {
        return HashCode.Combine(obj.FieldName, obj.FieldId, (int)obj.Mode, obj.Analyzer);
    }
}
