using System;

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
}
