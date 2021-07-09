﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NGitLab.Models;

namespace NGitLab.Mock
{
    public sealed class ReleaseInfo : GitLabObject
    {
        public ReleaseInfo()
        {
        }

        public Project Project => (Project)Parent;

        public string TagName { get; set; }

        public string Description { get; set; }

        public string Name { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime ReleasedAt { get; set; } = DateTime.UtcNow;

        public UserRef Author { get; set; }

        public Commit Commit { get; set; }

        public string CommitPath { get; set; }

        public string TagPath { get; set; }

        internal Models.ReleaseInfo ToReleaseClient()
        {
            return new Models.ReleaseInfo
            {
                TagName = TagName,
                Name = Name,
                Description = Description,
                CreatedAt = CreatedAt,
                ReleasedAt = ReleasedAt,
                Author = Author.ToClientAuthor(),
                Commit = Commit,
                CommitPath = CommitPath,
                TagPath = TagPath,
            };
        }
    }
}
