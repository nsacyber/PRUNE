using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PruneLibrary {
	[StructLayout(LayoutKind.Sequential)]
	public struct ProcessorDataStruct {

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst=50)]
		public string description;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 50)]
		public string name;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 5)]
		public string physCore;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 5)]
		public string logiCore;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 10)]
		public string coreSpeed;

		public ProcessorDataStruct(string desc, string nam, string phys, string log, string speed) {
			this.description = desc;
			this.name = nam;
			this.physCore = phys;
			this.logiCore = log;
			this.coreSpeed = speed;
		}

		public ProcessorDataStruct(int x) {
			this.description = "";
			this.name = "";
			this.physCore = "";
			this.logiCore = "";
			this.coreSpeed = "";
		}
	}
}
