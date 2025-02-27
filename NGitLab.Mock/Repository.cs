using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using LibGit2Sharp;
using NGitLab.Mock.Clients;
using NGitLab.Models;
using Blob = LibGit2Sharp.Blob;
using Branch = LibGit2Sharp.Branch;
using Commit = LibGit2Sharp.Commit;
using Identity = LibGit2Sharp.Identity;
using ObjectType = NGitLab.Models.ObjectType;
using Tag = LibGit2Sharp.Tag;
using Tree = NGitLab.Models.Tree;

namespace NGitLab.Mock;

public sealed class Repository : GitLabObject, IDisposable
{
    private static int _tempBranchName;

    private readonly object _lock = new();
    private TemporaryDirectory _directory;
    private string _repositoryDirectory;
    private LibGit2Sharp.Repository _repository;
    private readonly IList<ReleaseTag> _releaseTags = [];

    public Project Project => (Project)Parent;

    internal Repository(Project project)
    {
        Parent = project;
    }

    public string FullPath
    {
        get
        {
            if (_repositoryDirectory == null)
            {
                _ = GetGitRepository();
            }

            return _repositoryDirectory;
        }
    }

    public bool IsEmpty
    {
        get
        {
            if (_directory == null)
                return true;

            return !GetGitRepository().Commits.Any();
        }
    }

