using System.Runtime.InteropServices;

namespace PruneLibrary {
	[StructLayout(LayoutKind.Sequential)]
	public struct DiskDataStruct {
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 25)]
		public string manufacturer;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 25)]
		public string model;

		public DiskDataStruct(string man, string mod) {
			this.manufacturer = man;
			this.model = mod;
		}

		public DiskDataStruct(int x) {
			this.manufacturer = "";
			this.model = "";
		}
	}
}
