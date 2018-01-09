﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using CFX.Utilities;

namespace CFX.Transport
{
    public class AmqpCFXEndpoint : IDisposable
    {
        public AmqpCFXEndpoint()
        {
            channels = new ConcurrentDictionary<string, AmqpConnection>();
            IsOpen = false;
            if (!UseCompression.HasValue) UseCompression = false;
            if (!ReconnectInterval.HasValue) ReconnectInterval = TimeSpan.FromSeconds(5);
            if (!MaxMessagesPerTransmit.HasValue) MaxMessagesPerTransmit = 30;
            if (!DurableReceiverSetting.HasValue) DurableReceiverSetting = 1;
        }

        private AmqpRequestProcessor requestProcessor;
        private ConcurrentDictionary<string, AmqpConnection> channels;

        public event OnRequestHandler OnRequestReceived;
        public event CFXMessageReceivedHandler OnCFXMessageReceived;

        public string CFXHandle
        {
            get;
            private set;
        }

        public Uri RequestUri
        {
            get;
            private set;
        }

        public static bool? UseCompression
        {
            get;
            set;
        }
        public static TimeSpan? ReconnectInterval
        {
            get;
            set;
        }

        public static int? MaxMessagesPerTransmit
        {
            get;
            set;
        }

        public static uint? DurableReceiverSetting
        {
            get;
            set;
        }

        public bool IsOpen
        {
            get;
            private set;
        }

        public void Open(string cfxHandle, IPAddress requestAddress, int requestPort = 5672)
        {
            Uri uri = new Uri(string.Format("amqp://{0}:{1}", requestAddress.ToString(), requestPort));
            Open(cfxHandle, uri);
        }

        public void Open(string cfxHandle, Uri requestUri = null)
        {
            IsOpen = false;

            try
            {
                this.CFXHandle = cfxHandle;
                if (requestUri != null)
                    this.RequestUri = requestUri;
                else
                    this.RequestUri = new Uri(string.Format("amqp://{0}:5672", EnvironmentHelper.GetMachineName()));

                //requestProcessor = new AmqpRequestProcessor();
                //requestProcessor.Open(this.CFXHandle, this.RequestUri);
                //requestProcessor.OnRequestReceived += RequestProcessor_OnRequestReceived;

                IsOpen = true;
            }
            catch (Exception ex)
            {
                Cleanup();
                Debug.WriteLine(ex.Message);
            }
        }

        public bool TestChannel(Uri channelUri, out Exception error)
        {
            bool result = false;
            error = null;

            try
            {
                CFXHandle = Guid.NewGuid().ToString();
                AmqpConnection conn = new AmqpConnection(channelUri, this);
                conn.OpenConnection();
                conn.Close();
                result = true;
            }
            catch (Exception ex)
            {
                error = ex;
                Debug.WriteLine(ex.Message);
            }

            return result;
        }

        public void AddPublishChannel(AmqpChannelAddress address)
        {
            AddPublishChannel(address.Uri, address.Address);
        }

        public void AddPublishChannel(Uri networkAddress, string address)
        {
            if (!IsOpen) throw new Exception("The Endpoint must be open before adding or removing channels.");
            string key = networkAddress.ToString();

            AmqpConnection channel = null;
            if (channels.ContainsKey(key))
            {
                channel = channels[key];
            }
            else
            {
                channel = new AmqpConnection(networkAddress, this);
                channel.OnCFXMessageReceived += Channel_OnCFXMessageReceived;
                channels[key] = channel;
            }

            if (channel != null)
            {
                channel.AddPublishChannel(address);
            }
        }

        public void ClosePublishChannel(AmqpChannelAddress address)
        {
            ClosePublishChannel(address.Uri, address.Address);
        }

        public void ClosePublishChannel(Uri networkAddress, string address)
        {
            if (!IsOpen) throw new Exception("The Endpoint must be open before adding or removing channels.");
            string key = networkAddress.ToString();

            AmqpConnection channel = null;
            if (channels.ContainsKey(key))
            {
                channel = channels[key];
                channel.RemoveChannel(address);
            }
            else
            {
                throw new ArgumentException("The specified channel does not exist.");
            }
        }

        public void AddSubscribeChannel(AmqpChannelAddress address)
        {
            AddSubscribeChannel(address.Uri, address.Address);
        }

        public void AddSubscribeChannel(Uri networkAddress, string address)
        {
            if (!IsOpen) throw new Exception("The Endpoint must be open before adding or removing channels.");
            string key = networkAddress.ToString();

            AmqpConnection channel = null;
            if (channels.ContainsKey(key))
            {
                channel = channels[key];
            }
            else
            {
                channel = new AmqpConnection(networkAddress, this);
                channel.OnCFXMessageReceived += Channel_OnCFXMessageReceived;
                channels[key] = channel;
            }

            if (channel != null)
            {
                channel.AddSubscribeChannel(address);
            }
        }

        public void CloseSubscribeChannel(AmqpChannelAddress address)
        {
            CloseSubscribeChannel(address.Uri, address.Address);
        }

        public void CloseSubscribeChannel(Uri networkAddress, string address)
        {
            if (!IsOpen) throw new Exception("The Endpoint must be open before adding or removing channels.");
            string key = networkAddress.ToString();

            AmqpConnection channel = null;
            if (channels.ContainsKey(key))
            {
                channel = channels[key];
                channel.RemoveChannel(address);
            }
            else
            {
                throw new ArgumentException("The specified channel does not exist.");
            }
        }

        private void Channel_OnCFXMessageReceived(AmqpChannelAddress source, CFXEnvelope message)
        {
            OnCFXMessageReceived?.Invoke(source, message);
        }

        private CFXEnvelope RequestProcessor_OnRequestReceived(CFXEnvelope request)
        {
            if (OnRequestReceived != null) return OnRequestReceived(request);
            return null;
        }

        public void Close()
        {
            Cleanup();
        }

        public void Dispose()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (requestProcessor != null)
            {
                requestProcessor.Close();
                requestProcessor = null;
            }

            foreach (AmqpConnection conn in channels.Values)
            {
                conn.Dispose();
            }

            channels.Clear();
            IsOpen = false;
        }

        public void Publish(CFXEnvelope env)
        {
            foreach (AmqpConnection channel in channels.Values)
            {
                channel.Publish(env);
            }
        }

        public void Publish(CFXMessage msg)
        {
            CFXEnvelope env = new CFXEnvelope();
            env.MessageBody = msg;
            Publish(env);
        }

        public void PublishMany(IEnumerable<CFXEnvelope> envelopes)
        {
            foreach (AmqpConnection channel in channels.Values)
            {
                channel.PublishMany(envelopes);
            }
        }

        public void PublishMany(IEnumerable<CFXMessage> msgs)
        {
            List<CFXEnvelope> envelopes = new List<CFXEnvelope>();
            foreach (CFXMessage msg in msgs)
            {
                CFXEnvelope env = new CFXEnvelope();
                env.MessageBody = msg;
                envelopes.Add(env);
            }

            PublishMany(envelopes);
        }
    }
}
