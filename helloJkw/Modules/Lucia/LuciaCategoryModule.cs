﻿using Nancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using helloJkw.Extensions;

namespace helloJkw.Modules.Lucia
{
	public class LuciaCategoryModule : NancyModule
	{
		public LuciaCategoryModule()
		{
			Get["/lucia/category/{category}"] = _ =>
			{
				LuciaStatic.UpdateLuciaDir(5);
				string category = _.category;

				var productList = LuciaStatic.LuciaDir[category]
					.GetSubDirList()
					.Select(e => e.ProductInfo.ToExpando());

				var model = new
				{
					rootPath = LuciaStatic.RootPath,
					mainMenu = LuciaStatic.GetMainMenu(),
					category,
					productList,
				};
				return View["luciaCategory", model];
			};
		}
	}
}