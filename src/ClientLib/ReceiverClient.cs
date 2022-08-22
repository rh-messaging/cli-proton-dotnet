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
using Apache.Qpid.Proton.Client;

namespace ClientLib
{
    /// <summary>
    /// Class represent receiver from amqp broker
    /// </summary>
    public class ReceiverClient : CoreClient
    {
        #region Help methods
        /// <summary>
        /// Method for prepare receiver link
        /// </summary>
        /// <param name="options">receiver options</param>
        /// <returns>build receiver link</returns>
        private IReceiver PrepareReceiverLink(ReceiverOptions options)
        {
            Apache.Qpid.Proton.Client.ReceiverOptions receiverOptions = new Apache.Qpid.Proton.Client.ReceiverOptions();
            if (options.RecvBrowse)
                receiverOptions.SourceOptions.DistributionMode = DistributionMode.Copy;

            bool tx_batch_flag = String.IsNullOrEmpty(options.TxLoopendAction) ? (options.TxSize > 0) : true;
            IReceiver receiver;
            if (tx_batch_flag) {
                receiver = this.session.OpenReceiver(options.Address, receiverOptions);
            } else {
                receiver = this.connection.OpenReceiver(options.Address, receiverOptions);
            }
            return receiver;
        }
        #endregion

        #region Receive methods
        /// <summary>
        /// Method for browse or selector receive
        /// </summary>
        /// <param name="receiver">receiver link</param>
        /// <param name="options">receiver options</param>
        private void ReceiveAll(IReceiver receiver, ReceiverOptions options)
        {
            IDelivery delivery = null;

            while ((delivery = receiver.Receive(options.Timeout)) != null || options.isInfinityReceiving)
            {
                IMessage<object> message = delivery.Message();
                if (message != null)
                {
                    Formatter.LogMessage(message, options);
                    Utils.TsSnapStore(this.ptsdata, 'F', options.LogStats);

                    if (!String.IsNullOrEmpty(options.MsgSelector))
                    {
                        // TODO accept message?
                    }
                }
            }
        }

        /// <summary>
        /// Method for transactional receiving messages
        /// </summary>
        /// <param name="receiver">receiver link</param>
        /// <param name="options">receiver options</param>
        private void TransactionReceive(IReceiver receiver, ReceiverOptions options)
        {
            bool txFlag = true;
            int nReceived = 0;
            IMessage<object> message = null;
                IDelivery delivery = null;

            this.session.BeginTransaction();

            while (txFlag && (nReceived < options.MsgCount || options.MsgCount == 0) && options.TxSize > 0)
            {
                    for (int i = 0; i < options.TxSize; i++)
                    {
                        delivery = receiver.Receive(options.Timeout);
                        if (delivery == null) {
                            break;
                        }

                        message = delivery.Message();
                        if (message != null)
                        {
                            Formatter.LogMessage(message, options);
                            nReceived++;
                        } else {
                            break;
                        }
                    }

                    if (delivery != null) {
                        if (options.TxAction.ToLower() == "commit") {
                             this.session.CommitTransaction();
                             this.session.BeginTransaction();
                        } else if (options.TxAction.ToLower() == "rollback") {
                             this.session.RollbackTransaction();
                             this.session.BeginTransaction();
                        }
                    } else {
                        break;
                    }

                if (message == null || (options.MsgCount > 0 && ((options.MsgCount - nReceived) < options.TxSize)))
                {
                    txFlag = false;
                }
            }

            while (nReceived < options.MsgCount || options.MsgCount == 0)
            {
                delivery = receiver.Receive(options.Timeout);
                if (delivery == null) {
                    break;
                }

                message = delivery.Message();
                if (message != null)
                {
                    Formatter.LogMessage(message, options);
                    nReceived++;
                }
            }

            if (options.TxLoopendAction.ToLower() == "commit") {
                 this.session.CommitTransaction();
            } else if (options.TxLoopendAction.ToLower() == "rollback") {
                 this.session.RollbackTransaction();
            }
        }

        /// <summary>
        /// Standard receiving
        /// </summary>
        /// <param name="receiver">receiver link</param>
        /// <param name="options">receiver options</param>
        private void Receive(IReceiver receiver, ReceiverOptions options)
        {
            int nReceived = 0;
            IDelivery delivery;

            while (((delivery = receiver.Receive(options.Timeout)) != null) && (nReceived < options.MsgCount || options.MsgCount == 0))
            {
                if (options.Duration > 0)
                {
                    Utils.Sleep4Next(ts, options.MsgCount, options.Duration, nReceived + 1);
                }

                IMessage<object> message = delivery.Message();

                Formatter.LogMessage(message, options);
                nReceived++;

                if (options.ProcessReplyTo)
                {
                    ISender sender = this.connection.OpenSender(message.ReplyTo);
                    sender.Send(message);
                }

                if ((options.MsgCount > 0) && (nReceived == options.MsgCount))
                {
                    break;
                }

                Utils.TsSnapStore(this.ptsdata, 'F', options.LogStats);
            }
        }
        #endregion

        /// <summary>
        /// Main method for receiver (receive messages)
        /// </summary>
        /// <param name="args">array arguments from command line</param>
        /// <returns>return code</returns>
        public void Run(string[] args)
        {
            ReceiverOptions options = new ReceiverOptions();

            try
            {
                this.ParseArguments(args, options);

                if (options.RecvListener)
                {
                    // TODO listener
                }
                else
                {
                    //init timestamping
                    this.ptsdata = Utils.TsInit(options.LogStats);

                    Utils.TsSnapStore(this.ptsdata, 'B', options.LogStats);

                    this.SetAddress(options.Url);
                    this.CreateConnection(options);

                    Utils.TsSnapStore(this.ptsdata, 'C', options.LogStats);

                    this.CreateSession();

                    Utils.TsSnapStore(this.ptsdata, 'D', options.LogStats);

                    IReceiver receiver = this.PrepareReceiverLink(options);

                    IMessage<object> message = IMessage<object>.Create();

                    this.ts = Utils.GetTime();

                    Utils.TsSnapStore(this.ptsdata, 'E', options.LogStats);
                    int nReceived = 0;

                    bool tx_batch_flag = String.IsNullOrEmpty(options.TxLoopendAction) ? (options.TxSize > 0) : true;

                    //receiving of messages
                    if (options.RecvBrowse || !String.IsNullOrEmpty(options.MsgSelector) || options.isInfinityReceiving)
                        this.ReceiveAll(receiver, options);
                    else
                    {
                        if (tx_batch_flag)
                            this.TransactionReceive(receiver, options);
                        else
                            this.Receive(receiver, options);
                    }

                    if (options.CloseSleep > 0)
                    {
                        System.Threading.Thread.Sleep(options.CloseSleep);
                    }

                    this.CloseConnection();

                    Utils.TsSnapStore(this.ptsdata, 'G', options.LogStats);

                    //report timestamping
                    if (this.ptsdata.Count > 0)
                    {
                        Console.WriteLine("STATS " + Utils.TsReport(this.ptsdata,
                            nReceived, message.Body.ToString().Length * sizeof(Char), 0));
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
                this.CloseConnection();
            }
            Environment.Exit(exitCode);
        }
    }
}
