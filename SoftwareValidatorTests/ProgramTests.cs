using System.Collections.Concurrent;

namespace SoftwareValidator.Tests
{
	[TestClass()]
	public class ProgramTests
	{
		const string HASH_DATA = "F439nmf90dan4(W''FJDSIOFNSOIFNEIONBOAP#@(#";
		const string EXPECTED_HASH = "FC828ADC2998E567CD1D94E0F76A2F4BA3C26DAF9818DD120F8BD8911AC89CF0";

		[TestMethod()]
		public async Task ComputeFileHash3Test()
		{
			string filePath = Path.GetTempFileName();

			string? relPath = Path.GetDirectoryName(filePath);
			ArgumentNullException.ThrowIfNull(relPath);

			string fileName = Path.GetFileName(filePath);

			File.WriteAllText(filePath, HASH_DATA);
			ConcurrentDictionary<string, Program.HashInfo> fileIndex = [];

			await Program.ComputeFileHash3(filePath, relPath, fileIndex);

			Assert.IsTrue(fileIndex.TryGetValue(fileName, out Program.HashInfo? hashInfo));
			Assert.IsNotNull(hashInfo);
			Assert.AreEqual(fileName, hashInfo.Path);
			Assert.AreEqual(EXPECTED_HASH, hashInfo.Hash);
		}

		[TestMethod()]
		public void EnumerateAllDirectoriesTest()
		{
			List<string> directories = [];

			string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName().Replace(".", ""));
			string currentDirectory = root;
			Directory.CreateDirectory(currentDirectory);
			directories.Add(currentDirectory);

			for (int i = 0; i < 7; i++)
			{
				currentDirectory = Path.Combine(currentDirectory, Path.GetRandomFileName().Replace(".", ""));
				Directory.CreateDirectory(currentDirectory);
				directories.Add(currentDirectory);
			}

			for (int i = 0; i < 10; i++)
			{
				string subDir = Path.Combine(currentDirectory, Path.GetRandomFileName().Replace(".", ""));
				Directory.CreateDirectory(subDir);
				directories.Add(subDir);
			}

			List<string> dirs = Program.EnumerateAllDirectories(root).ToList();

			foreach (string dir in dirs)
			{
				Assert.IsTrue(directories.Remove(dir));
			}

			Assert.AreEqual(0, directories.Count);
		}

		[TestMethod()]
		public async Task CreateDirectoryIndexTest()
		{
			List<string> files = [];
			string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName().Replace(".", ""));
			Directory.CreateDirectory(root);

			for (int i = 0; i < 10; i++)
			{
				string file = Path.Combine(root, Path.GetRandomFileName());
				File.WriteAllText(file, HASH_DATA);
				files.Add(file);
			}

			ConcurrentDictionary<string, Program.HashInfo> returnValue = await Program.CreateDirectoryIndex(root);

			foreach (string file in files)
			{
				string relPath = Path.GetRelativePath(root, file);
				Assert.IsTrue(returnValue.TryGetValue(relPath, out Program.HashInfo? hashInfo));
				Assert.IsNotNull(hashInfo);
				Assert.AreEqual(relPath, hashInfo.Path);
				Assert.AreEqual(EXPECTED_HASH, hashInfo.Hash);
			}
		}

		[TestMethod()]
		public async Task UpdateDirectoryIndexTest()
		{
			List<string> ogFiles = [];
			List<string> newFiles = [];
			string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName().Replace(".", ""));
			Directory.CreateDirectory(root);

			for (int i = 0; i < 10; i++)
			{
				string file = Path.Combine(root, Path.GetRandomFileName());
				File.WriteAllText(file, HASH_DATA);
				ogFiles.Add(file);
			}

			ConcurrentDictionary<string, Program.HashInfo> fileIndex = await Program.CreateDirectoryIndex(root);

			for (int i = 0; i < 10; i++)
			{
				string file = Path.Combine(root, Path.GetRandomFileName());
				File.WriteAllText(file, HASH_DATA);
				newFiles.Add(file);
			}

			DateTime maxTime = DateTime.UtcNow;
			int changes = await Program.UpdateDirectoryIndex(fileIndex, root);

			Assert.AreEqual(10, changes);


			foreach (string file in newFiles)
			{
				string relPath = Path.GetRelativePath(root, file);
				Assert.IsTrue(fileIndex.TryGetValue(relPath, out Program.HashInfo? hashInfo));
				Assert.IsNotNull(hashInfo);
				Assert.AreEqual(relPath, hashInfo.Path);
				Assert.AreEqual(EXPECTED_HASH, hashInfo.Hash);
			}
			foreach (string file in ogFiles)
			{
				string relPath = Path.GetRelativePath(root, file);
				Assert.IsTrue(fileIndex.TryGetValue(relPath, out Program.HashInfo? hashInfo));
				Assert.IsNotNull(hashInfo);
				Assert.AreEqual(relPath, hashInfo.Path);
				Assert.AreEqual(EXPECTED_HASH, hashInfo.Hash);
				Assert.IsTrue(maxTime > hashInfo.ModifyTime);
			}


		}

		[TestMethod()]
		public void SaveIndexTest()
		{
			throw new NotImplementedException();
		}

		[TestMethod()]
		public void LoadIndexTest()
		{
			throw new NotImplementedException();
		}

		[TestMethod()]
		public void LoadMasterIndexTest()
		{
			throw new NotImplementedException();
		}

		[TestMethod()]
		public void LoadCurrentIndexTest()
		{
			throw new NotImplementedException();
		}

		[TestMethod()]
		public void CorrectErrorsTest()
		{
			throw new NotImplementedException();
		}

		[DataTestMethod()]
		[DataRow(1099511627776, "1.00TB")]
		[DataRow(1099511627775, "1024.00GB")]
		[DataRow(1073741824, "1.00GB")]
		[DataRow(1073741823, "1024.00MB")]
		[DataRow(1048576, "1.00MB")]
		[DataRow(1048575, "1024.00KB")]
		[DataRow(1024, "1.00KB")]
		[DataRow(1023, "1023B")]
		public void FormatBytesTest(double input, string expected)
		{
			string formatted = Program.FormatBytes(input);

			Assert.AreEqual(expected, formatted);
		}
	}
}