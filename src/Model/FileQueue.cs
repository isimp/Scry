using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Scry
{
    /// <summary>
    /// File work done on a worker thread, one job after another in the order asked: a file still
    /// being written is read once written, and the newest write is the one left in place. A job
    /// that fails does not stop the next. What a finding gives, or how it failed, is for whoever
    /// asked for it; a write's failure is told once by <see cref="Failures"/>. Asked for and told
    /// on the main thread.
    /// </summary>
    internal sealed class FileQueue
    {
        private Task _last = Task.CompletedTask;
        private readonly List<(Task Job, string Doing)> _writes = new List<(Task, string)>();

        /// <summary>Whether a job asked for is still to finish.</summary>
        public bool Busy => !_last.IsCompleted;

        /// <summary>Finds something once every job asked for before is done.</summary>
        public Task<T> Then<T>(Func<T> find)
        {
            var job = _last.ContinueWith(_ => find(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            _last = job;
            return job;
        }

        /// <summary>Writes once every job asked for before is done; <paramref name="doing"/> says what, should it fail.</summary>
        public void Then(string doing, Action write)
        {
            var job = _last.ContinueWith(_ => write(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            _last = job;
            _writes.Add((job, doing));
        }

        /// <summary>The writes that failed since last asked, each told once; those still running are told once done.</summary>
        public List<(string Doing, Exception Error)> Failures()
        {
            var failed = new List<(string, Exception)>();
            _writes.RemoveAll(w =>
            {
                if (!w.Job.IsCompleted) return false;
                if (w.Job.IsFaulted) failed.Add((w.Doing, w.Job.Exception?.GetBaseException()));
                return true;
            });
            return failed;
        }
    }
}
