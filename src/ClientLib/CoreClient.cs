/*
 * Copyright 2017 Red Hat Inc.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;

using Apache.Qpid.Proton.Client;

namespace ClientLib
{
    /// <summary>
    /// Represent together properties of sender and receiver
    /// </summary>
    public class CoreClient
    {
        ///private members
        protected int exitCode;
        protected List<double> ptsdata;
        protected double ts;
        protected string address;
        protected IClient client;
        protected ISession session;
        protected IConnection connection;

        /// <summary>
        /// Constructor of core class
        /// </summary>
        public CoreClient()
        {
            this.exitCode = ReturnCode.ERROR_OTHER;

            //add trapper for unhandled exception (System.Net.Sockets)
            AppDomain.CurrentDomain.UnhandledException += this.UnhandledExceptionTrapper;
        }

        #region help methods
        /// <summary>
        /// Method for parse arguments from command line
        /// </summary>
        /// <param name="args">argument from command line</param>
        /// <param name="typeArguments">sender or receiver</param>
        /// <returns>options</returns>
        protected void ParseArguments(string[] args, Options typeArguments)
        {
            try {
                var unrecognized = typeArguments.Parse(args);
                if (unrecognized.Count > 0)
                {
                    typeArguments.PrintHelp();
                    foreach (var item in unrecognized)
                        Console.WriteLine("ERROR: {{ 'cause': {0}}}", item);
                    Environment.Exit(ReturnCode.ERROR_ARG);
                }
            }
            catch
            {
                typeArguments.PrintHelp();
                Environment.Exit(ReturnCode.ERROR_ARG);
            }
            this.SetUpCliLogging(typeArguments);
        }

        /// <summary>
        /// Method for enabling cli logging
        /// </summary>
        /// <param name="options">parsed options from cmd</param>
        private void SetUpCliLogging(Options options)
        {
            // TODO
        }
        #endregion

        #region Connection and session methods
        /// <summary>
        /// Method for set address
        /// </summary>
        /// <param name="url">string url</param>
        protected void SetAddress(string url)
        {
            this.address = url;
        }

        /// <summary>
        /// Method for create connection
        /// </summary>
        protected void CreateConnection(ConnectionOptions options)
        {
            string rest = options.Url;
            string hostport;
            string serverHost;
            int serverPort;
            string user = null;
            string password = null;
            string address = null;
            string scheme = null;

            if (rest.Split("://").Length > 1) {
                scheme = rest.Split("://")[0];
                rest = rest.Split("://")[1];
            }

            if (rest.Split('@').Length > 1) {
                string credentials = rest.Split('@')[0];
                rest = rest.Split('@')[1];
                user = credentials.Split(':')[0];
                if (credentials.Split(':').Length > 1) {
                    password = credentials.Split(':')[1];
                }
            }

            if (rest.Split('/').Length > 1) {
                address = rest.Split('/')[1];
                hostport = rest.Split('/')[0];
            } else {
                hostport = rest;
            }

            if (hostport.Split(':').Length > 1) {
               serverHost = hostport.Split(':')[0];
               serverPort = int.Parse(hostport.Split(':')[1]);
            } else {
               serverHost = hostport;
               serverPort = 5672;
	    }

            this.client = IClient.Create();

	    // TODO SSL

            Apache.Qpid.Proton.Client.ConnectionOptions conn_options = new Apache.Qpid.Proton.Client.ConnectionOptions();
            conn_options.User = user;
            conn_options.Password = password;

            this.connection = client.Connect(serverHost, serverPort, conn_options);
        }

        /// <summary>
        /// Method for create session
        /// </summary>
        protected void CreateSession()
        {
            this.session = this.connection.OpenSession();
        }

        /// <summary>
        /// Private method to close session
        /// </summary>
        private void CloseSession()
        {
            if (this.session != null)
                this.session.Close();
        }

        /// <summary>
        /// Method for close link
        /// </summary>
        /// <param name="link">sender or receiver link</param>
        protected void CloseLink(ILink link)
        {
            if (link != null)
                link.Close();
        }

        /// <summary>
        /// Method for close session and connection
        /// </summary>
        protected void CloseConnection()
        {
            if (this.connection != null)
            {
                this.CloseSession();
                this.connection.Close();
            }
        }

        /// <summary>
        /// Method for close client
        /// </summary>
        protected void CloseClient()
	// TODO is this desired ??
        {
            if (this.client != null)
            {
                this.client.Close();
            }
        }
        #endregion

        #region exception methods
        /// <summary>
        /// Method for handling argument exception
        /// </summary>
        /// <param name="ex">exception</param>
        /// <param name="options">parsed options</param>
        protected void ArgumentExceptionHandler(Exception ex, Options options)
        {
            Console.Error.WriteLine(ex.StackTrace);
            Console.Error.WriteLine("Invalid command option: " + ex.Message);
            options.PrintHelp();
            this.exitCode = ReturnCode.ERROR_ARG;
        }

        /// <summary>
        /// Method for handling other exception
        /// </summary>
        /// <param name="ex">exception</param>
        /// <param name="options">parsed options</param>
        protected void OtherExceptionHandler(Exception ex, Options options)
        {
            Console.Error.WriteLine(ex.StackTrace);
            Console.Error.WriteLine("ERROR: {{'cause': '{0}'}}", ex.Message.ToString());

            if (options is SenderOptions || options is ReceiverOptions)
            {
                Utils.TsSnapStore(this.ptsdata, 'G', (options as SenderReceiverOptions).LogStats);
            }

            //report timestamping
            if (this.ptsdata != null && this.ptsdata.Count > 0)
            {
                Console.WriteLine("STATS " + Utils.TsReport(this.ptsdata, -1, -1, 1));
            }
            this.exitCode = ReturnCode.ERROR_OTHER;
        }

        /// <summary>
        /// Method to rap unhandled exception
        /// </summary>
        /// <param name="sender">sender object</param>
        /// <param name="ex">exception</param>
        void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs ex)
        {
            Console.Error.WriteLine("ERROR: {{'cause': '{0}'}}", ex.ToString());
            Environment.Exit(ReturnCode.ERROR_OTHER);
        }
        #endregion
    }
}