    private LibGit2Sharp.Repository GetGitRepository()
    {
        if (_directory == null)
        {
            lock (_lock)
            {
                if (_directory == null)
                {
                    var directory = TemporaryDirectory.Create();

                    // We happen the project full path to have a final path that match more closely the folder layout as found on GitLab
                    var repositoryDirectory = Path.Combine(directory.FullPath, Project.PathWithNamespace.Replace('/', Path.DirectorySeparatorChar));

                    if (Project.ForkedFrom == null)
                    {
                        LibGit2Sharp.Repository.Init(repositoryDirectory);

                        // libgit2sharp cannot init with a branch other than master
                        // Use symbolic-ref to keep the code compatible with older version of git
                        using var process = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "git",
                                Arguments = $"symbolic-ref HEAD \"refs/heads/{Project.DefaultBranch}\"",
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                WorkingDirectory = repositoryDirectory,
                            },
                        };

                        process.Start();
                        process.WaitForExit();
                        if (process.ExitCode != 0)
                        {
                            var error = process.StandardError.ReadToEnd();
                            throw new GitLabException($"Cannot update symbolic ref with branch '{Project.DefaultBranch}' in '{repositoryDirectory}': {error}");
                        }
                    }
                    else
                    {
                        LibGit2Sharp.Repository.Clone(Project.ForkedFrom.Repository.FullPath, repositoryDirectory);
                    }

                    _repository = new LibGit2Sharp.Repository(repositoryDirectory);

                    _repository.Config.Set("receive.advertisePushOptions", value: true);
                    _repository.Config.Set("uploadpack.allowFilter", value: true);
                    _repository.Config.Set("receive.denyCurrentBranch", value: "updateInstead"); // Allow git push to existing branches

                    _repositoryDirectory = repositoryDirectory;
                    _directory = directory;
                }
            }
        }

        return _repository;
    }

    public void SetRepoConfig<T>(string name, T value)
    {
        GetGitRepository().Config.Set(name, value);
    }

    public Commit FindMergeBase(Commit first, Commit second)
    {
        var repository = GetGitRepository();
        return repository.ObjectDatabase.FindMergeBase(first, second);
    }

    public bool IsRebaseNeeded(string branch, string ontoRebaseBranch)
    {
        var repository = GetGitRepository();

        var branchCommit = GetCommit(branch);
        var ontoRebaseCommit = GetCommit(ontoRebaseBranch);

        var commonCommit = repository.ObjectDatabase.FindMergeBase(branchCommit, ontoRebaseCommit);

        return commonCommit != ontoRebaseCommit;
    }

    public Branch GetBranch(string branchName)
    {
        if (string.IsNullOrEmpty(branchName))
            return null;
        var repository = GetGitRepository();
        return repository.Branches[branchName];
    }

    public IReadOnlyCollection<Branch> GetAllBranches()
    {
        var repository = GetGitRepository();
        return repository.Branches.ToList();
    }

    public Commit Commit(User user, string message)
    {
        return Commit(user, message, targetBranch: null, new[] { File.CreateFromText("test.txt", Guid.NewGuid().ToString()) }, Enumerable.Empty<string>());
    }

    public Commit Commit(User user, string message, IEnumerable<File> files)
    {
        return Commit(user, message, targetBranch: null, files);
    }

    public Commit Commit(User user, string message, string targetBranch, IEnumerable<File> files)
    {
        return Commit(user, message, targetBranch, files, Enumerable.Empty<string>());
    }

    public Commit Commit(User user, string message, string targetBranch, IEnumerable<File> files, IEnumerable<string> submodules)
    {
        var repository = GetGitRepository();
        if (targetBranch != null)
        {
            Commands.Checkout(repository, targetBranch);
        }

        foreach (var file in files)
        {
            var fullPath = Path.Combine(FullPath, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            System.IO.File.WriteAllBytes(fullPath, file.Content);
            repository.Index.Add(file.Path);
        }

        foreach (var submodule in submodules)
        {
            repository.Index.Add(submodule);
        }

        repository.Index.Write();

        var author = new Signature(user.UserName, user.Email, DateTimeOffset.UtcNow);
        var committer = author;
        return repository.Commit(message, author, committer);
    }

    public Commit Commit(CommitCreate commitCreate)
    {
        var repo = GetGitRepository();
        var hasStartBranch = !string.IsNullOrWhiteSpace(commitCreate.StartBranch);
        var hasStartSha = !string.IsNullOrWhiteSpace(commitCreate.StartSha);
        var isCreatingBranch = hasStartBranch || hasStartSha;

        var branchExistsAlready = repo.Branches.Any(b => string.Equals(b.FriendlyName, commitCreate.Branch, StringComparison.Ordinal));

        if (isCreatingBranch && branchExistsAlready)
        {
            throw GitLabException.BadRequest($"A branch called '{commitCreate.Branch}' already exists.");
        }

        var isRepoEmpty = !repo.Commits.Any();
        if (!isRepoEmpty)
        {
            var @ref = (hasStartBranch, hasStartSha) switch
            {
                (true, true) => throw GitLabException.BadRequest(
                    "GitLab server returned an error (BadRequest): start_branch, start_sha are mutually exclusive."),
                (true, _) => commitCreate.StartBranch,
                (_, true) => commitCreate.StartSha,
                _ => branchExistsAlready ? commitCreate.Branch : throw GitLabException.BadRequest(
                    "GitLab server returned an error (BadRequest): You can only create or edit files when you are on a branch."),
            };

            Commands.Checkout(repo, @ref);
        }

        var commit = CreateOnNonBareRepo(commitCreate);
        var branchStillMissing = !repo.Branches.Any(b => string.Equals(b.FriendlyName, commitCreate.Branch, StringComparison.Ordinal));
        if ((isCreatingBranch || isRepoEmpty) && (!isRepoEmpty || branchStillMissing))
        {
            repo.Branches.Add(commitCreate.Branch, commit.Sha);
        }

        return commit;
    }

    public Commit CherryPick(CommitCherryPick commitCherryPick)
    {
        var repo = GetGitRepository();
        Commands.Checkout(repo, commitCherryPick.Branch);

        var commit = GetCommit(commitCherryPick.Sha.ToString());
        var options = new CherryPickOptions
        {
            CommitOnSuccess = commitCherryPick.Message?.Length > 0,
        };
        var cherryPickResult = repo.CherryPick(commit, commit.Author, options);
        return cherryPickResult.Commit ?? repo.Commit(commit.Message, commit.Author, commit.Committer);
    }

    public void Checkout(string committishOrBranchNameSpec)
    {
        Commands.Checkout(GetGitRepository(), committishOrBranchNameSpec);
    }

    public Branch CreateAndCheckoutBranch(string branchName)
    {
        var branch = CreateBranch(branchName, "HEAD");
        Checkout(branch.CanonicalName);
        return branch;
    }

    public Branch CreateBranch(string branchName)
    {
        return CreateBranch(branchName, "HEAD");
    }

    public Branch CreateBranch(string branchName, string reference)
    {
        var repository = GetGitRepository();
        return repository.Branches.Add(branchName, reference);
    }

    public void RemoveBranch(string branchName)
    {
        var repository = GetGitRepository();
        Commands.Checkout(repository, Project.DefaultBranch);
        repository.Branches.Remove(branchName);
    }

    public TagCollection GetTags()
    {
        var repository = GetGitRepository();
        return repository.Tags;
    }

    public Tag CreateTag(string tagName)
    {
        return CreateTag(user: null, tagName, "HEAD", message: null, releaseNotes: null);
    }

    public Tag CreateTag(string tagName, string reference)
    {
        return CreateTag(user: null, tagName, reference, message: null, releaseNotes: null);
    }

    public Tag CreateTag(User user, string tagName, string reference, string message)
    {
        return CreateTag(user, tagName, reference, message, releaseNotes: null);
    }

    public Tag CreateTag(User user, string tagName, string reference, string message, string releaseNotes)
    {
        var repository = GetGitRepository();
        Tag tag;

        if (string.IsNullOrEmpty(message))
        {
            tag = repository.ApplyTag(tagName, reference);
        }
        else
        {
            var tagger = new Signature(user.Name, user.Email, DateTimeOffset.UtcNow);
            tag = repository.ApplyTag(tagName, reference, tagger, message);
        }

        if (!string.IsNullOrEmpty(releaseNotes))
        {
            _releaseTags.Add(new ReleaseTag(tagName, releaseNotes));
        }

        return tag;
    }

    public ReleaseTag GetReleaseTag(string tagName)
    {
        return _releaseTags.FirstOrDefault(r => string.Equals(r.Name, tagName, StringComparison.Ordinal));
    }

    public ReleaseTag CreateReleaseTag(string tagName, string releaseNotes)
    {
        if (_releaseTags.Any(r => string.Equals(r.Name, tagName, StringComparison.Ordinal)))
        {
            throw GitLabException.BadRequest();
        }

        var releaseTag = new ReleaseTag(tagName, releaseNotes);
        _releaseTags.Add(releaseTag);
        return releaseTag;
    }

    public ReleaseTag UpdateReleaseTag(string tagName, string releaseNotes)
    {
        if (!_releaseTags.Any(r => string.Equals(r.Name, tagName, StringComparison.Ordinal)))
        {
            throw GitLabException.BadRequest();
        }

        var releaseTag = _releaseTags.First(r => string.Equals(r.Name, tagName, StringComparison.Ordinal));
        releaseTag.ReleaseNotes = releaseNotes;
        return releaseTag;
    }

    public void DeleteTag(string tagName)
    {
        var repository = GetGitRepository();
        repository.Tags.Remove(tagName);
        if (_releaseTags.Any(r => string.Equals(r.Name, tagName, StringComparison.Ordinal)))
        {
            _releaseTags.Remove(_releaseTags.First(r => string.Equals(r.Name, tagName, StringComparison.Ordinal)));
        }
    }

    public Commit Merge(User user, string sourceBranch, string targetBranch)
    {
        var repository = GetGitRepository();
        Commands.Checkout(repository, targetBranch);
        var branch = repository.Branches[sourceBranch];

        var author = new Signature(user.UserName, user.Email, DateTimeOffset.UtcNow);

        var result = repository.Merge(branch, author);
        if (result.Commit != null)
            return result.Commit;

        if (result.Status == MergeStatus.UpToDate || result.Status == MergeStatus.FastForward)
            return repository.Branches[targetBranch].Tip;

        throw new GitLabException("Could not merge");
    }

    public Commit GetBranchTipCommit(string branchName)
    {
        var branch = GetBranch(branchName);
        if (branch != null)
            return branch.Tip;

        return null;
    }

    public IEnumerable<Commit> GetBranchCommits(string branchName)
    {
        var branch = GetBranch(branchName);
        if (branch == null)
            yield break;

        var filter = new CommitFilter
        {
            SortBy = CommitSortStrategies.Topological,
            IncludeReachableFrom = branch,
        };

        foreach (var commit in _repository.Commits.QueryBy(filter))
        {
            yield return commit;
        }
    }

    public IEnumerable<Commit> GetCommits()
    {
        return GetCommits(string.Empty);
    }

    public IEnumerable<Commit> GetCommits(string @ref)
    {
        var filter = new CommitFilter
        {
            SortBy = CommitSortStrategies.Topological,
        };

        if (!string.IsNullOrEmpty(@ref))
        {
            // @ref can represent a revision range:
            // https://docs.gitlab.com/ee/api/commits.html#list-repository-commits
            var range = @ref.Split(new[] { ".." }, StringSplitOptions.None);
            if (range.Length == 2)
            {
                filter.ExcludeReachableFrom = range[0];
                filter.IncludeReachableFrom = range[1];
            }
            else
            {
                filter.IncludeReachableFrom = @ref;
            }
        }

        try
        {
            return _repository?.Commits.QueryBy(filter).ToList() ?? Enumerable.Empty<Commit>();
        }
        catch (NotFoundException)
        {
            // @ref does not exist so no commits could be found
            return Enumerable.Empty<Commit>();
        }
    }

    public Commit GetCommit(string reference)
    {
        var repository = GetGitRepository();
        return repository.Lookup<Commit>(reference);
    }

    public Patch GetBranchFullPatch(string branchName)
    {
        var branch = GetBranch(branchName);
        var filter = new CommitFilter
        {
            SortBy = CommitSortStrategies.Reverse,
            IncludeReachableFrom = branchName,
        };
        var sourceCommit = GetGitRepository().Commits.QueryBy(filter).First();
        return GetGitRepository().Diff.Compare<Patch>(sourceCommit.Tree, branch.Tip.Tree);
    }

    public IEnumerable<Commit> GetCommits(GetCommitsRequest request)
    {
        var filter = new CommitFilter
        {
            SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Reverse,
        };

        if (!string.IsNullOrEmpty(request.RefName))
        {
            if (request.RefName.Contains("..", StringComparison.Ordinal))
            {
                var refs = request.RefName.Split(new[] { ".." }, StringSplitOptions.RemoveEmptyEntries);
                filter.ExcludeReachableFrom = refs[0];
                filter.IncludeReachableFrom = refs[1];
            }
            else
            {
                filter.IncludeReachableFrom = request.RefName;
            }
        }

        if (request.FirstParent != null)
        {
            filter.FirstParentOnly = (bool)request.FirstParent;
        }

        try
        {
            return _repository?.Commits.QueryBy(filter).ToList() ?? Enumerable.Empty<Commit>();
        }
        catch (NotFoundException)
        {
            return Enumerable.Empty<Commit>();
        }
    }

    public FileData GetFile(string filePath, string @ref)
    {
        var repo = GetGitRepository();
        try
        {
            Commands.Checkout(repo, @ref);
        }
        catch (LibGit2Sharp.NotFoundException)
        {
            throw GitLabException.NotFound("File not found");
        }

        var fileCompletePath = Path.Combine(FullPath, filePath);

        if (!System.IO.File.Exists(fileCompletePath))
        {
            throw GitLabException.NotFound("File not found");
        }

        var fileInfo = new FileInfo(fileCompletePath);
        var commit = GetCommit(@ref);
        return new FileData
        {
            Name = fileInfo.Name,
            Path = filePath,
            Size = (int)fileInfo.Length,
            Encoding = "base64",
            Content = Convert.ToBase64String(System.IO.File.ReadAllBytes(fileCompletePath)),
            Ref = @ref,
            BlobId = ((Blob)commit[filePath].Target).Id.ToString(),
            CommitId = commit.Sha,
            LastCommitId = repo.GetLastCommitForFileChanges(filePath)?.Sha,
        };
    }

    internal IEnumerable<Tree> GetTree() => GetTree(new());

    internal IEnumerable<Tree> GetTree(RepositoryGetTreeOptions repositoryGetTreeOptions)
    {
        var repo = GetGitRepository();
        var commitOrBranch = string.IsNullOrEmpty(repositoryGetTreeOptions.Ref) ? Project.DefaultBranch : repositoryGetTreeOptions.Ref;
        Commands.Checkout(repo, commitOrBranch);

        var tree = string.IsNullOrWhiteSpace(repositoryGetTreeOptions.Path)
            ? repo.Head.Tip.Tree
            : repo.Head.Tip[repositoryGetTreeOptions.Path]?.Target as LibGit2Sharp.Tree;

        if (tree is null)
        {
            // As confirmed by the GetAllTreeObjectsAtInvalidPath() test, GitLab's behavior for
            // GetTree() when Path doesn't exist is to return an empty collection.
            if (!string.IsNullOrWhiteSpace(repositoryGetTreeOptions.Path))
                return [];

            throw GitLabException.NotFound();
        }

        return InternalGetTree(repo.Head.Tip, tree, repositoryGetTreeOptions.Recursive);

        static IEnumerable<Tree> InternalGetTree(Commit tip, LibGit2Sharp.Tree tree, bool recurse)
        {
            foreach (var entry in tree)
            {
                if (entry.TargetType == TreeEntryTargetType.GitLink)
                {
                    continue;
                }

                yield return new Tree
                {
                    Id = new Sha1(entry.Target.Id.ToString()),
                    Name = entry.Name,
                    Type = entry.TargetType switch
                    {
                        TreeEntryTargetType.Blob => ObjectType.blob,
                        TreeEntryTargetType.Tree => ObjectType.tree,
                        _ => throw new InvalidOperationException(),
                    },
                    Mode = Convert.ToString((int)entry.Mode, 8).PadLeft(6, '0'),
                    Path = entry.Path,
                };

                if (entry.TargetType == TreeEntryTargetType.Tree && recurse)
                {
                    var subTree = (LibGit2Sharp.Tree)tip[entry.Path].Target;

                    foreach (var subEntry in InternalGetTree(tip, subTree, recurse: true))
                    {
                        yield return subEntry;
                    }
                }
            }
        }
    }

    internal string FetchBranchFromFork(Project fork, string branch)
    {
        if (fork?.ForkedFrom != Project)
            throw new InvalidOperationException("Cannot fetch MR source branch from unrelated project");

        var repo = GetGitRepository();
        var forkRemoteName = "fork" + fork.Id.ToString(CultureInfo.InvariantCulture);
        var forkRemote =
            repo.Network.Remotes[forkRemoteName] ??
            repo.Network.Remotes.Add(forkRemoteName, fork.Repository.FullPath);

        Commands.Fetch(repo, forkRemote.Name, new[] { branch }, new FetchOptions(), logMessage: "");

        return $"remotes/{forkRemote.Name}/{branch}";
    }

    public Commit Merge(User user, string sourceBranch, string targetBranch, Project targetProject)
    {
        // If it's on the same project just merge branch
        if (Parent == targetProject)
            return Merge(user, sourceBranch, targetBranch);

        var repo = GetGitRepository();
        var targetGitRepository = targetProject.Repository.GetGitRepository();

        var branch = repo.Branches[sourceBranch];
        var options = new PushOptions();

        var id = Interlocked.Increment(ref _tempBranchName).ToString(CultureInfo.InvariantCulture);
        var tempBranchName = "merge-" + id;
        var tempRemoteName = "origin-" + id;

        var remote = repo.Network.Remotes.Add(tempRemoteName, targetProject.Repository.FullPath);
        var tempBranch = repo.CreateBranch(tempBranchName, branch.Tip);
        repo.Branches.Update(tempBranch, b => b.Remote = tempRemoteName, b => b.UpstreamBranch = tempBranch.CanonicalName);

        // libgit2sharp cannot push to non-bare local repo: repo.Network.Push(remote, branch.CanonicalName, options);
        using var process = Process.Start("git", $"push {tempBranch.CanonicalName}");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new GitLabException("Cannot push branch");

        repo.Network.Remotes.Remove(tempRemoteName);
        repo.Branches.Remove(tempBranch);

        var merger = new Signature(user.UserName, user.Email, DateTimeOffset.UtcNow);
        Commands.Checkout(targetGitRepository, targetBranch);
        var result = targetGitRepository.Merge(tempBranchName, merger);
        targetGitRepository.Branches.Remove(tempBranchName);

        return result.Commit;
    }

    public bool Rebase(UserRef user, string sourceBranch, string targetBranch)
    {
        if (user is null)
            throw new ArgumentNullException(nameof(user));
        if (string.IsNullOrEmpty(sourceBranch))
            throw new ArgumentException("Cannot be null or empty", nameof(sourceBranch));
        if (string.IsNullOrEmpty(targetBranch))
            throw new ArgumentException("Cannot be null or empty", nameof(targetBranch));

        var repo = GetGitRepository();
        var rebase = repo.Rebase;

        var branch = repo.Branches[sourceBranch];
        var upstream = repo.Branches[targetBranch];

        var committer = new Identity(user.UserName, user.Email);
        var options = new RebaseOptions();

        _ = repo.Network.Remotes[targetBranch];

        var rebaseResult = rebase.Start(branch, upstream, onto: null, committer, options);
        if (rebaseResult.Status == RebaseStatus.Conflicts)
        {
            rebase.Abort();
            return false;
        }

        return true;
    }

    internal bool HasConflicts(UserRef user, string sourceBranch, string targetBranch)
    {
        if (user is null)
            throw new ArgumentNullException(nameof(user));
        if (string.IsNullOrEmpty(sourceBranch))
            throw new ArgumentException("Cannot be null or empty", nameof(sourceBranch));
        if (string.IsNullOrEmpty(targetBranch))
            throw new ArgumentException("Cannot be null or empty", nameof(targetBranch));

        var repo = GetGitRepository();

        var head = repo.Head;
        var branch = repo.Branches[sourceBranch];
        var upstream = repo.Branches[targetBranch];

        var signature = new Signature(user.UserName, user.Email, DateTimeOffset.UtcNow);
        var options = new MergeOptions
        {
            CommitOnSuccess = false,
            FastForwardStrategy = FastForwardStrategy.NoFastForward,
        };

        Commands.Checkout(repo, upstream);

        var resetTip = upstream.Tip;

        var mergeResult = repo.Merge(branch.Tip, signature, options);
        var result = mergeResult.Status == MergeStatus.Conflicts;

        repo.Reset(ResetMode.Hard, resetTip);
        Commands.Checkout(repo, head);

        return result;
    }

    public void Dispose()
    {
        try
        {
            _repository?.Dispose();
            _directory?.Dispose();
        }
        catch
        {
            // It's not important if the directory is not deleted
        }
    }

    private Commit CreateOnNonBareRepo(CommitCreate commit)
    {
        var repo = GetGitRepository();
        ApplyActions(commit);

        var defaultUser = Parent.Server.Users.First(Project.IsUserMember);

        var author = new Signature(commit.AuthorName ?? defaultUser.Name, commit.AuthorEmail ?? defaultUser.Email, DateTimeOffset.UtcNow);
        var committer = author;

        var newCommit = repo.Commit(commit.CommitMessage, author, committer);
        return newCommit;
    }

    private void ApplyActions(CommitCreate commit)
    {
        var repo = GetGitRepository();

        foreach (var action in commit.Actions)
        {
            GetActionHandler(action.Action).Handle(action, FullPath, repo);
        }
    }

    private static ICommitActionHandler GetActionHandler(string action)
    {
        if (!Enum.TryParse<CommitAction>(action, ignoreCase: true, out var result))
            throw new ArgumentOutOfRangeException(nameof(action));

        return result switch
        {
            CommitAction.Create => new CommitActionCreateHandler(),
            CommitAction.Delete => new CommitActionDeleteHandler(),
            CommitAction.Update => new CommitActionUpdateHandler(),
            CommitAction.Move => new CommitActionMoveHandler(),
            CommitAction.chmod => new CommitActionChmodHandler(),
            _ => throw new NotSupportedException(),
        };
    }

    internal int ComputeDivergence(Commit first, Commit second)
    {
        var repository = GetGitRepository();
        var historyDivergence = repository.ObjectDatabase.CalculateHistoryDivergence(first, second);

        var divergence = 0;
        if (historyDivergence.AheadBy.HasValue)
            divergence += historyDivergence.AheadBy.Value;
        if (historyDivergence.BehindBy.HasValue)
            divergence += historyDivergence.BehindBy.Value;

        return divergence;
    }

    internal void GetRawBlob(string sha, Action<Stream> parser)
    {
        var repository = GetGitRepository();
        var blob = repository.Lookup<Blob>(sha);

        if (blob is not null)
        {
            using var source = blob.GetContentStream();
            parser(source);
        }
    }
}
