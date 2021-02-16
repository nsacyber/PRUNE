using System;

namespace PruneLibrary 
{
	public struct DiskDataStruct {

		public int diskCount;
		public string manufacturer;
		public string model;

		public DiskDataStruct(int diskCount, string man, string mod) 
		{
			this.diskCount = diskCount;
			this.manufacturer = man;
			this.model = mod;
		}

		public DiskDataStruct(int x)
		{
			this.diskCount = 0;
			this.manufacturer = "";
			this.model = "";
		}

		public override string ToString()
		{
			return "Disk " + diskCount + ": " + manufacturer + ", " + model + Environment.NewLine;
		}
	}
}
