using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RedditDownloader
{
    class Program
    {
        static void Main(string[] args)
        {
            string subreddit = "";
            //string url = "https://api.pushshift.io/reddit/submission/search?subreddit={0}&limit=1000&sort=desc&before={1}&after={2}";
            do
            {
                Console.WriteLine("Enter the subreddit to download");
                var temp = Console.ReadLine();
                if (!String.IsNullOrWhiteSpace(temp))
                {
                    subreddit = temp;
                    break;
                }
            }
            while (true);
            Request r = new Request(subreddit, new DateTime(2021,4,13), DateTime.Now );
            r.GetAllData();

            //do
            //{
            //    Console.WriteLine("Enter the start date ");
            //    var temp = Console.ReadLine();
            //    if (!String.IsNullOrWhiteSpace(temp))
            //    {
            //        subreddit = temp;
            //        break;
            //    }
            //}
            //while (true);
        }        
    }

    class Request
    {
        string filePath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) +"\\";

        static string BASE_URL = "https://api.pushshift.io/reddit/{0}/search?subreddit={1}&limit=1000&sort=desc&before={2}&after={3}";

        //CSV writer configuration
        static CsvConfiguration conf = new CsvConfiguration(CultureInfo.CurrentCulture)
        {
            NewLine = Environment.NewLine,
            Delimiter = "|",
            HasHeaderRecord = true,
        };

        public string Subreddit { get;  }
        public  DateTime StartDateTime { get;  }
        public DateTime EndDateTime { get; }

        public Request(string subreddit, DateTime startDateTime, DateTime endDateTime)
        {
            Subreddit = subreddit;
            StartDateTime = startDateTime;
            EndDateTime = endDateTime;
        }

        public void GetAllData()
        {
            var submissionsTask = Task.Run(() => GetSubmissions());
            var commentsTask = Task.Run(() => GetComments());

            Task.WaitAll(submissionsTask, commentsTask);

            List<RedditSubmission> submissions = submissionsTask.Result;
            List<RedditComment> comments = commentsTask.Result;

            SaveSubmissionsToCSV(submissions, "submissions.csv");
            Console.WriteLine("Submissions were save to submissions.csv");

            SaveCommentsToCSV(comments);
            Console.WriteLine("Comments were save to comments.csv");

            //Code to merge posts and comments and save them in file
            List<RedditSubmission> submissionsWithComments = new List<RedditSubmission>(submissions);
            Console.WriteLine(submissionsWithComments.Count());
            foreach(RedditComment comment in comments)
            {
                RedditSubmission rs = submissionsWithComments.Where(x => x.ID.Equals(comment.ID)).SingleOrDefault();
                Console.WriteLine(rs.ID);
            }
            Console.WriteLine($"First Post: \n {submissionsWithComments[0].ID} | {submissionsWithComments[0].Title} \n comments: {submissionsWithComments[0].Comments}");

        }

        private void SaveSubmissionsToCSV(List<RedditSubmission> submissions,string fileName)
        {            
            using(var stream = File.Open(filePath + fileName,FileMode.Create))
            using(var writer = new StreamWriter(stream,Encoding.UTF8))
            using(var csvWriter = new CsvWriter(writer, conf))
            {
                csvWriter.WriteHeader<RedditSubmission>();
                csvWriter.NextRecord();
                foreach(RedditSubmission submission in submissions)
                {
                    submission.Title = submission.Title.Replace("\n", " ").Replace(";", "");
                    submission.SelfText = submission.SelfText.Replace("\n", " ").Replace(";", "");
                }
                csvWriter.WriteRecords(submissions);
                writer.Flush();
            }
        }

        private void SaveCommentsToCSV(List<RedditComment> comments)
        {
            
            using (var stream = File.Open(filePath + "comments.csv", FileMode.Create))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            using (var csvWriter = new CsvWriter(writer, conf))
            {
                csvWriter.WriteHeader<RedditComment>();
                csvWriter.NextRecord();
                foreach (RedditComment comment in comments)
                {
                    comment.Body = comment.Body.Replace("\n", " ").Replace(";", "");
                }
                csvWriter.WriteRecords(comments);
                writer.Flush();
            }
        }

        private List<RedditSubmission> GetSubmissions()
        {
            int count = 0;

            long startDateTimeUnix = ToUnixTime(StartDateTime);
            long endDateTimeUnix = ToUnixTime(EndDateTime);

            List<RedditSubmission> submissions = new List<RedditSubmission>();

            WebClient wc = new WebClient();  
            
            while (true)
            {
                string new_url = string.Format(
                    BASE_URL,
                    "submission",
                    Subreddit,
                    endDateTimeUnix,
                    startDateTimeUnix
                    );

                string json = wc.DownloadString(new_url);
                Thread.Sleep(100);
                JObject data = JObject.Parse(json);

                JToken token = data["data"];
                if (token == null)
                    break;

                if (data["data"].Count() == 0)
                    break;

                foreach (var item in data["data"])
                {
                    count += 1;
                    endDateTimeUnix = (long)item["created_utc"] - 1;
                    submissions.Add(
                        new RedditSubmission
                        {
                            ID=(string)item["id"],
                            Title = (string)item["title"],
                            SelfText = (string)(item["selftext"] == null ? "" : item["selftext"]),
                            Subreddit = (string)item["subreddit"],
                            DateTimeCreatedUTC = (long)item["created_utc"],
                            URL = (string)item["url"]
                        });
                }
                Console.WriteLine($"Saved {count} submissions through {FromUnixTime(endDateTimeUnix).Date}");
            }
            wc.Dispose();
            Console.WriteLine($"No of submissions is {count}");
            return submissions;
        }       

        private List<RedditComment> GetComments()
        {
            int count = 0;

            List<RedditComment> comments = new List<RedditComment>();

            long startDateTimeUnix = ToUnixTime(StartDateTime);
            long endDateTimeUnix = ToUnixTime(EndDateTime);

            using (WebClient wc = new WebClient())
            {
                while (true)
                {
                    string new_url = string.Format(
                        BASE_URL,
                        "comment",
                        Subreddit,
                        endDateTimeUnix,
                        startDateTimeUnix
                        );

                    string json = wc.DownloadString(new_url);
                    Thread.Sleep(100);
                    JObject data = JObject.Parse(json);

                    JToken token = data["data"];
                    if (token == null)
                        break;

                    if (data["data"].Count() == 0)
                        break;


                    foreach (var item in data["data"])
                    {
                        count += 1;
                        endDateTimeUnix = (long)item["created_utc"] - 1;
                        var id_temp = (string)item["link_id"];
                        var id = id_temp.Split('_').Last();

                        comments.Add(
                            new RedditComment
                            {
                                Body = (string)item["body"],
                                ID = id,
                                Subreddit = (string)item["subreddit"],
                                DateTimeCreatedUTC = (long)item["created_utc"],
                                URL = (string)item["url"]
                            });
                    }
                    Console.WriteLine($"Saved {count} comments through {FromUnixTime(endDateTimeUnix).Date}");

                }
            }
            Console.WriteLine($"No of comments is {count}");
            return comments;
        }
        
        //Converts UTC DateTime to Unix Timestamp
        private static long ToUnixTime(DateTime dateTime) => ((DateTimeOffset)dateTime).ToUnixTimeSeconds();

        //Gets UTC time from epoch
        private static DateTime FromUnixTime(long unixTime) => DateTimeOffset.FromUnixTimeSeconds(unixTime).DateTime;

    }
}
