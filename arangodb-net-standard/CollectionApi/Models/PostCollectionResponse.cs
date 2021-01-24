﻿using System.Net;

namespace ArangoDBNetStandard.CollectionApi.Models
{
    public class PostCollectionResponse
    {
        public bool Error { get; set; }

        public HttpStatusCode Code { get; set; }

        public bool WaitForSync { get; set; }

        public CollectionType Type { get; set; }

        public int Status { get; set; }

        public long JournalSize { get; set; }

        public PostCollectionResponseCollectionKeyOptions KeyOptions { get; set; }

        public string GloballyUniqueId { get; set; }

        public string StatusString { get; set; }

        public string Id { get; set; }

        public string Name { get; set; }

        public bool DoCompact { get; set; }

        public bool IsSystem { get; set; }

        public int IndexBuckets { get; set; }

        public bool IsVolatile { get; set; }

    }
}