using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace NGitLab.Models;

public class CommitCreate
{
    [Required]
    [JsonPropertyName("branch")]
    public string Branch { get; set; }

    [JsonPropertyName("commit_message")]
    public string CommitMessage { get; set; }

    [JsonPropertyName("start_branch")]
    public string StartBranch { get; set; }

    [JsonPropertyName("start_sha")]
    public string StartSha { get; set; }

    [JsonPropertyName("author_email")]
    public string AuthorEmail { get; set; }

    [JsonPropertyName("author_name")]
    public string AuthorName { get; set; }

    [JsonPropertyName("actions")]
    public IList<CreateCommitAction> Actions { get; set; } = [];

    [JsonPropertyName("force")]
    public bool? Force { get; set; }
}

public class CreateCommitAction
{
    [JsonPropertyName("action")]
    public string Action { get; set; }

    [JsonPropertyName("file_path")]
    public string FilePath { get; set; }

    [JsonPropertyName("previous_path")]
    public string PreviousPath { get; set; }

    [JsonPropertyName("content")]
    public string Content { get; set; }

    [JsonPropertyName("encoding")]
    public string Encoding { get; set; }

    [JsonPropertyName("execute_filemode")]
    public bool IsExecutable { get; set; }
}
