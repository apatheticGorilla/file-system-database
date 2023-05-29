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
			List<FileData> files = db.FilesWithExtension(".zip");
			foreach (FileData file in files) {
				Console.WriteLine("==========================================");
				Console.WriteLine("\n fileID: " + file.GetFileID().ToString());
				Console.WriteLine("\n File Name: " + file.GetName());
				Console.WriteLine("\n File Path: " + file.GetPath());
				Console.WriteLine("\n File Extension: " + file.GetExtension());
				Console.WriteLine("\n File Size: " + file.GetSize().ToString());
				Console.WriteLine("\n Parent Folder Index: " + file.GetParentIndex().ToString());
				Console.WriteLine("==========================================");
			}
		}
	}
}
