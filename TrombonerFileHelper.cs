﻿using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TootTallyCustomTromboner
{
    public static class TrombonerFileHelper
    {
        public const string BONER_FILE_EXT = ".boner";
        public static FileInfo[] GetFilesFromDirectory(string directory) => GetOrCreateDirectory(directory).GetFiles();

        public static DirectoryInfo GetOrCreateDirectory(string directory)
        {
            if (!Directory.Exists(directory))
                return Directory.CreateDirectory(directory);
            return new DirectoryInfo(directory);
        }

        public static List<FileInfo> GetAllBonerFilesFromDirectory(string diretory) => GetFilesFromDirectory(diretory).Where(x => x.FullName.Contains(BONER_FILE_EXT)).ToList();
    }
}
