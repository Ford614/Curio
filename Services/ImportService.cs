using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Curio.Models;
using System.Text;

namespace Curio.Services
{
    public static class ImportService
    {
        public const long MaxSingleFileSizeBytes = 50 * 1024 * 1024; // 50 MB
        public const long MaxZipTotalExtractSizeBytes = 100 * 1024 * 1024; // 100 MB

        private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".msi", ".bat", ".cmd", ".ps1", ".vbs", ".vbe", ".js", ".jse",
            ".wsf", ".wsh", ".msc", ".scr", ".cpl", ".com", ".pif", ".reg", ".dll",
            ".sys", ".drv", ".csh", ".ksh", ".bash", ".sh"
        };

        private static readonly HashSet<string> ValidCursorExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cur", ".ani"
        };

        private static readonly HashSet<string> ValidZipContentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cur", ".ani", ".inf"
        };

        public static string GetDownloadsDirectory()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Curio", "Downloads");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public static string GetTempExtractDirectory()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Curio", "TempExtract");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public static bool IsExecutableExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension)) return false;
            string ext = extension.StartsWith(".") ? extension : "." + extension;
            return ExecutableExtensions.Contains(ext);
        }

        public static ImportResult ProcessImport(IEnumerable<string> fileOrFolderPaths)
        {
            var result = new ImportResult();
            var processedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (fileOrFolderPaths == null) return result;

            foreach (var inputPath in fileOrFolderPaths)
            {
                if (string.IsNullOrWhiteSpace(inputPath)) continue;

                try
                {
                    if (Directory.Exists(inputPath))
                    {
                        ProcessDirectory(inputPath, result, processedPaths);
                    }
                    else if (File.Exists(inputPath))
                    {
                        ProcessFile(inputPath, result, processedPaths);
                    }
                    else
                    {
                        result.LogMessages.Add($"[警告] パスが見つかりません: {inputPath}");
                    }
                }
                catch (Exception ex)
                {
                    result.LogMessages.Add($"[エラー] インポート失敗 '{inputPath}': {ex.Message}");
                    result.ErrorCount++;
                }
            }

            return result;
        }

        private static void ProcessDirectory(string dirPath, ImportResult result, HashSet<string> processedPaths)
        {
            result.LogMessages.Add($"[情報] フォルダをスキャン中: {dirPath}");

            try
            {
                foreach (var file in Directory.EnumerateFiles(dirPath, "*.*", SearchOption.AllDirectories))
                {
                    ProcessFile(file, result, processedPaths);
                }
            }
            catch (Exception ex)
            {
                result.LogMessages.Add($"[エラー] フォルダの読み込み中にエラーが発生しました ({dirPath}): {ex.Message}");
                result.ErrorCount++;
            }
        }

        private static void ProcessFile(string filePath, ImportResult result, HashSet<string> processedPaths)
        {
            if (processedPaths.Contains(filePath)) return;

            string extension = Path.GetExtension(filePath).ToLowerInvariant();

            // 1. Security Check: Executables
            if (IsExecutableExtension(extension))
            {
                result.LogMessages.Add($"[セキュリティ除外] 実行可能ファイルをブロックしました: {Path.GetFileName(filePath)}");
                result.SkippedExecutablesCount++;
                return;
            }

            // 2. Security Check: File Size Limit
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MaxSingleFileSizeBytes)
            {
                result.LogMessages.Add($"[エラー] ファイルサイズが上限 (50MB) を超えています: {Path.GetFileName(filePath)} ({fileInfo.Length / 1024 / 1024}MB)");
                result.ErrorCount++;
                return;
            }

            // 3. Process Zip Archives
            if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ProcessZipArchive(filePath, result, processedPaths);
                return;
            }

            // 4. Process .cur / .ani Cursors
            if (ValidCursorExtensions.Contains(extension))
            {
                try
                {
                    var preview = CursorPreviewRenderer.CreatePreview(filePath);
                    var item = new CursorFileInfo
                    {
                        FilePath = filePath,
                        FileName = fileInfo.Name,
                        Extension = extension,
                        Preview = preview
                    };

                    result.ImportedFiles.Add(item);
                    processedPaths.Add(filePath);
                    result.LogMessages.Add($"[成功] カーソル検出: {fileInfo.Name}");
                }
                catch (Exception ex)
                {
                    result.LogMessages.Add($"[エラー] カーソル読み込み失敗 '{fileInfo.Name}': {ex.Message}");
                    result.ErrorCount++;
                }
            }
        }

        private static void ProcessZipArchive(string zipFilePath, ImportResult result, HashSet<string> processedPaths)
        {
            result.LogMessages.Add($"[情報] ZIPアーカイブを展開解読中: {Path.GetFileName(zipFilePath)}");

            string targetExtractDir = Path.Combine(GetTempExtractDirectory(), Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(targetExtractDir);
                string canonicalTargetDir = Path.GetFullPath(targetExtractDir);
                if (!canonicalTargetDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    canonicalTargetDir += Path.DirectorySeparatorChar;
                }

                long totalExtractedBytes = 0;

using (var archive = ZipFile.Open(
    zipFilePath,
    ZipArchiveMode.Read,
    Encoding.GetEncoding(932)))
{
{
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue; // Directory entry

                        string entryExt = Path.GetExtension(entry.Name).ToLowerInvariant();

                        // Executable check inside ZIP
                        if (IsExecutableExtension(entryExt))
                        {
                            result.LogMessages.Add($"[セキュリティ除外] ZIP内の実行可能ファイルをスキップしました: {entry.FullName}");
                            result.SkippedExecutablesCount++;
                            continue;
                        }

                        // Zip Slip Protection
                        string destinationPath = Path.GetFullPath(Path.Combine(targetExtractDir, entry.FullName));
                        if (!destinationPath.StartsWith(canonicalTargetDir, StringComparison.OrdinalIgnoreCase))
                        {
                            result.LogMessages.Add($"[セキュリティ警告] Zip Slip攻撃パターンを検出・ブロックしました: {entry.FullName}");
                            result.ErrorCount++;
                            continue;
                        }

                        // Filter for valid cursor or INF files
                        if (!ValidZipContentExtensions.Contains(entryExt))
                        {
                            continue;
                        }

                        // File size bomb check
                        totalExtractedBytes += entry.Length;
                        if (totalExtractedBytes > MaxZipTotalExtractSizeBytes)
                        {
                            result.LogMessages.Add($"[エラー] ZIPの解凍上限サイズ (100MB) を超過したため中断しました: {Path.GetFileName(zipFilePath)}");
                            result.ErrorCount++;
                            break;
                        }

                        string? destinationDir = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                        {
                            Directory.CreateDirectory(destinationDir);
                        }

                        entry.ExtractToFile(destinationPath, overwrite: true);

                        // If it's a cursor file (.cur / .ani), process as imported cursor
                        if (ValidCursorExtensions.Contains(entryExt))
                        {
                            ProcessFile(destinationPath, result, processedPaths);
                        }
                        else
                        {
                            result.LogMessages.Add($"[情報] ZIP内構成ファイルを展開: {entry.Name}");
                        }
                    }
                }

                result.LogMessages.Add($"[完了] ZIPの展開が完了しました: {Path.GetFileName(zipFilePath)}");
            }}}
            catch (Exception ex)
            {
                result.LogMessages.Add($"[エラー] ZIPファイルの展開・処理に失敗しました ({Path.GetFileName(zipFilePath)}): {ex.Message}");
                result.ErrorCount++;
            }
        }

        public static void CleanTempExtracts()
        {
            try
            {
                string tempDir = GetTempExtractDirectory();
                if (Directory.Exists(tempDir))
                {
                    foreach (var dir in Directory.GetDirectories(tempDir))
                    {
                        try { Directory.Delete(dir, true); } catch { }
                    }
                }
            }
            catch { }
        }
    }
}
