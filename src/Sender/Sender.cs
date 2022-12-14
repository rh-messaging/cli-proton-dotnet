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

using ClientLib;
using System;
using System.Linq;

namespace Sender
{
    /// <summary>
    /// Class represent sender to amqp broker
    /// </summary>
    class Sender
    {
        /// <summary>
        /// Main method of sender
        /// </summary>
        /// <param name="args">args from command line</param>
        static void Main(string[] args)
        {
                SenderClient client = new SenderClient();
                client.Run(args);
        }

    }
}

