using Microsoft.Data.Sqlite;

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
			FparamBasename.ParameterName = "basename";
			FparamPath = FileCommand.CreateParameter();
			FparamPath.ParameterName = "path";
			FparamExtension = FileCommand.CreateParameter();
			FparamExtension.ParameterName = "extension";
			FparamSize = FileCommand.CreateParameter();
			FparamSize.ParameterName = "size";
			FparamParent = FileCommand.CreateParameter();
			FparamParent.ParameterName = "parent";

			FileCommand.Parameters.Add(FparamBasename);
			FileCommand.Parameters.Add(FparamPath);
			FileCommand.Parameters.Add(FparamExtension);
			FileCommand.Parameters.Add(FparamSize);
			FileCommand.Parameters.Add(FparamParent);

			DirCommand = connection.CreateCommand();
			DirCommand.CommandText = "INSERT INTO folders(basename, folder_path, parent) VALUES($basename, $path, $parent) ";

			DparamBasename = DirCommand.CreateParameter();
			DparamBasename.ParameterName = "basename";
			DparamPath = DirCommand.CreateParameter();
			DparamPath.ParameterName = "path";
			DparamParent = DirCommand.CreateParameter();
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
			foreach (string item in items) {
				string clean = item.Replace("\"", "\"\"");
				query += ",\"" + clean + "\"";
			}
			return query[1..];
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
		void vacuum() {
			var command = connection.CreateCommand();
			command.CommandText = "VACUUM";
			command.ExecuteNonQuery();
			command.Dispose();
		}
	}
}
