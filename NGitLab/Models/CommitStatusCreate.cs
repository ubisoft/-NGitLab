﻿using System.Text.Json.Serialization;

namespace NGitLab.Models;

public class CommitStatusCreate
{
    [JsonPropertyName("sha")]
    public string CommitSha;

    [JsonPropertyName("state")]
    public string State;

    [JsonPropertyName("status")]
    public string Status;

    [JsonPropertyName("ref")]
    public string Ref;

    [JsonPropertyName("name")]
    public string Name;

    [JsonPropertyName("target_url")]
    public string TargetUrl;

    [JsonPropertyName("description")]
    public string Description;

    [JsonPropertyName("coverage")]
    public int? Coverage;

    [JsonPropertyName("pipeline_id")]
    public long? PipelineId;
}
