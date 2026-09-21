using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Curio.Models;
using Curio.Services;

namespace Curio.Test
{
    public static class TestRunner
    {
        public static void RunTests()
        {
            Console.WriteLine("=== Curio Functional Verification ===");

            string tempDir = Path.Combine(Path.GetTempPath(), "Curio_Test_" + Guid.NewGuid().ToString("N"));
            string customStorageDir = Path.Combine(Path.GetTempPath(), "Curio_Storage_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(customStorageDir);

            try
            {
                // 1. Create dummy cursor files
                string arrowCur = Path.Combine(tempDir, "arrow.cur");
                string helpAni = Path.Combine(tempDir, "help_select.ani");
                string busyCur = Path.Combine(tempDir, "busy_waiting.cur");
                string textBeam = Path.Combine(tempDir, "text_ibeam.cur");

                byte[] dummyCur = CreateDummyCursorBytes();
                File.WriteAllBytes(arrowCur, dummyCur);
                File.WriteAllBytes(helpAni, dummyCur);
                File.WriteAllBytes(busyCur, dummyCur);
                File.WriteAllBytes(textBeam, dummyCur);

                Console.WriteLine($"[1] Created test cursor files in {tempDir}");

                // 2. Test Scanner
                var scanResult = CursorScanner.ScanDirectory(tempDir, true);
                Console.WriteLine($"[2] Scanner result: {scanResult.AllFiles.Count} files found, {scanResult.ErrorCount} errors.");
                if (scanResult.AllFiles.Count != 4)
                {
                    throw new Exception($"Expected 4 files, got {scanResult.AllFiles.Count}");
                }

                // 3. Test Matcher
                var mappings = CursorMatcher.MatchRoles(scanResult.AllFiles);
                int assignedCount = mappings.Count(m => m.IsAssigned);
                Console.WriteLine($"[3] Matcher result: {assignedCount} roles auto-assigned.");

                var arrowMapping = mappings.FirstOrDefault(m => m.Role.RegistryKey == "Arrow");
                var helpMapping = mappings.FirstOrDefault(m => m.Role.RegistryKey == "Help");
                var waitMapping = mappings.FirstOrDefault(m => m.Role.RegistryKey == "Wait");
                var ibeamMapping = mappings.FirstOrDefault(m => m.Role.RegistryKey == "IBeam");

                Console.WriteLine($"  - Arrow -> {arrowMapping?.SelectedFile?.FileName}");
                Console.WriteLine($"  - Help -> {helpMapping?.SelectedFile?.FileName}");
                Console.WriteLine($"  - Wait -> {waitMapping?.SelectedFile?.FileName}");
                Console.WriteLine($"  - IBeam -> {ibeamMapping?.SelectedFile?.FileName}");

                if (arrowMapping?.SelectedFile == null || helpMapping?.SelectedFile == null)
                {
                    throw new Exception("Auto-matching failed for standard keywords!");
                }

                // 4. Test Custom Storage Directory Setting
                Console.WriteLine($"[4] Testing custom storage directory setting: {customStorageDir}");
                RegistrySchemeManager.StorageDirectory = customStorageDir;
                if (!RegistrySchemeManager.StorageDirectory.Equals(customStorageDir, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("Setting StorageDirectory failed!");
                }

                // 5. Test Scheme Installation & Registry Management
                string testSchemeName = "UnitTest_CurioScheme_" + DateTime.Now.Ticks;
                Console.WriteLine($"[5] Installing test scheme '{testSchemeName}'...");

                var installResult = RegistrySchemeManager.InstallScheme(testSchemeName, mappings, applyImmediately: false);
                Console.WriteLine($"  - Install success: {installResult.Success}, target dir: {installResult.TargetDirectory}");

                if (!installResult.Success)
                {
                    throw new Exception("Scheme installation failed!");
                }

                if (!installResult.TargetDirectory.StartsWith(customStorageDir, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("Target directory did not use custom storage path!");
                }

                // Verify scheme exists in registry
                bool exists = RegistrySchemeManager.SchemeExists(testSchemeName);
                Console.WriteLine($"  - SchemeExists check: {exists}");
                if (!exists)
                {
                    throw new Exception("SchemeExists returned false after installation!");
                }

                // Verify installed schemes list
                var installedList = RegistrySchemeManager.GetInstalledSchemes();
                var registeredTestScheme = installedList.FirstOrDefault(s => s.Name.Equals(testSchemeName, StringComparison.OrdinalIgnoreCase));
                Console.WriteLine($"  - Registered scheme found in list: {registeredTestScheme != null}, isManagedByApp: {registeredTestScheme?.IsManagedByApp}");

                if (registeredTestScheme == null || !registeredTestScheme.IsManagedByApp)
                {
                    throw new Exception("Registered scheme was not properly recognized as managed!");
                }

                // 6. Test Unified ImportService (Zip, Executable Filtering, Zip Slip Protection)
                Console.WriteLine("[6] Testing Unified ImportService & Security Protections...");
                TestImportService(tempDir, dummyCur);

                // 7. Test AppSettings Persistence & StyleManager
                Console.WriteLine("[7] Testing AppSettings Persistence & UIStyle / Theme Switching...");
                TestSettingsAndStyleManager();

                // 8. Test Scheme Cleanup / Uninstall
                Console.WriteLine($"[8] Deleting test scheme '{testSchemeName}'...");
                var deleteLogs = new System.Collections.Generic.List<string>();
                bool deleted = RegistrySchemeManager.DeleteScheme(testSchemeName, deleteLogs);
                Console.WriteLine($"  - Delete success: {deleted}");

                bool existsAfterDelete = RegistrySchemeManager.SchemeExists(testSchemeName);
                Console.WriteLine($"  - SchemeExists after delete: {existsAfterDelete}");
                if (existsAfterDelete)
                {
                    throw new Exception("Scheme still exists in registry after delete!");
                }

                Console.WriteLine("=== All Curio Verification Tests PASSED! ===");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
                if (Directory.Exists(customStorageDir))
                {
                    Directory.Delete(customStorageDir, true);
                }
                RegistrySchemeManager.StorageDirectory = RegistrySchemeManager.GetDefaultStorageDirectory();
            }
        }

        private static void TestSettingsAndStyleManager()
        {
            var settings = AppSettings.Load();
            string originalStyle = settings.UIStyle;
            string originalTheme = settings.Theme;

            try
            {
                // Test saving Modern style & Dark theme
                settings.UIStyle = "Modern";
                settings.Theme = "Dark";
                settings.Save();

                var reloaded = AppSettings.Load();
                if (!reloaded.UIStyle.Equals("Modern", StringComparison.OrdinalIgnoreCase) ||
                    !reloaded.Theme.Equals("Dark", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("AppSettings failed to persist Modern style or Dark theme!");
                }

                // Test saving XP style & Light theme
                settings.UIStyle = "XP";
                settings.Theme = "Light";
                settings.Save();

                var reloadedXP = AppSettings.Load();
                if (!reloadedXP.UIStyle.Equals("XP", StringComparison.OrdinalIgnoreCase) ||
                    !reloadedXP.Theme.Equals("Light", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("AppSettings failed to persist XP style or Light theme!");
                }

                Console.WriteLine("  - AppSettings settings.json persistence PASSED!");
            }
            finally
            {
                settings.UIStyle = originalStyle;
                settings.Theme = originalTheme;
                settings.Save();
            }
        }

        private static void TestImportService(string tempDir, byte[] dummyCurBytes)
        {
            string zipPath = Path.Combine(tempDir, "test_pack.zip");

            using (var zipStream = new FileStream(zipPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var entryCur = archive.CreateEntry("normal_cursor.cur");
                using (var s = entryCur.Open()) s.Write(dummyCurBytes);

                var entryAni = archive.CreateEntry("subfolder/animated_cursor.ani");
                using (var s = entryAni.Open()) s.Write(dummyCurBytes);

                var entryInf = archive.CreateEntry("install_setup.inf");
                using (var writer = new StreamWriter(entryInf.Open())) writer.WriteLine("; INF file");

                var entryExe = archive.CreateEntry("malicious_virus.exe");
                using (var writer = new StreamWriter(entryExe.Open())) writer.WriteLine("DUMMY EXE CONTENT");

                var entryBat = archive.CreateEntry("dangerous_script.bat");
                using (var writer = new StreamWriter(entryBat.Open())) writer.WriteLine("DUMMY BAT CONTENT");
            }

            Console.WriteLine("  - Created test zip package with valid cursors and forbidden executables.");

            var importResult = ImportService.ProcessImport(new[] { zipPath });

            Console.WriteLine($"  - ImportResult: {importResult.ImportedFiles.Count} cursors imported, {importResult.SkippedExecutablesCount} executables blocked.");

            if (importResult.SkippedExecutablesCount < 2)
            {
                throw new Exception($"Executable filtering failed! Expected at least 2 blocked, got {importResult.SkippedExecutablesCount}");
            }

            if (importResult.ImportedFiles.Count < 2)
            {
                throw new Exception($"Zip cursor extraction failed! Expected 2 cursors, got {importResult.ImportedFiles.Count}");
            }

            if (importResult.ImportedFiles.Any(f => f.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) || f.Extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)))
            {
                throw new Exception("SECURITY FAILURE: Executable file was imported into cursor list!");
            }

            ImportService.CleanTempExtracts();
            Console.WriteLine("  - Security checks and Zip extraction PASSED!");
        }

        private static byte[] CreateDummyCursorBytes()
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((short)0); // Reserved
                bw.Write((short)2); // Type: 2 = CUR
                bw.Write((short)1); // Count: 1 image

                bw.Write((byte)32); // Width
                bw.Write((byte)32); // Height
                bw.Write((byte)0);  // ColorCount
                bw.Write((byte)0);  // Reserved
                bw.Write((short)0); // HotspotX
                bw.Write((short)0); // HotspotY
                
                int bmpHeaderAndDataSize = 40 + (32 * 4) + (32 * 4);
                bw.Write(bmpHeaderAndDataSize);
                bw.Write(22);       // ImageOffset

                bw.Write(40);        // biSize
                bw.Write(32);        // biWidth
                bw.Write(64);        // biHeight
                bw.Write((short)1);  // biPlanes
                bw.Write((short)1);  // biBitCount
                bw.Write(0);         // biCompression
                bw.Write(0);         // biSizeImage
                bw.Write(0);         // biXPelsPerMeter
                bw.Write(0);         // biYPelsPerMeter
                bw.Write(0);         // biClrUsed
                bw.Write(0);         // biClrImportant

                bw.Write((int)0x00000000);
                bw.Write((int)0x00FFFFFF);

                bw.Write(new byte[32 * 4]);
                bw.Write(new byte[32 * 4]);

                return ms.ToArray();
            }
        }
    }
}
