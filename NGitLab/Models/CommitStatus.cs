﻿using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace NGitLab.Models
{
    [DataContract]
    public class CommitStatus
    {
        [JsonPropertyName("id")]
        public int ProjectId;

        [JsonPropertyName("sha")]
        public string CommitSha;

        [JsonPropertyName("ref")]
        public string Ref;

        [JsonPropertyName("status")]
        public string Status;

        [JsonPropertyName("name")]
        public string Name;
    }
}
