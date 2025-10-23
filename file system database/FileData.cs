namespace file_system_database {


	public readonly struct FileData(int fileID, string name, string path, string extension, long size, int parentIndex) {
		readonly int fileID = fileID;
		readonly string name = name;
		readonly string path = path;
		readonly string extension = extension;
		readonly long size = size;
		readonly int parentIndex = parentIndex;

		public int GetFileID() { return fileID; }
		public string GetName() { return name; }
		public string GetPath() { return path; }
		public string GetExtension() { return extension; }
		public long GetSize() { return size; }
		public int GetParentIndex() { return parentIndex; }

	}
}
