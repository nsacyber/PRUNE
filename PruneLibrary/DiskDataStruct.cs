namespace PruneLibrary {
	public unsafe struct DiskDataStruct {
		public fixed char manufacturer[51];
		public fixed char model[51];

		public DiskDataStruct(string man, string mod) {
			fixed (char* manArr = manufacturer, modelArr = model) {
				for (int i = 0; i < 51; i++) {
					modelArr[i] = (char)0;

					if (i < 40) {
						manArr[i] = (char)0;
					}
				}

				//Create copy size variables
				int manSize = man.Length >= 50 ? 50 : man.Length;
				int modelSize = mod.Length >= 50 ? 50 : mod.Length;

				//Copy description
				for (int i = 0; i < modelSize; i++) {
					modelArr[i] = mod[i];
				}

				for (int i = 0; i < manSize; i++) {
					manArr[i] = man[i];
				}
			}
		}

		public DiskDataStruct(int x) {
			fixed (char* manArr = manufacturer, modelArr = model) {
				for (int i = 0; i < 51; i++) {
					modelArr[i] = (char)0;

					if (i < 40) {
						manArr[i] = (char)0;
					}
				}
			}
		}
	}
}
