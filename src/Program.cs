using System;
using System.Reactive.Linq;
using System.Threading;
using Notakey.SDK;

namespace Notakey.HelloWorld.CSharp
{
    class MainClass
    {
        static ManualResetEvent waitEvent = new ManualResetEvent(false);

        public static void Main(string[] args)
        {
            Console.WriteLine("Binding to API ...");

            /**
             * 1. The first task is to bind a SimpleApi instance to an API (endpoint + access_id combination)
             * After the api instance is bound to the remote API, it can be used to perform other operations.
             * 
             * NOTE: before you are bound to an application, do not use any other functions!
             */
            var api = new SimpleApi();
            api
                .Bind("https://demo.notakey.com/api", "235879a9-a3f3-42b4-b13a-4836d0fd3bf8")
                .SingleAsync()
                .Subscribe(uri => OnBound(uri, api), OnBindError);

            waitEvent.WaitOne();

            Console.WriteLine("Finished. Press RETURN to quit.");
            Console.ReadLine();
        }

        /// <summary>
        /// Callback that is invoked once the API instance is bound to a Notakey application.
        /// 
        /// From this point on, you can request authentication for users.
        /// </summary>
        /// <param name="address"></param>
        private static void OnBound(Uri address, SimpleApi boundApi)
        {
            Console.WriteLine("SUCCESS: bound to {0}", address);
            Console.WriteLine("Requesting verification for user 'demo' ...");
            
            /* 2. Now that we are bound to an application, 
             * we can invoke PerformFullVerification and other
             * methods.
             * 
             * PerformFullVerification will return once the result is known
             * (approved/denied) or the request expires (by default 5 minutes).
             */
            boundApi
                .PerformFullVerification("demo", "Notakey .NET Demo", "This is an example authentication request sent from the .NET example application", null)
                .SingleAsync()
                .Finally(() => waitEvent.Set())
                .Subscribe(OnVerificationResponse, OnVerificationError);
        }

        /// <summary>
        /// Callback that is invoked once the authentication request is processed or expires.
        /// </summary>
        /// <param name="request"></param>
        private static void OnVerificationResponse(ApprovalRequestResponse request)
        {
            /**
             * 3. ApprovalGranted is a convenience property, but it does not perform
             * signature validation.
             * 
             * In a real-world scenario, you could now verify the response payload
             * to be sure of the received data.
             */
            Console.WriteLine("SUCCESS: verification response: {0}", request.ApprovalGranted);
        }

        /// <summary>
        /// Callback that is invoked if the authentication request could not be made (or response loaded)
        /// </summary>
        /// <param name="e"></param>
        private static void OnVerificationError(Exception e)
        {
            Console.Error.WriteLine("ERROR: failed to perform verification: {0}", e.ToString());
        }

        /// <summary>
        /// Callback that is invoked, if Notakey.SDK can not bind to API
        /// </summary>
        /// <param name="e"></param>
        private static void OnBindError(Exception e)
        {
            waitEvent.Set();
            Console.Error.WriteLine("ERROR: failed to bind to API: {0}", e.ToString());
        }
    }
}
