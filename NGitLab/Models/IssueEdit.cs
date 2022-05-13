﻿using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NGitLab.Models
{
    public class IssueEdit
    {
        [Required]
        [JsonPropertyName("id")]
        public int Id;

        [Required]
        [JsonPropertyName("issue_id")]
        public int IssueId;

        [JsonPropertyName("title")]
        public string Title;

        [JsonPropertyName("description")]
        public string Description;

        [JsonPropertyName("assignee_id")]
        public int? AssigneeId;

        [JsonPropertyName("milestone_id")]
        public int? MilestoneId;

        [JsonPropertyName("labels")]
        public string Labels;

        [JsonPropertyName("state_event")]
        public string State;

        [JsonPropertyName("due_date")]
        public string DueDate;
    }
}
