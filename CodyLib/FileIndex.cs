using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.Design.Serialization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodyLib
{
	public class FileIndex<T> where T : IEquatable<T>
	{
		private class IndexedData<T>
		{
			[JsonInclude()]
			public string FilePath;

			[JsonInclude()]
			public T Data;

			[JsonInclude()]
			public DateTime UTCModifyTime;
		}

		[JsonInclude()]
		public string Root {get; private set;}

		public delegate Task<T> FileOperation<T>(FileStream fs);

		[JsonIgnore()]
		public IEnumerable<string> Keys
		{
			get
			{
				return Index.Keys;
			}
		}

		[JsonInclude()]
		private ConcurrentDictionary<string, IndexedData<T>> Index;

		[JsonIgnore()]
		private FileOperation<T> FileTask;

		public FileIndex(string _Root, FileOperation<T> _FileTask )
		{
			Root = _Root;
			FileTask = _FileTask;
			Index = new ConcurrentDictionary<string, IndexedData<T>>();
		}

		public FileIndex()
		{
		}

		public bool ContainsKey(string key)
		{
			return Index.ContainsKey(key);
		}

		private IndexedData<T> this[string key]
		{
			get 
			{
				return Index[key];
			}
		}

		public IEnumerable<string> EnumerateDirectories()
		{
			Stack<string> toVisit = new();
			toVisit.Push(Root);

			while (toVisit.Count > 0)
			{
				string currentDirectory = toVisit.Pop();

				foreach (string directory in Directory.GetDirectories(currentDirectory))
					toVisit.Push(directory);

				yield return currentDirectory;
			}
		}

		private async Task VisitFile(string filePath)
		{
			string relativePath = Path.GetRelativePath(Root, filePath);

			FileStream fs = File.OpenRead(filePath);
			Index[relativePath] = new IndexedData<T>
			{
				FilePath = filePath,
				Data = await FileTask(fs),
				UTCModifyTime = DateTime.UtcNow
			};
		}

		private async Task<bool> UpdateFile(string filePath, ConcurrentDictionary<string, bool> visitedPaths)
		{
			string relativePath = Path.GetRelativePath(Root, filePath);
			FileInfo info = new(filePath);
			if (!(Index.ContainsKey(relativePath) && Index[relativePath].UTCModifyTime > info.LastWriteTimeUtc))
			{
				Index[relativePath] = new IndexedData<T>
				{
					FilePath = filePath,
					Data = await FileTask(File.OpenRead(filePath)),
					UTCModifyTime = DateTime.UtcNow
				};

				if (visitedPaths.ContainsKey(relativePath))
					visitedPaths[relativePath] = true;

				return true;
			}
			else
			{
				if (visitedPaths.ContainsKey(relativePath))
					visitedPaths[relativePath] = true;
				return false;
			}
		}

		public async Task<int> CreateIndex()
		{
			int totalFiles = 0;
			List<Task> fileTasks = new();

			foreach (string directoryPath in EnumerateDirectories())
			{
				foreach (string filePath in Directory.EnumerateFiles(directoryPath))
				{
					fileTasks.Add(VisitFile(filePath));
				}
			}

			while (fileTasks.Count > 0)
			{
				Task completedTask = await Task.WhenAny(fileTasks);
				fileTasks.Remove(completedTask);

				await completedTask;
				totalFiles++;
				Console.WriteLine(totalFiles);
			}

			return totalFiles;
		}

		public async Task<int> UpdateIndex()
		{
			int updates = 0;

			ConcurrentDictionary<string, bool> visitedPaths = new();
			foreach (string key in Index.Keys)
				visitedPaths[key] = false;

			List<Task<bool>> fileTasks = [];

			foreach (string directoryPath in EnumerateDirectories())
			{
				foreach (string filePath in Directory.EnumerateFiles(directoryPath))
				{
					fileTasks.Add(UpdateFile(filePath, visitedPaths));
				}
			}

			while (fileTasks.Count > 0)
			{
				Task<bool> completedTask = await Task.WhenAny(fileTasks);
				fileTasks.Remove(completedTask);

				await completedTask;

				if(completedTask.Result)
					updates++;
			}

			foreach (string key in Index.Keys)
			{
				if (visitedPaths.TryGetValue(key, out bool value) && !value)
				{
					Index.Remove(key, out _);
					updates++;
				}
			}

			return updates;
		}

		public void SaveIndex(string savePath)
		{
			string json = JsonSerializer.Serialize(this);
			File.WriteAllText(savePath, json);
		}

		public static FileIndex<T> LoadIndex(string savePath, FileOperation<T> fileTask)
		{
			if (File.Exists(savePath))
			{
				string json = File.ReadAllText(savePath);
				JsonSerializerOptions options = new();
				FileIndex<T>? fileData = JsonSerializer.Deserialize<FileIndex<T>>(json, options);

				if (fileData is null)
					throw new JsonException("Unable to parse JSON");

				fileData.FileTask = fileTask;

				return fileData;
			}
			else
			{
				throw new FileNotFoundException(savePath);
			}
		}

		public static int CorrectErrors<T>(FileIndex<T> master, FileIndex<T> current, bool force = false) where T : IEquatable<T>
		{
			int errorCount = 0;

			foreach (string relativePath in master.Keys)
			{
				if (!(current.ContainsKey(relativePath) && current[relativePath].Data.Equals(master[relativePath].Data)))
				{
					string from = Path.Combine(master.Root, relativePath);
					string to = Path.Combine(current.Root, relativePath);
					Console.WriteLine($"{relativePath} NG");
					Console.WriteLine($"\t{from}->{to}");

					if (force)
						File.Copy(from, to);

					errorCount++;
				}
			}

			return errorCount;
		}
	}
}
