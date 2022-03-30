﻿using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace NGitLab.Models
{
    [DataContract]
    public class LabelEdit
    {
        public LabelEdit()
        {
        }

        public LabelEdit(int projectId, Label label)
        {
            Id = projectId;
            Name = label.Name;
        }

        [Required]
        [JsonPropertyName("id")]
        public int Id;

        [Required]
        [JsonPropertyName("name")]
        public string Name;

        [JsonPropertyName("new_name")]
        public string NewName;

        [JsonPropertyName("color")]
        public string Color;

        [JsonPropertyName("description")]
        public string Description;
    }
}
