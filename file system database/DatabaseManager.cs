using Microsoft.Data.Sqlite;
using System.Text;

namespace file_system_database {

	public class DatabaseManager {
		private readonly SqliteConnection connection;
		private SqliteCommand FileCommand;
		private SqliteCommand DirCommand;
		private SqliteCommand QueryCommand;

		private SqliteParameter FparamBasename;
		private SqliteParameter FparamPath;
		private SqliteParameter FparamExtension;
		private SqliteParameter FparamSize;
		private SqliteParameter FparamParent;
		private SqliteParameter DparamBasename;
		private SqliteParameter DparamPath;
		private SqliteParameter DparamParent;

		private int maxDepth;
		private int searchDepth = 0;

		public DatabaseManager(string dbPath) {
			connection = new("Data Source=" + dbPath);
			connection.Open();
		}

		void PrepCommands() {
			FileCommand = connection.CreateCommand();
			FileCommand.CommandText = "INSERT INTO files(basename, file_path, extension, size, parent) VALUES($basename, $path, $extension, $size, $parent) ";

			FparamBasename = FileCommand.CreateParameter();
			FparamPath = FileCommand.CreateParameter();
			FparamExtension = FileCommand.CreateParameter();
			FparamSize = FileCommand.CreateParameter();
			FparamParent = FileCommand.CreateParameter();

			FparamBasename.ParameterName = "basename";
			FparamPath.ParameterName = "path";
			FparamExtension.ParameterName = "extension";
			FparamSize.ParameterName = "size";
			FparamParent.ParameterName = "parent";

			FileCommand.Parameters.Add(FparamBasename);
			FileCommand.Parameters.Add(FparamPath);
			FileCommand.Parameters.Add(FparamExtension);
			FileCommand.Parameters.Add(FparamSize);
			FileCommand.Parameters.Add(FparamParent);

			DirCommand = connection.CreateCommand();
			DirCommand.CommandText = "INSERT INTO folders(basename, folder_path, parent) VALUES($basename, $path, $parent) ";

			DparamBasename = DirCommand.CreateParameter();
			DparamPath = DirCommand.CreateParameter();
			DparamParent = DirCommand.CreateParameter();

			DparamBasename.ParameterName = "basename";
			DparamPath.ParameterName = "path";
			DparamParent.ParameterName = "parent";

			DirCommand.Parameters.Add(DparamBasename);
			DirCommand.Parameters.Add(DparamPath);
			DirCommand.Parameters.Add(DparamParent);

			QueryCommand = connection.CreateCommand();
		}

		public void Create() {
			var transaction = connection.BeginTransaction();
			var command = connection.CreateCommand();
			command.CommandText = @"
				DROP TABLE IF EXISTS files;
				DROP TABLE IF EXISTS folders;
				DROP INDEX IF EXISTS folder_path;
				CREATE TABLE files(
					file_id INTEGER PRIMARY KEY AUTOINCREMENT,
					basename TEXT,
					file_path TEXT,
					extension TEXT,
					size INT,
					parent INT,
					FOREIGN KEY (parent) REFERENCES folders (folder_id)
					);
				CREATE TABLE folders(
				folder_id INTEGER PRIMARY KEY AUTOINCREMENT,
				basename TEXT,
				folder_path TEXT,
				parent INT
				);
				CREATE UNIQUE INDEX folder_path ON folders(folder_path);
			";
			command.ExecuteNonQuery();
			transaction.Commit();
		}

		public void Update(string[] paths, int maxSearchDepth) {
			var transaction = connection.BeginTransaction();
			PrepCommands();
			var command = connection.CreateCommand();
			command.CommandText = @"DELETE FROM files;
									DELETE FROM folders;";
			command.ExecuteNonQuery();

			maxDepth = maxSearchDepth;
			List<FolderData> folderData = new();
			foreach (string path in paths) {
				folderData.Add(new FolderData(path, 0));
			}

			command.ExecuteNonQuery();
			AddFoldersToDatabase(folderData);
			Dictionary<string, int> ids = FolderIDs(paths);
			foreach (string pth in paths) {
				scan(pth, ids[pth]);
			}
			transaction.Commit();
			vacuum();
		}

		private void scan(string path, int parentIndex) {
			searchDepth++;
			if (maxDepth > 0 && searchDepth >= maxDepth) {
				searchDepth--;
				return;
			}

			List<FileData> fileData = new();
			List<FolderData> folderData = new();

			string[] paths;
			try {
				paths = Directory.GetDirectories(path);
			}
			catch (UnauthorizedAccessException) {
				Console.WriteLine("Access Denied: {0}", path);
				searchDepth--;
				return;
			}
			catch (DirectoryNotFoundException) {
				Console.WriteLine("Could not Find: {0}", path);
				searchDepth--;
				return;
			}

			foreach (string directory in paths) {
				folderData.Add(new FolderData(directory, parentIndex));
			}

			foreach (string file in Directory.GetFiles(path)) {
				FileData d = new(file, parentIndex);
				d.findInfo();
				fileData.Add(d);
			}
			AddFilesToDatabase(fileData);
			AddFoldersToDatabase(folderData);

			fileData.Clear();
			fileData.TrimExcess();
			folderData.Clear();
			folderData.TrimExcess();

			Dictionary<string, int> folderIds = FolderIDs(paths);
			foreach (string pth in paths) {
				scan(pth, folderIds[pth]);
			}
			searchDepth--;
		}

