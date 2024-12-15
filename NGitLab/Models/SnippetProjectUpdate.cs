﻿using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NGitLab.Models;

public class SnippetProjectUpdate
{
    public long ProjectId;

    [Required]
    [JsonPropertyName("id")]
    public long SnippetId { get; set; }

    [Required]
    [JsonPropertyName("title")]
    public string Title;

    [JsonPropertyName("description")]
    public string Description;

    [Required]
    [JsonPropertyName("visibility")]
    public VisibilityLevel Visibility;

    /// <summary>
    /// An array of snippet files. Required when updating snippets with multiple files.
    /// </summary>
    [JsonPropertyName("files")]
    public SnippetUpdateFile[] Files { get; set; }
}
