﻿using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace NGitLab.Models
{
    [DataContract]
    public class Milestone
    {
        [JsonPropertyName("id")]
        public int Id;

        [JsonPropertyName("title")]
        public string Title;

        [JsonPropertyName("description")]
        public string Description;

        [JsonPropertyName("due_date")]
        public string DueDate;

        [JsonPropertyName("start_date")]
        public string StartDate;

        [JsonPropertyName("state")]
        public string State;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt;

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt;
    }

    public enum MilestoneState
    {
        active,
        closed,
    }
}
