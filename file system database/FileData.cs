namespace file_system_database {


	internal readonly struct FileData {
		readonly string name;
		readonly string path;
		readonly string extension;
		readonly long size;
		readonly int parentIndex;

		public FileData(string name, string path, int parentIndex, string extension, long size) {
			this.name = name;
			this.path = path;
			this.parentIndex = parentIndex;
			this.extension = extension;
			this.size = size;
		}

		public string GetName() { return name; }
		public string GetPath() { return path; }
		public string GetExtension() { return extension; }
		public long GetSize() { return size; }
		public int GetParentIndex() { return parentIndex; }

	}
}
