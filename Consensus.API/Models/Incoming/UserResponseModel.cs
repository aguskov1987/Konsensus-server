﻿namespace Consensus.API.Models.Incoming
{
    public enum ResponseToType
    {
        Statement,
        Effect
    }

    public class UserResponseModel
    {
        public string HiveId { get; set; }
        public ResponseToType Type { get; set; }
        public string ItemId { get; set; }
        public bool Agree { get; set; }
    }
}