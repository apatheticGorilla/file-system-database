namespace file_system_database
{
	class Program
	{
		static void Main(string[] args) {
			DatabaseManager db = new("E:\\test.db");
			//db.Create();
			string[] drives = { "C:\\", "D:\\", "F:\\", "G:\\", "E:\\"};
			db.Update(drives, 0);
			//db.RemoveFolder("C:\\Program Files");
			//db.AddFolder("Z:\\",0);
			//db.TestFunction("C:\\");
			//List<(int, String, String, String, int, int)> values = db.FilesWithExtension(".txt");
			//foreach ((int, String, String, String, int, int) t in values) {
			//	Console.WriteLine(t);
			//}
			Console.WriteLine("blep");
		}
	}
}
