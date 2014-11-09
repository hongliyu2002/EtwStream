﻿using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

namespace EtwStream
{
    // In-Process(EventListener) Listener

    public static partial class ObservableEventListener
    {
        /// <summary>
        /// Observe In-Process EventSource events. It's no across ETW.
        /// </summary>
        public static IObservableEventListener<EventWrittenEventArgs> FromEventSource(EventSource eventSource, EventLevel level = EventLevel.Verbose, EventKeywords matchAnyKeyword = EventKeywords.None, IDictionary<string, string> arguments = null)
        {
            if (eventSource == null) throw new ArgumentNullException("eventSource");

            var listener = new MicrosoftEventSourceListener();
            listener.EnableEvents(eventSource, level, matchAnyKeyword, arguments);
            return listener;
        }

        public static IObservableEventListener<EventWrittenEventArgs> FromMicrosoftEventSource(string eventSourceName, EventLevel level = EventLevel.Verbose, EventKeywords matchAnyKeyword = EventKeywords.None, IDictionary<string, string> arguments = null)
        {
            if (eventSourceName == null) throw new ArgumentNullException("eventSourceName");

            foreach (var item in EventSource.GetSources())
            {
                if (item.Name.Equals(eventSourceName, StringComparison.Ordinal))
                {
                    return FromEventSource(item, level);
                }
            }

            var listener = new MicrosoftEventSourceListener();
            listener.RegisterDelay(new MicrosoftArgs
            {
                EventSourceName = eventSourceName,
                Level = level,
                MatchAnyKeyword = matchAnyKeyword,
                Arguments = arguments
            });

            return listener;
        }

        class MicrosoftEventSourceListener : EventListener, IObservableEventListener<EventWrittenEventArgs>
        {
            Subject<EventWrittenEventArgs> subject = new Subject<EventWrittenEventArgs>();
            object delayLock = new object();
            LinkedList<MicrosoftArgs> delayedRegisters = new LinkedList<MicrosoftArgs>();

            public void RegisterDelay(MicrosoftArgs args)
            {
                lock (delayLock)
                {
                    delayedRegisters.AddFirst(args);
                }
            }

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                base.OnEventSourceCreated(eventSource);

                lock (delayLock)
                {
                    var node = delayedRegisters.First;
                    while (node != null)
                    {
                        var currentNode = node;
                        node = currentNode.Next;

                        if (eventSource.Name.Equals(currentNode.Value.EventSourceName, StringComparison.Ordinal))
                        {
                            this.EnableEvents(eventSource, currentNode.Value.Level, currentNode.Value.MatchAnyKeyword, currentNode.Value.Arguments);
                            delayedRegisters.Remove(currentNode);
                        }
                    }
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                subject.OnNext(eventData);
            }

            public IDisposable Subscribe(IObserver<EventWrittenEventArgs> observer)
            {
                return subject.Subscribe(observer);
            }

            public override void Dispose()
            {
                subject.Dispose();
                base.Dispose();
            }
        }

        class MicrosoftArgs
        {
            public string EventSourceName;
            public EventLevel Level;
            public EventKeywords MatchAnyKeyword;
            public IDictionary<string, string> Arguments;
        }
    }
}
