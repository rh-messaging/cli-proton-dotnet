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
using System.Diagnostics;
using System.Transactions;

using Apache.Qpid.Proton.Types.Messaging;
using Apache.Qpid.Proton.Client;
using SendOptions = Apache.Qpid.Proton.Client.SenderOptions;

namespace ClientLib
{
    /// <summary>
    /// Class represent sender to amqp broker
    /// </summary>
    public class SenderClient : CoreClient
    {
        #region Content methods
        /// <summary>
        /// Method create method content
        /// </summary>
        /// <param name="options">options from parse arguments from cmd</param>
        /// <returns>content</returns>
        static object CreateMsgContent(SenderOptions options, int indexOfMessage)
        {
            object content = String.Empty;

            if (!(String.IsNullOrEmpty(options.Content))) {
                if (options.Content.Contains("{0}"))
                    content = String.Format((string)options.Content, indexOfMessage);
                else
                    content = options.Content;
            }

            else if (options.ListContent.Count > 0)
                content = options.ListContent;
            else if (options.MapContent.Count > 0)
                content = options.MapContent;
            else if (!(String.IsNullOrEmpty(options.ContentFromFile)))
                content = options.ContentFromFile;
            return content;
        }

        /// <summary>
        /// Method create message
        /// </summary>
        /// <param name="options">options from parse arguments from cmd</param>
        /// <param name="nSent">count of send message</param>
        /// <returns>message</returns>
        static IMessage<object> CreateMessage(SenderOptions options, int nSent)
        {
            object content = CreateMsgContent(options, nSent);
            IMessage<object> basemsg = IMessage<object>.Create(content);
            IAdvancedMessage<object> msg = basemsg.ToAdvancedMessage();

            if (!String.IsNullOrEmpty(options.Id))
                msg.MessageId = options.Id;
            if (!String.IsNullOrEmpty(options.CorrelationId))
                msg.CorrelationId = options.CorrelationId;
            if (!String.IsNullOrEmpty(options.Subject))
                msg.Subject = options.Subject;
            if (!String.IsNullOrEmpty(options.ContentType))
                msg.ContentType = options.ContentType;
            if (options.UserId != null && options.UserId.Length != 0)
                msg.UserId = options.UserId;
            if (!String.IsNullOrEmpty(options.ReplyTo))
                msg.ReplyTo = options.ReplyTo;

            if (options.MessageAnnotations.Count  > 0) {
                msg.Annotations = new MessageAnnotations(options.MessageAnnotations);
            }

            if (!String.IsNullOrEmpty(options.GroupId))
                msg.GroupId = options.GroupId;
            if (!String.IsNullOrEmpty(options.GroupId))
                msg.GroupId = options.GroupId;
            msg.GroupSequence = (uint)options.GroupSequence;
            if (!String.IsNullOrEmpty(options.ReplyToGroupId))
                msg.ReplyToGroupId = options.ReplyToGroupId;
            if (!String.IsNullOrEmpty(options.To))
                msg.To = options.To;

            // set up message header
            msg.Header = new Header();

            if (options.Durable.HasValue)
            {
                msg.Durable = options.Durable.Value;
            }

            if (options.Priority.HasValue)
            {
                msg.Priority = options.Priority.Value;
            }

            if (options.Ttl.HasValue)
            {
                msg.TimeToLive = options.Ttl.Value;
            }

            //set up application properties
            if (options.Properties.Count > 0)
            {
                foreach (KeyValuePair<string, object> p in options.Properties)
                {
                    msg.SetProperty(p.Key.ToString(), p.Value);
                }
            }
            return msg;
        }
        #endregion

        #region Help method
        /// <summary>
        /// Method for return sender statistic dictionary
        /// </summary>
        /// <param name="snd">sender</param>
        /// <returns>dictionary object for sender stats</returns>
         static Dictionary<string, object> GetSenderStats(ISender snd)
         {
             Dictionary<string, object> stats = new Dictionary<string, object>();
             Dictionary<string, object> sender = new Dictionary<string, object>();
             //code
             stats["sender"] = sender;
             stats["timestamp"] = Utils.GetTime();
             return stats;
         }

        /// <summary>
        /// Prepare sender link with options
        /// </summary>
        /// <param name="options">sender options</param>
        /// <returns>built sender link</returns>
        private ISender PrepareSender(SenderOptions options)
        {
            string addr;
            SendOptions SenderOptions = new SendOptions();
            if (!options.isInfinitySending)
                SenderOptions.SendTimeout = options.Timeout;

            if (!options.AutoSettle.Equals(true))
                SenderOptions.AutoSettle = false;
            if (options.Settlement.Equals(SettlementMode.AtLeastOnce))
                SenderOptions.DeliveryMode = DeliveryMode.AtLeastOnce;
            else if (options.Settlement.Equals(SettlementMode.AtMostOnce))
                SenderOptions.DeliveryMode = DeliveryMode.AtMostOnce;
            else if (options.Settlement.Equals(SettlementMode.ExactlyOnce))
                throw new NotImplementedException("not supported by Qpid Proton DotNet client");

            bool txBatchFlag = String.IsNullOrEmpty(options.TxLoopendAction) ? (options.TxSize > 0) : true;

            if (this.address != null)
                addr = this.address;
	    else
		addr = options.Address;

            ISender sender;
            if (txBatchFlag)
                sender = this.session.OpenSender(addr, SenderOptions);
            else
                sender = this.connection.OpenSender(addr, SenderOptions);

            return sender;
        }
        #endregion

