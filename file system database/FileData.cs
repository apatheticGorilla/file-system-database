namespace file_system_database {


	public readonly struct FileData {
		readonly int fileID;
		readonly string name;
		readonly string path;
		readonly string extension;
		readonly long size;
		readonly int parentIndex;

		public FileData(int fileID, string name, string path, string extension, long size, int parentIndex) {
			this.fileID = fileID;
			this.name = name;
			this.path = path;
			this.parentIndex = parentIndex;
			this.extension = extension;
			this.size = size;
		}
		public int GetFileID() { return fileID; }
		public string GetName() { return name; }
		public string GetPath() { return path; }
		public string GetExtension() { return extension; }
		public long GetSize() { return size; }
		public int GetParentIndex() { return parentIndex; }

	}
}
