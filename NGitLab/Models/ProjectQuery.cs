using System;
using System.Collections.Generic;

namespace NGitLab.Models;

/// <summary>
/// Allows to use more advanced GitLab queries for getting projects.
/// </summary>
public class ProjectQuery
{
    public ProjectQueryScope Scope { get; set; } = ProjectQueryScope.Accessible;

    /// <summary>
    /// Limit by archived status
    /// </summary>
    public bool? Archived { get; set; }

    /// <summary>
    /// Limit by visibility public, internal, or private
    /// </summary>
    public VisibilityLevel? Visibility { get; set; }

    /// <summary>
    /// Return projects ordered by id, name, path, created_at, updated_at, or last_activity_at fields. Default is created_at
    /// </summary>
    public string OrderBy { get; set; }

    /// <summary>
    /// Return list of authorized projects matching the search criteria
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Order projects results ascending. Default is descending
    /// </summary>
    public bool? Ascending { get; set; }

    /// <summary>
    /// Return only the ID, URL, name, and path of each project
    /// </summary>
    public bool? Simple { get; set; }

    /// <summary>
    /// Include project statistics
    /// </summary>
    public bool? Statistics { get; set; }

    /// <summary>
    /// Specifies how many records per page (GitLab supports a maximum of 100 items per page and defaults to 20).
    /// </summary>
    public int? PerPage { get; set; }

    /// <summary>
    /// Project visible by user
    /// </summary>
    public long? UserId { get; set; }

    /// <summary>
    /// Limit to projects where current user has at least this access level
    /// (optional)
    /// </summary>
    public AccessLevel? MinAccessLevel { get; set; }

    /// <summary>
    /// Limit results to projects with last_activity after specified time.
    /// </summary>
    public DateTimeOffset? LastActivityAfter { get; set; }

    /// <summary>
    /// Limit results to projects that match all of the given topics.
    /// </summary>
    public IList<string> Topics { get; } = [];
}

public enum ProjectQueryScope
{
    /// <summary>
    /// Get a list of projects for which the authenticated user is a member.
    /// </summary>
    Accessible,

    /// <summary>
    /// Get a list of projects owned by the authenticated user.
    /// </summary>
    Owned,

    /// <summary>
    /// Get a list of all projects which the authenticated user can see.
    /// </summary>
    All,
}
