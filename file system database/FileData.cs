namespace file_system_database {


	internal readonly struct FileData {
		readonly string name;
		readonly string path;
		readonly string extension;
		readonly long size;
		readonly int parentIndex;

		public FileData(string path, int parentIndex) {
			this.path = path;
			this.parentIndex = parentIndex;
			size = 0;
			FileInfo fi = new(path);
			name = fi.Name;
			try {
				size = fi.Length;
			}
			catch (FileNotFoundException) {
				//TODO logging
			}
			extension = fi.Extension;
		}

		public string GetName() { return name; }
		public string GetPath() { return path; }
		public string GetExtension() { return extension; }
		public long GetSize() { return size; }
		public int GetParentIndex() { return parentIndex; }

	}
}
