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
using Apache.Qpid.Proton.Types;
using RecvOptions = Apache.Qpid.Proton.Client.ReceiverOptions;

namespace ClientLib
{

    /// <summary>
    /// AMQP JMS selecror class
    /// </summary>
    public class AmqpJmsSelectorType : IDescribedType
    {
        private string selector;
        public object Descriptor => 0x0000468C00000004UL;
        public object Described => selector;

        public AmqpJmsSelectorType(string selector)
        {
            this.selector = selector;
        }

        public override string ToString()
        {
            return "AmqpJmsSelectorType{" + selector + "}";
        }
    }

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
            string addr;
            RecvOptions receiverOptions = new RecvOptions();
            if (options.RecvBrowse)
                receiverOptions.SourceOptions.DistributionMode = DistributionMode.Copy;
            if (!String.IsNullOrEmpty(options.Action))
                receiverOptions.AutoAccept = false;
            if (options.AutoSettleOff.Equals(true))
                receiverOptions.AutoSettle = false;

            if (options.Settlement.Equals(SettlementMode.AtLeastOnce))
                receiverOptions.DeliveryMode = DeliveryMode.AtLeastOnce;
            else if (options.Settlement.Equals(SettlementMode.AtMostOnce))
                receiverOptions.DeliveryMode = DeliveryMode.AtMostOnce;
            else if (options.Settlement.Equals(SettlementMode.ExactlyOnce))
                throw new NotImplementedException("not supported by Qpid Proton DotNet client");

            if (!string.IsNullOrEmpty(options.MsgSelector))
            {
               IDescribedType clientJmsSelector = new AmqpJmsSelectorType(options.MsgSelector);
               IDictionary<string, object> filters = new Dictionary<string, object>();
               filters.Add("jms-selector", clientJmsSelector);
               receiverOptions.SourceOptions.Filters = filters;
            }

            bool txBatchFlag = String.IsNullOrEmpty(options.TxLoopendAction) ? (options.TxSize > 0) : true;


            if (this.address != null) {
                addr = this.address;
            } else {
                addr = options.Address;
            }

            IReceiver receiver;
            if (txBatchFlag) {
                receiver = this.session.OpenReceiver(addr, receiverOptions);
            } else {
                receiver = this.connection.OpenReceiver(addr, receiverOptions);
            }
            return receiver;
        }

        /// <summary>
        /// Method performs desired action on delivery
        /// </summary>
        /// <param name="delivery">delivery object</param>
        /// <param name="action">acknowlegde action (accept, reject, release, noack)</param>
        /// <returns>null</returns>
        private void DeliveryAcknowledge(IDelivery delivery, string action, bool autoSettleOff)
        {
            if (action.ToLower().Equals("reject"))
                delivery.Reject("test-condition", "test-description");
            else if (action.ToLower().Equals("release"))
                delivery.Release();
            else if (!action.ToLower().Equals("noack"))
                delivery.Accept();
	    else if (autoSettleOff.Equals(true))
		delivery.Settle();
        }
        #endregion

        #region Receive methods
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

		        if (!String.IsNullOrEmpty(options.Action))
		            DeliveryAcknowledge(delivery, options.Action, options.AutoSettleOff);

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

		if (!String.IsNullOrEmpty(options.Action))
		    DeliveryAcknowledge(delivery, options.Action, options.AutoSettleOff);

                Formatter.LogMessage(message, options);
                nReceived++;

                if (options.ProcessReplyTo)
                {
                    ISender sender = this.connection.OpenSender(message.ReplyTo);
                    sender.Send(message);
                    sender.Close();
                }

                if ((options.MsgCount > 0) && (nReceived == options.MsgCount))
                    break;

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
                    throw new NotImplementedException("Listener functionality is not available Qpid Proton DotNet client");
                }
                else
                {
                    bool txBatchFlag = String.IsNullOrEmpty(options.TxLoopendAction) ? (options.TxSize > 0) : true;

                    //init timestamping
                    this.ptsdata = Utils.TsInit(options.LogStats);

                    Utils.TsSnapStore(this.ptsdata, 'B', options.LogStats);

                    this.CreateConnection(options);

                    Utils.TsSnapStore(this.ptsdata, 'C', options.LogStats);

                    // session are only used for transactions
                    if (txBatchFlag)
                        this.CreateSession();

                    Utils.TsSnapStore(this.ptsdata, 'D', options.LogStats);

                    IReceiver receiver = this.PrepareReceiverLink(options);

                    this.ts = Utils.GetTime();

                    Utils.TsSnapStore(this.ptsdata, 'E', options.LogStats);
                    int nReceived = 0;

                    if (options.Capacity > -1)
                        receiver.AddCredit(Convert.ToUInt32(options.Capacity));

		    //receiving of messages
                    if (txBatchFlag)
                        this.TransactionReceive(receiver, options);
                    else
                        this.Receive(receiver, options);

                    if (options.CloseSleep > 0)
                    {
                        System.Threading.Thread.Sleep(options.CloseSleep);
                    }

                    // resource cleanup
                    this.CloseLink(receiver);
                    this.CloseClient();

                    Utils.TsSnapStore(this.ptsdata, 'G', options.LogStats);

                    //report timestamping
                    if (this.ptsdata.Count > 0)
                    {
                        Console.WriteLine("STATS " + Utils.TsReport(this.ptsdata, nReceived, 0, 0));
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
                this.CloseClient();
            }
            Environment.Exit(exitCode);
        }
    }
}
