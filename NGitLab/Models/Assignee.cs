﻿using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace NGitLab.Models
{
    [DataContract]
    public class Assignee
    {
        [JsonPropertyName("id")]
        public int Id;

        [JsonPropertyName("username")]
        public string Username;

        [JsonPropertyName("email")]
        public string Email;

        [JsonPropertyName("name")]
        public string Name;

        [JsonPropertyName("state")]
        public string State;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt;
    }
}
