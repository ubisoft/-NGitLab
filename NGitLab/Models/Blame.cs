﻿using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace NGitLab.Models
{
    public sealed class Blame : IEquatable<Blame>
    {
        [JsonPropertyName("commit")]
        public BlameCommit Commit { get; set; }

        [JsonPropertyName("lines")]
        public string[] Lines { get; set; }

        public bool Equals(Blame other)
        {
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return Equals(Commit, other.Commit)
                && Lines is not null && other.Lines is not null && Lines.SequenceEqual(other.Lines, StringComparer.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is Blame blame && Equals(blame);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Commit != null ? Commit.GetHashCode() : 0) * 397) ^ (Lines != null ? Lines.GetHashCode() : 0);
            }
        }
    }
}
