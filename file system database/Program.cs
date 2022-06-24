namespace file_system_database {
	class Program {
		static void Main(string[] args) {
			DatabaseManager db = new("E:\\test.db");
			db.Create();
			string[] drives = { "C:\\", "D:\\", "F:\\", "G:\\","E:\\" , "Z:\\"};
			db.Update(drives, 0);
			//db.AddFolder("Z:\\",0);
	
			Console.WriteLine("blep");
		}
	}
}
