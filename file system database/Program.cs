namespace file_system_database {
	class Program {
		static void Main(string[] args) {
			DatabaseManager db = new("E:\\test.db");
			//db.Create()
			DriveInfo[] driveInfos = DriveInfo.GetDrives();
			string[] drives = new string[driveInfos.Length];
			for(int i = 0; i < driveInfos.Length; i++) {
				drives[i] = driveInfos[i].Name;
			}
			db.Update(drives, 0);
			//db.RemoveFolder("C:\\Program Files");
			//db.AddFolder("Z:\\",0);
			//db.TestFunction("C:\\");
			Thread.Sleep(2000);
			List<(int, String, String, String, int, int)> values = db.FilesWithExtension(".cc");
			foreach ((int, String, String, String, int, int) t in values) {
				Console.WriteLine(t);
			}
		}
	}
}