		public void AddFolder(string path, int maxSearchDepth) {
			maxDepth = maxSearchDepth;
			List<FolderData> folderData = new();
			DirectoryInfo di = new(path);
			int index = 0;

			if (di.Parent != null) index = FolderIndex(di.Parent.ToString());
			folderData.Add(new FolderData(path, index));
			var transaction = connection.BeginTransaction();
			PrepCommands();
			AddFoldersToDatabase(folderData);
			scan(path, FolderIndex(path));
			transaction.Commit();
		}

		public void RemoveFolder(string path) {
			var transaction = connection.BeginTransaction();
			PrepCommands();
			//int index = FolderIndex(path);
			List<int> indexes = new();
			indexes.Add(FolderIndex(path));
			indexes.AddRange(GetSubfolders(indexes));
			string query = FormatInQuery(indexes);
			var command = connection.CreateCommand();
			command.CommandText = @"
				DELETE FROM folders WHERE folder_id IN($indexes);
				DELETE FROM files WHERE parent IN($indexes);
				";
			var paramIndexes = command.CreateParameter();
			paramIndexes.ParameterName = "indexes";
			command.Parameters.Add(paramIndexes);
			paramIndexes.Value = query;
			command.ExecuteNonQuery();
			vacuum();
			transaction.Commit();
		}

		void AddFilesToDatabase(List<FileData> files) {

			foreach (FileData data in files) {
				FparamBasename.Value = data.GetName();
				FparamPath.Value = data.GetPath();
				FparamExtension.Value = data.GetExtension();
				FparamSize.Value = data.GetSize();
				FparamParent.Value = data.GetParentIndex();
				FileCommand.ExecuteNonQuery();
			}
		}

		void AddFoldersToDatabase(List<FolderData> folders) {

			foreach (FolderData data in folders) {
				DparamBasename.Value = data.GetName();
				DparamPath.Value = data.GetPath();
				DparamParent.Value = data.GetParentIndex();
				DirCommand.ExecuteNonQuery();
			}

		}

		Dictionary<string, int> FolderIDs(string[] folders) {
			Dictionary<string, int> ids = new();
			if (folders.Length == 0) return ids;
			//var command = connection.CreateCommand();
			string query = FormatInQuery(folders);
			//Console.WriteLine(query);
			QueryCommand.CommandText = "SELECT Folder_path, folder_id FROM folders WHERE folder_path IN(" + query + ")";

			using (var reader = QueryCommand.ExecuteReader()) {

				while (reader.Read()) {
					ids.Add(reader.GetString(0), reader.GetInt32(1));
				}
			}
			return ids;
		}

		int FolderIndex(string folder) {
			var command = connection.CreateCommand();
			command.CommandText = "SELECT folder_id FROM folders WHERE folder_path = $path";
			var paramfolder = command.CreateParameter();
			paramfolder.ParameterName = "path";
			paramfolder.Value = folder;
			command.Parameters.Add(paramfolder);
			using (var reader = command.ExecuteReader()) {
				while (reader.Read()) return reader.GetInt32(0);
			}
			return 0;
		}

		string FormatInQuery(string[] items) {
			//TODO replace with StringBuilder
			string query = "";
			StringBuilder sb = new("");
			foreach (string item in items) {
				string clean = item.Replace("\"", "\"\"");
				sb.Append(",\"");
				sb.Append(clean);
				sb.Append("\"");
				
				//query += ",\"" + clean + "\"";
			}
			query = sb.ToString();
			return query[1..];
		}

		string FormatInQuery(List<int> items) {
			string query = "";
			StringBuilder sb = new("");
			foreach (int item in items) {
				sb.Append(",\"");
				sb.Append(item);
				sb.Append("\"");
				//query += ",\"" + item.ToString() + "\"";
			}
			query = sb.ToString();
			return query[1..];
		}

		List<int> GetSubfolders(List<int> folders) {
			List<int> subfolders = new();
			//var command = connection.CreateCommand();
			string query = FormatInQuery(folders);
			QueryCommand.CommandText = "SELECT folder_id FROM folders WHERE parent IN(" + query + ")";
			using (var reader = QueryCommand.ExecuteReader()) {
				while (reader.Read()) {
					subfolders.Add(reader.GetInt32(0));
				}
			}
			if (subfolders.Count > 0) {
				subfolders.AddRange(GetSubfolders(subfolders));
			}
			return subfolders;
		}

		void vacuum() {
			var command = connection.CreateCommand();
			command.CommandText = "VACUUM";
			command.ExecuteNonQuery();
			command.Dispose();
		}

		public void TestFunction(String s) {
			connection.BeginTransaction();
			PrepCommands();
			List<int> items = new List<int>();
			items.Add(FolderIndex(s));
			Console.WriteLine(GetSubfolders(items));
		}
	}
}
