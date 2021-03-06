﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Extensions;
using System.Net;
using HtmlAgilityPack;

namespace helloJkw.Test
{
	#region Classes
	public class Match
	{
		public int Date { get; set; }
		public string Away { get; set; }
		public string Home { get; set; }
		public int AwayScore { get; set; }
		public int HomeScore { get; set; }
	}

	public class TeamMatch
	{
		public int Season { get; set; }
		public int Date { get; set; }
		public string Team { get; set; }
		public int Score { get; set; }
		public string OtherTeam { get; set; }
		public int OtherScore { get; set; }
		public bool IsHome { get; set; }
		public bool IsWin { get { return Score > OtherScore; } }
		public bool IsLose { get { return Score < OtherScore; } }
		public bool IsDraw { get { return Score == OtherScore; } }
	}

	public class ChartObject
	{
		public int Year { get; set; }
		public string DateList { get; set; }
		public List<Tuple<string, string>> TeamGBInfo { get; set; }
		public double MaximumGB { get; set; }
	}
	#endregion

	public static class KboMatch
	{
		static object _updateLock = new object();
		public static List<Match> _matchList { get; private set; }

		static string _filepathMatchHistory = @"jkw/project/kbo/kbochart/matchHistory.txt";
		static string _filepathSeasonInfo = @"jkw/project/kbo/kbochart/seasonInfo.txt";

		public static DateTime LastUpdateTime { get; private set; }
		public static List<Season> SeasonList;
		public static int RecentSeason { get { return _matchList.Max(t => t.Date).Year(); } }

		static KboMatch()
		{
			Reload();
		}

		public static void Reload()
		{
			SeasonList = GetSeasonList(_filepathSeasonInfo);
			_matchList = GetMatchList(_filepathMatchHistory);
			foreach (var season in SeasonList)
			{
				season.MatchList = null;
				season.TeamMatchList = null;
				season.StandingList = null;
				season.chartObject = null;
			}
			LastUpdateTime = DateTime.Now;
			Update(0);
		}

		#region Update (경기 결과가 변경되면 파일 저장한다.)
		public static void Update(int minute = 5)
		{
			lock (_updateLock)
			{
				if (minute > 0 && DateTime.Now.Subtract(LastUpdateTime).TotalMinutes < minute)
					return;
				LastUpdateTime = DateTime.Now;

				var beginDate = _matchList.Max(t => t.Date);
				var endDate = DateTime.Today.ToInt();
				Season updateSeason = null;
				int updateDate = 0;
				foreach (var date in beginDate.DateRange(endDate))
				{
					var season = SeasonList.Where(e => e.Year == date.Year()).FirstOrDefault();
					if (season == null) continue;
					if (date < season.BeginDate || date > season.EndDate) continue;

					// kbo website 에서 데이터 가져온다.
					var matchList = GetMatchList(date);
					// 두 MatchList 가 같다는 뜻은 경기 결과가 변한게 없다는 뜻.
					var currentMatchList = _matchList.Where(t => t.Date == date).ToList();
					if (currentMatchList.EqualMatchList(matchList)) continue;

					foreach (var match in currentMatchList)
					{
						Logger.Log("Remove match {Date}, {Away}, {Home}".WithVar(match));
						_matchList.Remove(match);
					}
					_matchList.AddRange(matchList);
					updateSeason = season;
					updateDate = updateDate == 0 ? date : Math.Min(updateDate, date);
				}

				// MatchList 가 뭔가 바뀐 경우다!
				if (updateSeason != null)
				{
					Logger.Log("Update after {updateDate}".WithVar(new { updateDate }));
					updateSeason.GetStandingList(true, updateDate);
					updateSeason.chartObject = null;
					SaveMatchList(_filepathMatchHistory);
				}
			}
		}
		#endregion

		#region SeasonList (from json file)
		public static List<Season> GetSeasonList(string filepath)
		{
			var seasonInfoJson = File.ReadAllText(filepath, Encoding.UTF8);
			return JsonConvert.DeserializeObject<List<Season>>(seasonInfoJson);
			//var seasonInfoJson = JObject.Parse(File.ReadAllText(filepath, Encoding.UTF8));

			//return seasonInfoJson["season"].Children()
			//	.Select(e => JsonConvert.DeserializeObject<Season>(e.ToString()))
			//	.ToList();
		}
		#endregion

