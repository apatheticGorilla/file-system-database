﻿namespace file_system_database {
	class Program {
		static void Main(string[] args) {
			DatabaseManager db = new("D:\\test.db");
			db.Create();
			string[] drives = { "C:\\", "D:\\" };
			db.Update(drives,0);
		}
	}
}