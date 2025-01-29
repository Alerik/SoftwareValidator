using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using System.Text.Json;
using CodyLib;

namespace SoftwareValidator
{
	public class Program
	{
		const int BENCHMARK_COUNT = 10;
		static long totalBytes = 0;

		public class HashInfo
		{
			public string Path { get; set; }
			public string Hash { get; set; }
			public DateTime ModifyTime { get; set; }

			public HashInfo()
			{
				Path = "";
				Hash = "";
			}
		}

		// I think I'm at the limits of performance out of C#
		// I'm achieving nearly 1Gb/s on my SSD with Release code
		// Half of the spec according to Amazon
		public static async Task<string> ComputeFileHash3(FileStream fileStream)
		{
			using SHA256 sha = SHA256.Create();
			byte[] checksum = await sha.ComputeHashAsync(fileStream);
			string hash = BitConverter.ToString(checksum).Replace("-", string.Empty);

			return hash;
		}

		public static async Task<FileIndex<string>?> LoadMasterIndex(string masterDir, bool useCache = true)
		{
			FileIndex<string> masterIndex = null;

			try
			{
				if (useCache)
					masterIndex = FileIndex<string>.LoadIndex(MASTER_INDEX_PATH, ComputeFileHash3);
				else
					throw new FileNotFoundException();

				Console.WriteLine("Found cached Master index");
			}
			catch (Exception e) when (e is FileNotFoundException || e is JsonException)
			{
				masterIndex = new FileIndex<string>(masterDir, ComputeFileHash3);
				await masterIndex.CreateIndex();

				if (useCache)
				{
					masterIndex.SaveIndex(MASTER_INDEX_PATH);
					Console.WriteLine("Cached Master index");
				}
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("Encountered unexpected exception");
				Console.WriteLine(e);
				Console.ReadKey();
			}

			return masterIndex;
		}

		public static async Task<FileIndex<string>?> LoadCurrentIndex(string currentDir, bool useCache = true)
		{
			FileIndex<string> currentIndex = null;

			try
			{
				if (useCache)
					currentIndex = FileIndex<string>.LoadIndex(CURRENT_INDEX_PATH, ComputeFileHash3);
				else
					throw new FileNotFoundException();

				Console.WriteLine("Found cached Current index");
			}
			catch (Exception e) when (e is FileNotFoundException || e is JsonException)
			{
				currentIndex = new FileIndex<string>(currentDir, ComputeFileHash3);
				await currentIndex.CreateIndex();
			}
			catch (Exception e)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("Encountered unexpected exception");
				Console.WriteLine(e);
				Console.ReadKey();
			}

			if (currentIndex != null)
			{
				int updates = await currentIndex.UpdateIndex();

				if (useCache)
				{
					if (updates > 0)
					{
						currentIndex.SaveIndex(CURRENT_INDEX_PATH);
						Console.WriteLine("Updated Current index");
					}
					else if (!File.Exists(CURRENT_INDEX_PATH))
					{
						currentIndex.SaveIndex(CURRENT_INDEX_PATH);
						Console.WriteLine("Cached Current index");
					}
				}
			}

			return currentIndex;
		}
 
		public static string FormatBytes(double bytes) 
		{
			double tb, gb, mb, kb;

			if ((tb = bytes / 1099511627776.00) >= 1)
				return $"{tb:F2}TB";	
			else if ((gb = bytes / 1073741824.00) >= 1)
				return $"{gb:F2}GB";	
			else if ((mb = bytes / 1048576.0) >= 1)
				return $"{mb:F2}MB";	
			else if ((kb = bytes / 1024.0) >= 1)
				return $"{kb:F2}KB";	
				else
			return $"{bytes}B";	
		}

		static async Task<int> Benchmark_Driver(string masterDir, string currentDir)
		{
			Console.WriteLine($"Running {BENCHMARK_COUNT} benchmark passes");
			DateTime start = DateTime.Now;

			for (int i = 0; i < BENCHMARK_COUNT; i++)
			{
				Console.WriteLine($"\t{i+1}/{BENCHMARK_COUNT}");
				Task<FileIndex<string>?> tskMasterIndex = LoadMasterIndex(masterDir, false);
				Task<FileIndex<string>?> tskCurrentIndex = LoadCurrentIndex(currentDir, false);

				await Task.WhenAll(tskMasterIndex, tskCurrentIndex);

				if (tskCurrentIndex.Result is null)
				{
					return 1;
				}

				if (tskMasterIndex.Result is null)
				{

					return 1;
				}
			}

			DateTime end = DateTime.Now;

			double totalTime = (end - start).TotalSeconds;
			Console.WriteLine($"Validated {FormatBytes(totalBytes)} in {totalTime:F2}s");

			double averageTime = totalTime / BENCHMARK_COUNT; ;
			string rate = FormatBytes(totalBytes / totalTime);

			Console.WriteLine($"Average cycle time {averageTime:F2}s @ {rate}/s");

			Console.WriteLine();
			Console.WriteLine("Press the any key to continue...");
			Console.ReadKey();

			return 0;
		}

