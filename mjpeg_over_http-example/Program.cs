using System.Net;
using System.Text;

namespace mjpeg_over_http
{
    class Program
    {
        private static void Main(string[] args)
        {
            var server = new HttpListener();
            server.Prefixes.Add(@"http://+:60000/"); // サーバーのURLを指定
            server.Start();

            // 非同期でぶん回すためのタスク
            Task.Run(async () =>
            {
                while (true)
                {
                    // GetContextAsync()でリクエスト受け付け -> ProcessRequestAsync(context)で応答
                    // ProcessRequestAsync()での処理内容に関わらずすぐに次のGetContextAsync()が回る
                    var context = await server.GetContextAsync();
                    _ = ProcessRequestAsync(context); // リクエストの処理を非同期で開始
                }
            });

            Thread.Sleep(-1);
        }

        /// <summary>
        ///     受信したリクエストからクエリパラメータを取り出して対応する処理を行う
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        static async Task ProcessRequestAsync(HttpListenerContext context)
        {
            // 受信contextからリクエスト内容とレスポンスの作成を行う
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            // クエリパラメータを取得する
            string action = request.QueryString.Get("action") ?? "snapshot";
            string fps = request.QueryString.Get("fps") ?? "100";

            // クエリのactionパラメータごとの処理を行う
            // actionパラメータがstreamの場合以外はaction=snapshotとして処理する
            switch (action)
            {
                case "stream":
                    // クライアントへストリームを非同期で送信
                    await SendStreamAsync(context, fps);
                    break;
                default:
                    // クライアントへスナップショットを非同期で送信
                    await SendSnapshotAsync(context);
                    break;
            }

            // 処理完了時にresponseを閉じる
            response.Close();
        }

        /// <summary>
        ///     指定された速度でmjpeg送信
        /// </summary>
        /// <param name="context"></param>
        /// <param name="fps"></param>
        /// <returns></returns>
        static async Task SendStreamAsync(HttpListenerContext context, string fps)
        {
            int frame;
            if (!int.TryParse(fps, out frame))
            {
                frame = 2;
            }
            frame = 1000 / frame;
            string imagePath = $"{Directory.GetCurrentDirectory()}/image.jpg";

            var response = context.Response;
            response.AddHeader("Content-Type", "multipart/x-mixed-replace; boundary=--myboundary");
            response.SendChunked = true;

            // outputを破棄せずに送信を繰り返す
            using (var output = response.OutputStream)
            {
                while (true)
                {
                    byte[] imageBytes = File.ReadAllBytes(imagePath);
                    byte[] mjpegheader = Encoding.UTF8.GetBytes($"\r\n--myboundary\r\nContent-Length: {imageBytes.Length}\r\nContent-Type: image/jpeg\r\n\r\n");

                    await output.WriteAsync(mjpegheader, 0, mjpegheader.Length);
                    await output.WriteAsync(imageBytes, 0, imageBytes.Length);
                    await output.FlushAsync();

                    Thread.Sleep(frame);
                }
            }
        }

        /// <summary>
        ///     最新のデータを単発送信
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        static async Task SendSnapshotAsync(HttpListenerContext context)
        {
            var response = context.Response;
            response.ContentType = "image/jpeg";

            string imagePath = $"{Directory.GetCurrentDirectory()}/image.jpg";
            byte[] imageBytes = File.ReadAllBytes(imagePath);

            using (var output = response.OutputStream)
            {
                await output.WriteAsync(imageBytes, 0, imageBytes.Length);
                await output.FlushAsync();
            }
        }
    }
}