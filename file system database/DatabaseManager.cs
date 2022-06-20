using Microsoft.Data.Sqlite;

namespace file_system_database {

	public class DatabaseManager {
		private readonly SqliteConnection connection;
		private int maxDepth;
		private int searchDepth = 0;

		public DatabaseManager(string dbPath) {
			connection = new("Data Source=" + dbPath);
			connection.Open();
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
			maxDepth = maxSearchDepth;
			List<FolderData> folderData = new();
			foreach (string path in paths) {
				folderData.Add(new FolderData(path, 0));
			}
			var transaction = connection.BeginTransaction();
			AddFoldersToDatabase(folderData);
			Dictionary<string, int> ids = FolderIDs(paths);
			foreach (string pth in paths) {
				scan(pth, ids[pth]);
			}
			transaction.Commit();
		}

		private void scan(string path, int parentIndex) {
			searchDepth++;
			if (maxDepth > 0 && searchDepth > maxDepth) {
				searchDepth--;
				//TODO logging
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
			catch (FileNotFoundException) {
				Console.WriteLine("Could not Find: {0}",path);
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
			//int[] folderIDs = FolderIDs(paths);
			Dictionary<string, int> folderIds = FolderIDs(paths);
			foreach (string pth in paths) {
				scan(pth, folderIds[pth]);
			}
			searchDepth--;
		}

		void AddFilesToDatabase(List<FileData> files) {
			
			var command = connection.CreateCommand();
			command.CommandText = "INSERT INTO files(basename, file_path, extension, size, parent) VALUES($basename, $path, $extension, $size, $parent) ";

			var paramBasename = command.CreateParameter();
			paramBasename.ParameterName = "basename";
			var paramPath = command.CreateParameter();
			paramPath.ParameterName = "path";
			var paramExtension = command.CreateParameter();
			paramExtension.ParameterName = "extension";
			var paramSize = command.CreateParameter();
			paramSize.ParameterName = "size";
			var paramParent = command.CreateParameter();
			paramParent.ParameterName = "parent";

			command.Parameters.Add(paramBasename);
			command.Parameters.Add(paramPath);
			command.Parameters.Add(paramExtension);
			command.Parameters.Add(paramSize);
			command.Parameters.Add(paramParent);

			foreach (FileData data in files) {
				paramBasename.Value = data.GetName();
				paramPath.Value = data.GetPath();
				paramExtension.Value = data.GetExtension();
				paramSize.Value = data.GetSize();
				paramParent.Value = data.GetParentIndex();
				command.ExecuteNonQueryAsync();
			}
			
		}

		void AddFoldersToDatabase(List<FolderData> folders) {
			//var transaction = connection.BeginTransaction();
			var command = connection.CreateCommand();
			command.CommandText = "INSERT INTO folders(basename, folder_path, parent) VALUES($basename, $path, $parent) ";

			var paramBasename = command.CreateParameter();
			paramBasename.ParameterName = "basename";
			var paramPath = command.CreateParameter();
			paramPath.ParameterName = "path";
			var paramParent = command.CreateParameter();
			paramParent.ParameterName = "parent";

			command.Parameters.Add(paramBasename);
			command.Parameters.Add(paramPath);
			command.Parameters.Add(paramParent);

			foreach (FolderData data in folders) {
				paramBasename.Value = data.GetName();
				paramPath.Value = data.GetPath();

				paramParent.Value = data.GetParentIndex();


				command.ExecuteNonQuery();
			}
			//transaction.Commit();
		}

		Dictionary<string, int> FolderIDs(string[] folders) {
			Dictionary<string, int> ids = new();
			if (folders.Length == 0) return ids;
			var command = connection.CreateCommand();
			string query = FormatInQuery(folders);
			//Console.WriteLine(query);
			command.CommandText = "SELECT Folder_path, folder_id FROM folders WHERE folder_path IN(" + query + ")";

			using (var reader = command.ExecuteReader()) {

				while (reader.Read()) {
					ids.Add(reader.GetString(0), reader.GetInt32(1));
				}
			}
			return ids;
		}

		string FormatInQuery(string[] items) {
			//TODO replace with StringBuilder
			string query = "";
			foreach (string item in items) {
				string clean = item.Replace("\"", "\"\"");
				query += ",\"" + clean + "\"";
				//query.Concat(",\"");
				//query.Concat(clean);
				//query.Concat("\"");
			}
			return query[1..];
		}

	}


}
