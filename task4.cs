using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;


namespace CustomThreadPool
{
    public interface IThreadPool
    {
        void EnqueueAction(Action action);
        long GetTasksProcessedCount();
    }

    public class CustomThreadPoolWrapper : IThreadPool
    {
        private long processedTask;
        public void EnqueueAction(Action action)
        {
            CustomThreadPool.AddAction(delegate
            {
                action.Invoke();
                Interlocked.Increment(ref processedTask);
            });
        }

        public long GetTasksProcessedCount() => processedTask;
    }

    public static class CustomThreadPool
    {
        private static Queue<Action> queue = new Queue<Action>();
        private static Dictionary<int, WorkStealingQueue<Action>> actionPools = new Dictionary<int, WorkStealingQueue<Action>>();
        
        static CustomThreadPool()
        {
            void Worker()
            {
                while (true)
                {

                    Action currentAction = delegate { };

                    while (actionPools[Thread.CurrentThread.ManagedThreadId].LocalPop(ref currentAction))
                    {
                        currentAction.Invoke();
                    }

                    bool check = true;
                    lock (queue)
                    {
                        if (queue.TryDequeue(out var action))
                        {
                            actionPools[Thread.CurrentThread.ManagedThreadId].LocalPush(action);
                        }
                        else
                        {
                            check = false;
                        }
                    }

                    if (!check)
                    {
                        foreach (var threadPool in actionPools)
                        {
                            Action action = delegate { };
                            if (threadPool.Value.TrySteal(ref action))
                            {
                                actionPools[Thread.CurrentThread.ManagedThreadId].LocalPush(action);
                                check = true;
                                break;
                            }
                        }
                    }

                    if (!check)
                    {
                        lock (queue)
                        {
                            if (queue.TryDequeue(out var action))
                            {
                                actionPools[Thread.CurrentThread.ManagedThreadId].LocalPush(action);
                            }
                            else
                            {
                                Monitor.Wait(queue);
                            }
                        }
                    }
                }
            }

            StartBckThreads(Worker, 16);
        }

        public static void AddAction(Action action)
        {
            lock (queue)
            {
                queue.Enqueue(action);
                Monitor.Pulse(queue);
            }
        }

        private static Thread[] StartBckThreads(Action action, int count)
        {
            return Enumerable.Range(0, count).Select(_ => StartBckThread(action)).ToArray();
        }
        
        private static Thread StartBckThread(Action action)
        {
            var thread = new Thread(() => action())
            {
                IsBackground = true
            };
            actionPools[thread.ManagedThreadId] = new WorkStealingQueue<Action>();

            thread.Start();
            
            return thread;
        }
    }
}