using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Curio.Models;

namespace Curio.Services
{
    public class ScanResult
    {
        public List<CursorFileInfo> AllFiles { get; } = new();
        public Dictionary<string, List<CursorFileInfo>> GroupedByFolder { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> LogMessages { get; } = new();
        public int ErrorCount { get; set; }
    }

    public static class CursorScanner
    {
        public static ScanResult ScanDirectory(string rootPath, bool includeSubfolders)
        {
            var result = new ScanResult();

            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                result.LogMessages.Add($"[エラー] フォルダが存在しません: {rootPath}");
                result.ErrorCount++;
                return result;
            }

            var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            try
            {
                result.LogMessages.Add($"[情報] フォルダのスキャンを開始: {rootPath} (サブフォルダ含む: {includeSubfolders})");

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(rootPath, "*.*", searchOption)
                                     .Where(f => f.EndsWith(".cur", StringComparison.OrdinalIgnoreCase) ||
                                                 f.EndsWith(".ani", StringComparison.OrdinalIgnoreCase));
                }
                catch (Exception ex)
                {
                    result.LogMessages.Add($"[警告] フォルダの列挙中にエラーが発生しました ({ex.Message})。個別に検索します。");
                    files = EnumerateFilesSafe(rootPath, searchOption, result);
                }

                int foundCount = 0;
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        string relativePath = Path.GetRelativePath(rootPath, file);
                        string folderKey = Path.GetDirectoryName(relativePath) ?? string.Empty;
                        if (string.IsNullOrEmpty(folderKey))
                        {
                            folderKey = Path.GetFileName(rootPath) ?? "ルート";
                        }

                        var preview = CursorPreviewRenderer.CreatePreview(file);

                        var item = new CursorFileInfo
                        {
                            FilePath = file,
                            RelativePath = relativePath,
                            FileName = fileInfo.Name,
                            Extension = fileInfo.Extension.ToLowerInvariant(),
                            Preview = preview
                        };

                        result.AllFiles.Add(item);

                        if (!result.GroupedByFolder.ContainsKey(folderKey))
                        {
                            result.GroupedByFolder[folderKey] = new List<CursorFileInfo>();
                        }
                        result.GroupedByFolder[folderKey].Add(item);

                        foundCount++;
                    }
                    catch (Exception ex)
                    {
                        result.LogMessages.Add($"[エラー] ファイルの読み込みスキップ: {file} ({ex.Message})");
                        result.ErrorCount++;
                    }
                }

                result.LogMessages.Add($"[完了] スキャン終了: 計 {foundCount} 個のカーソルファイル (.cur / .ani) を検出しました。");
            }
            catch (Exception ex)
            {
                result.LogMessages.Add($"[致命的エラー] スキャン処理が中断されました: {ex.Message}");
                result.ErrorCount++;
            }

            return result;
        }

        private static IEnumerable<string> EnumerateFilesSafe(string path, SearchOption option, ScanResult result)
        {
            var files = new List<string>();

            try
            {
                foreach (var file in Directory.GetFiles(path))
                {
                    if (file.EndsWith(".cur", StringComparison.OrdinalIgnoreCase) ||
                        file.EndsWith(".ani", StringComparison.OrdinalIgnoreCase))
                    {
                        files.Add(file);
                    }
                }
            }
            catch (Exception ex)
            {
                result.LogMessages.Add($"[警告] アクセス不可フォルダをスキップ: {path} ({ex.Message})");
                result.ErrorCount++;
            }

            if (option == SearchOption.AllDirectories)
            {
                try
                {
                    foreach (var dir in Directory.GetDirectories(path))
                    {
                        files.AddRange(EnumerateFilesSafe(dir, option, result));
                    }
                }
                catch (Exception ex)
                {
                    result.LogMessages.Add($"[警告] サブフォルダアクセス不可: {path} ({ex.Message})");
                    result.ErrorCount++;
                }
            }

            return files;
        }
    }
}
