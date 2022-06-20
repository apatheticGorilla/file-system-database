﻿namespace file_system_database {


	internal class FileData {
		string name;
		readonly string path;
		string extension;
		long size;
		int parentIndex;

		public FileData(string path, int parentIndex) {
			this.path = path;
			this.parentIndex = parentIndex;
		}

		public string GetName() { return name; }
		public string GetPath() { return path; }
		public string GetExtension() { return extension; }
		public long GetSize() { return size; }
		public int GetParentIndex() { return parentIndex; }

		public void findInfo() {
			FileInfo fi = new FileInfo(path);
			name = fi.Name;
			size = fi.Length;
			extension = fi.Extension;
		}
	}
}