		#region Save MatchList
		/// <summary>
		/// MatchList 를 json 형태로 저장
		/// </summary>
		/// <param name="filepath"></param>
		static void SaveMatchList(string filepath)
		{
#if (DEBUG)
			return;
#endif
			lock (_updateLock)
			{
				var resultString = JsonConvert.SerializeObject(_matchList.OrderByDescending(e => e.Date))
					.RegexReplace(@"\}\,", "},\n  ");

				File.WriteAllText(filepath, resultString, Encoding.UTF8);
			}
		}
		#endregion

		#region ChartObject
		/// <summary>
		/// 차트를 그리기 위한 객체
		/// 다 string 형태로 바꿔서 전달한다.
		/// </summary>
		/// <param name="season"></param>
		/// <returns></returns>
		public static ChartObject GetChartObject(int year, int beginDate = 0, int endDate = 0)
		{
			var season = SeasonList.Where(e => e.Year == year).FirstOrDefault();
			if (season == null) season = SeasonList.Last();
			return season.GetChartObject(beginDate, endDate);
		}
		#endregion

		#region Match (from json file)
		/// <summary>
		/// json 형식으로 구성된 file 에서 게임(Match) 정보를 읽어온다.
		/// </summary>
		/// <param name="filepath"></param>
		/// <returns></returns>
		public static List<Match> GetMatchList(string filepath)
		{
			var matchHistoryJson = File.ReadAllText(filepath, Encoding.UTF8);
			return JsonConvert.DeserializeObject<List<Match>>(matchHistoryJson);
		}
		#endregion

		#region Match (from kbo site)
		/// <summary>
		/// kbo 홈페이지에서 해당일자에 해당하는 게임정보를 크롤링해온다.
		/// </summary>
		/// <param name="date"></param>
		/// <returns></returns>
		public static List<Match> GetMatchList(int date)
		{
			var matchList = new List<Match>();
			try
			{
				#region ReadHtml
				var url = "http://www.koreabaseball.com/Schedule/ScoreBoard/ScoreBoard.aspx?gameDate={0}".With(date);
				var webRequest = WebRequest.Create(url);
				var response = webRequest.GetResponse();
				string html;
				using (var reader = new StreamReader(response.GetResponseStream()))
				{
					html = reader.ReadToEnd();
				}
				#endregion

				var htmlDoc = new HtmlDocument();
				htmlDoc.LoadHtml(html);

				// 경기가 없으면 스킵!
				if (htmlDoc.DocumentNode.SelectNodes("//div[@class='smsScore']") == null)
					return matchList;

				matchList = htmlDoc.DocumentNode.SelectNodes("//div[@class='smsScore']")
					.Where(e => e.SelectSingleNode("div/strong[@class='flag']/span").InnerText != "경기전")
					.Select(e =>
					{
						var away = e.SelectSingleNode("div[@class='score_wrap']/p[@class='leftTeam']");
						var home = e.SelectSingleNode("div[@class='score_wrap']/p[@class='rightTeam']");
						return new Match
						{
							Date = date,
							Away = away.SelectSingleNode("strong").InnerText,
							AwayScore = away.SelectSingleNode("em").InnerText.ToInt(),
							Home = home.SelectSingleNode("strong").InnerText,
							HomeScore = home.SelectSingleNode("em").InnerText.ToInt(),
						};
					})
					.ToList();
			}
			catch
			{
			}
			return matchList;
		}
		#endregion

		#region TeamMatch
		/// <summary>
		/// 각 팀별 게임을 구한다.
		/// </summary>
		/// <param name="matchList"></param>
		/// <returns></returns>
		public static List<TeamMatch> GetTeamMatchList(this IEnumerable<Match> matchList)
		{
			var teamMatchList = new List<TeamMatch>();
			foreach (var match in matchList)
			{
				teamMatchList.Add(new TeamMatch
				{
					Date = match.Date,
					Team = match.Away,
					Score = match.AwayScore,
					OtherTeam = match.Home,
					OtherScore = match.HomeScore,
					IsHome = false
				});
				teamMatchList.Add(new TeamMatch
				{
					Date = match.Date,
					Team = match.Home,
					Score = match.HomeScore,
					OtherTeam = match.Away,
					OtherScore = match.AwayScore,
					IsHome = true
				});
			}
			return teamMatchList.OrderByDescending(e => e.Date).ToList();
		}
		#endregion

