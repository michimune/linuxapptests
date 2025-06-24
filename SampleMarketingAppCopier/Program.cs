using System;
using System.IO;
using System.Linq;

namespace SampleMarketingAppCopier
{
    class Program
    {
        static int Main(string[] args)
        {
            // Check if command line parameter is provided
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Error: SampleMarketingApp directory path not specified.");
                Console.Error.WriteLine("Usage: SampleMarketingAppCopier <SampleMarketingApp directory>");
                return 1;
            }

            string sourceDirectory = args[0];

            // Validate source directory exists
            if (!Directory.Exists(sourceDirectory))
            {
                Console.Error.WriteLine($"Error: Source directory '{sourceDirectory}' does not exist.");
                return 1;
            }            // Get the parent directory of the source directory
            string? parentDirectory = Directory.GetParent(sourceDirectory)?.FullName;
            if (parentDirectory == null)
            {
                Console.Error.WriteLine("Error: Unable to determine parent directory.");
                return 1;
            }

            string targetDirectory = Path.Combine(parentDirectory, "SampleMarketingAppBad");

            try
            {
                // Remove target directory if it already exists
                if (Directory.Exists(targetDirectory))
                {
                    Console.WriteLine($"Target directory '{targetDirectory}' already exists. Removing...");
                    Directory.Delete(targetDirectory, true);
                }

                // Copy the entire directory
                Console.WriteLine($"Copying '{sourceDirectory}' to '{targetDirectory}'...");
                CopyDirectory(sourceDirectory, targetDirectory);
                Console.WriteLine("Directory copied successfully.");

                // Update requirements.txt by removing the first line
                string requirementsPath = Path.Combine(targetDirectory, "requirements.txt");
                if (File.Exists(requirementsPath))
                {
                    Console.WriteLine("Updating requirements.txt...");
                    UpdateRequirementsFile(requirementsPath);
                    Console.WriteLine("requirements.txt updated successfully.");
                }
                else
                {
                    Console.WriteLine("Warning: requirements.txt not found in the copied directory.");
                }

                Console.WriteLine("Operation completed successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        static void CopyDirectory(string sourceDir, string targetDir)
        {
            // Create target directory
            Directory.CreateDirectory(targetDir);

            // Copy all files
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string targetFile = Path.Combine(targetDir, fileName);
                File.Copy(file, targetFile, true);
            }

            // Recursively copy subdirectories
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(subDir);
                string targetSubDir = Path.Combine(targetDir, dirName);
                CopyDirectory(subDir, targetSubDir);
            }
        }

        static void UpdateRequirementsFile(string filePath)
        {
            // Read all lines from the file
            string[] lines = File.ReadAllLines(filePath);

            // Skip the first line if file is not empty
            if (lines.Length > 0)
            {
                string[] updatedLines = lines.Skip(1).ToArray();
                File.WriteAllLines(filePath, updatedLines);
            }
        }
    }
}
