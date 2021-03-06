﻿using Nancy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Dynamic;
using Extensions;
using helloJkw.Utils;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace helloJkw
{
	public class YearGroup
	{
		public int Year;
		public List<MonthGroup> MonthList;
	}

	public class MonthGroup
	{
		public int Month;
		public List<DateTime> DateList;
	}
	public class JkwDiaryModule : JkwModule
	{
		public JkwDiaryModule()
		{
			#region Show Diary

			#region Get /diary/{diaryName?}/{date?}
			Get["/diary/{diaryName?}/{date?}"] = _ =>
			{
				if (!session.IsLogin)
					return View["diary/jkwDiaryRequireLogin", Model];

                DiaryThemeManager.Reload();

				// 자신의 다이어리가 있다면 가장 우선적으로 보여준다.
				// 없으면 나의 다이어리를 보여준다.
				string defaultDiaryName = UserManager.GetUser("112902876433833556239").DiaryName;
				string diaryName = _.diaryName != null ? _.diaryName
					: string.IsNullOrEmpty(session.User.DiaryName)
						? defaultDiaryName : session.User.DiaryName;
				if (!DiaryManager.IsValidDiaryName(diaryName))
					diaryName = defaultDiaryName;

				var diaryUserInfo = UserManager.GetUserInfoByDiaryName(diaryName);
				if (diaryUserInfo == null)
					return View["diary/jkwDiarySomethingWrong", Model];

				// 내 일기장 아닌데 허용 목록에 없으면 커트
				if (!diaryUserInfo.IsDiaryAcceptedUser(session.User))
					return View["diary/jkwDiarySomethingWrong", Model];

                var userInfo = UserManager.GetJsonInfo(session.User);
                var themeName = userInfo?.DiaryTheme ?? diaryUserInfo.DiaryTheme;
                var diaryTheme = DiaryThemeManager.GetTheme(themeName);

				bool withSecure = session.User.DiaryName == diaryName;

				var diaryList = _.date != null
					? DiaryManager.GetDiary(diaryName, ((string)_.date).ToDate(), withSecure)
					: DiaryManager.GetLastDiary(diaryName, withSecure);
				var date = diaryList.Any() ? diaryList.First().Date : DateTime.MinValue;
				var prevDate = DiaryManager.GetPrevDate(diaryName, date, withSecure);
				var nextDate = DiaryManager.GetNextDate(diaryName, date, withSecure);

				HitCounter.Hit("diary-{diaryName}-{date}".WithVar(new { diaryName, date = date.ToInt() }));

				Model.Date = date;
				Model.DayOfWeek = date.GetWeekday(DateLanguage.KR, WeekdayFormat.D);
				Model.hasPrev = prevDate != DateTime.MinValue;
				Model.hasNext = nextDate != DateTime.MinValue;
				Model.PrevDate = prevDate;
				Model.NextDate = nextDate;
				Model.DiaryName = diaryName;
				Model.DiaryList = diaryList;
				Model.IsMine = session.User.DiaryName == diaryName;
                Model.Theme = diaryTheme;
				return View["diary/jkwDiaryHome", Model];
			};
			#endregion

			#region Get /diary/home
			Get["/diary/home"] = _ =>
			{
				if (!session.IsLogin)
					return View["diary/jkwDiaryRequireLogin", Model];

				return null;
			};
			#endregion

			#region Get /diary/showdates/{diaryName}
			Get["/diary/showdates/{diaryName}"] = _ =>
			{
				if (!session.IsLogin)
					return View["diary/jkwDiaryRequireLogin", Model];

				string diaryName = _.diaryName;
				bool withSecure = session.User.DiaryName == diaryName;
				var dateList = DiaryManager.GetAllDates(diaryName, withSecure);

				var dateGroup = dateList
					.GroupBy(x => x.Year)
					.Select(x => new YearGroup
					{
						Year = x.Key,
						MonthList = x.GroupBy(e => e.Month)
									.Select(e => new MonthGroup{ Month = e.Key, DateList = e.Select(t => t).ToList() })
									.OrderByDescending(e => e.Month)
									.ToList()
					})
					.OrderByDescending(x => x.Year)
					.ToList();

                var userInfo = UserManager.GetJsonInfo(session.User);
                var themeName = userInfo?.DiaryTheme;
                var diaryTheme = DiaryThemeManager.GetTheme(themeName);

                Model.DiaryName = diaryName;
				Model.DateGroup = dateGroup;
                Model.Theme = diaryTheme;
                return View["diary/jkwDiaryShowDates", Model];
			};
			#endregion

			#region Get /diary/search/{diaryName}
			Get["/diary/search/{diaryName}"] = _ =>
			{
				if (!session.IsLogin)
					return View["diary/jkwDiaryRequireLogin", Model];

				string diaryName = _.diaryName;
				bool withSecure = session.User.DiaryName == diaryName;
                //DiaryManager.

                var userInfo = UserManager.GetJsonInfo(session.User);
                var themeName = userInfo?.DiaryTheme;
                var diaryTheme = DiaryThemeManager.GetTheme(themeName);

                Model.Theme = diaryTheme;

                return View["diary/jkwDiarySearch", Model];
			};
			#endregion

			#endregion

			#region Write Diary

			#region Get /diary/write/{diaryName}
			Get["/diary/write/{diaryName}"] = _ =>
			{
				if (!session.IsLogin)
					return View["diary/jkwDiaryRequireLogin", Model];

				string diaryName = _.diaryName;
				if (session.User.DiaryName != diaryName)
					return View["diary/jkwDiarySomethingWrong", Model];

                var userInfo = UserManager.GetJsonInfo(session.User);
                var themeName = userInfo.DiaryTheme;
                var diaryTheme = DiaryThemeManager.GetTheme(themeName);

                Model.Date = DateTime.Today;
				Model.DayOfWeek = DateTime.Today.GetWeekday(DateLanguage.KR, WeekdayFormat.D);
				Model.DiaryName = diaryName;
                Model.Theme = diaryTheme;
				return View["diary/jkwDiaryWrite", Model];
			};
			#endregion

			#region Post /diary/write
			Post["/diary/write"] = _ =>
			{
				if (!session.IsLogin)
					return "로그인을 해주세요.";

				string diaryName = Request.Form["diaryName"];
				DateTime date = ((string)Request.Form["date"]).ToDate();
				string text = Request.Form["text"];

				if (session.User.DiaryName != diaryName)
					return "본인 다이어리가 아닙니다.";

				try
				{
					DiaryManager.WriteDiary(diaryName, date, text, isSecure: false);
				}
				catch (Exception ex)
				{
					ex.WriteLog();
					return ex.Message;
				}

				return "success";
			};
			#endregion

			#endregion

			#region Modify Diary

			#region Get /diary/modify/{diaryName}/{date}
			Get["/diary/modify/{diaryName}/{date}"] = _ =>
			{
				if (!session.IsLogin)
					return View["diary/jkwDiaryRequireLogin", Model];

				string diaryName = _.diaryName;
				DateTime date = ((string)_.date).ToDate();

				if (session.User.DiaryName != diaryName)
					return View["diary/jkwDiarySomethingWrong", Model];

				var diaryList = DiaryManager.GetDiary(diaryName, date, withSecure: true);

                var userInfo = UserManager.GetJsonInfo(session.User);
                var themeName = userInfo.DiaryTheme;
                var diaryTheme = DiaryThemeManager.GetTheme(themeName);

                Model.DiaryName = diaryName;
				Model.Date = date;
				Model.DayOfWeek = date.GetWeekday(DateLanguage.KR, WeekdayFormat.D);
				Model.DiaryList = diaryList;
                Model.Theme = diaryTheme;

                return View["diary/jkwDiaryModify", Model];
			};
			#endregion

			#region Post /diary/modify
			Post["/diary/modify"] = _ =>
			{
				if (!session.IsLogin)
					return "로그인을 해주세요.";

				string diaryName = Request.Form["diaryName"];
				DateTime date = ((string)Request.Form["date"]).ToDate();

				if (session.User.DiaryName != diaryName)
					return "본인 다이어리가 아닙니다.";

				try
				{
					string json = Request.Form["diaryList"];
					var diaryList = ((JArray)JsonConvert.DeserializeObject(json))
						.Select(x => new
						{
							Index = ((string)((dynamic)x).Index).ToInt(),
							Text = (string)((dynamic)x).Text
						});

					foreach (var diary in diaryList)
					{
						if (string.IsNullOrEmpty(diary.Text.Trim()))
						{
							DiaryManager.DeleteDiary(diaryName, date, diary.Index);
						}
						else
						{
							DiaryManager.ModifyDiary(diaryName, date, diary.Index, diary.Text);
						}
					}
				}
				catch (Exception ex)
				{
					ex.WriteLog();
					return ex.Message;
				}

				return "success";
			};
			#endregion

			#endregion

			#region Search Diary

			#region Post /diary/search
			Post["/diary/search"] = _ =>
			{
				dynamic result = new ExpandoObject();
				result.result = false;
				result.Message = "";

				if (!session.IsLogin)
				{
					result.Message = "로그인을 해주세요.";
					return JsonConvert.SerializeObject(result);
				}

				var diaryName = session.User.DiaryName;

				var beginDateNum = ((string)Request.Form["beginDate"]).ToInt();
				var endDateNum = ((string)Request.Form["endDate"]).ToInt();
				var weekday = ((string)Request.Form["weekday"]).ToInt();
				var searchText = (string)Request.Form["searchText"];
				var beginDate = beginDateNum >= 2000 && beginDateNum <= 2030 ? new DateTime(beginDateNum, 1, 1) : beginDateNum.ToDate();
				var endDate = endDateNum >= 2000 && endDateNum <= 2030 ? new DateTime(endDateNum, 12, 31) : endDateNum.ToDate();

				if (beginDateNum == 0 || endDateNum == 0)
				{
					result.Message = "날짜를 확인해주세요.";
					return JsonConvert.SerializeObject(result);
				}

				try
				{
					var diaryList = DiaryManager.SearchDiary(diaryName, beginDate, endDate, weekday, searchText, withSecure: false);
					result.result = true;
					result.DiaryList = diaryList.Select(x => new
					{
						Date = x.Date.ToString("yyyy.MM.dd"),
						Weekday = x.Date.GetWeekday(DateLanguage.KR, WeekdayFormat.D),
						x.Text
					})
					.ToList();
					return JsonConvert.SerializeObject(result);
				}
				catch (Exception ex)
				{
					ex.WriteLog();
					result.Message = ex.Message;
					return JsonConvert.SerializeObject(result);
				}
			};
            #endregion

            #endregion

            #region Theme
            Get["/diary/theme"] = _ =>
            {
                //if (!session.IsLogin)
                //    return View["diary/jkwDiaryRequireLogin", Model];

                var userInfo = UserManager.GetJsonInfo(session?.User);

                DiaryThemeManager.Reload();
                var themes = DiaryThemeManager.GetAllThemes();
                Model.Themes = themes;
                Model.ThemeTitles = DiaryTheme.Titles();
                Model.CurrentThemeName = userInfo?.DiaryTheme ?? "";
                return View["diary/jkwDiaryTheme", Model];
            };

            Post["/diary/theme/reload"] = _ =>
            {
                DiaryThemeManager.Reload();
                return "";
            };

            Post["/diary/theme/set"] = _ =>
            {
                var userInfo = UserManager.GetJsonInfo(session?.User);
                userInfo.DiaryTheme = Request.Form["themeName"];
                UserManager.SaveUserInfo();
                return "";
            };

            Post["/diary/theme/save"] = _ =>
            {
                if (!session.IsLogin)
                    return "";

                var themeText = (string)Request.Form["theme"];
                var theme = JsonConvert.DeserializeObject<DiaryTheme>(themeText);
                theme.Owner = session?.User?.Email ?? "";
                DiaryThemeManager.AddOrUpdate(theme);
                return "";
            };
            #endregion
        }
	}
}
