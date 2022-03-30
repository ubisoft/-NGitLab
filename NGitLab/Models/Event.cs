﻿using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using NGitLab.Extensions;

namespace NGitLab.Models
{
    /// <summary>
    /// Events are user activity such as commenting a merge request.
    /// </summary>
    [DataContract]
    public class Event
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("project_id")]
        public int ProjectId { get; set; }

        [JsonPropertyName("action_name")]
        public DynamicEnum<EventAction> Action { get; set; }

        [JsonPropertyName("target_id")]
        public long? TargetId { get; set; }

        [JsonPropertyName("target_iid")]
        public long? TargetIId { get; set; }

        [JsonPropertyName("target_type")]
        public DynamicEnum<EventTargetType> TargetType { get; set; }

        [JsonPropertyName("target_title")]
        public string TargetTitle { get; set; }

        [JsonPropertyName("author_id")]
        public int AuthorId { get; set; }

        [JsonPropertyName("author_username")]
        public string AuthorUserName { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("note")]
        public Note Note { get; set; }

        [JsonPropertyName("push_data")]
        public PushData PushData { get; set; }

        /// <summary>
        /// The target is either a GitLab object (like a merge request)
        /// or a commit object
        /// </summary>
        public string ResolvedTargetTitle
        {
            get
            {
                if (TargetTitle != null)
                {
                    return $"{TargetType} '{TargetTitle}'";
                }

                if (PushData != null)
                {
                    return $"{PushData.RefType} '{PushData.Ref}'";
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Debug display
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return ToString(ProjectId.ToString());
        }

        public string ToString(string projectName)
        {
            return $"{AuthorUserName} {Action} {ResolvedTargetTitle} at {projectName} ({GetAge(CreatedAt)})";
        }

        private static string GetAge(DateTime date)
        {
            var age = DateTime.UtcNow.Subtract(date);

            if (age.TotalDays > 1)
                return age.TotalDays.ToStringInvariant("0") + " days ago";

            if (age.TotalHours > 1)
                return age.Hours.ToStringInvariant("0") + " hours ago";

            return age.Minutes.ToStringInvariant("0") + " minutes ago";
        }
    }
}
