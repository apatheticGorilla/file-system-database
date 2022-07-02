namespace file_system_database
{
	class Program
	{
		static void Main(string[] args) {
			DatabaseManager db = new("E:\\test.db");
			//db.Create();
			string[] drives = { "C:\\", "D:\\", "F:\\", "G:\\", "E:\\" };
			db.Update(drives, 0);
			//db.RemoveFolder("C:\\Program Files");
			//db.AddFolder("Z:\\",0);
			//db.TestFunction("C:\\");
			Console.WriteLine("blep");
		}
	}
}
