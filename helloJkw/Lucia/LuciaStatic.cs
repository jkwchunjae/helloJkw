﻿using Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace helloJkw
{
	public static class LuciaStatic
	{
		static object _updateLock = new object();

		public static string RootPath = @"lucia";
		public static string RootPathWeb = @"lucia-web";
		public static string RootPathMobile = @"lucia-mobile";
		public static LuciaDirInfo LuciaDir;

		private static DateTime _lastUpdateTime;

		public static string MainDirName
		{
			get
			{
				return LuciaDir.GetDirNames().Where(e => e.Contains("main")).First();
			}
		}

		public static LuciaDirInfo UpdateLuciaDir(int minute = 10)
		{
			lock (_updateLock)
			{
				if (DateTime.Now.Subtract(_lastUpdateTime).TotalMinutes < minute)
					return LuciaDir;
				LuciaDir = RootPath.CreateDirInfo();
				var rootFullPath = Path.GetFullPath(RootPath).Replace(@"\", "/");
				if (rootFullPath[rootFullPath.Length - 1] != '/') rootFullPath += '/';
				ImageResizer.SyncImages(sourcePath: rootFullPath, sourceFolder: "/lucia/", targetFolder: "/lucia-web/", optimalWidth: 700, optimalHeight: 600);
				ImageResizer.SyncImages(sourcePath: rootFullPath, sourceFolder: "/lucia/", targetFolder: "/lucia-mobile/", optimalWidth: 400, optimalHeight: 500);
				_lastUpdateTime = DateTime.Now;
				return LuciaDir;
			}
		}

		public static IEnumerable<string> GetMainMenu()
		{
			return LuciaDir.GetDirNames()
				.Where(e => e != MainDirName)
				.Select(e => e.RemovePrefixNumber());
		}
	}
}
