namespace file_system_database {
	public readonly struct FolderData {
		readonly string name;
		readonly string path;
		readonly int parentIndex;

		public FolderData(string name, string path, int parentIndex) {
			this.name = name;
			this.path = path;
			this.parentIndex = parentIndex;


		}
		public string GetName() { return name; }
		public string GetPath() { return path; }
		public int GetParentIndex() { return parentIndex; }

	}
}