        #region Send methods
        /// <summary>
        /// Method for transactional sending of messages
        /// </summary>
        /// <param name="sender">sender link</param>
        /// <param name="options">options</param>
        private void TransactionSend(ISender sender, SenderOptions options)
        {
            int nSent = 0;
            bool txFlag = true;
            IMessage<object> message;

            this.session.BeginTransaction();

            while (txFlag && options.TxSize > 0)
            {
                using (var txs = new TransactionScope(TransactionScopeOption.RequiresNew))
                {
                    for (int i = 0; i < options.TxSize; i++)
                    {
                        message = CreateMessage(options, nSent);

                        if ((options.Duration > 0) && (options.DurationMode == "before-send"))
                            Utils.Sleep4Next(this.ts, options.MsgCount, (options.Duration), nSent + 1);

                        sender.Send(message);

                        if ((options.Duration > 0) && (options.DurationMode == "after-send-before-tx-action"))
                            Utils.Sleep4Next(this.ts, options.MsgCount, (options.Duration), nSent + 1);

                        Formatter.LogMessage(message, options);

                        nSent++;
                    }

                    if (options.TxAction.ToLower() == "commit") {
                        txs.Complete();
                        this.session.CommitTransaction();
                        this.session.BeginTransaction();
                    } else if (options.TxAction.ToLower() == "rollback") {
                        txs.Complete();
                        this.session.RollbackTransaction();
                        this.session.BeginTransaction();
                    }

                    if ((options.Duration > 0) && (options.DurationMode == "after-send-after-tx-action"))
                        Utils.Sleep4Next(ts, options.MsgCount, (options.Duration), nSent);

                }
                //set up txBatchFlag
                if ((options.MsgCount - nSent) < options.TxSize)
                {
                    txFlag = false;
                }
            }
            //rest of messages
            using (var txs = new TransactionScope(TransactionScopeOption.RequiresNew))
            {
                while (nSent < options.MsgCount)
                {
                    message = CreateMessage(options, nSent);
                    sender.Send(message);
                    Formatter.LogMessage(message, options);
                    nSent++;
                }

                if (options.TxLoopendAction.ToLower() == "commit") {
                    txs.Complete();
                    this.session.CommitTransaction();
                } else if (options.TxLoopendAction.ToLower() == "rollback") {
                    txs.Complete();
                    this.session.RollbackTransaction();
                }
            }
        }

        /// <summary>
        /// Method for standard sending of messages
        /// </summary>
        /// <param name="sender">sender link</param>
        /// <param name="options">options</param>
        private void Send(ISender sender, SenderOptions options)
        {
            int nSent = 0;
            IMessage<object> message;

            while ((nSent < options.MsgCount))
            {
                message = CreateMessage(options, nSent);
                if ((options.Duration > 0) && (options.DurationMode == "before-send"))
                    Utils.Sleep4Next(ts, options.MsgCount, (options.Duration), nSent + 1);

                ITracker tracker = sender.Send(message);
                tracker.AwaitSettlement();
		if (!options.AutoSettle.Equals(true))
                    tracker.Settle();

                if ((options.Duration > 0) && ((options.DurationMode == "after-send-before-tx-action") ||
                        (options.DurationMode == "after-send-after-tx-action")))
                    Utils.Sleep4Next(ts, options.MsgCount, (options.Duration), nSent + 1);

                Formatter.LogMessage(message, options);
                nSent++;
            }
        }
        #endregion

        /// <summary>
        /// Main method of sender
        /// </summary>
        /// <param name="args">args from command line</param>
        /// <returns>int status exit code</returns>
        public void Run(string[] args)
        {
            SenderOptions options = new SenderOptions();

            try
            {
                this.ParseArguments(args, options);
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

                ISender sender = this.PrepareSender(options);

                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                this.ts = Utils.GetTime();
                Utils.TsSnapStore(this.ptsdata, 'E', options.LogStats);

                //sending of messages
                if (txBatchFlag)
                    this.TransactionSend(sender, options);
                else
                    this.Send(sender, options);

                if (options.LogStats.IndexOf("endpoints") > -1)
                {
                    Dictionary<string, object> stats = GetSenderStats(sender);
                    Formatter.PrintStatistics(stats);
                }


                Utils.TsSnapStore(this.ptsdata, 'F', options.LogStats);
                //close-sleep
                if (options.CloseSleep > 0)
                {
                    System.Threading.Thread.Sleep(options.CloseSleep);
                }

                // resource cleanup
                this.CloseLink(sender);
                this.CloseClient();

                Utils.TsSnapStore(this.ptsdata, 'G', options.LogStats);

                if (this.ptsdata.Count > 0)
                {
                    Console.WriteLine("STATS " + Utils.TsReport(this.ptsdata, options.MsgCount,
                        options.Content.Length * sizeof(Char), 0));
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
            Environment.Exit(this.exitCode);
        }
    }
}
