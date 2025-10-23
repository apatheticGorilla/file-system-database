namespace file_system_database {
	public readonly struct FolderData(string name, string path, int parentIndex) {
		readonly string name = name;
		readonly string path = path;
		readonly int parentIndex = parentIndex;

		public string GetName() { return name; }
		public string GetPath() { return path; }
		public int GetParentIndex() { return parentIndex; }

	}
}
