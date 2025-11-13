using AngleSharp.Html.Parser;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal class Program
{

    internal static readonly HttpClient client = new();
    internal static Config config = new();
    internal static int WAIT = 10;

    private static void Main(string[] args)
    {
        Console.WriteLine("MEVM v1.0\n");

        var now = DateTime.Now;
        var get = now.AddMonths(-1);

        var ym = get.ToString("yyyyMM");

        if (now.Day == 2)
            Console.WriteLine("注意: 本日は" + now.Day + "日です。");
        Console.WriteLine(ym + " で取得・作成していいですか？[y/n]");
        var res = Console.ReadLine();
        if (res == "y" || res == "Y")
        {
            for (var dt = new DateTime(get.Year, get.Month, 1); dt < new DateTime(now.Year, now.Month, 1); dt = dt.AddDays(1))
            {
                GetDB1d(dt);
                GetHypo(dt);
                Thread.Sleep(WAIT);
            }










        }

    }


    /// <summary>
    /// 震度データベース1日分取得
    /// </summary>
    /// <param name="getDate">取得日</param>
    public static void GetDB1d(DateTime getDate)
    {
        try
        {
            var savePath = config.SaveDir_EqData + "\\EQDB\\" + getDate.ToString("yyyy\\\\MM\\\\dd") + ".csv";
            var response = Regex.Unescape(client.GetStringAsync("https://www.data.jma.go.jp/svd/eqdb/data/shindo/api/api.php?mode=search&dateTimeF[]=" + getDate.ToString("yyyy-MM-dd") + "&dateTimeF[]=00:00&dateTimeT[]=" + getDate.ToString("yyyy-MM-dd") + "&dateTimeT[]=23:59&mag[]=0.0&mag[]=9.9&dep[]=0&dep[]=999&epi[]=99&pref[]=99&city[]=99&station[]=99&obsInt=1&maxInt=1&additionalC=true&Sort=S0&Comp=C0&seisCount=false&observed=false").Result);
            var json = JsonNode.Parse(response);

            var csv = new StringBuilder("地震の発生日,地震の発生時刻,震央地名,緯度,経度,深さ,Ｍ,最大震度\n");
            var res = json!["res"];
            var viewText = "";
            if (res is JsonArray jsonArray)
            {
                foreach (var data in res.AsArray())
                {
                    var ot = (string?)data!["ot"]!.AsValue();
                    if (ot == null)
                        continue;
                    csv.Append(ot.Replace(" ", ","));
                    csv.Append(',');
                    csv.Append((string?)data["name"]!.AsValue());
                    csv.Append(',');
                    csv.Append((string?)data["latS"]!.AsValue());
                    csv.Append(',');
                    csv.Append((string?)data["lonS"]!.AsValue());
                    csv.Append(',');
                    csv.Append((string?)data["dep"]!.AsValue());
                    csv.Append(',');
                    csv.Append((string?)data["mag"]!.AsValue());
                    csv.Append(',');
                    csv.Append((string?)data["maxI"]!.AsValue());
                    csv.AppendLine();
                }
                viewText = res.AsArray().Count.ToString();
            }
            else
                viewText = (string)res!.AsValue()!;

            Directory.CreateDirectory(config.SaveDir_EqData + "\\HypoList\\" + getDate.ToString("yyyy\\\\MM"));
            File.WriteAllText(savePath, csv.ToString());
            Console.WriteLine(getDate.ToString("yyyy/MM/dd") + " EQDB: " + viewText);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }

    public static readonly HtmlParser parser = new();

    /// <summary>
    /// 震源リスト(無感含む)を震度データベース互換に変換
    /// </summary>
    /// <remarks>震度は---になります</remarks>
    public static void GetHypo(DateTime getDate)
    {
        try
        {
            var savePath = config.SaveDir_EqData + "\\HypoList\\" + getDate.ToString("yyyy\\\\MM\\\\dd") + ".csv";
            var response = client.GetStringAsync("https://www.data.jma.go.jp/eqev/data/daily_map/" + getDate.ToString("yyyyMMdd") + ".html").Result;
            var document = parser.ParseDocument(response);
            var pre = document.QuerySelector("pre")!.TextContent;
            var lines_converted = pre.Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(2).Select(HypoText2EqdbData);

            var csv = "地震の発生日,地震の発生時刻,震央地名,緯度,経度,深さ,Ｍ,最大震度\n" + string.Join('\n', lines_converted)
                .Replace(",- km,", ",不明,").Replace(",-,", ",不明,").Replace("'", "′")
                .Replace("/1/", "/01/").Replace("/2/", "/02/").Replace("/3/", "/03/").Replace("/4/", "/04/").Replace("/5/", "/05/")//月調整
                .Replace("/6/", "/06/").Replace("/7/", "/07/").Replace("/8/", "/08/").Replace("/9/", "/09/")
                .Replace("/1,", "/01,").Replace("/2,", "/02,").Replace("/3,", "/03,").Replace("/4,", "/04,").Replace("/5,", "/05,")//日調整
                .Replace("/6,", "/06,").Replace("/7,", "/07,").Replace("/8,", "/08,").Replace("/9,", "/09,")
                .Replace(":1.", ":01.").Replace(":2.", ":02.").Replace(":3.", ":03.").Replace(":4.", ":04.").Replace(":5.", ":05.")//秒調整
                .Replace(":6.", ":06.").Replace(":7.", ":07.").Replace(":8.", ":08.").Replace(":9.", ":09.").Replace(":0.", ":00.")
                .Replace("°1.", "°01.").Replace("°2.", "°02.").Replace("°3.", "°03.").Replace("°4.", "°04.").Replace("°5.", "°05.")//緯度経度分調整
                .Replace("°6.", "°06.").Replace("°7.", "°07.").Replace("°8.", "°08.").Replace("°9.", "°09.").Replace("°0.", "°00.")
                + "\n";

            Directory.CreateDirectory(config.SaveDir_EqData + "\\HypoList\\" + getDate.ToString("yyyy\\\\MM"));
            File.WriteAllText(savePath, csv.ToString());
            Console.WriteLine(getDate.ToString("yyyy/MM/dd") + " Hypo: " + lines_converted.Count());
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }


    /// <summary>
    /// 震源リスト1行のデータを震度データベース形式に変換します。
    /// </summary>
    /// <param name="text">csv1行</param>
    /// <returns>震度データベース形式のデータ</returns>
    public static string HypoText2EqdbData(string text)
    {
        var datas = text.Replace("° ", "°").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return datas[0] + "/" + datas[1] + "/" + datas[2] + "," + datas[3] + ":" + datas[4] + "," + datas[9] + "," + datas[5] + "," + datas[6] + "," +
            datas[7] + " km," + datas[8] + ",---";
    }

}

internal class Config
{
    public string SaveDir_EqData { get; set; } = @"D:\Ichihai1415\data\_github\Data";
    public string SaveDir_Video { get; set; } = @"D:\Ichihai1415\video\_quake\_monthly-eq";
}