﻿using System;
using System.Text.Json.Serialization;

namespace NGitLab.Models;

public class ProjectIssueNote
{
    [JsonPropertyName("id")]
    public long NoteId;

    [JsonPropertyName("body")]
    public string Body;

    [JsonPropertyName("attachment")]
    public string Attachment;

    [JsonPropertyName("author")]
    public Author Author;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt;

    [JsonPropertyName("system")]
    public bool System;

    [JsonPropertyName("noteable_id")]
    public long NoteableId;

    [JsonPropertyName("noteable_type")]
    public string NoteableType;

    [JsonPropertyName("noteable_iid")]
    public long Noteable_Iid;

    [JsonPropertyName("resolvable")]
    public bool Resolvable;

    [JsonPropertyName("confidential")]
    public bool Confidential;
}
