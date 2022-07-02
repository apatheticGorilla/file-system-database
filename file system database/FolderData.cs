namespace file_system_database
{
	internal class FolderData
	{
		readonly string name;
		readonly string path;
		readonly int parentIndex;

		public FolderData(string path, int parentIndex)
		{
			this.path = path;
			this.parentIndex = parentIndex;
			DirectoryInfo dirInfo = new(path);
			name = dirInfo.Name;
		}
		public string GetName() { return name; }
		public string GetPath() { return path; }
		public int GetParentIndex() { return parentIndex; }

	}
}
