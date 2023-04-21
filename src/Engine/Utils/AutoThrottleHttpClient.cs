using Microsoft.Extensions.Logging;

namespace Engine.Utils
{

    public class AutoThrottleHttpClient : HttpClient
    {

        #region Constructor, Props, and Privates

        private readonly bool ignoreRetryHeader;
        private readonly ILogger debugTracer;
        private DateTime? _nextCallEarliestTime = null;
        private int _concurrentCalls = 0, _throttledCalls = 0, _completedCalls = 0;
        private object _concurrentCallsObj = new object(), _throttledCallsObject = new object(), _completedCallsObject = new object();


        public AutoThrottleHttpClient(bool ignoreRetryHeader, ILogger debugTracer)
        {
            this.Timeout = TimeSpan.FromHours(1);
            this.ignoreRetryHeader = ignoreRetryHeader;
            this.debugTracer = debugTracer;
        }
        public AutoThrottleHttpClient(bool ignoreRetryHeader, ILogger debugTracer, DelegatingHandler handler) : base(handler)
        {
            this.Timeout = TimeSpan.FromHours(1);
            this.ignoreRetryHeader = ignoreRetryHeader;
            this.debugTracer = debugTracer;
        }


        #endregion

        /// <summary>
        /// Execute a method that returns a HttpResponseMessage, with throttling retry logic
        /// </summary>
        public async Task<HttpResponseMessage> ExecuteHttpCallWithThrottleRetries(Func<Task<HttpResponseMessage>> httpAction, string url)
        {
            HttpResponseMessage? response = null;
            int retries = 0, secondsToWait = 0;
            bool retryDownload = true;
            while (retryDownload)
            {
                lock (_concurrentCallsObj)
                {
                    _concurrentCalls++;
                }

                // Figure out if we need to wait. Sleep thread outside lock
                TimeSpan? sleepTimeNeeded = null;
                lock (this)
                {
                    if (_nextCallEarliestTime != null && _nextCallEarliestTime > DateTime.Now)
                    {
                        sleepTimeNeeded = _nextCallEarliestTime.Value.Subtract(DateTime.Now);
                    }
                }
                if (sleepTimeNeeded.HasValue)
                {
                    lock (this)
                    {
                        _throttledCalls++;
                    }
                    Thread.Sleep(sleepTimeNeeded.Value);
                    lock (this)
                    {
                        _nextCallEarliestTime = null;
                    }
                }

                // Get response but don't buffer full content (which will buffer overlflow for large files)
                response = await httpAction();

                lock (_concurrentCallsObj)
                {
                    _concurrentCalls--;
                }

                if (!response.IsSuccessStatusCode && response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    retries++;
                    lock (this)
                    {
                        _throttledCalls++;
                    }

                    // Do we have a "retry-after" header & should we use it?
                    var waitValue = response.GetRetryAfterHeaderSeconds();
                    if (!ignoreRetryHeader && waitValue.HasValue)
                    {
                        secondsToWait = waitValue.Value;
                        debugTracer.LogWarning($"{Constants.THROTTLE_ERROR} for {url}. Waiting to retry for attempt #{retries} (from 'retry-after' header)...");
                    }
                    else
                    {
                        // We have to guess how much to back-off. Loop with ever-increasing wait.
                        if (retries == Constants.MAX_SPO_API_RETRIES)
                        {
                            // Don't try forever
                            debugTracer.LogError($"{Constants.THROTTLE_ERROR}. Maximum retry attempts {Constants.MAX_SPO_API_RETRIES} has been attempted for {url}.");

                            // Allow normal HTTP exception & abort download
                            response.EnsureSuccessStatusCode();
                        }

                        // We've not reached throttling max retries...keep retrying
                        debugTracer.LogTrace($"{Constants.THROTTLE_ERROR} downloading from REST. Waiting {retries} seconds to try again...");

                        secondsToWait = retries * 2;
                    }

                    // Wait before trying again
                    lock (this)
                    {
                        _nextCallEarliestTime = DateTime.Now.AddSeconds(secondsToWait);
                    }

                }
                else
                {
                    // Not HTTP 429. Don't bother retrying & let caller handle any error
                    retryDownload = false;

                    lock (_completedCallsObject)
                    {
                        _completedCalls++;
                    }
                }
            }

            return response!;
        }


        public int ConcurrentCalls
        {
            get
            {
                lock (_concurrentCallsObj)
                {
                    return _concurrentCalls;
                }
            }
        }
        public int ThrottledCalls
        {
            get
            {
                lock (_throttledCallsObject)
                {
                    return _throttledCalls;
                }
            }
        }

        public int CompletedCalls
        {
            get
            {
                lock (_completedCallsObject)
                {
                    return _completedCalls;
                }
            }
        }
    }

}
