using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace HeatLamp
{
    public static class SetHeater
    {
        public static void HeatSet(string hostname, string set, string version, string lod)
        {
            try
            {
                string url = string.Format("http://{0}/sets/{1}/{2}/query/{3}/-9999,-9999,-9999/9999,9999,9999", hostname, set, version, lod);
                string result = GetJson(url);

                var cubes = JsonConvert.DeserializeObject<QueryResult>(result);

                if (cubes.status == "OK")
                {
                    foreach (var cube in cubes.result)
                    {
                        try
                        {
                            string cubeUrl = string.Format("http://{0}/sets/{1}/{2}/models/{3}/{4},{5},{6}", hostname, set, version, lod, cube[0], cube[1], cube[2]);
                            GetCube(cubeUrl);
                        }
                        catch
                        { }
                    }
                }
            }
            catch { }

        }

        private static string GetJson(string url)
        {
            WebClient webClient = new WebClient();
            return webClient.DownloadString(url);
        }

        private static void GetCube(string url)
        {
            WebClient webClient = new WebClient();
            var cubeData = webClient.DownloadData(url);
            Trace.TraceInformation("Got " + url);
            cubeData = null;
        }
    }

    public class QueryResult
    {
        public string status { get; set; }
        public List<int[]> result { get; set; }
    }
}
