﻿using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace NGitLab.Models
{
    [DataContract]
    public class WikiPageCreate
    {
        [JsonPropertyName("format")]
        public string Format;

        [JsonPropertyName("content")]
        public string Content;

        [JsonPropertyName("title")]
        public string Title;
    }
}
