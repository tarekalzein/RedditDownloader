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
    public class Request
    {
        string filePath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "\\";

        static string BASE_URL = "https://api.pushshift.io/reddit/{0}/search?subreddit={1}&limit=1000&sort=desc&before={2}&after={3}";

        List<string> summary = new List<string>();
        List<string> errorList = new List<string>();

        //CSV writer configuration
        static CsvConfiguration conf = new CsvConfiguration(CultureInfo.CurrentCulture)
        {
            NewLine = Environment.NewLine,
            Delimiter = "|",
            HasHeaderRecord = true,
        };

        public string[] Subreddits { get; }
        public DateTime StartDateTime { get; }
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

            Parallel.ForEach(Subreddits, subreddit =>
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
            });


            SaveSubmissionsToCSV(submissions, "submissions.csv");
            Console.WriteLine($"{submissions.Count} submissions are saved to submissions.csv");

            SaveCommentsToCSV(comments);
            Console.WriteLine($"{comments.Count} Comments are saved to comments.csv");

            //Code to merge posts and comments and save them in file
            List<RedditSubmission> submissionsWithComments = new List<RedditSubmission>(submissions);
            foreach (RedditComment comment in comments)
            {
                RedditSubmission rs = submissionsWithComments.Where(x => x.ID.Equals(comment.ID)).SingleOrDefault();
                if (rs != null)
                {
                    rs.Comments += comment.Body + " ";
                }
            }
            SaveSubmissionsToCSV(submissionsWithComments, "submissionsWithComments.csv");
            Console.WriteLine($"{submissionsWithComments.Count} submissions with their comments are merged and saved to submissionsWithComments.csv");


            Console.WriteLine(">>>>Task is Done.<<<<");
            Console.WriteLine("\n");
            summary.ForEach(x => Console.WriteLine(x));

            Console.WriteLine("\n");
            Console.WriteLine("Error List:");
            errorList.ForEach(x => Console.WriteLine(x));

        }

        private void SaveSubmissionsToCSV(List<RedditSubmission> submissions, string fileName)
        {
            using (var stream = File.Open(filePath + fileName, FileMode.Create))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            using (var csvWriter = new CsvWriter(writer, conf))
            {
                csvWriter.WriteHeader<RedditSubmission>();
                csvWriter.NextRecord();
                foreach (RedditSubmission submission in submissions)
                {
                    submission.Title = submission.Title.Replace("\n", " ").Replace(";", "");
                    submission.SelfText = submission.SelfText.Replace("\n", " ").Replace(";", "");
                }
                try
                {
                    csvWriter.WriteRecords(submissions);

                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error saving data to csv: {e.Message}");
                    errorList.Add("I/O error:" + e.Message);
                }
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

            using (WebClient wc = new WebClient())
            {
                while (true)
                {
                    string new_url = string.Format(
                        BASE_URL,
                        "submission",
                        subreddit,
                        endDateTimeUnix,
                        startDateTimeUnix
                        );
                    try
                    {
                        string json = wc.DownloadString(new_url);
                        Thread.Sleep(1000);
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
                                    ID = (string)item["id"],
                                    Title = (string)item["title"],
                                    SelfText = (string)(item["selftext"] == null ? "" : item["selftext"]),
                                    Subreddit = (string)item["subreddit"],
                                    DateTimeCreatedUTC = (long)item["created_utc"],
                                    URL = (string)item["url"]
                                });
                        }
                        Console.WriteLine($"Saved {count} submissions from {subreddit} through {FromUnixTime(endDateTimeUnix).Date}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error getting data from {new_url}: {e.Message}. Now retrying");
                        errorList.Add($"Error fetching data: from {new_url}, error message: {e.Message}");

                    }
                }
            }
            summary.Add($"{count} submissions fetched from {subreddit}");
            Console.WriteLine($"No of submissions fetched from {subreddit} is {count}");
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

                    try
                    {
                        string json = wc.DownloadString(new_url);
                        Thread.Sleep(1000);
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
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error getting data from {new_url}: {e.Message}. Now retrying");
                        errorList.Add($"Error fetching data: from {new_url}, error message: {e.Message}");
                    }
                    Console.WriteLine($"Saved {count} comments from {subreddit} through {FromUnixTime(endDateTimeUnix).Date}");
                }
            }
            Console.WriteLine($"No of comments fetched from {subreddit} is {count}");
            summary.Add($"{count} comments fetched from {subreddit}");
            return comments;
        }

        //Converts UTC DateTime to Unix Timestamp
        private static long ToUnixTime(DateTime dateTime) => ((DateTimeOffset)dateTime).ToUnixTimeSeconds();

        //Gets UTC time from epoch
        private static DateTime FromUnixTime(long unixTime) => DateTimeOffset.FromUnixTimeSeconds(unixTime).DateTime;

    }
}