		#region Standing
		/// <summary>
		/// 팀 순위, 승률, 게임차 등을 구한다.
		/// 최적화를 상당히 하였다.
		/// 마지막 update 이후의 데이터만 변경되도록 수정.
		/// </summary>
		/// <param name="teamMatchList"></param>
		/// <returns></returns>
		public static List<Standing> GetStandingList(this IEnumerable<TeamMatch> teamMatchList, int updateDate = 0)
		{
			Season season = SeasonList.Where(e => e.Year == updateDate.Year()).FirstOrDefault();
			List<Standing> standingList = (season == null || season.StandingList == null) ? new List<Standing>() : season.StandingList;
			var teamList = teamMatchList.Select(e => e.Team).Distinct().ToList();
			var dateList = teamMatchList.Select(e => e.Date).Distinct().Where(e => season == null || season.StandingList == null || e >= updateDate).OrderBy(e => e).ToList();

			#region updateDate 이후 데이터 삭제
			var deleteList = standingList.Where(e => e.Date >= updateDate).ToList();
			foreach (var standing in deleteList)
			{
				Logger.Log("Remove {Date}, {Team}".WithVar(standing));
				standingList.Remove(standing);
			}
			#endregion

			foreach (var date in dateList)
			{
				var currStandingList = new List<Standing>();

				var yesterdayList = standingList.OrderBy(e => e.Date).GroupBy(e => e.Team).Select(e => e.Last());
				var currList = teamMatchList.Where(e => e.Date == date);

				#region 승, 무, 패, 승률
				foreach (var team in teamList)
				{
					var standing = new Standing { Date = date, Team = team };

					// 가장 최근 순위 기록을 구한다.
					var yesterday = yesterdayList.Where(e => e.Team == team).FirstOrDefault();
					standing.Win = yesterday != null ? yesterday.Win : 0;
					standing.Draw = yesterday != null ? yesterday.Draw : 0;
					standing.Lose = yesterday != null ? yesterday.Lose : 0;

					// 최근 기록에서 해당 경기 결과를 반영한다.
					// foreach 인 이유는 더블헤더 경기가 있기 때문에!
					foreach (var curr in currList.Where(t => t.Team == team))
					{
						standing.Win += (curr != null && curr.IsWin) ? 1 : 0;
						standing.Draw += (curr != null && curr.IsDraw) ? 1 : 0;
						standing.Lose += (curr != null && curr.IsLose) ? 1 : 0;
					}

					// 승률 = 승 / (승 + 패)  // 무승부는 무시! (2009년 무승부는 '패' 처리)
					standing.PCT = (standing.Win == 0)? 0 : ((double)standing.Win / (standing.Win + standing.Lose + (date.Year() == 2009 ? standing.Draw : 0)));

					currStandingList.Add(standing);
				}
				#endregion

				#region 순위
				foreach (var team in currStandingList)
				{
					team.Rank = currStandingList.Where(e => e.PCT > team.PCT).Count() + 1;
				}
				#endregion

				#region 승차 (게임차)
				var rank1 = currStandingList.Where(e => e.Rank == 1).First();
				foreach (var team in currStandingList)
				{
					if (date.Year() == 2009)
					{
						team.GB = team.Rank == 1 ? 0 : ((rank1.Win - team.Win) - ((rank1.Lose + rank1.Draw) - (team.Lose + team.Draw))) / 2.0;
					}
					else
					{
						team.GB = team.Rank == 1 ? 0 : ((rank1.Win - team.Win) - (rank1.Lose - team.Lose)) / 2.0;
					}
				}
				#endregion
				standingList.AddRange(currStandingList);
			}

			return standingList.OrderBy(e => e.Date).ThenBy(e => e.Rank).ToList();
		}
		#endregion

		#region EqualMatchList (두 Match 비교)
		public static bool EqualMatchList(this List<Match> match1, List<Match> match2)
		{
			// match1 은 항상 not null 이겠지만 그냥 이렇게 구현 함.
			if (match1 == null && match2 == null) return true;
			if (match1 == null || match2 == null) return false;
			if (match1.Count() != match2.Count()) return false;

			var orderedMatch1 = match1.OrderBy(e => e.Date).ThenBy(e => e.Home).ThenBy(e => e.Away);
			var orderedMatch2 = match2.OrderBy(e => e.Date).ThenBy(e => e.Home).ThenBy(e => e.Away);
			foreach (var pair in orderedMatch1.Zip(orderedMatch2, (m1, m2) => new { m1, m2 }))
			{
				if (pair.m1.Date != pair.m2.Date) return false;
				if (pair.m1.Home != pair.m2.Home) return false;
				if (pair.m1.Away != pair.m2.Away) return false;
				if (pair.m1.HomeScore != pair.m2.HomeScore) return false;
				if (pair.m1.AwayScore != pair.m2.AwayScore) return false;
			}

			return true;
		}
		#endregion
	}
}
