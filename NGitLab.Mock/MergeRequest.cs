using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NGitLab.Models;
using Commit = LibGit2Sharp.Commit;

namespace NGitLab.Mock
{
    public sealed class MergeRequest : GitLabObject
    {
        private static readonly User DefaultUser = new User("default");

        private string _consolidatedSourceBranch;
        private string _previousHeadSha;
        private string _previousStartSha;

        private string _headSha;
        private string _startSha;
        private string _baseSha;
        private bool _hasConflicts;
        private int? _divergedCommitsCount;

        public MergeRequest()
        {
            Comments = new NoteCollection<MergeRequestComment>(this);
        }

        public Project Project => (Project)Parent;

        public int Id { get; internal set; }

        public int Iid { get; internal set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public UserRef Author { get; set; }

        public UserRef Assignee
        {
            get => Assignees?.FirstOrDefault();
            set
            {
                Assignees.Clear();
                if (value != null)
                {
                    Assignees.Add(value);
                }
            }
        }

        public IList<UserRef> Assignees { get; set; } = new List<UserRef>();

        public IList<UserRef> Reviewers { get; set; } = new List<UserRef>();

        public string SourceBranch { get; set; }

        public string TargetBranch { get; set; }

        public Project SourceProject { get; set; }

        public string HeadSha
        {
            get
            {
                RefreshInternalState();
                return _headSha;
            }
        }

        public string StartSha
        {
            get
            {
                RefreshInternalState();
                return _startSha;
            }
        }

        public string BaseSha
        {
            get
            {
                RefreshInternalState();
                return _baseSha;
            }
        }

        public bool HasConflicts
        {
            get
            {
                RefreshInternalState();
                return _hasConflicts;
            }
        }

        public int? DivergedCommitsCount
        {
            get
            {
                RefreshInternalState();
                return _divergedCommitsCount;
            }
        }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? MergedAt { get; set; }

        public DateTimeOffset? ClosedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        public Sha1 Sha { get; private set; }

        public Sha1? MergeCommitSha { get; set; }

        public bool ShouldRemoveSourceBranch { get; set; }

        public bool ForceRemoveSourceBranch { get; set; }

        public bool Squash { get; set; }

        public bool MergeWhenPipelineSucceeds { get; set; }

        public bool RebaseInProgress { get; set; }

        public string WebUrl => Server.MakeUrl($"{Project.PathWithNamespace}/-/merge_requests/{Iid.ToString(CultureInfo.InvariantCulture)}");

        public Pipeline HeadPipeline
        {
            get
            {
                if (string.IsNullOrEmpty(HeadSha))
                    return null;
                var headSha = new Sha1(_headSha);
                var pipeline = Project.Pipelines
                  .Where(p => p.Sha.Equals(headSha))
                  .OrderByDescending(p => p.CreatedAt)
                  .FirstOrDefault();
                return pipeline;
            }
        }

        public IList<string> Labels { get; } = new List<string>();

        public NoteCollection<MergeRequestComment> Comments { get; }

        public bool WorkInProgress => Title is not null &&
            (Title.StartsWith("WIP:", StringComparison.OrdinalIgnoreCase) ||
             Title.StartsWith("Draft:", StringComparison.OrdinalIgnoreCase));

        public IList<UserRef> Approvers { get; } = new List<UserRef>();

        public MergeRequestChangeCollection Changes
        {
            get
            {
                var stats = Project.Repository.GetBranchFullPatch(SourceBranch);
                var changes = new MergeRequestChangeCollection(this);
                foreach (var stat in stats)
                {
                    var diff = stat.Patch.Substring(stat.Patch.IndexOf("@@", StringComparison.Ordinal));
                    changes.Add(diff, stat.OldPath, stat.Path, stat.Status);
                }

                return changes;
            }
        }

        public MergeRequestState State
        {
            get
            {
                if (MergedAt.HasValue)
                    return MergeRequestState.merged;

                if (ClosedAt.HasValue)
                    return MergeRequestState.closed;

                return MergeRequestState.opened;
            }
        }

        public IEnumerable<Commit> Commits
        {
            get
            {
                var targetBranch = Project.Repository.GetBranch(TargetBranch);
                return SourceProject.Repository.GetBranchCommits(SourceBranch).TakeWhile(commit => commit.Id != targetBranch.Tip.Id);
            }
        }

        internal void UpdateSha()
        {
            var commit = SourceProject.Repository.GetBranchTipCommit(SourceBranch);
            if (commit != null)
            {
                Sha = new Sha1(commit.Sha);
            }
        }

        public void Accept(User user)
        {
            var mergeCommit = Project.Repository.Merge(user, ConsolidatedSourceBranch, TargetBranch, Project);

            MergeCommitSha = new Sha1(mergeCommit.Sha);
            MergedAt = DateTimeOffset.UtcNow;
            UpdatedAt = DateTimeOffset.UtcNow;

            if (ForceRemoveSourceBranch || ShouldRemoveSourceBranch)
            {
                SourceProject.Repository.RemoveBranch(SourceBranch);
            }

            if (Project != SourceProject)
                Project.Repository.RemoveBranch(ConsolidatedSourceBranch);
        }

        public RebaseResult Rebase(User user)
        {
            SourceProject.Repository.Rebase(user, SourceBranch, TargetBranch);

            UpdatedAt = DateTimeOffset.UtcNow;

            return new RebaseResult { RebaseInProgress = true };
        }

        public IEnumerable<MergeRequestDiscussion> GetDiscussions()
        {
            var sha1 = SHA1.Create();
            var i = 0;
            var discussions = new List<MergeRequestDiscussion>();
            foreach (var comment in Comments)
            {
                if (string.IsNullOrEmpty(comment.ThreadId))
                {
                    discussions.Add(new MergeRequestDiscussion
                    {
                        Id = Convert.ToBase64String(sha1.ComputeHash(Encoding.UTF8.GetBytes($"{Iid}:{i}"))),
                        IndividualNote = true,
                        Notes = new[] { comment.ToMergeRequestCommentClient() },
                    });
                }
                else
                {
                    var discussion = discussions.Find(x => string.Equals(x.Id, comment.ThreadId, StringComparison.Ordinal));
                    if (discussion == null)
                    {
                        discussions.Add(new MergeRequestDiscussion
                        {
                            Id = comment.ThreadId,
                            IndividualNote = false,
                            Notes = new[] { comment.ToMergeRequestCommentClient() },
                        });
                    }
                    else
                    {
                        discussion.Notes = discussion.Notes.Concat(new[] { comment.ToMergeRequestCommentClient() }).ToArray();
                    }
                }

                i++;
            }

            return discussions;
        }

        internal Models.MergeRequest ToMergeRequestClient()
        {
            return new Models.MergeRequest
            {
                Assignees = GetUsers(Assignees),
                Author = Author.ToUserClient(),
                CreatedAt = CreatedAt.UtcDateTime,
                UpdatedAt = UpdatedAt.UtcDateTime,
                MergedAt = MergedAt?.UtcDateTime,
                ClosedAt = ClosedAt?.UtcDateTime,
                Description = Description,
                Id = Id,
                Iid = Iid,
                ProjectId = Project.Id,
                MergeCommitSha = MergeCommitSha?.ToString(),
                Sha = Sha.ToString(),
                ForceRemoveSourceBranch = ForceRemoveSourceBranch,
                MergeWhenPipelineSucceeds = MergeWhenPipelineSucceeds,
                ShouldRemoveSourceBranch = ShouldRemoveSourceBranch,
                SourceBranch = SourceBranch,
                SourceProjectId = SourceProject.Id,
                TargetBranch = TargetBranch,
                TargetProjectId = Project.Id,
                Title = Title,
                WorkInProgress = WorkInProgress,
                Squash = Squash,
                MergeStatus = "can_be_merged",
                State = State.ToString(),
                WebUrl = WebUrl,
                HeadPipeline = HeadPipeline?.ToPipelineClient(),
                Labels = Labels.ToArray(),
                RebaseInProgress = RebaseInProgress,
                Reviewers = GetUsers(Reviewers),
                HasConflicts = HasConflicts,
                DivergedCommitsCount = DivergedCommitsCount,
                DiffRefs = new DiffRefs
                {
                    BaseSha = BaseSha,
                    StartSha = StartSha,
                    HeadSha = HeadSha,
                },
            };
        }

        internal static Models.User[] GetUsers(IList<UserRef> userRefs)
        {
            var users = new List<Models.User>();
            foreach (var userRef in userRefs)
            {
                var user = userRef.ToUserClient();
                users.Add(user);
            }

            return users.ToArray();
        }

        internal string ConsolidatedSourceBranch
        {
            get
            {
                RefreshInternalState();
                return _consolidatedSourceBranch;
            }
        }

        internal Commit SourceBranchHeadCommit => SourceProject?.Repository?.GetBranchTipCommit(SourceBranch);

        internal Commit TargetBranchHeadCommit => Project?.Repository?.GetBranchTipCommit(TargetBranch);

        private void RefreshInternalState()
        {
            // Once the MR is merged or closed, stop updating its internal state
            if (State != MergeRequestState.opened)
                return;

            var headSha = SourceBranchHeadCommit?.Sha;
            if (headSha is not null)
                _headSha = headSha;

            var startSha = TargetBranchHeadCommit?.Sha;
            if (startSha is not null)
                _startSha = startSha;

            if (_headSha is null || _startSha is null ||
                (string.Equals(_previousHeadSha, _headSha, StringComparison.Ordinal) &&
                 string.Equals(_previousStartSha, _startSha, StringComparison.Ordinal)))
            {
                return;
            }

            _previousHeadSha = _headSha;
            _previousStartSha = _startSha;

            // If source project is not target project, fetch the former's source branch into the latter
            _consolidatedSourceBranch = SourceBranch;
            if (SourceProject != Project)
            {
                _consolidatedSourceBranch = Project.Repository.FetchBranchFromFork(SourceProject, SourceBranch);
            }

            var commonCommit = Project.Repository.FindMergeBase(TargetBranchHeadCommit, SourceBranchHeadCommit);
            if (commonCommit is null)
                throw new InvalidOperationException($"Branch '{SourceBranch}' does not seem to stem from branch '{TargetBranch}'");

            _baseSha = commonCommit.Sha;

            _divergedCommitsCount = Project.Repository.ComputeDivergence(TargetBranchHeadCommit, commonCommit);

            // Determine if there are conflicts, by performing a rebase on a transient copy of the source branch
            var smallBranchName = Guid.NewGuid().ToString("N");
            var transientBranchName = "refs/transient/" + smallBranchName;

            var branchFullPath = Path.Combine(Project.Repository.FullPath, ".git/refs/transient", smallBranchName);
            Directory.CreateDirectory(Path.GetDirectoryName(branchFullPath)!);
            System.IO.File.WriteAllText(branchFullPath, Project.Repository.GetCommit(SourceBranch).Sha);

            try
            {
                _hasConflicts = !Project.Repository.Rebase(DefaultUser, transientBranchName, TargetBranch);
            }
            catch
            {
                _hasConflicts = true;
            }

            //System.IO.File.Delete(branchFullPath);
        }
    }
}
