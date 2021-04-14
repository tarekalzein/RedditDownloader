using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedditDownloader
{
    public class RedditComment
    {
        public string ID { get; set; }
        public string Body { get; set; }
        public string Subreddit { get; set; }
        public long DateTimeCreatedUTC { get; set; }
        public string URL { get; set; }
    }
}
