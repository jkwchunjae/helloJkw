﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy;
using Nancy.Hosting.Self;
using Nancy.Conventions;
using Extensions;
using Nancy.TinyIoc;
using Nancy.Bootstrapper;
using helloJkw.Game.Worldcup;

namespace helloJkw
{
	class Program
	{
		static void Main(string[] args)
		{
			#region Setting
			var argsDic = args.ToCommandArgumentsDictionary().ToDefaultDictionary();

			#region Port
			string port = argsDic["port"];
			if (port == null)
			{
				Console.Write("Port: ");
				port = Console.ReadLine();
			}
			Logger.Log("Port: {port}", new { port });
			#endregion

			#endregion

			using (var host = new NancyHost(new Uri("http://localhost:" + port)))
			{
				host.Start();
				Logger.Log("Start HelloJkw");

				while (true)
				{
					Console.ReadLine();
					Console.ReadLine();
					Console.Write("Command : ");
					var ans = Console.ReadLine();
					if (ans == "quit" || ans == "exit")
					{
						Console.Write("Really? (Y/n): ");
						ans = Console.ReadLine();
						if (ans == "Y")
							break;
					}
				}
			}
		}
	}

	public class Bootstrapper : DefaultNancyBootstrapper
	{
		protected override void ConfigureConventions(NancyConventions nancyConventions)
		{
			base.ConfigureConventions(nancyConventions);
			nancyConventions.StaticContentsConventions.Clear();

			nancyConventions.StaticContentsConventions.AddDirectory("Static", "Static");
			nancyConventions.StaticContentsConventions.AddDirectory("jkw", "jkw/static");
			nancyConventions.StaticContentsConventions.AddDirectory("lucia", "lucia");
			nancyConventions.StaticContentsConventions.AddDirectory("lucia-web", "lucia-web");
			nancyConventions.StaticContentsConventions.AddDirectory("lucia-mobile", "lucia-mobile");
		}

		protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
		{
			base.ApplicationStartup(container, pipelines);
			this.Conventions.ViewLocationConventions.Add((viewName, model, context) =>
				{ return string.Concat("Views/Lucia/", viewName); });
			this.Conventions.ViewLocationConventions.Add((viewName, model, context) =>
				{ return string.Concat("Views/Jkw/", viewName); });
			this.Conventions.ViewLocationConventions.Add((viewName, model, context) =>
				{ return string.Concat("Views/Games/", viewName); });
		}
	}
}
