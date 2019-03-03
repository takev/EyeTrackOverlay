using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace EyeTrackOverlay
{
    public class GazeServer : WebSocketSharp.Server.WebSocketBehavior
    {
        Timer timer;

        protected override void OnOpen(WebSocket webSocket)
        {
            Console.WriteLine("Socket open.");

            base.OnOpen(webSocket);

            // 60 times a second.
            timer = new Timer(this.SendCurrentGazePoint, null, 0, 17);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            timer.Dispose();
        }

        protected override void OnError(ErrorEventArgs e)
        {
            base.OnError(e);
            timer.Dispose();
        }

        void SendCurrentGazePoint(Object stateInfo)
        {
            var tmpGazePoint = GetGazePoint();
            var tmpGazePointString = String.Format("gazePoint {0} {1} {2}", tmpGazePoint.Item1, tmpGazePoint.Item2, tmpGazePoint.Item3);

            try {
                Send(tmpGazePointString);
            } catch {
                timer.Dispose();
            }
        }

        static Object gazePointLock = new object();
        static bool eyePositionValid = false;
        static Tuple<double, double> gazePoint;

        public static void SetEyePosition(Tobii.Interaction.EyePositionData eyePosition)
        {
            lock (gazePointLock)
            {
                eyePositionValid = eyePosition.HasLeftEyePosition && eyePosition.HasRightEyePosition;
            }
        }

        public static void SetGazePoint(double x, double y)
        {
            lock (gazePointLock) {
                gazePoint = Tuple.Create(x, y);
            }
        }

        public static Tuple<double, double, bool> GetGazePoint()
        {
            Tuple<double, double, bool> tmp;

            lock (gazePointLock) {
                tmp = Tuple.Create(gazePoint.Item1, gazePoint.Item2, eyePositionValid);
            }
            return tmp;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var wssv = new WebSocketSharp.Server.WebSocketServer("ws://192.168.178.24:42834");
            wssv.AddWebSocketService<GazeServer>("/gaze");

            var tobiiHost = new Tobii.Interaction.Host();
            var gazePointDataStream = tobiiHost.Streams.CreateGazePointDataStream();
            gazePointDataStream.GazePoint((gazePointX, gazePointY, _) => GazeServer.SetGazePoint(gazePointX, gazePointY));

            var eyePositionDataStream = tobiiHost.Streams.CreateEyePositionStream();
            eyePositionDataStream.EyePosition((eyePosition) => GazeServer.SetEyePosition(eyePosition));

            wssv.Start();

            while (true)
            {
                Thread.Sleep(60000);
            }
        }
    }
}
