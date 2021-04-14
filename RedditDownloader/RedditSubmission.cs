using CsvHelper.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedditDownloader
{
    public class RedditSubmission
    {
        public string ID { get; set; }
        public string Title { get; set; }
        public string SelfText { get; set; }
        public string Subreddit { get; set; }
        public string URL { get; set; }
        public long DateTimeCreatedUTC { get; set; }
        public string Comments { get; set; }
    }

    public class RedditSubmissionMap: ClassMap<RedditSubmission>
    {
        public RedditSubmissionMap()
        {
            Map(m => m.ID);
            Map(m => m.Title);
            Map(m => m.SelfText);
            Map(m => m.Subreddit);
            Map(m => m.URL);
            Map(m => m.DateTimeCreatedUTC);
            Map(m => m.Comments).Ignore(); //When using custom map
        }
    }
}
