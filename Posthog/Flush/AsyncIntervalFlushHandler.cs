using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PostHog.Model;
using PostHog.Request;
using UnityEngine;

namespace PostHog.Flush
{
    internal class AsyncIntervalFlushHandler : IDisposable
    {
        /// <summary>
        /// Our servers only accept payloads smaller than 32KB
        /// </summary>
        private const int ActionMaxSize = 32 * 1024;

        /// <summary>
        /// Our servers only accept request smaller than 512KB we left 12kb as margin error
        /// </summary>
        private const int BatchMaxSize = 500 * 1024;

        private readonly string _apiKey;

        private readonly CancellationTokenSource _continue;

        private readonly TimeSpan _flushInterval;

        private readonly int _maxBatchSize;

        private readonly int _maxQueueSize;

        private readonly ConcurrentQueue<BaseAction> _queue;

        private readonly IRequestHandler _requestHandler;

        private readonly Semaphore _semaphore;

        private readonly int _threads;

        private Timer? _timer;

        internal AsyncIntervalFlushHandler(IRequestHandler requestHandler,
            int maxQueueSize,
            int maxBatchSize,
            TimeSpan flushInterval,
            int threads,
            string apiKey)
        {
            // Debug.Log("AsyncFlushHandler Created");

            _queue = new ConcurrentQueue<BaseAction>();
            _requestHandler = requestHandler;
            _maxQueueSize = maxQueueSize;
            _maxBatchSize = maxBatchSize;
            _continue = new CancellationTokenSource();
            _flushInterval = flushInterval;
            _threads = threads;
            _semaphore = new Semaphore(_threads, _threads);
            _apiKey = apiKey;

            // Debug.Log("AsyncFlushHandler - running interval");
            RunInterval();
            // Debug.Log("AsyncFlushHandler - interval running");
        }

        public void Dispose()
        {
            _timer?.Dispose();
            _semaphore?.Dispose();
            _continue?.Cancel();
        }

        public async Task FlushAsync()
        {
            // Debug.Log("Flushing...");
            await PerformFlush().ConfigureAwait(false);
            WaitWorkersToBeReleased();
        }

        public void Process(BaseAction action)
        {
            action.Size = JsonConvert.SerializeObject(action).Length;

            if (action.Size > ActionMaxSize)
            {
                return;
            }

            _queue.Enqueue(action);
            if (_queue.Count >= _maxQueueSize)
            {
                _ = PerformFlush();
            }
        }


        private async Task FlushImpl()
        {
            // Debug.Log("Starting FlushImpl method.");

            var current = new List<BaseAction>();
            // Debug.Log("Initialized current list.");

            var currentSize = 0;
            // Debug.Log("Initialized currentSize to 0.");

            // Debug.Log("queue.IsEmpty: " + _queue.IsEmpty + " _continue.Token.IsCancellationRequested: " + _continue.Token.IsCancellationRequested);

            while (!_queue.IsEmpty && !_continue.Token.IsCancellationRequested)
            {
                // Debug.Log("Entering main loop as queue is not empty and cancellation has not been requested.");

                do
                {
                    // Debug.Log("Entering inner loop.");

                    if (!_queue.TryDequeue(out var action))
                    {
                        // Debug.Log("Failed to dequeue action. Breaking out of inner loop.");
                        break;
                    }

                    // Debug.Log("Successfully dequeued action.");

                    current.Add(action);
                    // Debug.Log($"Added action to current list. Current list size: {current.Count}");

                    currentSize += action.Size;
                    // Debug.Log($"Updated currentSize to {currentSize}.");

                } while (!_queue.IsEmpty && current.Count < _maxBatchSize && !_continue.Token.IsCancellationRequested &&
                         currentSize < BatchMaxSize - ActionMaxSize);
                // Debug.Log("Exited inner loop.");

                if (current.Count > 0)
                {
                    // Debug.Log("Current list has actions to process.");

                    // we have a batch that we're trying to send
                    var batch = new Batch(current, _apiKey);
                    // Debug.Log("Created a new batch with api key" + _apiKey);

                    // make the request here
                    await _requestHandler.MakeRequest(batch);
                    // Debug.Log("Made a request with the batch.");

                    // mark the current batch as null
                    current = new List<BaseAction>();
                    // Debug.Log("Reinitialized current list.");

                    currentSize = 0;
                    // Debug.Log("Reset currentSize to 0.");
                }
                else
                {
                    // Debug.Log("Current list is empty. No actions to process.");
                }
            }

            // Debug.Log("Exited main loop. Ending FlushImpl method.");
        }


        private async void IntervalCallback(object state)
        {
            await PerformFlush();
        }

        private async Task PerformFlush()
        {
            // Debug.Log("Performing flush");
            if (!_semaphore.WaitOne(1))
            {
                // Debug.Log("Semaphore not available");
                return;
            }

            try
            {
                await FlushImpl();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void RunInterval()
        {
            var initialDelay = _queue.Count == 0 ? _flushInterval : TimeSpan.Zero;
            _timer = new Timer(IntervalCallback, new { }, initialDelay, _flushInterval);
        }

        private void WaitWorkersToBeReleased()
        {
            for (var i = 0; i < _threads; i++) _semaphore.WaitOne();
            _semaphore.Release(_threads);
        }
    }
}