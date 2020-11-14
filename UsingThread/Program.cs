using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace UsingThread
{
    class ThreadArgs
    {
        // Threadから結果を戻すための構造体サンプル
        public int Count { get; set; } = 0;
        public ManualResetEvent Evt { get; set; } = null;
    }

    class Program
    {
        /// <summary>
        /// 計算を行う本体。別スレッドで実行させる。
        /// </summary>
        /// <param name="args">ThreadArgs型の引数</param>
        static void ExecuteCalculation(object a)
        {
            ThreadArgs args = (ThreadArgs)a;

            // ここで時間のかかる計算処理を実行する
            //   --- 時間がかかる処理のサンプル ---
            int count;
            for (count = 0; count < 10; count++)
            {
                Console.WriteLine($"--- Thread:{Thread.CurrentThread} is alive. Count = {count} ---");
                Thread.Sleep(1000);
            }

            // 処理結果の代入
            args.Count = count;

            // 完了したことを通知
            args.Evt.Set();
        }

        /// <summary>
        /// これはリスナ実装のサンプル。
        /// --------------------------------------------------------------------
        /// ExecuteCalculation()を別スレッドで実行する。キャンセルが通知されたら
        /// ExecuteCalculationスレッドを速やかに破棄して終了する。
        /// --------------------------------------------------------------------
        /// </summary>
        /// <param name="ctkn">呼び出し元からキャンセルを受け取るためのトークン
        static void Listener(CancellationToken ctkn)
        {
            ManualResetEvent evt = new ManualResetEvent(false);
            ThreadArgs args = new ThreadArgs()
            {
                Evt = evt,
            };

            // 計算処理を別スレッドで実行
            Thread th = new Thread(ExecuteCalculation);
            th.Start(args);

            // キャンセルとスレッド完了を並列で待つ
            int finish = WaitHandle.WaitAny(new WaitHandle[] { ctkn.WaitHandle, evt });

            // キャンセルされたかスレッド完了かで処理を分ける
            //  --- finishには発火した方のidxが入る ---
            switch (finish)
            {
                case 0:
                    // キャンセルされた
                    //  -- 実行中を計算スレッドを破棄する --
                    if (th.IsAlive)
                        th.Abort();
                    Console.WriteLine("-- Cancelされました --");
                    break;
                case 1:
                    // 計算スレッドが終了した
                    Console.WriteLine("-- 計算が終了しました --");
                    break;

            }

            // 処理結果を表示
            Console.WriteLine($"@@@@ 計算結果:Count={args.Count} @@@@");


        }
        static void Main(string[] args)
        {

            CancellationTokenSource cts = new CancellationTokenSource();

            // Listener()を実行するタスクを起動する
            //   これは本番の模擬
            Task t = Task.Run(() =>
            {
                Listener(cts.Token);
            });

            // 標準入力でEnterが押されたら計算スレッドをキャンセルする
            while (true)
            {
                if (Console.ReadKey().Key == ConsoleKey.Enter)
                {
                    if (t.Status != TaskStatus.RanToCompletion && t.Status != TaskStatus.Faulted)
                        cts.Cancel();
                    break;
                }
            }

            Console.WriteLine("Press Any Key to finish.");
            Console.ReadLine();
        }
    }
}
