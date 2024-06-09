using Microsoft.ServiceBus.Messaging;
using ServiceBusExplorer.Helpers;
using ServiceBusExplorer.Utilities.Helpers;
using System.Collections.Generic;
using System.Linq;

namespace ServiceBusExplorer.Common.Helpers.Modeshift
{
    public class MessagePeeker
    {
        private readonly WriteToLogDelegate writeToLog;
        private readonly MessageReceiver messageReceiver;
        private readonly IBrokeredMessageInspector messageInspector;
        private readonly bool isPartitionedTopic;

        public MessagePeeker(WriteToLogDelegate writeToLog,
            MessageReceiver messageReceiver,
            IBrokeredMessageInspector messageInspector,
            bool isPartitionedTopic)
        {
            this.writeToLog = writeToLog;
            this.messageReceiver = messageReceiver;
            this.messageInspector = messageInspector;
            this.isPartitionedTopic = isPartitionedTopic;
        }

        public List<BrokeredMessage> Peek(
            int count,
            long deadLetterMessageCount)
        {
            var peeks = 0;

            // Get initial lower bound
            var initialMessages = PeekBatch(count);
            peeks += isPartitionedTopic ? initialMessages.Count() : 1;
            if (!initialMessages.Any())
            {
                writeToLog($"Peeked with {peeks} api calls.");
                return new List<BrokeredMessage>();
            }

            if(initialMessages.Count() < count)
            {
                writeToLog($"Peeked with {peeks} api calls.");
                return initialMessages.ToList();
            }

            var initialSequence = initialMessages.Last().SequenceNumber;
            var lowerBound = initialSequence;
            var step = deadLetterMessageCount;
            var messages = new List<BrokeredMessage>();

            // Incrementally find an upper bound
            while (true)
            {
                messages = PeekBatch(lowerBound + step, count).ToList();
                peeks += isPartitionedTopic ? messages.Count() : 1;
                if (messages.Count < count)
                {
                    break;
                }
                lowerBound += step;
                step *= 2;
            }

            // Binary search to find the approximate last position
            var upperBound = lowerBound + step;
            while (lowerBound < upperBound)
            {
                var midPoint = (lowerBound + upperBound) / 2;
                messages = PeekBatch(midPoint, count).ToList();
                peeks += isPartitionedTopic ? messages.Count() : 1;
                if (messages.Count < count)
                {
                    upperBound = midPoint;
                }
                else
                {
                    lowerBound = midPoint + 1;
                }
            }

            writeToLog($"Peeked with {peeks} api calls.");

            return messages;
        }

        private List<BrokeredMessage> PeekBatch(int count)
        {
            return PeekBatch(null, count);
        }

        private List<BrokeredMessage> PeekBatch(long? fromSequenceNumber, int count)
        {
            if (!isPartitionedTopic)
            {
                if (fromSequenceNumber.HasValue)
                {
                    return messageReceiver.PeekBatch(fromSequenceNumber.Value, count).ToList();
                }

                return messageReceiver.PeekBatch(count).ToList();
            }

            var brokeredMessages = new List<BrokeredMessage>();

            for (var i = 0; i < count; i++)
            {
                BrokeredMessage message;

                if (i == 0 && fromSequenceNumber.HasValue)
                {
                    message = messageReceiver.Peek(fromSequenceNumber.Value);
                }
                else
                {
                    message = messageReceiver.Peek();
                }

                if (message != null)
                {
                    if (messageInspector != null)
                    {
                        message = messageInspector.AfterReceiveMessage(message);
                    }
                    brokeredMessages.Add(message);
                }
                else
                {
                    break;
                }
            }

            return brokeredMessages;
        }
    }
}
