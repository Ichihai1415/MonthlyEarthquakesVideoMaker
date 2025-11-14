using AngleSharp.Html.Parser;
using MonthlyEarthquakesVideoMaker;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static MonthlyEarthquakesVideoMaker.Conv;

[SupportedOSPlatform("windows")]
internal class Program
{

    internal static readonly HttpClient client = new();
    internal static Config config = new();
    internal static Config_Color color = new();
    internal static int WAIT = 10;
    [SupportedOSPlatform("windows")]
    public static FontFamily font = new("Koruri");
    public static StringFormat string_Right = new()
    {
        Alignment = StringAlignment.Far,
        LineAlignment = StringAlignment.Far
    };

    private static void Main(string[] args)
    {
        Console.WriteLine("MEVM v1.0\n");

        var now = DateTime.Now;
        var get = now.AddMonths(-1);

# if DEBUG_NOTE
        config = new()
        {
            SaveDir_EqData = @"C:\Ichihai1415\tmp\eqdata",
            SaveDir_Video = @"C:\Ichihai1415\tmp\eqdata"
        };
#endif


        var ym = get.ToString("yyyyMM");

        if (now.Day == 2)
            Console.WriteLine("注意: 本日は" + now.Day + "日です。");
        Console.WriteLine(ym + " で取得・作成していいですか？[y/n]");
        var res = Console.ReadLine();
        if (res == "y" || res == "Y")
        {
            Console.WriteLine("取得中...");
            for (var dt = new DateTime(get.Year, get.Month, 1); dt < new DateTime(now.Year, now.Month, 1); dt = dt.AddDays(1))
            {
                Console.WriteLine(dt.ToString("yyyy/MM/dd"));
                GetDB1d(dt);
                GetHypo(dt);
                Thread.Sleep(WAIT);
            }
            Console.WriteLine("描画中...");
            ReadyVideo(get);
        }
        Console.WriteLine("終了しました。");
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


    /// <summary>
    /// 動画用画像を描画します。
    /// </summary>
    public static void ReadyVideo(DateTime dt)
    {
        try
        {
            var files = Directory.EnumerateFiles(config.SaveDir_EqData + "\\EQDB\\" + dt.ToString("yyyy\\\\MM\\\\dd") + ".csv").Concat(Directory.EnumerateFiles(config.SaveDir_EqData + "\\HypoList\\" + dt.ToString("yyyy\\\\MM\\\\dd") + ".csv"));
            var datas_ = MergeFiles([.. files]).Replace("\r", "").Split('\n');//gitで触ると\r付く
            var datas = (IEnumerable<Data>)datas_.Where(x => x.Contains('°')).Where(x => !x.Contains("不明データ")).Select(Text2Data).OrderBy(a => a.Time);//データじゃないやつついでに緯度経度ないやつも除外

            var drawConfig = new DrawConfig()
            {
                StartTime = dt,
                EndTime = dt.AddMonths(1),
                DrawSpan = new TimeSpan(0, 10, 0),
                DisappTime = new TimeSpan(12, 0, 0),
                MapSize = 1080,
                LatSta = 20,
                LatEnd = 50,
                LonSta = 120,
                LonEnd = 150,
                MagSizeType = 21,
                TextInt = 1,
                EnableLegend = true
            };
            var saveDir = "output\\" + DateTime.Now.ToString("yyyyMMddHHmmss");
            var dataSum = datas.Count();
            Directory.CreateDirectory(saveDir);
            var zoomW = drawConfig.MapSize / (drawConfig.LonEnd - drawConfig.LonSta);
            var zoomH = drawConfig.MapSize / (drawConfig.LatEnd - drawConfig.LatSta);
            var sizeX = drawConfig.MagSizeType - (drawConfig.MagSizeType / 10 * 10);//倍率
            var drawTime = drawConfig.StartTime;//描画対象時間
            var bitmap_baseMap = DrawMap(drawConfig);
            var bitmap_legend = DrawLegend(drawConfig);

            //各描画開始
            for (var i = 1; drawTime < drawConfig.EndTime; i++)//DateTime:古<新==true
            {
                datas = [.. datas.SkipWhile(data => data.Time < drawTime - drawConfig.DisappTime)];//除外//SkipWhileなので.OrderBy(a => a.Time)で並び替えられていることが必要
                var datas_Draw = datas.Where(data => data.Time < drawTime + drawConfig.DrawSpan);//抜き出し

                using var bitmap = (Bitmap)bitmap_baseMap.Clone();
                using var g = Graphics.FromImage(bitmap);

                var texts = new StringBuilder[] { new("\n"), new("\n"), new("999.9km\n\n"), new("\n"), new("\n") };
                var alpha = color.Hypo_Alpha;

                var font_msD45 = new Font(font, drawConfig.MapSize / 45f, GraphicsUnit.Pixel);
                var sb_text = new SolidBrush(color.Text);
                var sb_text_sub = new SolidBrush(Color.FromArgb(127, color.Text));
                var pen_hypo = new Pen(Color.FromArgb(alpha, 127, 127, 127));
                var pen_line = new Pen(Color.FromArgb(127, color.Text), drawConfig.MapSize / 1080f);

                foreach (var data in datas_Draw)//imageとの違い
                {
                    //imageとの違い
                    alpha = data.Time >= drawTime ? color.Hypo_Alpha : (int)((1d - (drawTime - data.Time).TotalSeconds / drawConfig.DisappTime.TotalSeconds) * color.Hypo_Alpha);//消える時間の割合*基本透明度
                    var size = (float)(drawConfig.MagSizeType / 10 == 1
                        ? (Math.Max(1, data.Mag) * drawConfig.MapSize / 216d)
                        : (Math.Max(1, data.Mag) * (Math.Max(1, data.Mag) * drawConfig.MapSize / 216d))) * sizeX;//精度と統一のためd
                    g.FillEllipse(Depth2Color(data.Depth, alpha), (float)(((data.Lon - drawConfig.LonSta) * zoomW) - size / 2f), (float)(((drawConfig.LatEnd - data.Lat) * zoomH) - size / 2f), size, size);
                    g.DrawEllipse(new Pen(Color.FromArgb(alpha, 127, 127, 127)), (float)(((data.Lon - drawConfig.LonSta) * zoomW) - size / 2f), (float)(((drawConfig.LatEnd - data.Lat) * zoomH) - size / 2f), size, size);
                    if ((Math.Abs(data.MaxInt) >= drawConfig.TextInt && data.MaxInt != -1) || (drawConfig.TextInt == -1 && data.MaxInt == -1))//↑imageとの違い
                    {
                        texts[0].AppendLine(data.Time.ToString("yyyy/MM/dd HH:mm:ss.f"));
                        texts[1].AppendLine(data.Hypo);//詳細不明の可能性
                        texts[2].Append(data.Depth == null ? "不明" : data.Depth.ToString());
                        texts[2].AppendLine(data.Depth == null ? "" : "km");
                        texts[3].Append(double.IsNaN(data.Mag) ? "不明" : 'M');
                        texts[3].AppendLine(double.IsNaN(data.Mag) ? "" : data.Mag.ToString("0.0"));
                        texts[4].AppendLine(MaxIntInt2String(data.MaxInt, true));
                    }
                }
                var depthSize = g.MeasureString(texts[2].ToString(), font_msD45);//string Formatに必要
                var depthHeadSize = g.MeasureString("999.9km\n深さ ", font_msD45);//最大幅計算用
                var oneLineHeight = g.MeasureString("999.9km", font_msD45).Height;//調整用

                g.FillRectangle(new SolidBrush(color.InfoBack), drawConfig.MapSize, 0, bitmap.Width - drawConfig.MapSize, drawConfig.MapSize);
                g.DrawString("発生日時", font_msD45, sb_text_sub, drawConfig.MapSize, 0);
                g.DrawString("震央", font_msD45, sb_text_sub, drawConfig.MapSize * 1.25f, 0);
                g.DrawString("深さ \n   ", font_msD45, sb_text_sub, new RectangleF(new PointF(drawConfig.MapSize * 1.5f, 0), depthHeadSize), string_Right);
                g.DrawString("規模", font_msD45, sb_text_sub, drawConfig.MapSize * 1.602625f, 0);
                g.DrawString("最大震度", font_msD45, sb_text_sub, drawConfig.MapSize * 1.675f, 0); g.DrawString(texts[0].ToString(), font_msD45, sb_text, drawConfig.MapSize, 0);

                g.DrawString(texts[0].ToString(), font_msD45, sb_text, drawConfig.MapSize, 0);
                g.DrawString(texts[1].ToString(), font_msD45, sb_text, drawConfig.MapSize * 1.25f, 0);
                g.FillRectangle(new SolidBrush(color.InfoBack), drawConfig.MapSize * 1.5f, drawConfig.MapSize / 30f, bitmap.Width - drawConfig.MapSize * 1.5f, drawConfig.MapSize * 29 / 30f);
                g.DrawString(texts[2].ToString(), font_msD45, sb_text, new RectangleF(new PointF(drawConfig.MapSize * 1.5f, -oneLineHeight), depthSize), string_Right);
                g.DrawString(texts[3].ToString(), font_msD45, sb_text, drawConfig.MapSize * 1.602625f, 0);
                g.DrawString(texts[4].ToString(), font_msD45, sb_text, drawConfig.MapSize * 1.675f, 0);
                g.DrawLine(pen_line, drawConfig.MapSize, drawConfig.MapSize / 30f, bitmap.Width, drawConfig.MapSize / 30f);

                g.DrawImage(bitmap_legend, 0, 0);
                var xBase = drawConfig.MapSize;
                g.DrawString(drawTime.ToString("yyyy/MM/dd HH:mm:ss"), new Font(font, drawConfig.MapSize / 30f, GraphicsUnit.Pixel), new SolidBrush(color.Text), xBase + drawConfig.MapSize / 9f * 4, drawConfig.MapSize * 23 / 24f);

                var savePath = saveDir + "\\" + i.ToString("d5") + ".png";
                bitmap.Save(savePath, ImageFormat.Png);
                Console.WriteLine(drawTime.ToString("yyyy/MM/dd HH:mm:ss") + "  " + i.ToString("d5") + ".png : " + datas_Draw.Count() + "  (内部残り: " + datas.Count() + " / " + dataSum + ")");
                drawTime += drawConfig.DrawSpan;
                if (i % 10 == 0)
                    GC.Collect();
            }
            using var pro = Process.Start("ffmpeg", "-framerate 30 -i \"" + saveDir + "\\%05d.png\" -vcodec libx264 -pix_fmt yuv420p -r 30 \"" + config.SaveDir_Video + "\\" + dt.ToString("yyyyMM") + ".mp4\"");
            pro.WaitForExit();
        }
        catch (Exception ex)
        {
            Console.WriteLine("エラーが発生しました。" + ex + "\n再度実行してください。");
        }
    }


    /// <summary>
    /// ファイルを結合します。
    /// </summary>
    public static string MergeFiles(string[] files)
    {
        if (files.Length == 0)
        {
            Console.WriteLine("結合するファイルのパスを1行ごとに入力してください。空文字が入力されたら結合を開始します。フォルダのパスを入力するとすべて読み込みます。※観測震度検索をしているものとしていないものの結合はできますが他ソフトで処理をする際エラーとなる可能性があります。このソフトでは問題ありません。");
            List<string> filesTmp = [];
            while (true)
            {
                var file = Console.ReadLine();
                if (!string.IsNullOrEmpty(file))
                    filesTmp.Add(file.Replace("\"", ""));
                else if (filesTmp.Count == 0)
                {
                    Console.WriteLine("中止します。");
                    return "";
                }
                else
                    break;
            }
            files = [.. filesTmp];
        }

        var stringBuilder = new StringBuilder();
        List<string> files2 = [];
        foreach (var file in files)
        {
            var f = file.Replace("\"", "");
            if (f.EndsWith(".csv"))
                files2.Add(f);
            else
            {
                Console.WriteLine("ファイル名取得中... ", false);
                Console.WriteLine(f);
                var openPaths = Directory.EnumerateFiles(f, "*.csv", SearchOption.AllDirectories);
                foreach (var path in openPaths)
                    files2.Add(path);
            }
        }
        foreach (var file in files2)
        {
            if (File.Exists(file))
            {
                Console.WriteLine("読み込み中... ", false);
                Console.WriteLine(file);
                stringBuilder.Append(File.ReadAllText(file).Replace("地震の発生日,地震の発生時刻,震央地名,緯度,経度,深さ,Ｍ,最大震度,検索対象最大震度\n", "").Replace("地震の発生日,地震の発生時刻,震央地名,緯度,経度,深さ,Ｍ,最大震度\n", ""));
            }
            else
                Console.WriteLine($"{file}が見つかりません。");
        }
        stringBuilder.Insert(0, "地震の発生日,地震の発生時刻,震央地名,緯度,経度,深さ,Ｍ,最大震度\n");
        return stringBuilder.ToString();
    }

    /// <summary>
    /// 凡例を描画します(同一描画防止)。
    /// </summary>
    /// <param name="config">設定</param>
    /// <returns>凡例(凡例外は透明)</returns>
    public static Bitmap DrawLegend(DrawConfig config)
    {
        var alpha = color.Hypo_Alpha;
        var sizeX = config.MagSizeType - (config.MagSizeType / 10 * 10);//倍率
        var xBase = config.MapSize;

        var font_msD45 = new Font(font, config.MapSize / 45f, GraphicsUnit.Pixel);
        var sb_text_sub = new SolidBrush(Color.FromArgb(127, color.Text));
        var pen_hypo = new Pen(Color.FromArgb(alpha, 127, 127, 127));
        var pen_line = new Pen(Color.FromArgb(127, color.Text), config.MapSize / 1080f);

        var bitmap_legend = new Bitmap(config.MapSize * 16 / 9, config.MapSize);
        using var g = Graphics.FromImage(bitmap_legend);

        if (!config.EnableLegend)
        {
            g.FillRectangle(new SolidBrush(color.InfoBack), config.MapSize, config.MapSize * 26f / 27f, config.MapSize / 9f * 7f + 1f, config.MapSize / 27f + 1f);//一応+1
            g.DrawString("地図データ:気象庁, Natural Earth", new Font(font, config.MapSize / 36f, GraphicsUnit.Pixel), sb_text_sub, xBase, config.MapSize * 26 / 27f);

            //g.FillRectangle(Brushes.Black, config.MapSize, config.MapSize * 26f / 27f, config.MapSize / 9f * 7f + 1f, config.MapSize / 27f + 1f);//一応+1
            //g.DrawString("2222/22/22 22:22:22", new Font(font, config.MapSize / 30f, GraphicsUnit.Pixel), new SolidBrush(color.Text), xBase + config.MapSize / 9f * 4, config.MapSize * 23 / 24f);

            return bitmap_legend;
        }

        //凡例
        switch (config.MagSizeType)
        {
            case 11:
            case 12:
            case 13:
                // sizes:                    - (config.MapSize / 10f + magMaxSize)
                //   [mag_txt]               -  config.MapSize / 48f
                //   [mag_leg]               -  magMaxSize
                //   [dep_leg]               -  config.MapSize / 48f
                //   [map_source, datetime]  -  config.MapSize / 30f
                var magMaxSize = 10 * config.MapSize / 216f * sizeX;//マグニチュード凡例円のサイズ(余白を含めたM10相当サイズ)
                var yBase = config.MapSize - magMaxSize - config.MapSize / 10f;

                g.FillRectangle(new SolidBrush(color.InfoBack), xBase, yBase, config.MapSize / 9f * 7f + 1f, magMaxSize + 20f * config.MapSize * sizeX + 1f);//一応+1
                g.DrawLine(pen_line, xBase + config.MapSize / 80f, yBase + config.MapSize / 216f, config.MapSize * 1261f / 720f, yBase + config.MapSize / 216f);

                for (int m = 1; m <= 8; m++)
                {
                    var size = m * config.MapSize / 216f * sizeX;
                    var magLTx = config.MapSize + LEGEND_MAG_X_1X[m] * config.MapSize / 216f;//left top
                    var magLTy = config.MapSize - magMaxSize / 2f - size / 2f - config.MapSize / 14f;
                    // mag_text
                    var magSampleSize = g.MeasureString("M" + m + ".0", font_msD45);
                    g.DrawString("M" + m + ".0", font_msD45, sb_text_sub, magLTx + size / 2f - magSampleSize.Width / 2f, config.MapSize * 9f / 10f + config.MapSize / 540f - magMaxSize);
                    // mag_legend
                    g.FillEllipse(new SolidBrush(color.Legend_Mag_Fill), magLTx, magLTy, size, size);
                    g.DrawEllipse(pen_hypo, magLTx, magLTy, size, size);
                }
                break;
            case 21:
            case 22:
                // sizes:                    - (config.MapSize / 3.8f + config.MapSize / 10f (= config.MapSize * 69 / 190f))
                //   [mag_txt]               - (not fixed position)
                //   [mag_leg]               - (not use value)
                //   [dep_leg]               -  config.MapSize / 48f
                //   [map_source, datetime]  -  config.MapSize / 30f
                magMaxSize = config.MapSize / 3.8f;//マグニチュード凡例円部分のサイズ(適当)
                yBase = config.MapSize - magMaxSize - config.MapSize / 10f;

                g.FillRectangle(new SolidBrush(color.InfoBack), xBase, yBase, config.MapSize / 9 * 7 + 1, magMaxSize + 20 * config.MapSize * sizeX + 1);//一応+1
                g.DrawLine(pen_line, xBase + config.MapSize / 80f, yBase + config.MapSize / 108f, config.MapSize * 1263 / 720f, yBase + config.MapSize / 108f);

                for (int m = 1; m <= 7; m++)
                {
                    var size = m * m * config.MapSize / 216f * sizeX;
                    var magLTx = config.MapSize + LEGEND_MAG_X_2X[m, 0] * config.MapSize / 216f;//left top
                    var magLTy = config.MapSize / 2f + LEGEND_MAG_X_2X[m, 1] * config.MapSize / 216f;
                    // mag_text
                    var magSampleSize = g.MeasureString("M" + m + ".0", font_msD45);
                    g.DrawString("M" + m + ".0", font_msD45, sb_text_sub, magLTx + size / 2f - magSampleSize.Width / 2f, magLTy - config.MapSize / 30f);
                    // mag_legend
                    g.FillEllipse(new SolidBrush(color.Legend_Mag_Fill), magLTx, magLTy, size, size);
                    g.DrawEllipse(pen_hypo, magLTx, magLTy, size, size);
                }
                break;
        }
        // dep_legend
        using (var textGP = new GraphicsPath())
            for (int di = 0; di < LEGEND_DEP_EX.Length; di++)
            {
                //円部分
                textGP.StartFigure();
                textGP.AddString("●", font, 0, config.MapSize / 48f, new PointF(config.MapSize + config.MapSize * (di + 0.125f) / 10.8f, config.MapSize * 13f / 14f), StringFormat.GenericDefault);
                g.FillPath(Depth2Color(LEGEND_DEP_EX[di], 255), textGP);
                g.DrawPath(new Pen(Color.FromArgb(color.Hypo_Alpha, color.Text), config.MapSize / 1080f), textGP);
                textGP.Reset();
                //文字部分
                g.DrawString("　" + (LEGEND_DEP_EX[di] == 0 ? " " : string.Empty) + LEGEND_DEP_EX[di] + "km", new Font(font, config.MapSize / 48f, GraphicsUnit.Pixel), sb_text_sub, config.MapSize + config.MapSize * (di + 0.125f) / 10.8f, config.MapSize * 13 / 14f);
            }
        ;

        g.DrawString("地図データ:気象庁, Natural Earth", new Font(font, config.MapSize / 36f, GraphicsUnit.Pixel), sb_text_sub, xBase, config.MapSize * 26 / 27f);
        //g.DrawString("2222/22/22 22:22:22", new Font(font, config.MapSize / 30f, GraphicsUnit.Pixel), new SolidBrush(color.Text), xBase + config.MapSize / 9f * 4, config.MapSize * 23 / 24f);
        return bitmap_legend;
    }

    /// <summary>
    /// 右下マグニチュード凡例用(11~13用)(0は不使用)　mapSize=216での右欄(168*216)内でのx座標
    /// </summary>
    public static readonly int[] LEGEND_MAG_X_1X = [-1, 6, 17, 30, 46, 65, 87, 112, 140];

    /// <summary>
    /// 右下深さ凡例値
    /// </summary>
    public static readonly int[] LEGEND_DEP_EX = [0, 10, 20, 30, 50, 100, 300, 700];

    /// <summary>
    /// 右下マグニチュード凡例用(21~22用)(0は不使用)　mapSize=216での右欄(168*216)内での座標(yは適当基準(コード参照))
    /// </summary>
    public static readonly int[,] LEGEND_MAG_X_2X = { { -1, -1 }, { 13, 50 }, { 11, 62 }, { 7, 80 }, { 20, 73 }, { 40, 64 }, { 70, 53 }, { 111, 40 } };

    /// <summary>
    /// ベースの地図を描画します
    /// </summary>
    /// <param name="config">設定</param>
    /// <returns>描画された地図</returns>
    /// <exception cref="Exception">マップデータの読み込みに失敗した場合</exception>
    public static Bitmap DrawMap(DrawConfig config)//todo:新しい描画方法に変える
    {
        var mapImg = new Bitmap(config.MapSize * 16 / 9, config.MapSize);
        var zoomW = config.MapSize / (config.LonEnd - config.LonSta);
        var zoomH = config.MapSize / (config.LatEnd - config.LatSta);
        var json = JsonNode.Parse(File.ReadAllText("map-world.geojson")) ?? throw new Exception("マップデータの読み込みに失敗しました。");
        var g = Graphics.FromImage(mapImg);
        g.Clear(color.Map.Sea);
        var maps = new GraphicsPath();
        maps.StartFigure();
        foreach (var json_1 in json["features"]!.AsArray())
        {
            if (json_1!["geometry"] == null)
                continue;
            var points = json_1["geometry"]!["coordinates"]![0]!.AsArray().Select(json_2 => new Point((int)(((double)json_2![0]! - config.LonSta) * zoomW), (int)((config.LatEnd - (double)json_2[1]!) * zoomH))).ToArray();
            if (points.Length > 2)
                maps.AddPolygon(points);
        }
        g.FillPath(new SolidBrush(color.Map.World), maps);

        json = JsonNode.Parse(File.ReadAllText("map-jp.geojson")) ?? throw new Exception("マップデータの読み込みに失敗しました。");
        maps.Reset();
        maps.StartFigure();
        foreach (var json_1 in json["features"]!.AsArray())
        {
            if ((string?)json_1!["geometry"]!["type"] == "Polygon")
            {
                var points = json_1["geometry"]!["coordinates"]![0]!.AsArray().Select(json_2 => new Point((int)(((double)json_2![0]! - config.LonSta) * zoomW), (int)((config.LatEnd - (double)json_2[1]!) * zoomH))).ToArray();
                if (points.Length > 2)
                    maps.AddPolygon(points);
            }
            else
            {
                foreach (var json_2 in json_1["geometry"]!["coordinates"]!.AsArray())
                {
                    var points = json_2![0]!.AsArray().Select(json_3 => new Point((int)(((double)json_3![0]! - config.LonSta) * zoomW), (int)((config.LatEnd - (double)json_3[1]!) * zoomH))).ToArray();
                    if (points.Length > 2)
                        maps.AddPolygon(points);
                }
            }
        }
        g.FillPath(new SolidBrush(color.Map.Japan), maps);
        g.DrawPath(new Pen(color.Map.Japan_Border, config.MapSize / 1080f), maps);
        //var mdsize = g.MeasureString("地図データ:気象庁, Natural Earth", new Font(font, config.MapSize / 28, GraphicsUnit.Pixel));
        //g.DrawString("地図データ:気象庁, Natural Earth", new Font(font, config.MapSize / 28, GraphicsUnit.Pixel), new SolidBrush(color.Text), config.MapSize - mdsize.Width, config.MapSize - mdsize.Height);
        g.Dispose();
        return mapImg;
    }



}

internal class Config
{
    public string SaveDir_EqData { get; set; } = @"D:\Ichihai1415\data\_github\Data";
    public string SaveDir_Video { get; set; } = @"D:\Ichihai1415\video\_quake\_monthly-eq";
}