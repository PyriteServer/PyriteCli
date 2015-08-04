using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;

namespace PyriteCloudRole
{
    public class WorkerRole : RoleEntryPoint
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly ManualResetEvent runCompleteEvent = new ManualResetEvent(false);

        public override void Run()
        {
            Trace.TraceInformation("PyriteCloudRole is running");

            try
            {
                this.RunAsync(this.cancellationTokenSource.Token).Wait();
            }
            catch(Exception ex)
            {
                Trace.TraceError("Exception hit: " + ex.ToString());
                throw;                 
            }
            finally
            {
                this.runCompleteEvent.Set();
            }
        }

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            bool result = base.OnStart();

            Trace.TraceInformation("PyriteCloudRole has been started");

            return result;
        }

        public override void OnStop()
        {
            Trace.TraceInformation("PyriteCloudRole is stopping");

            this.cancellationTokenSource.Cancel();
            this.runCompleteEvent.WaitOne();

            base.OnStop();

            Trace.TraceInformation("PyriteCloudRole has stopped");
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            Scanner scanner = new Scanner();
            while (!cancellationToken.IsCancellationRequested)
            {
                await scanner.DoWorkAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(1000).ConfigureAwait(false);
            }
        }
    }
}
