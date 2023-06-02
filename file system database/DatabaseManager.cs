using Microsoft.Data.Sqlite;
using System.Diagnostics;
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

		/// <summary>
		/// Used to create, update, and query the file system database
		/// </summary>
		/// <param name="dbPath">the filepath of the database file</param>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		public DatabaseManager(string dbPath) {
			bool DbExists = File.Exists(dbPath);
			connection = new("Data Source=" + dbPath);
			connection.Open();
			//automatically create tables if the file did not exist before opening the connection
			if (!DbExists) Create();
		}
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

		/// <summary>
		/// Prepares parameters and commands for use.
		/// This must be called after creating a transaction that will use these commands and must be called again after committing that transaction.
		/// </summary>
		void PrepCommands() {
			//Command for adding files to DB
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

			//Command for adding folders to DB
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

			//basic command for queries
			QueryCommand = connection.CreateCommand();
		}

		/// <summary>
		///		Creates the database tables and an index for faster population.
		/// </summary>
		public void Create() {
			var transaction = connection.BeginTransaction();
			var command = connection.CreateCommand();
			command.CommandText = @"
				DROP TABLE IF EXISTS files;
				DROP TABLE IF EXISTS folders;
				DROP INDEX IF EXISTS folder_path;
				CREATE TABLE files(
					basename TEXT,
					file_path TEXT,
					extension TEXT,
					size INT,
					parent INT
				);
				CREATE TABLE folders(
					basename TEXT,
					folder_path TEXT,
					parent INT
				);
				CREATE UNIQUE INDEX folder_path ON folders(folder_path);
				CREATE INDEX parent ON folders(parent);
			";
			command.ExecuteNonQuery();
			transaction.Commit();
		}

		/// <summary>
		///  Deletes old data and re-scans folders
		/// </summary>
		/// <param name="paths">Addresses of folders to scan</param>
		/// <param name="maxSearchDepth">Folders and files deeper than this number will not be searched. Set to 0 to disable.</param>
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
				Scan(pth, ids[pth]);
			}
			transaction.Commit();
			Vacuum();
		}

		/// <summary>
		/// Gets the basename, extension and size of a file and puts them into a FileData object
		/// </summary>
		/// <param name="path">The full path to the file</param>
		/// <param name="parentIndex">The index of the parent folder in the database</param>
		/// <returns>a FileData struct with the gathered information</returns>
		FileData ScanFile(string path, int parentIndex) {
			long size = 0;
			FileInfo fi = new(path);
			string name = fi.Name;
			try {
				size = fi.Length;
			}
			catch (FileNotFoundException) {
				Debug.WriteLine("Could not find file: " + path);
			}
			string extension = fi.Extension;
			return new FileData(-1, name, path, extension, size, parentIndex);
		}

		/// <summary>
		/// The heart of database population, making use of recursion to scan file systems
		/// </summary>
		/// <param name="path">Filepath of the folder to scan</param>
		/// <param name="parentIndex">rowid of the parent folder, passed to the method to cut down on queries</param>
		private void Scan(string path, int parentIndex) {
			searchDepth++;
			//end execution if max search depth is reached.
			if (maxDepth > 0 && searchDepth >= maxDepth) {
				searchDepth--;
				return;
			}

			List<FileData> fileData = new();
			List<FolderData> folderData = new();

			//attempt to get directories or end execution if failed
			string[] paths;
			try {
				paths = Directory.GetDirectories(path);
			}
			catch (UnauthorizedAccessException) {
				Debug.WriteLine("Access Denied: " + path);
				searchDepth--;
				return;
			}
			catch (DirectoryNotFoundException) {
				Debug.WriteLine("Could not Find: " + path);
				searchDepth--;
				return;
			}

			//save data for each folder to a struct and add to the list.
			foreach (string directory in paths)
				folderData.Add(new FolderData(directory, parentIndex));

			//get files and save their info to a list of structs.
			foreach (string file in Directory.GetFiles(path))
				fileData.Add(ScanFile(file, parentIndex));

			//insert into database in bulk.
			AddFilesToDatabase(fileData);
			AddFoldersToDatabase(folderData);

			fileData.Clear();
			fileData.TrimExcess();
			folderData.Clear();
			folderData.TrimExcess();

			//query the IDs of the folders in one query to pass to Scan()
			Dictionary<string, int> folderIds = FolderIDs(paths);
			foreach (string pth in paths) {
				Scan(pth, folderIds[pth]);
			}
			searchDepth--;
		}

		/// <summary>
		/// Adds a folder to the database without deleting existing data.
		/// </summary>
		/// <param name="path">Address of the folder to be added.</param>
		/// <param name="maxSearchDepth">Folders and files deeper than this number will not be searched. Set to 0 to disable.</param>
		public void AddFolder(string path, int maxSearchDepth) {
			maxDepth = maxSearchDepth;
			List<FolderData> folderData = new();
			DirectoryInfo di = new(path);
			int index = 0;

			if (di.Parent != null) index = FolderIndex(di.Parent.ToString()); //TODO see what happens if the parent is not in the database
			folderData.Add(new FolderData(path, index));
			var transaction = connection.BeginTransaction();
			PrepCommands();
			AddFoldersToDatabase(folderData);
			Scan(path, FolderIndex(path));
			transaction.Commit();
		}

		/// <summary>
		/// Removes a folder and all children from the database.
		/// </summary>
		/// <param name="path">Address of the folder to delete</param>
		public void RemoveFolder(string path) {
			var transaction = connection.BeginTransaction();
			PrepCommands();
			List<int> indexes = new() {
				FolderIndex(path)
			};

			//get children
			indexes.AddRange(GetSubfolders(indexes));
			string query = FormatInQuery(indexes);
			var command = connection.CreateCommand();

			//delete the records.
			command.CommandText = @"
				DELETE FROM folders WHERE rowid IN($indexes);
				DELETE FROM files WHERE parent IN($indexes);
				";
			var paramIndexes = command.CreateParameter();
			paramIndexes.ParameterName = "indexes";
			command.Parameters.Add(paramIndexes);
			paramIndexes.Value = query;
			command.ExecuteNonQuery();

			Vacuum();
			transaction.Commit();
		}

		/// <summary>
		/// insert file data in bulk
		/// </summary>
		/// <param name="files">FileData for each row</param>
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

		/// <summary>
		/// Insert folder data in bulk
		/// </summary>
		/// <param name="folders">FolderData for each row</param>
		void AddFoldersToDatabase(List<FolderData> folders) {

			foreach (FolderData data in folders) {
				DparamBasename.Value = data.GetName();
				DparamPath.Value = data.GetPath();
				DparamParent.Value = data.GetParentIndex();
				DirCommand.ExecuteNonQuery();
			}

		}

		/// <summary>
		/// Queries the database for the IDs of the given folders.
		/// </summary>
		/// <param name="folders">Filepaths of the folders</param>
		/// <returns>Dictionary of Ids using the filepath as the key</returns>
		Dictionary<string, int> FolderIDs(string[] folders) {
			Dictionary<string, int> ids = new();
			if (folders.Length == 0) return ids;
			string query = FormatInQuery(folders);
			QueryCommand.CommandText = "SELECT Folder_path, rowid FROM folders WHERE folder_path IN(" + query + ")";

			using (var reader = QueryCommand.ExecuteReader()) {
				while (reader.Read())
					ids.Add(reader.GetString(0), reader.GetInt32(1));
			}
			return ids;
		}

		/// <summary>
		/// Queries the database for the index of the given folder.
		/// </summary>
		/// <param name="folder">Filepath of the folder</param>
		/// <returns>rowid from the corresponding database row</returns>
		int FolderIndex(string folder) {
			var command = connection.CreateCommand();
			command.CommandText = "SELECT rowid FROM folders WHERE folder_path = $path";
			var paramfolder = command.CreateParameter();
			paramfolder.ParameterName = "path";
			paramfolder.Value = folder;
			command.Parameters.Add(paramfolder);
			using (var reader = command.ExecuteReader()) {
				while (reader.Read()) return reader.GetInt32(0);
			}
			return 0;
		}

		/// <summary>
		/// turns list of items into a single string for queries using WHERE ___ IN
		/// </summary>
		/// <param name="items">an array of strings</param>
		/// <returns>A comma delimited string of input values</returns>
		static string FormatInQuery(string[] items) {
			StringBuilder sb = new("");
			foreach (string item in items) {
				string clean = item.Replace("\"", "\"\"");
				sb.Append(",\"");
				sb.Append(clean);
				sb.Append('\"');
			}
			string query = sb.ToString();
			return query[1..];
		}

		/// <summary>
		/// turns list of items into a single string for queries using WHERE ___ IN
		/// </summary>
		/// <param name="items">a List of strings</param>
		/// <returns>A comma delimited string of input values</returns>
		static string FormatInQuery(List<int> items) {
			StringBuilder sb = new("");
			foreach (int item in items) {
				sb.Append(",\"");
				sb.Append(item);
				sb.Append('\"');
			}
			string query = sb.ToString();
			return query[1..];
		}

		/// <summary>
		/// Recursively queries database for the children of the given folder and the children of all subfolders
		/// </summary>
		/// <param name="folders">Indexes of the folders to query</param>
		/// <returns>Indexes of all child folders</returns>
		List<int> GetSubfolders(List<int> folders) {
			List<int> subfolders = new();
			string query = FormatInQuery(folders);
			QueryCommand.CommandText = "SELECT rowid FROM folders WHERE parent IN(" + query + ")";
			using (var reader = QueryCommand.ExecuteReader()) {
				while (reader.Read())
					subfolders.Add(reader.GetInt32(0));
			}
			if (subfolders.Count > 0)
				subfolders.AddRange(GetSubfolders(subfolders));

			return subfolders;
		}

		/// <summary>
		/// Recursively queries database for the children of the given folder and the children of all subfolders
		/// </summary>
		/// <param name="folderPath">File path of the folder to query</param>
		/// <returns>Indexes of all child folder</returns>
		List<int> GetSubfolders(string folderPath) {
			List<int> folder = new() {
				FolderIndex(folderPath)
			};

			return GetSubfolders(folder);
		}

		/// <summary>
		/// creates and runs a vacuum command
		/// </summary>
		void Vacuum() {
			var command = connection.CreateCommand();
			command.CommandText = "VACUUM";
			command.ExecuteNonQuery();
			command.Dispose();
		}

		//public void TestFunction(String s) {
		//	connection.BeginTransaction();
		//	PrepCommands();
		//	List<int> items = new();
		//	items.Add(FolderIndex(s));
		//	Console.WriteLine(GetSubfolders(items));
		//}

		/// <summary>
		/// Searches the database for all files which contain <paramref name="extension">extension</paramref> and places column data in <c>FileData</c> structs.
		/// </summary>
		/// <param name="extension">The file extension to search for I.E ".txt"</param>
		/// <returns>A list of FileData objects</returns>
		public List<FileData> FilesWithExtension(string extension) {
			List<FileData> values = new();
			PrepCommands();
			QueryCommand.CommandText = "SELECT * FROM files WHERE extension=\"" + extension + "\"";
			using (var reader = QueryCommand.ExecuteReader()) {
				while (reader.Read()) {
					values.Add(new FileData(
						0,
						reader.GetString(0),
						reader.GetString(1),
						reader.GetString(2),
						reader.GetInt64(3),
						reader.GetInt32(4)));
				}
			}
			return values;
		}

		/// <summary>
		/// Finds all subfolders inside the reference folder and writes the structure to the output folder.
		/// </summary>
		/// <param name="referenceFolder">The folder to mimic the structure of.</param>
		/// <param name="outputFolder">The file path that the structure is written to.</param>
		public void RecreateFolderStructure(string referenceFolder, string outputFolder) {
			PrepCommands();
			recreateFolderStructure(referenceFolder, outputFolder);
		}

		void recreateFolderStructure(string referenceFolder, string outputFolder) {
			//get folder basename
			string basename = "";

			QueryCommand.CommandText = "SELECT basename FROM folders WHERE folder_path=\"" + referenceFolder + "\"";
			using (var reader = QueryCommand.ExecuteReader()) {
				while (reader.Read()) basename = reader.GetString(0);
			}
			Debug.Assert(basename.Length > 0);

			//create the directory
			string dir = outputFolder + "\\" + basename.Replace(":", "");
			if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
			else Debug.WriteLine("The folder " + dir + " already exists");

			//get paths of child folders
			int index = FolderIndex(referenceFolder);
			QueryCommand.CommandText = "SELECT folder_path FROM folders WHERE parent = \"" + index + "\"";
			List<string> children = new();
			using (var reader = QueryCommand.ExecuteReader()) {
				while (reader.Read()) children.Add(reader.GetString(0));
			}
			//Recursively create structure for each child
			foreach (var child in children) recreateFolderStructure(child, dir);
		}

		/// <summary>
		/// Finds all subfolders inside the reference folder and writes the structure to the output folder and creates empty files in place of the originals.
		/// </summary>
		/// <param name="referenceFolder">The folder to mimic the structure of.</param>
		/// <param name="outputFolder">The file path that the structure is written to.</param>
		public void RecreateFileStructure(string referenceFolder, string outputFolder) {
			PrepCommands();
			//create the folders 
			recreateFolderStructure(referenceFolder, outputFolder);

			string refFolder;
			string outFolder = outputFolder;
			List<int> folders;
			//get subfolders and handle file path edge cases
			if (referenceFolder.Length <= 3) { //referenceFolder is a drive letter
				folders = GetSubfolders(referenceFolder);
				outFolder = outFolder + referenceFolder[..1] + "\\";
				refFolder = referenceFolder;
			} else if (referenceFolder.EndsWith('\\')) {
				refFolder = referenceFolder[..(referenceFolder.Length - 1)]; //remove extra '\'
				folders = GetSubfolders(refFolder);
				refFolder = refFolder[..refFolder.LastIndexOf("\\")];
			} else {
				folders = GetSubfolders(referenceFolder);
				refFolder = referenceFolder[..referenceFolder.LastIndexOf("\\")];
			}

			//get paths of files residing in the folders
			string parentQuery = FormatInQuery(folders);
			QueryCommand.CommandText = "SELECT file_path FROM files WHERE parent IN(" + parentQuery + ")";
			List<string> files = new();
			using (var reader = QueryCommand.ExecuteReader()) {
				while (reader.Read()) files.Add(reader.GetString(0));
			}

			//write files
			for (int i = 0; i < files.Count; i++) {
				//change file path to target outputFolder
				string file = files[i].Replace(refFolder, outFolder);
				File.Create(file).Close();
			}
		}
	}
}
