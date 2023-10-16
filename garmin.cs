using System;
using System.Net;
using System.Text.Json;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Globalization;

namespace Garmin_to_Gpx
{

    // necessary (?) for JSON deserialization for auth
    class AuthResponse 
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public string scope { get; set; }
        public string jti { get; set; }
        public int expires_in { get; set; }

    }

    // necessary (?) for JSON deserialization of activity details
    class CActivityDetails 
    {
        public class CDescriptor 
        {
            public class CUnit 
            {
                public int id { get; set; }
                public string key { get; set; }
                public float factor { get; set; }
            }
            public int metricsIndex { get; set; }
            public string key { get; set; }
            public CUnit unit { get; set; }
        }

        public class CDetails 
        {
            public float?[] metrics { get; set; }
        }

        public class CGeo 
        {
            public class CPoint 
            {
                public float? lat { get; set; }
                public float? lon { get; set; }
                public float? altitude { get; set; }
                public long? time { get; set; }
                public bool? timerStart { get; set; }
                public bool? timerStop { get; set; }
                public float? distanceFromPreviousPoint { get; set; }
                public float? distanceInMeters { get; set; }
                public float? speed { get; set; }
                public float? cumulativeAscent { get; set; }
                public float? cumulativeDescent { get; set; }
                public bool? extendendCoordinate { get; set; }
                public bool? valid { get; set; }
            }
            public CPoint startPoint { get; set; }
            public CPoint endPoint { get; set; }
            public float minLat { get; set; }
            public float maxLat { get; set; }
            public float minLon { get; set; }
            public float maxLon { get; set; }
            public CPoint[] polyline { get; set; }
        }        
        public long activityId { get; set; }
        public int measurementCount { get; set; }
        public int metricsCount { get; set; }
        public string? heartRateDTO { get; set; }
        public bool detailsAvailable { get; set; }
        public CDescriptor[] metricDescriptors { get; set; }
        public CDetails[] activityDetailMetrics { get; set; }
        public CGeo geoPolylineDTO { get; set; }
    }

    class Program
    {
        static int Main(string[] args)
        {        
            // quick and dirty way to pass command line argument if run in debug
#if DEBUG
            args = new[] { "https://connect.garmin.com/modern/activity/xxyyzz" };
#endif

            if (args.Length < 1) 
            {
                Console.WriteLine("Please provide activity URL to convert");
                return 1;
            }
            Console.WriteLine("Analyzing activity " + args[0]);
            string[] arg_tokens = args[0].Split("/");
            string act_id;
            if (arg_tokens.Length < 6) 
            {
                return 1;
            }
            else 
            {
                act_id = arg_tokens[5];
            }
            Console.WriteLine("Id: " + act_id);

            CookieContainer cookies = new CookieContainer();
            
            // TODO are cookies actually necessary for following requests?
            Console.WriteLine("First GET to set cookies");
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(args[0]);
            req.Method = "GET";
            req.CookieContainer = cookies;
            CookieCollection retCookies = ((HttpWebResponse)req.GetResponse()).Cookies;
            foreach (Cookie c in retCookies) 
            {
                cookies.Add(c);
            }
            if (cookies.Count == 0) 
            {
                return 1;
            }

            // Mandatory step
	        Console.WriteLine("Get public auth");
            req = (HttpWebRequest)WebRequest.Create("https://connect.garmin.com/services/auth/token/public");
            req.Method = "POST";
            req.CookieContainer = cookies;
            HttpWebResponse respBody = (HttpWebResponse)req.GetResponse();
            string authCode;
            using (var reader = new System.IO.StreamReader(respBody.GetResponseStream(), Encoding.UTF8))
            {
                string responseText = reader.ReadToEnd();
                AuthResponse jsonObj = JsonSerializer.Deserialize<AuthResponse>(responseText);
                authCode = jsonObj.access_token;
            }
	        Console.WriteLine("Got token " + authCode);

	        Console.WriteLine("Get activity details:");
            req = (HttpWebRequest)WebRequest.Create("https://connect.garmin.com/activity-service/activity/" + act_id + "/details");
            req.Method = "GET";
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli;
            req.CookieContainer = cookies;

            // TODO try to strip headers to bare mininum
            req.Headers.Add("User-Agent: Mozilla/5.0");
            req.Headers.Add("Accept: application/json, text/javascript, */*; q=0.01");
            req.Headers.Add("Accept-Language: en-US,en;q=0.5");
            req.Headers.Add("Accept-Encoding: gzip, deflate, br");
            req.Headers.Add("NK: NT");
            req.Headers.Add("X-app-ver: 4.72.1.2");
            req.Headers.Add("X-lang: it-IT");
            req.Headers.Add("DI-Backend: connectapi.garmin.com");
            req.Headers.Add("X-Requested-With: XMLHttpRequest");
            req.Headers.Add("DNT: 1");
            req.Headers.Add("Connection: keep-alive");
            req.Headers.Add("Referer: " + args[0]);
            req.Headers.Add("Sec-Fetch-Dest: empty");
            req.Headers.Add("Sec-Fetch-Mode: cors");
            req.Headers.Add("Sec-Fetch-Site: same-origin");
            req.Headers.Add("Authorization: Bearer " + authCode);
            respBody = (HttpWebResponse)req.GetResponse();

            List<Tuple<float?, float?, float?>> gpxData = new List<Tuple<float?, float?, float?>>();

            using (var reader = new System.IO.StreamReader(respBody.GetResponseStream(), Encoding.UTF8))
            {
                string responseText = reader.ReadToEnd();
                try 
                {
                    CActivityDetails jsonObj = JsonSerializer.Deserialize<CActivityDetails>(responseText);
                    string[] gpxLabels = { "directLatitude", "directLongitude", "directElevation" };
                    int[] gpxIndices = { -1, -1, -1 };
                    foreach (CActivityDetails.CDescriptor met in jsonObj.metricDescriptors) 
                    {
                        for (int i = 0; i < 3; i++) 
                        {
                            if (gpxLabels[i] == met.key) 
                            {
                                gpxIndices[i] = met.metricsIndex;
                            }
                        }
                    }
                    if (gpxIndices[0] < 0 || gpxIndices[1] < 0 || gpxIndices[2] < 0) 
                    {
                        return 1;
                    }
                    foreach (CActivityDetails.CDetails det in jsonObj.activityDetailMetrics) 
                    {
                        gpxData.Add(Tuple.Create<float?, float?, float?> ( 
                            det.metrics[gpxIndices[0]],
                            det.metrics[gpxIndices[1]],
                            det.metrics[gpxIndices[2]]
                        ));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return 1;
                }

            }

            // This line is necessary to avoid serializing float numbers with "," as decimal separator in European locales
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-us");
            using (StreamWriter outputFile = new StreamWriter(act_id + ".gpx"))
            {
                outputFile.Write(@"
<?xml version=""1.0"" encoding=""UTF-8""?>
<gpx creator="""" version=""1.1""
  xsi:schemaLocation=""http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/11.xsd""
  xmlns=""http://www.topografix.com/GPX/1/1""
  xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:ns2=""http://www.garmin.com/xmlschemas/GpxExtensions/v3"">
  <metadata>
    <name>$1</name>
  </metadata>
  <trk>
  ");
                foreach (Tuple<float?, float?, float?> point in gpxData)
                {
                    outputFile.Write("    <trkpt lat=\"" + point.Item1.ToString() + "\" lon=\"" + point.Item2.ToString() + @""">
      <ele>" + point.Item3.ToString() + @"</ele>
    </trkpt>
    ");
                }
                outputFile.Write(@"  </trk>
</gpx>");
            }

            return 0;
        }
    }
}
