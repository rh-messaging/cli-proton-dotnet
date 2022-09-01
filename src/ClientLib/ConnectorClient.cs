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
using System.Threading;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

using Apache.Qpid.Proton.Client;

namespace ClientLib
{
    /// <summary>
    /// Connector client class
    /// </summary>
    public class ConnectorClient : CoreClient
    {
        /// <summary>
        /// Method for run connector
        /// </summary>
        /// <param name="args">cmd line arguments</param>
        public void Run(string[] args)
        {
            Collection<IConnection> connections = new Collection<IConnection>();
            Collection<ISession> sessions = new Collection<ISession>();
            Collection<ISender> senders = new Collection<ISender>();
            Collection<IReceiver> receivers = new Collection<IReceiver>();

            ConnectorOptions options = new ConnectorOptions();

            try
            {
                //parse args
                this.ParseArguments(args, options);

                IClient client = IClient.Create();

                //create connections
                if (Regex.IsMatch(options.ObjCtrl, "[CESR]"))
                {

                    for (int i = 0; i < options.MsgCount; i++)
                    {
                        // TODO, host, port, options (incl. credentials)
                        connections.Add(client.Connect(options.Url));
                    }
                }

                //request to create at least a session (connection needed)
                if (Regex.IsMatch(options.ObjCtrl, "[ESR]"))
                {
                    try
                    {
                        foreach (IConnection conn in connections)
                        {
                            sessions.Add(conn.OpenSession());
                        }
                    }
                    // catch (AmqpException ae)
                    catch (Exception ae)
                    {
                        Console.Error.WriteLine(ae.Message);
                        for (int i = sessions.Count; i < connections.Count; i++)
                        {
                            sessions.Add(null);
                            Environment.Exit(ReturnCode.ERROR_OTHER);
                        }
                    }

                    string address = options.Address;

                    if (address != String.Empty)
                    {
                        if (Regex.IsMatch(options.ObjCtrl, "[S]"))
                        {
                            try
                            {
                                int i = 0;
                                foreach (ISession s in sessions)
                                {
                                    i++;
                                    if (s != null)
                                        senders.Add(s.OpenSender(address));
                                    else
                                        senders.Add(null);
                                }
                            }
                            catch (Exception ae)
                            {
                                Console.Error.WriteLine(ae.Message);
                                for (int i = senders.Count; i < sessions.Count; i++)
                                {
                                    senders.Add(null);
                                }
                                Environment.Exit(ReturnCode.ERROR_OTHER);
                            }
                        }
                        if (Regex.IsMatch(options.ObjCtrl, "[R]"))
                        {
                            try
                            {
                                int i = 0;
                                foreach (ISession s in sessions)
                                {
                                    i++;
                                    if (s != null) {
                                        receivers.Add(s.OpenReceiver(address));
				    }
                                    else
                                        receivers.Add(null);
                                }
                            }
                            catch (Exception ae)
                            {
                                Console.Error.WriteLine(ae.Message);
                                for (int i = receivers.Count; i < sessions.Count; i++)
                                {
                                    receivers.Add(null);
                                }
                                Environment.Exit(ReturnCode.ERROR_OTHER);
                            }
                        }
                    }
                }
                this.exitCode = ReturnCode.ERROR_SUCCESS;
            }
            catch (ArgumentException ex)
            {
                this.ArgumentExceptionHandler(ex, options);
            }
            catch (Exception ex)
            {
                this.OtherExceptionHandler(ex, options);
            }
            finally
            {
                if (options.CloseSleep > 0)
                    Thread.Sleep(options.CloseSleep);

                foreach (IReceiver rec in receivers)
                {
                    if (rec != null)
                    {
                        rec.Close();
                    }
                }
                foreach (ISender sen in senders)
                {
                    if (sen != null)
                    {
                        sen.Close();
                    }
                }
                foreach (ISession ses in sessions)
                {
                    if (ses != null)
                    {
                        ses.Close();
                    }
                }
                foreach (IConnection c in connections)
                {
                    if (c != null)
                    {
                        c.Close();
                    }
                }
            }
            Environment.Exit(this.exitCode);
        }
    }
}
