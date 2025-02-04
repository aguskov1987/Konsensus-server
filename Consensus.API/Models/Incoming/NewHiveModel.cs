﻿using Consensus.Backend.Models;

namespace Consensus.API.Models.Incoming
{
    public class NewHiveModel
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string SeedLabel { get; set; }
        public PointType SeedType { get; set; }
    }
}