		static async Task Main_Driver(string masterDir, string currentDir, bool force = false)
		{
			Console.ForegroundColor = ConsoleColor.White;
			Console.WriteLine($"Comparing {currentDir} to {masterDir}");
			DateTime start = DateTime.Now;

			Task<FileIndex<string>?> tskMasterIndex = LoadMasterIndex(masterDir);
			Task<FileIndex<string>?> tskCurrentIndex = LoadCurrentIndex(currentDir);

			await Task.WhenAll(tskMasterIndex, tskCurrentIndex);


			if (tskCurrentIndex.Result is null)
				return;

			if (tskMasterIndex.Result is null)
				return;

			int errors = FileIndex<string>.CorrectErrors(tskMasterIndex.Result, tskCurrentIndex.Result, force);

			DateTime end = DateTime.Now;
			double totalTime = (end - start).TotalSeconds;

			if (errors == 0)
			{
				Console.ForegroundColor = ConsoleColor.Green;
				if(totalBytes > 0)
					Console.WriteLine($"Validated {FormatBytes(totalBytes)} in {totalTime:F2}s");
				else
					Console.WriteLine($"Validated Current in {totalTime:F2}s");
				Console.ForegroundColor = ConsoleColor.White;
			}
			else
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine($"Found and fixed {errors} errors in {totalTime:F2}s");
				Console.ForegroundColor = ConsoleColor.White;
			}

			Console.WriteLine();
			Console.WriteLine("Press the any key to continue...");
			Console.ReadKey();
		}

		static void WriteHelp()
		{
		string help = 
"""
Usage: SoftwareValidator.exe masterDirectory currentDirectory [OPTIONS]

Arguments:
  masterDirectory            The path to the master directory containing the reference files or base data.
  currentDirectory           The path to the current directory to be validated against the master directory.

Options:
  -b, --benchmark            Run in benchmark mode. Enables repeated performance benchmarking for average process cycle time. Files ARE NOT changed
  -f, --force                Run in force fire mode. Overwrites corrupted files with master files. Files ARE changes

Examples:
  SoftwareValidator.exe /path/to/master /path/to/current
  SoftwareValidator.exe /path/to/master /path/to/current -b
  SoftwareValidator.exe /path/to/master /path/to/current -d

Description:
  SoftwareValidator.exe is a command-line utility designed to validate and correct files between two directories: the masterDirectory and the currentDirectory. The tool performs a hash-based validation and inteligently copies corrupted files from the master directory to current directory.

Exit Codes:
  0   Successful execution.
  1   Error encountered during execution.
  2   Invalid arguments or missing required parameters.
""";

			Console.WriteLine(help);
		}

		static void WriteIncorrectArgCount()
		{
//			string help =
//"""
//Error: Incorrect number of arguments supplied.

//Usage: SoftwareValidator.exe masterDirectory currentDirectory [OPTIONS]

//  masterDirectory            The path to the master directory containing the reference files or base data.
//  currentDirectory           The path to the current directory to be validated against the master directory.

//For more information, use the --help flag.
//""";

//			Console.WriteLine(help);
		}

		static void WriteInvalidDirectory(string directoryName, string directoryPath)
		{
//			string help = 
//$"""
//Error: Invalid {directoryName} directory specified [{directoryPath}].

//The directory 'path/to/directory' does not exist or is not accessible.

//Please verify that the directory path is correct and try again.
//""";

//			Console.WriteLine(help);
		}

		static void WriteInvalidFlag(string flag)
		{

//			string help =
//$"""
//Error: Invalid flag '{flag}' provided.

//The flag '-invalidFlag' is not recognized.

//Usage: SoftwareValidator.exe masterDirectory currentDirectory [OPTIONS]

//Valid flags:
//  -b, --benchmark            Run in benchmark mode.
//  -f, --force                Force file overwrites.

//For more information, use the --help flag.

//""";

//			Console.WriteLine(help);
		}

		private static readonly string CURRENT_INDEX_PATH = @"C:\Users\Jackson\Source\Repos\SoftwareValidator\TEST\index.json";
		private static readonly string MASTER_INDEX_PATH = @"C:\Users\Jackson\Source\Repos\SoftwareValidator\TEST\_index.json";

		static async Task<int> Main(string[] args)
		{
			if (args.Length == 1)
			{
				if (args[0] == "--help" || args[0] == "-h" || args[0] == "/?" || args[0] == "/h")
				{
					WriteHelp();
					return 0;
				}
				return 1;
			}
			if (args.Length < 2 || args.Length > 3)
			{
				WriteIncorrectArgCount();
				return 1;
			}

			string masterPath = args[0];
			string currentPath = args[1];

			if (!Directory.Exists(masterPath))
			{
				WriteInvalidDirectory("MASTER", masterPath);
				return 2;
			}

			if (!Directory.Exists(currentPath))
			{
				WriteInvalidDirectory("CURRENT", currentPath);
				return 2;
			}

			string masterDir = args[0];
			string currentDir = args[1];

			bool force = false;
			bool benchmark = false;

			if (args.Length == 3)
			{
				switch (args[2])
				{
					case "-b":
					case "--benchmark":
					case "/b":
					case "/benchmark":
						benchmark = true;
						break;
					case "-f":
					case "--force":
					case "/f":
					case "/force":
						force = true;
						break;

					default:
						WriteInvalidFlag(args[2]);
						return 2;
				}
			}

			if (benchmark)
			{
				await Benchmark_Driver(masterDir, currentDir);
				return 0;
			}
			else
			{
				await Main_Driver(masterDir, currentDir, force);
				return 0;
			}
		}
	}
}
