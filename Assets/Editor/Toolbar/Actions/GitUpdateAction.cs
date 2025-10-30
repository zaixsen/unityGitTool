using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace Toolbar
{
    public class GitUpdateAction : ToolbarActionBase
    {
        private bool _clientOpened = false;
        private const int InitTimeoutMs = 20000; // 初始化阶段命令的超时时间（毫秒）
        protected override string ButtonName => "GitUpdateAction";

        protected override string ButtonText => "Git更新";

        protected override string ButtonTooltip => "Git 更新：\n更新时会暂存本地修改，若有冲突，请使用本地的Git工具进行冲突合并";

        protected override bool DefaultVisible => false;

        public override void Execute()
        {
            var repoPath = GetRepoPath();
            _clientOpened = false;
            using (var scope = BeginProgress("Git 更新", "初始化", 0f))
            {
                bool stashed = false;
                var summary = new StringBuilder();

                scope.Update("暂存本地改动 (stash)", 0.15f);
                if (!ExecuteGitCommand("stash -u -m \"UnityToolbarAuto\"", repoPath, out var out1, out var err1, InitTimeoutMs))
                    return;
                stashed = true;
                if (!string.IsNullOrEmpty(out1)) summary.AppendLine($"stash 输出:\n{out1}");
                if (!string.IsNullOrEmpty(err1)) summary.AppendLine($"stash 警告:\n{err1}");

                scope.Update("解析上游/远端/分支", 0.35f);
                var hasUpstream = TryGetUpstream(repoPath, out var remote, out var branch);

                var pullCmd = hasUpstream ? "pull --rebase" : $"pull --rebase {remote} {branch}";
                summary.AppendLine(hasUpstream ? "使用 tracking upstream 执行: git pull --rebase" : $"使用 {remote}/{branch} 执行: git {pullCmd}");

                scope.Update("拉取远端并 rebase", 0.6f);
                var pulled = ExecuteGitCommand(pullCmd, repoPath, out var out2, out var err2);
                if (!string.IsNullOrEmpty(out2)) summary.AppendLine($"pull 输出:\n{out2}");
                if (!string.IsNullOrEmpty(err2)) summary.AppendLine($"pull 警告:\n{err2}");

                if (!pulled)
                {
                    if (stashed)
                    {
                        scope.Update("恢复本地改动 (stash apply)", 0.7f);
                        ExecuteGitCommand("stash apply", repoPath, out var outApply, out var errApply);
                    }
                    return;
                }

                scope.Update("应用 stash (stash pop)", 0.85f);
                if (!ExecuteGitCommand("stash pop", repoPath, out var out3, out var err3))
                {
                    // 出错时尝试恢复本地改动
                    ExecuteGitCommand("stash apply", repoPath, out var outApply2, out var errApply2);
                    scope.Update("stash pop 失败，已尝试 apply 恢复", 0.9f);

                    // 结合 Git 输出生成更完整的冲突提示
                    var errMsg = !string.IsNullOrEmpty(err3) ? err3 : out3;
                    var guidance = GetConflictGuidance("stash pop", errMsg);
                    var sb = new StringBuilder();
                    sb.AppendLine("stash pop 失败，已尝试 stash apply 恢复本地改动（stash 未删除）。");
                    if (!string.IsNullOrEmpty(errMsg))
                    {
                        sb.AppendLine();
                        sb.AppendLine("错误输出:");
                        sb.AppendLine(errMsg);
                    }
                    if (!string.IsNullOrEmpty(guidance))
                    {
                        sb.AppendLine();
                        sb.AppendLine("建议处理:");
                        sb.AppendLine(guidance);
                        TryOpenLocalGitClient(repoPath);
                    }
                    ShowInfo("Git 命令错误提示", sb.ToString());
                    return;
                }
                if (!string.IsNullOrEmpty(out3)) summary.AppendLine($"stash pop 输出:\n{out3}");
                if (!string.IsNullOrEmpty(err3)) summary.AppendLine($"stash pop 警告:\n{err3}");

                scope.Update("更新完成", 1f);
                if (!string.IsNullOrEmpty(summary.ToString()))
                {
                    UnityEngine.Debug.Log("[GitUpdateAction] 更新完成摘要:\n" + summary);
                }
            }
        }

        private bool ExecuteGitCommand(string command, string workingDirectory, out string output, out string error, int timeoutMs = 0)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = command,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = new Process())
            {
                process.StartInfo = processInfo;
                process.Start();
                if (timeoutMs > 0)
                {
                    var exited = process.WaitForExit(timeoutMs);
                    if (!exited)
                    {
                        try { process.Kill(); } catch { }
                        // 确保进程已退出
                        try { process.WaitForExit(2000); } catch { }

                        output = string.Empty;
                        error = $"命令超时({timeoutMs} ms): {command}";

                        var composedTimeout = $"命令: {command}\n错误: {error}\n\n建议处理:\n请检查网络或仓库状态，稍后重试。";
                        EditorUtility.DisplayDialog("Git 更新失败", "Git 命令超时，请检查网络或仓库状态。", "确定");
                        ShowInfo("Git 命令错误提示", composedTimeout);
                        return false;
                    }

                    output = process.StandardOutput.ReadToEnd();
                    error = process.StandardError.ReadToEnd();
                }
                else
                {
                    output = process.StandardOutput.ReadToEnd();
                    error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                }
                var exitCode = process.ExitCode;

                if (exitCode != 0)
                {
                    var msg = !string.IsNullOrEmpty(error) ? error : output;
                    var isStashPop = command.Contains("stash pop");
                    var guidance = GetConflictGuidance(command, msg);
                    var composed = string.IsNullOrEmpty(guidance)
                        ? $"命令: {command}\n错误: {msg}"
                        : $"命令: {command}\n错误: {msg}\n\n建议处理:\n{guidance}";

                    if (!string.IsNullOrEmpty(guidance))
                    {
                        TryOpenLocalGitClient(workingDirectory);
                        EditorUtility.DisplayDialog("Git 更新冲突", "检测到拉取/变基过程中的冲突，请按指引处理。", "好的");
                    }

                    if (!isStashPop)
                    {
                        ShowInfo("Git 命令错误提示", composed);
                    }
                    return false;
                }

                if (!string.IsNullOrEmpty(output))
                    UnityEngine.Debug.Log(output);
                if (!string.IsNullOrEmpty(error))
                    UnityEngine.Debug.LogWarning(error);

                return true;
            }
        }

        private string GetConflictGuidance(string command, string msg)
        {
            var text = (msg ?? string.Empty).ToLowerInvariant();
            var isRebasePull = command.StartsWith("pull") && command.Contains("--rebase");
            var isStashPop = command.Contains("stash pop");

            // 扩展冲突识别，兼容中文提示与常见报错语句
            bool hasConflictHints =
                text.Contains("conflict") ||
                text.Contains("merge conflict") ||
                text.Contains("could not apply") ||
                text.Contains("rebase") ||
                text.Contains("overwritten") ||
                text.Contains("failed to merge") ||
                (msg ?? string.Empty).Contains("冲突") ||
                (msg ?? string.Empty).Contains("合并冲突") ||
                (msg ?? string.Empty).Contains("无法应用") ||
                (msg ?? string.Empty).Contains("拒绝");

            if (isRebasePull && hasConflictHints)
            {
                var sb = new StringBuilder();
                sb.AppendLine("1) 打开本地 Git 工具或使用命令行：");
                sb.AppendLine("   - 查看状态: git status");
                sb.AppendLine("   - 解决冲突后标记: git add <冲突文件>");
                sb.AppendLine("   - 继续当前变基: git rebase --continue");
                sb.AppendLine("   - 或放弃变基: git rebase --abort");
                sb.AppendLine();
                sb.AppendLine("提示：冲突中的 Mine=本地改动，Theirs=远端更新版本。");
                return sb.ToString();
            }

            if (isStashPop && hasConflictHints)
            {
                var sb = new StringBuilder();
                sb.AppendLine("1) 冲突后 stash 条目通常不会删除，仍可在 git stash list 中看到。");
                sb.AppendLine("2) 解决冲突：编辑冲突文件，保留所需内容后执行: git add <冲突文件>");
                sb.AppendLine("3) 确认工作区无误后，可删除对应 stash：git stash drop stash@{N}");
                sb.AppendLine("4) 若出现未跟踪文件冲突，需手动备份/重命名后再继续。");
                return sb.ToString();
            }

            return string.Empty;
        }

        private void TryOpenLocalGitClient(string repoPath)
        {
            if (_clientOpened) return;
            try
            {
                var tgPath = FindTortoiseGitProcPath();
                if (!string.IsNullOrEmpty(tgPath) && File.Exists(tgPath))
                {
                    _clientOpened = true;
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = tgPath,
                        Arguments = $"/path:\"{repoPath}\" /command:resolve",
                        UseShellExecute = true
                    });
                    return;
                }

                var stPath = FindSourceTreePath();
                if (!string.IsNullOrEmpty(stPath) && File.Exists(stPath))
                {
                    _clientOpened = true;
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = stPath,
                        Arguments = $"-f \"{repoPath}\"",
                        WorkingDirectory = repoPath,
                        UseShellExecute = true
                    });
                    return;
                }

                _clientOpened = true;
                Process.Start(new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "gui",
                    WorkingDirectory = repoPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"自动打开本地 Git 工具失败: {ex.Message}");
            }
        }

        private string FindTortoiseGitProcPath()
        {
            try
            {
                var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                var candidates = new[]
                {
                    Path.Combine(pf, "TortoiseGit", "bin", "TortoiseGitProc.exe"),
                    Path.Combine(pf86, "TortoiseGit", "bin", "TortoiseGitProc.exe")
                };
                foreach (var c in candidates)
                {
                    if (File.Exists(c)) return c;
                }
            }
            catch { }
            return null;
        }

        private string FindSourceTreePath()
        {
            try
            {
                var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var candidates = new[]
                {
                    Path.Combine(pf, "SourceTree", "SourceTree.exe"),
                    Path.Combine(pf86, "SourceTree", "SourceTree.exe"),
                    Path.Combine(local, "SourceTree", "SourceTree.exe")
                };
                foreach (var c in candidates)
                {
                    if (File.Exists(c)) return c;
                }
            }
            catch { }
            return null;
        }

        private bool TryGetUpstream(string repoPath, out string remote, out string branch)
        {
            remote = null;
            branch = null;

            if (ExecuteGitCommand("rev-parse --abbrev-ref --symbolic-full-name @{u}", repoPath, out var upOut, out var upErr))
            {
                var up = (upOut ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(up) && up.Contains("/"))
                {
                    var idx = up.IndexOf('/');
                    remote = up.Substring(0, idx);
                    branch = up.Substring(idx + 1);
                    return true;
                }
            }

            ExecuteGitCommand("remote", repoPath, out var remOut, out var remErr);
            var remotes = (remOut ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(r => r.Trim())
                .ToArray();
            remote = remotes.Contains("origin") ? "origin" : (remotes.Length > 0 ? remotes[0] : null);

            ExecuteGitCommand("rev-parse --abbrev-ref HEAD", repoPath, out var curOut, out var curErr);
            var currentBranch = (curOut ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(currentBranch) || currentBranch == "HEAD")
            {
                if (!string.IsNullOrEmpty(remote) && ExecuteGitCommand($"remote show {remote}", repoPath, out var showOut, out var showErr))
                {
                    var headLine = (showOut ?? string.Empty)
                        .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault(l => l.Contains("HEAD branch:"));
                    if (!string.IsNullOrEmpty(headLine))
                    {
                        var name = headLine.Substring(headLine.IndexOf(':') + 1).Trim();
                        if (!string.IsNullOrEmpty(name))
                            currentBranch = name;
                    }
                }
            }

            if (string.IsNullOrEmpty(currentBranch))
                currentBranch = "develop";

            if (!string.IsNullOrEmpty(remote))
            {
                ExecuteGitCommand($"ls-remote --heads {remote} {currentBranch}", repoPath, out var lsOut, out var lsErr);
                if (string.IsNullOrEmpty(lsOut))
                {
                    if (ExecuteGitCommand($"remote show {remote}", repoPath, out var showOut2, out var showErr2))
                    {
                        var headLine2 = (showOut2 ?? string.Empty)
                            .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .FirstOrDefault(l => l.Contains("HEAD branch:"));
                        if (!string.IsNullOrEmpty(headLine2))
                        {
                            var name2 = headLine2.Substring(headLine2.IndexOf(':') + 1).Trim();
                            if (!string.IsNullOrEmpty(name2))
                                currentBranch = name2;
                        }
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(remote))
            {
                remote = "origin";
            }

            branch = currentBranch;
            return false;
        }

        private string GetRepoPath()
        {
            try
            {
                var startDir = Directory.GetParent(Application.dataPath)?.FullName;
                var current = startDir;
                while (!string.IsNullOrEmpty(current))
                {
                    var dotGitPath = Path.Combine(current, ".git");
                    if (Directory.Exists(dotGitPath) || File.Exists(dotGitPath))
                    {
                        return current;
                    }

                    var parent = Directory.GetParent(current);
                    if (parent == null) break;
                    current = parent.FullName;
                }

                var unityDir = startDir ?? Directory.GetParent(Application.dataPath).FullName;
                var projectDir = Directory.GetParent(unityDir)?.FullName ?? unityDir;
                return projectDir ?? unityDir;
            }
            catch
            {
                var unityDir = Directory.GetParent(Application.dataPath).FullName;
                return unityDir;
            }
        }
    }
}
