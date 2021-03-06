﻿// 
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// 
// Microsoft Bot Framework: http://botframework.com
// 
// Bot Builder SDK Github:
// https://github.com/Microsoft/BotBuilder
// 
// Copyright (c) Microsoft Corporation
// All rights reserved.
// 
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using Microsoft.Bot.Builder.Internals.Fibers;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Bot.Builder.Dialogs
{
    /// <summary>
    /// The resumption cookie that can be used to resume a conversation with a user. 
    /// </summary>
    [Serializable]
    public sealed class ResumptionCookie : IEquatable<ResumptionCookie>
    {
        public Address Address { get; set; }

        /// <summary>
        /// The user name.
        /// </summary>
        public string UserName { set; get; }

        /// <summary>
        /// True if the <see cref="Address.ServiceUrl"/> is trusted; False otherwise.
        /// </summary>
        /// <remarks> <see cref="Conversation.ResumeAsync{T}(ResumptionCookie, T, System.Threading.CancellationToken)"/> adds 
        /// the host of the <see cref="Address.ServiceUrl"/> to <see cref="MicrosoftAppCredentials.TrustedHostNames"/> if this flag is True.
        /// </remarks>
        public bool IsTrustedServiceUrl { private set; get; }

        /// <summary>
        /// The IsGroup flag for conversation.
        /// </summary>
        public bool IsGroup { set; get; }

        /// <summary>
        /// The locale of message.
        /// </summary>
        public string Locale { set; get; }

        /// <summary>
        /// Creates an instance of the resumption cookie. 
        /// </summary>
        /// <param name="userId"> The user Id.</param>
        /// <param name="botId"> The bot Id.</param>
        /// <param name="conversationId"> The conversation Id.</param>
        /// <param name="channelId"> The channel Id of the conversation.</param>
        /// <param name="serviceUrl"> The service url of the conversation.</param>
        /// <param name="locale"> The locale of the message.</param>
        public ResumptionCookie(string userId, string botId, string conversationId, string channelId, string serviceUrl, string locale = "en")
            : this(new Address(
                                  // purposefully using named arguments because these all have the same type
                                  botId: botId,
                                  channelId: channelId,
                                  userId: userId,
                                  conversationId: conversationId,
                                  serviceUrl: serviceUrl
                               )
                  )
        {
            SetField.CheckNull(nameof(locale), locale);
            this.Locale = locale;
        }

        /// <summary>
        /// Creates an instance of resumption cookie form a <see cref="Dialogs.Address"/> 
        /// </summary>
        /// <param name="address"> The address.</param>
        /// <remarks>Allows <see cref="JsonSerializer"/> to deserialize an instance of resumption cookie.</remarks>
        [JsonConstructor]
        public ResumptionCookie(Address address)
        {
            this.Address = address;
            IsTrustedServiceUrl = MicrosoftAppCredentials.IsTrustedServiceUrl(address.ServiceUrl);
        }

        /// <summary>
        /// Creates an instance of resumption cookie form a <see cref="Connector.IMessageActivity"/>
        /// </summary>
        /// <param name="msg"> The message.</param>
        public ResumptionCookie(IMessageActivity msg)
        {
            this.Address = new Address(msg);
            UserName = msg.From?.Name;
            IsTrustedServiceUrl = MicrosoftAppCredentials.IsTrustedServiceUrl(msg.ServiceUrl);
            var isGroup = msg.Conversation?.IsGroup;
            IsGroup = isGroup.HasValue && isGroup.Value;
            Locale = msg.Locale;
        }

        public bool Equals(ResumptionCookie other)
        {
            return other != null
                && object.Equals(this.Address, other.Address)
                && object.Equals(this.UserName, other.UserName)
                && this.IsTrustedServiceUrl == other.IsTrustedServiceUrl
                && this.IsGroup == other.IsGroup
                && object.Equals(this.Locale, other.Locale);
        }

        public override bool Equals(object other)
        {
            return this.Equals(other as ResumptionCookie);
        }

        public override int GetHashCode()
        {
            return this.Address.GetHashCode();
        }

        /// <summary>
        /// Creates a message from the resumption cookie.
        /// </summary>
        /// <returns> The message that can be sent to bot based on the resumption cookie</returns>
        public IMessageActivity GetMessage()
        {
            return new Activity
            {
                Id = Guid.NewGuid().ToString(),
                Recipient = new ChannelAccount
                {
                    Id = this.Address.BotId
                },
                ChannelId = this.Address.ChannelId,
                ServiceUrl = this.Address.ServiceUrl,
                Conversation = new ConversationAccount
                {
                    Id = this.Address.ConversationId,
                    IsGroup = this.IsGroup
                },
                From = new ChannelAccount
                {
                    Id = this.Address.UserId,
                    Name = this.UserName
                },
                Locale = this.Locale
            };
        }

        /// <summary>
        /// Deserializes the GZip serialized <see cref="ResumptionCookie"/> using <see cref="Extensions.GZipSerialize(ResumptionCookie)"/>.
        /// </summary>
        /// <param name="str"> The Base64 encoded string.</param>
        /// <returns> An instance of <see cref="ResumptionCookie"/></returns>
        public static ResumptionCookie GZipDeserialize(string str)
        {
            byte[] bytes = Convert.FromBase64String(str);

            using (var stream = new MemoryStream(bytes))
            using (var gz = new GZipStream(stream, CompressionMode.Decompress))
            {
                return (ResumptionCookie)(new BinaryFormatter().Deserialize(gz));
            }
        }
    }

    public partial class Extensions
    {
        /// <summary>
        /// Binary serializes <see cref="ResumptionCookie"/> using <see cref="GZipStream"/>.
        /// </summary>
        /// <param name="resumptionCookie"> The resumption cookie.</param>
        /// <returns> A Base64 encoded string.</returns>
        public static string GZipSerialize(this ResumptionCookie resumptionCookie)
        {
            using (var cmpStream = new MemoryStream())
            using (var stream = new GZipStream(cmpStream, CompressionMode.Compress))
            {
                new BinaryFormatter().Serialize(stream, resumptionCookie);
                stream.Close();
                return Convert.ToBase64String(cmpStream.ToArray());
            }
        }
    }
}
