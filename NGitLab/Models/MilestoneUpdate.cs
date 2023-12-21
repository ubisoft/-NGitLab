﻿using System.Text.Json.Serialization;

namespace NGitLab.Models;

public class MilestoneUpdate
{
    [JsonPropertyName("title")]
    public string Title;

    [JsonPropertyName("description")]
    public string Description;

    [JsonPropertyName("due_date")]
    public string DueDate;

    [JsonPropertyName("start_date")]
    public string StartDate;
}

public class MilestoneUpdateState
{
    [JsonPropertyName("state_event")]
    public string NewState;
}

// ReSharper disable InconsistentNaming
public enum MilestoneStateEvent
{
    activate,
    close,
}
