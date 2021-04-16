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
            string[] subreddits;
            DateTime endDateTime;
            DateTime startDateTime;
            

            do
            {
                Console.WriteLine("Enter the subreddits to download, use the following format: subreddit1,subreddit2,subreddit3");
                var temp = Console.ReadLine();                
                if (!String.IsNullOrWhiteSpace(temp))
                {
                    temp = temp.Replace(" ", "");
                    subreddits = temp.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);                    
                    break;
                }
                else
                {
                    Console.WriteLine("Please enter a valid subreddit/multiple subreddits");
                }
            }
            while (true);

            do
            {
                Console.WriteLine("Enter the start date (Date to start scraping for data), Leave empty to fetch all data without date time limit");
                var temp = Console.ReadLine();
                if (String.IsNullOrWhiteSpace(temp))
                {
                    startDateTime = new DateTime(1990, 01, 01);//no reddit by that time.
                    break;
                }
                if (!String.IsNullOrWhiteSpace(temp))
                {
                    if(DateTime.TryParse(temp, out startDateTime))
                        break;
                    else
                        Console.WriteLine("Please enter a valid start date");
                }
            }
            while (true);

            do
            {
                Console.WriteLine("Enter the end date (Date to stop data scraping), Leave empty set end date and time to {Now}");
                var temp = Console.ReadLine();
                if (String.IsNullOrWhiteSpace(temp))
                {
                    endDateTime = DateTime.Now;
                    break;
                }
                else if (!String.IsNullOrWhiteSpace(temp))
                {
                    if (DateTime.TryParse(temp, out endDateTime))
                        break;
                    else
                        Console.WriteLine("Please enter a valid end date");
                }
            }
            while (true);

            Console.WriteLine($"\n" +
                $"Searching for submissions between {startDateTime.ToString("d")} and {endDateTime.ToString("d")} from the following subreddit(s):");
            foreach (string s in subreddits)
            {
                Console.WriteLine($"<{s}>");
            }
            Console.WriteLine(Environment.NewLine);


            Request r = new Request(subreddits, startDateTime, endDateTime);
            r.GetAllData();            
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

        public string[] Subreddits { get;  }
        public  DateTime StartDateTime { get;  }
        public DateTime EndDateTime { get; }

        public Request(string[] subreddits, DateTime startDateTime, DateTime endDateTime)
        {
            Subreddits = subreddits;
            StartDateTime = startDateTime;
            EndDateTime = endDateTime;
        }

        public void GetAllData()
        {
            List<RedditSubmission> submissions = new List<RedditSubmission>();
            List<RedditComment> comments = new List<RedditComment>();

            foreach (string subreddit in Subreddits)
            {
                Console.WriteLine($"Fetching Submissions From {subreddit}");
                var submissionsTask = Task.Run(() => GetSubmissions(subreddit));

                Console.WriteLine($"Fetching Comments From {subreddit}");
                var commentsTask = Task.Run(() => GetComments(subreddit));

                Task.WaitAll(submissionsTask, commentsTask);

                if (submissionsTask.Result.Count > 0)
                    submissions.AddRange(submissionsTask.Result);

                if (commentsTask.Result.Count > 0)
                    comments.AddRange(commentsTask.Result);
            }


            SaveSubmissionsToCSV(submissions, "submissions.csv");
            Console.WriteLine($"{submissions.Count} submissions are saved to submissions.csv");

            SaveCommentsToCSV(comments);
            Console.WriteLine($"{comments.Count} Comments are saved to comments.csv");

            //Code to merge posts and comments and save them in file
            List<RedditSubmission> submissionsWithComments = new List<RedditSubmission>(submissions);
            foreach(RedditComment comment in comments)
            {
                RedditSubmission rs = submissionsWithComments.Where(x => x.ID.Equals(comment.ID)).SingleOrDefault();
                if (rs != null)
                {
                    rs.Comments += comment.Body + " ";
                }
            }
            SaveSubmissionsToCSV(submissionsWithComments, "submissionsWithComments.csv");
            Console.WriteLine($"{submissionsWithComments.Count} submissions with their comments are merged and saved to submissionsWithComments.csv");

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

        private List<RedditSubmission> GetSubmissions(string subreddit)
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
                    subreddit,
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

        private List<RedditComment> GetComments(string subreddit)
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
                        subreddit,
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
