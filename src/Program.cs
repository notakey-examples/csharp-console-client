using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using Notakey.SDK;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Notakey.Examples.SDK.Client
{
    class MainClass
    {
        static MyKeystore keyStore = new MyKeystore();
        static ManualResetEvent waitEvent = new ManualResetEvent(false);
        static AccessToken myToken;
        static NtkService ntkApi;
        // List of emitted keytokens
        static Dictionary<string, string> userKeyTokens = new Dictionary<string, string> { };

        public static void Main(string[] args)
        {
            Console.WriteLine("Binding to API ...");

            /**
             * 1. The first task is to bind a SimpleApi instance to an API (endpoint + access_id combination)
             * After the api instance is bound to the remote API, it can be used to perform other operations.
             *
             * NOTE: before you are bound to an application, do not use any other functions!
             */

            // demo
            var api = new NtkService("https://demoapi.notakey.com/api", "235879a9-a3f3-42b4-b13a-4836d0fd3bf8");

            var userName = "demo";

            // You can also detect if API is in good shape before requesting anything
            // api
            //     .PerformHealthCheck()
            //     .Subscribe(hc => Console.WriteLine("Health: {0} = {1}",
            //         hc.FirstOrDefault(x => x.Key == "STATUS").Key,
            //         hc.FirstOrDefault(x => x.Key == "STATUS").Value));

            // Bind to API with client ID and secret
            api
                .Bind("469082d815d273fa6c410338f4e6e817a68772ca3766ad4fc3bfb4ae28b72525",
                    "d8281809e82d95d8279448d0f7c3a806691fc307a4bc66018b19ce57b4019cf6",
                     new List<string> { "urn:notakey:auth", "urn:notakey:keyservice" })
                .SingleAsync()
                .Subscribe(myToken => OnBound(myToken, api, userName), OnBindError);

            waitEvent.WaitOne();

            Console.WriteLine("Finished first authentication request");

            api
                .Bind(myToken)
                .SingleAsync()
                .Subscribe(myToken => OnBound(myToken, api, userName), OnBindError);

            waitEvent.Reset();

            waitEvent.WaitOne();

            Console.WriteLine("Finished second authentication request");

            if (userKeyTokens.Count != 2)
            {
                Console.WriteLine("Some or all key tokens are missing, cannot continue");
                Console.ReadLine();
                return;
            }

            Console.WriteLine("Will perform end-to-end encrypted message demo");


            MessagingTest();

            Console.WriteLine("All operations complete, press any key");
            Console.ReadLine();
        }

        /// <summary>
        /// Callback that is invoked once the API instance is bound to a Notakey application.
        ///
        /// From this point on, you can request authentication for users.
        /// </summary>
        /// <param name="address"></param>
        private static void OnBound(AccessToken token, NtkService boundApi, string userName)
        {
            // store api reference to be used in messaging demo
            ntkApi = boundApi;
            Console.WriteLine("SUCCESS: bound to {0} with token {1}", ntkApi.BoundParams.ApplicationEndpoint.AbsolutePath, token.Token);
            Console.WriteLine("Access token valid until {0} UTC", token.ValidBefore.ToString());
            Console.WriteLine("Requesting verification for user '{0}' ...", userName);

            // Store token for later reuse
            myToken = token;

            /* 2. Now that we are bound to an application,
             * we can invoke PerformFullVerification and other
             * methods.
             *
             * PerformFullVerification will return once the result is known
             * (approved/denied) or the request expires (by default 5 minutes).
             */
            ntkApi
                .PerformFullVerification(userName, "Notakey .NET Demo", "This is an example authentication request sent from the .NET example application", Path.GetRandomFileName())
                .SingleAsync()
                .Finally(() => waitEvent.Set())
                .Subscribe(resp => OnVerificationResponse(resp, boundApi), OnVerificationError);
        }

        /// <summary>
        /// Callback that is invoked once the authentication request is processed or expires.
        /// </summary>
        /// <param name="request"></param>
        private static void OnVerificationResponse(AuthResponse request, NtkService boundApi)
        {
            /**
             * 3. ApprovalGranted is a convenience property, but it does not perform
             * signature validation.
             *
             * In a real-world scenario, you could now verify the response payload
             * to be sure of the received data.
             */

            Console.WriteLine("SUCCESS: verification response: {0}", request.ApprovalGranted);
            Console.WriteLine("Storing keytoken {0} for user ID {1}", request.KeyToken, request.UserId);
            userKeyTokens.Add(request.KeyToken, request.UserId);

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

        private static void MessagingTest()
        {

            var sendTokenPair = userKeyTokens.Last();
            userKeyTokens.Remove(sendTokenPair.Key);
            var rcvTokenPair = userKeyTokens.Last();
            userKeyTokens.Remove(rcvTokenPair.Key);

            var senderCypherApi = new NtkCypher(ntkApi, keyStore, new NtkCryptoEntity(rcvTokenPair.Value));
            var rcvCypherApi = new NtkCypher(ntkApi, keyStore, new NtkCryptoEntity(rcvTokenPair.Value));

            NtkCryptoEntity sender, receiver;

            try
            {
                var message = "Hello world!";

                Console.WriteLine("Using keytoken {0} for send", sendTokenPair);
                // BootstrapEntity returns NtkCryptoEntity object with Pkey field identifying current user, that can be used as receiver identifier
                // Pkey.Expired can be used for check if key needs to be bootstraped again
                sender = senderCypherApi.BootstrapEntity(sendTokenPair.Key).GetAwaiter().GetResult();
                Console.WriteLine("Registered key for sender {0} ({1}):\n{2}", sender.UserId, sender.Pkey.Uuid, sender.Pkey.ToString());

                if (!sender.Pkey.Expired)
                {
                    Console.WriteLine("Sender key is valid, can receive messages until {0}", sender.Pkey.ValidBefore);
                }

                Console.WriteLine("Using keytoken {0} for receive", rcvTokenPair);
                receiver = rcvCypherApi.BootstrapEntity(rcvTokenPair.Key).GetAwaiter().GetResult();
                Console.WriteLine("Registered key for receiver {0} ({1}):\n{2}", receiver.UserId, receiver.Pkey.Uuid, receiver.Pkey.ToString());

                if (rcvCypherApi.QueryOwner().Expired)
                {
                    Console.WriteLine("Receiver key has expired");
                    // request new authentication and trade rcvTokenPair.Key for new key
                    // makes sense to do this before key has already expired
                    receiver = rcvCypherApi.BootstrapEntity(rcvTokenPair.Key).GetAwaiter().GetResult();

                }

                // binary format
                var cypherB = senderCypherApi.SendTo(receiver.Pkey.Uuid, System.Text.Encoding.UTF8.GetBytes(message)).GetAwaiter().GetResult();
                Console.WriteLine("Sending cypher to user {0}:\n{1} ({2} bytes)", receiver.UserId, "<binary data>", cypherB.Length);

                var resB = rcvCypherApi.ReceiveMsg(cypherB).GetAwaiter().GetResult();
                var gotMessage = System.Text.Encoding.UTF8.GetString(resB);
                Console.WriteLine("Decrypted cypher from user {0}:\n{1}", sender.UserId, gotMessage);

                // serialized string in base64 and reversed sender / receiver
                var cypher = rcvCypherApi.SendTo(sender.Pkey.Uuid, "Cool, got your message: " + gotMessage).GetAwaiter().GetResult();
                Console.WriteLine("Sending cypher to user {0}:\n{1} ({2} bytes)", sender.UserId, cypher, cypher.Length);

                var res = senderCypherApi.ReceiveMsg(cypher).GetAwaiter().GetResult();
                Console.WriteLine("Decrypted cypher from user {0}:\n{1}", receiver.UserId, res);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Caught exception: {0} {1}", nameof(ex), ex.Message);
            }

        }

    }


    public class MyKeystore : INtkCypherStore
    {
        Dictionary<string, Tuple<byte[], byte[], int>> keyStore = new Dictionary<string, Tuple<byte[], byte[], int>>();

        public void StoreKey(string keyUuid, byte[] pkey, byte[] okey, int exp)
        {
            if (GetKey(keyUuid) == null)
                keyStore.Add(keyUuid, new Tuple<byte[], byte[], int>(pkey, okey, exp));
        }

        public Tuple<byte[], byte[], int> GetKey(string keyUuid)
        {
            return keyStore.FirstOrDefault(k => k.Key.Equals(keyUuid)).Value;
        }
    }
}

