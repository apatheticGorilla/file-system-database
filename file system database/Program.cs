namespace file_system_database {
	class Program {
		static void Main(string[] args) {
			DatabaseManager db = new("C:\\Temp\\test.db");
			//db.Create();
			//string[] drives = {"C:\\", "D:\\", "F:\\", "G:\\"};
			//db.Update(drives,10);
			//db.AddFolder("E:\\",0);

			Console.WriteLine("blep");
		}
	}
}
