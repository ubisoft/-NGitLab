﻿using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace NGitLab.Models
{
    [DataContract]
    [DebuggerDisplay("{Name}")]
    public class User
    {
        public const string Url = "/users";

        [DataMember(Name = "id")]
        public int Id;

        [DataMember(Name = "username")]
        public string Username;

        [DataMember(Name = "email")]
        public string Email;

        [DataMember(Name = "name")]
        public string Name;

        [DataMember(Name = "skype")]
        public string Skype;

        [DataMember(Name = "linkedin")]
        public string Linkedin;

        [DataMember(Name = "twitter")]
        public string Twitter;

        [DataMember(Name = "state")]
        public string State;

        [DataMember(Name = "blocked")]
        public bool Blocked;

        [DataMember(Name = "created_at")]
        public DateTime CreatedAt;

        [DataMember(Name = "last_activity_on")]
        public DateTime LastActivityOn;

        [DataMember(Name = "avatar_url")]
        public string AvatarURL;

        [DataMember(Name = "bio")]
        public string Bio;

        [DataMember(Name = "color_scheme_id")]
        public int ColorSchemeId;

        [DataMember(Name = "theme_id")]
        public int ThemeId;

        [DataMember(Name = "website_url")]
        public string WebsiteURL;

        [DataMember(Name = "is_admin")]
        public bool IsAdmin;

        [DataMember(Name = "can_create_group")]
        public bool CanCreateGroup;

        [DataMember(Name = "can_create_project")]
        public bool CanCreateProject;

        [DataMember(Name = "identities")]
        public Identity[] Identities;

        [Obsolete("This does not match GitLab's Api. Use Identities.Provider instead.")]
        [DataMember(Name = "provider")]
        public string Provider;

        [Obsolete("This does not match GitLab's Api. Use Identities.ExternUid instead.")]
        [DataMember(Name = "extern_uid")]
        public string ExternUid;
    }
}
