﻿using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NGitLab.Models;

public class TagCreate
{
    /// <summary>
    /// (required) - The name of a tag
    /// </summary>
    [Required]
    [JsonPropertyName("tag_name")]
    public string Name;

    /// <summary>
    /// (required) - Create tag using commit SHA, another tag name, or branch name.
    /// </summary>
    [Required]
    [JsonPropertyName("ref")]
    public string Ref;

    /// <summary>
    /// (optional) - Creates annotated tag.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message;
}
