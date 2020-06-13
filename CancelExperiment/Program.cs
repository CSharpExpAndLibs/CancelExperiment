using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;


namespace CancelExperiment
{
    class Program
    {
        static ManualResetEvent _mre = null;

        static void execute1(CancellationToken token, ManualResetEvent mre, string name)
        {
            
            Console.WriteLine("--- Task (id={0},name={1}) is executed ---", Task.CurrentId, name);

            // タスク実行開始時点でキャンセルされていたら、状況を表示してSleepしてからタスク終了
            // 呼び出し元のWaitAll()がタスクの終了を検知することの検証
            if (token.IsCancellationRequested)
            {
                Console.WriteLine("--- Already cancelled Task (id={0},name={1}) ---", 
                    Task.CurrentId, name);
                Console.WriteLine("--- Task (id={0},name={1}) will sleep 3 [sec] ---",
                    Task.CurrentId, name);
                Thread.Sleep(3000);
                return;
            }
            while (true)
            {
                // キャンセル、手動リセット、及びタイムアウトの3つのイベントで待機する。
                // タイムアウトの時はそのままタスクを継続し、他の場合はタスクを終了する。
                int wi = WaitHandle.WaitAny(new WaitHandle[] { token.WaitHandle, mre }, 5000);
                switch (wi)
                {
                    case 0:
                        Console.WriteLine("--- Task (id={0},name={1}) is canceled ---",
                            Task.CurrentId, name);
                        return;
                    case 1:
                        Console.WriteLine("--- Task (id={0},name={1}) is reseted ---",
                            Task.CurrentId, name);
                        return;
                    default:
                        Console.WriteLine("--- Task (id={0},name={1}) is Timeout and continue ---",
                            Task.CurrentId, name);
                        break;
                }
            }
        }

        static void SetMre(ManualResetEvent mre)
        {
            _mre = mre;
        }

        static void execute2(CancellationToken token, string name)
        {
            Console.WriteLine("--- Task (id={0},name={1}) is executed ---", Task.CurrentId, name);

            // タスク実行開始時点でキャンセルされていたら、状況を表示してSleepしてからタスク終了
            // 呼び出し元のWaitAll()がタスクの終了を検知することの検証
            if (token.IsCancellationRequested)
            {
                Console.WriteLine("--- Already cancelled Task (id={0},name={1}) ---",
                    Task.CurrentId, name);
                Console.WriteLine("--- Task (id={0},name={1}) will sleep 3 [sec] ---",
                    Task.CurrentId, name);
                Thread.Sleep(3000);
                return;
            }

            // 何処か外部のクラスにmreを渡す操作を模擬
            ManualResetEvent mre = new ManualResetEvent(false);
            SetMre(mre);

            while (true)
            {
                // キャンセル、手動リセット、及びタイムアウトの3つのイベントで待機する。
                // 例として、タイムアウトの時はそのままタスクを継続し、他の場合はタスクを終了する。
                //
                // この実装パターンに従えば、RTOSのEvent Flagと同等の特性を得ることが可能。
                // つまり、実行メッセージを送付してその相手からの通知を受けることと、キャンセル
                // 通知を両方ともハンドリングすることが出来る。
                int wi = WaitHandle.WaitAny(new WaitHandle[] { token.WaitHandle, mre }, 5000);
                switch (wi)
                {
                    case 0:
                        Console.WriteLine("--- Task (id={0},name={1}) is canceled ---",
                            Task.CurrentId, name);
                        return;
                    case 1:
                        Console.WriteLine("--- Task (id={0},name={1}) is reseted ---",
                            Task.CurrentId, name);
                        return;
                    default:
                        Console.WriteLine("--- Task (id={0},name={1}) is Timeout and continue ---",
                            Task.CurrentId, name);
                        break;
                }
            }

        }


        static void DispTaskState(Dictionary<string, Task> tasks)
        {
            foreach (string name in tasks.Keys)
            {
                Task t = tasks[name];
                Console.WriteLine("--- Task (id={0},name={1}) status is {2} ---", t.Id, name, t.Status);
            }

        }


        static void Main(string[] args)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            Dictionary<string, Task> tasks = new Dictionary<string, Task>();
            ManualResetEvent mre = new ManualResetEvent(false);

            // タスク作成
            //  Task1:呼び出し元で定義されたEventとCancelをデリゲートに渡す。
            //  Task2:CancelをデリゲートとTask起動メソッドに渡す。Eventはタスク実行時に
            //        自ら定義する。
            //  タスクディスパッチ前にキャンセルが発効していた場合の動作はTask1とTask2で異なる。
            //  Task1はタスクが起動されるのに対し、Task2は起動前にキャンセル状態となり、起動されない。
            //  この特性の違いは、Experiment(A)で検証される。
            tasks["Task1"] = Task.Run(() => execute1(token, mre, "Task1"));
            tasks["Task2"] = Task.Run(() => execute2(token, "Task2"), token);

            // Experiment(A) タスク実行前にCancelした時の動作を比較
            cts.Cancel();
            try
            {
                Task.WaitAll(tasks.Values.ToArray());
            }
            catch (AggregateException e)
            {
                Console.WriteLine("--- Any Tasks were canceled before executing ---");
                for (int j = 0; j < e.InnerExceptions.Count; j++)
                {
                    Console.WriteLine("--- {0} ---", e.InnerExceptions[j].ToString());
                }
            }

            // タスクの状態を表示
            DispTaskState(tasks);
            // タスクを再立ち上げ
            cts.Dispose();
            cts = new CancellationTokenSource();
            token = cts.Token;
            tasks["Task1"] = Task.Run(() => execute1(token, mre, "Task1"));
            tasks["Task2"] = Task.Run(() => execute2(token, "Task2"), token);

            bool flag = true;
            while (flag)
            {
                // キーボードからの入力に応じてタスクにシグナルを送る
                Console.WriteLine("--- Press c to Cancel, r1/r2 to Reset Task1/Task2, e to Exit ---");
                string line = Console.ReadLine();
                switch (line)
                {
                    case "c":
                        cts.Cancel();
                        Task.WaitAll(tasks.Values.ToArray());
                        DispTaskState(tasks);
                        // タスクは全部再作成
                        cts.Dispose();
                        _mre.Dispose();
                        cts = new CancellationTokenSource();
                        token = cts.Token;
                        tasks["Task1"] = Task.Run(() => execute1(token, mre, "Task1"));
                        tasks["Task2"] = Task.Run(() => execute2(token, "Task2"), token);
                        break;
                    case "r1":
                        mre.Set();
                        Task.WaitAny(tasks.Values.ToArray());
                        DispTaskState(tasks);
                        // Task1だけ再作成
                        mre.Reset();
                        tasks["Task1"] = Task.Run(() => execute1(token, mre, "Task1"));
                        break;
                    case "r2":
                        while(_mre == null)
                        {
                            // Task2がまだ起動されていなくて_mreが作成されていない
                            // 場合があるので、回避する
                            Thread.Sleep(1);
                        }
                        _mre.Set();
                        Task.WaitAny(tasks.Values.ToArray());
                        DispTaskState(tasks);
                        // Task2だけ再作成
                        _mre.Dispose();
                        _mre = null;
                        tasks["Task2"] = Task.Run(() => execute2(token, "Task2"), token);
                        break;
                    case "e":
                        flag = false;
                        break;
                    default:
                        break;
                }
            }
            Console.WriteLine("Press Any key to exit.");
            Console.ReadLine();

        }
    }
}
