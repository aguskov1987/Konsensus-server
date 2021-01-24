﻿using System.Collections.Generic;

namespace ArangoDBNetStandard.GraphApi.Models
{
    public class DeleteVertexQuery
    {
        public bool? WaitForSync { get; set; }

        public bool? ReturnOld { get; set; }

        internal string ToQueryString()
        {
            List<string> query = new List<string>();
            if (WaitForSync != null)
            {
                query.Add("waitForSync=" + WaitForSync.ToString().ToLower());
            }
            if (ReturnOld != null)
            {
                query.Add("returnOld=" + ReturnOld.ToString().ToLower());
            }

            return string.Join("&", query);
        }
    }
}
