using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PageToMovie.Core.Models;

namespace PageToMovie.Engine;

public sealed class ProjectContributionService
{
    private readonly ILogger<ProjectContributionService> _logger;

    public ProjectContributionService(ILogger<ProjectContributionService> logger)
    {
        _logger = logger;
    }

    public Task<ContributionDiffDto> ComputeDiffAsync(
        string projectId,
        string parentProjectId,
        string targetDir,
        string originDir,
        CancellationToken ct = default)
    {
        var result = new ContributionDiffDto
        {
            ProjectId = projectId,
            ParentProjectId = parentProjectId,
        };

        if (!Directory.Exists(targetDir) || !Directory.Exists(originDir))
        {
            _logger.LogWarning("Cannot compute diff: target or origin directory missing. Target: {Target}, Origin: {Origin}", targetDir, originDir);
            return Task.FromResult(result);
        }

        var filesToCompare = new List<(string RelPath, string Category)>
        {
            ("source/screenplay.fountain", "Screenplay"),
            ("cast_seeds.json", "Cast Seeds"),
            ("blueprint.clips.grok.json", "Shot Plan"),
            ("project_rules.json", "Rules")
        };

        // Also check any additional .fountain files in source/
        var sourceDirTarget = Path.Combine(targetDir, "source");
        if (Directory.Exists(sourceDirTarget))
        {
            foreach (var f in Directory.GetFiles(sourceDirTarget, "*.fountain"))
            {
                var rel = Path.Combine("source", Path.GetFileName(f)).Replace('\\', '/');
                if (!filesToCompare.Any(x => string.Equals(x.RelPath, rel, StringComparison.OrdinalIgnoreCase)))
                {
                    filesToCompare.Add((rel, "Screenplay"));
                }
            }
        }

        bool overallHasConflicts = false;

        foreach (var (relPath, category) in filesToCompare)
        {
            var oursFile = Path.Combine(targetDir, relPath);
            var theirsFile = Path.Combine(originDir, relPath);

            var oursExists = File.Exists(oursFile);
            var theirsExists = File.Exists(theirsFile);

            if (!oursExists && !theirsExists) continue;

            var oursContent = oursExists ? File.ReadAllText(oursFile) : "";
            var theirsContent = theirsExists ? File.ReadAllText(theirsFile) : "";

            string status;
            if (!oursExists) status = "deleted";
            else if (!theirsExists) status = "added";
            else if (string.Equals(oursContent, theirsContent, StringComparison.Ordinal)) status = "identical";
            else status = "modified";

            var lines = ComputeLineDiff(oursContent, theirsContent, out bool fileHasConflicts);
            if (fileHasConflicts) overallHasConflicts = true;

            result.FileDiffs.Add(new ContributionDiffItemDto
            {
                FilePath = relPath,
                Category = category,
                Status = status,
                OursContent = oursContent,
                TheirsContent = theirsContent,
                Lines = lines
            });
        }

        result.HasConflicts = overallHasConflicts;
        return Task.FromResult(result);
    }

    private static List<DiffLineDto> ComputeLineDiff(string ours, string theirs, out bool hasConflicts)
    {
        hasConflicts = false;
        var oursLines = (ours ?? "").Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var theirsLines = (theirs ?? "").Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        var result = new List<DiffLineDto>();
        int i = 0, j = 0;
        int lineOurs = 1, lineTheirs = 1;

        while (i < oursLines.Length || j < theirsLines.Length)
        {
            if (i < oursLines.Length && j < theirsLines.Length && string.Equals(oursLines[i], theirsLines[j], StringComparison.Ordinal))
            {
                result.Add(new DiffLineDto
                {
                    Kind = "unchanged",
                    LineNumberOurs = lineOurs++,
                    LineNumberTheirs = lineTheirs++,
                    Content = oursLines[i]
                });
                i++;
                j++;
            }
            else if (i < oursLines.Length && (j >= theirsLines.Length || !theirsLines.Contains(oursLines[i])))
            {
                result.Add(new DiffLineDto
                {
                    Kind = "added",
                    LineNumberOurs = lineOurs++,
                    LineNumberTheirs = null,
                    Content = oursLines[i]
                });
                i++;
            }
            else if (j < theirsLines.Length)
            {
                result.Add(new DiffLineDto
                {
                    Kind = "deleted",
                    LineNumberOurs = null,
                    LineNumberTheirs = lineTheirs++,
                    Content = theirsLines[j]
                });
                j++;
            }
        }

        return result;
    }
}
