﻿using System.Collections.Generic;

namespace Raven.Server.Documents.Sharding.ShardResultStreaming.ShardResultComparer;

public class StreamDocumentByLastModifiedComparer : Comparer<ShardStreamItem<Document>>
{
    public static StreamDocumentByLastModifiedComparer Instance = new StreamDocumentByLastModifiedComparer();
        
    public override int Compare(ShardStreamItem<Document> x, ShardStreamItem<Document> y)
    {
        return DocumentByLastModifiedComparer.Instance.Compare(x.Item, y.Item);
    }
}
