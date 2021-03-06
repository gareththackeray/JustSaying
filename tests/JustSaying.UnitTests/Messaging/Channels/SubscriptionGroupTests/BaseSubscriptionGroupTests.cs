using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using JustSaying.AwsTools.MessageHandling;
using JustSaying.AwsTools.MessageHandling.Dispatch;
using JustSaying.Fluent;
using JustSaying.Messaging.Channels.SubscriptionGroups;
using JustSaying.Messaging.MessageHandling;
using JustSaying.Messaging.MessageProcessingStrategies;
using JustSaying.Messaging.Middleware;
using JustSaying.Messaging.Monitoring;
using JustSaying.TestingFramework;
using JustSaying.UnitTests.Messaging.Channels.Fakes;
using JustSaying.UnitTests.Messaging.Channels.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

using HandleMessageMiddleware = JustSaying.Messaging.Middleware.MiddlewareBase<JustSaying.Messaging.Middleware.HandleMessageContext, bool>;

namespace JustSaying.UnitTests.Messaging.Channels.SubscriptionGroupTests
{
    public abstract class BaseSubscriptionGroupTests : IAsyncLifetime
    {
        protected IList<ISqsQueue> Queues;
        protected MiddlewareMap MiddlewareMap;
        protected TrackingLoggingMonitor Monitor;
        protected FakeSerializationRegister SerializationRegister;
        protected int ConcurrencyLimit = 8;

        public ITestOutputHelper OutputHelper { get; }

        protected HandleMessageMiddleware Middleware;
        protected InspectableHandler<SimpleMessage> Handler;

        protected ISubscriptionGroup SystemUnderTest { get; private set; }

        protected static readonly TimeSpan TimeoutPeriod = TimeSpan.FromSeconds(2);

        protected ILoggerFactory LoggerFactory { get; }
        protected ILogger Logger { get; }

        public BaseSubscriptionGroupTests(ITestOutputHelper testOutputHelper)
        {
            OutputHelper = testOutputHelper;
            LoggerFactory = testOutputHelper.ToLoggerFactory();
            Logger = LoggerFactory.CreateLogger(GetType());
        }

        public async Task InitializeAsync()
        {
            GivenInternal();

            SystemUnderTest = CreateSystemUnderTest();

            await WhenAsync().ConfigureAwait(false);
        }

        private void GivenInternal()
        {
            Queues = new List<ISqsQueue>();
            Handler = new InspectableHandler<SimpleMessage>();

            var testResolver = new FakeServiceResolver(sc => sc
                .AddLogging(l => l.AddXUnit(OutputHelper)));

            Middleware = new HandlerMiddlewareBuilder(testResolver, testResolver).UseHandler(ctx => Handler).Build();
            Monitor = new TrackingLoggingMonitor(LoggerFactory.CreateLogger<TrackingLoggingMonitor>());
            SerializationRegister = new FakeSerializationRegister();
            MiddlewareMap = new MiddlewareMap();

            Given();
        }

        protected abstract void Given();

        // Default implementation
        protected virtual async Task WhenAsync()
        {
            foreach (ISqsQueue queue in Queues)
            {
                MiddlewareMap.Add<SimpleMessage>(queue.QueueName, Middleware);
            }

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var completion = SystemUnderTest.RunAsync(cts.Token);

            await Patiently.AssertThatAsync(OutputHelper,
                () => Until() || cts.IsCancellationRequested);

            cts.Cancel();
            await completion.HandleCancellation();
        }

        protected virtual bool Until()
        {
            OutputHelper.WriteLine("Checking if handler has received any messages");
            return Handler.ReceivedMessages.Any();
        }

        private ISubscriptionGroup CreateSystemUnderTest()
        {
            var messageBackoffStrategy = Substitute.For<IMessageBackoffStrategy>();
            var messageContextAccessor = Substitute.For<IMessageContextAccessor>();

            Logger.LogInformation("Creating MessageDispatcher with serialization register type {Type}",
                SerializationRegister.GetType().FullName);

            var dispatcher = new MessageDispatcher(
                SerializationRegister,
                Monitor,
                MiddlewareMap,
                LoggerFactory,
                messageBackoffStrategy,
                messageContextAccessor);

            var defaults = new SubscriptionGroupSettingsBuilder()
                .WithDefaultConcurrencyLimit(ConcurrencyLimit);

            var subscriptionGroupFactory = new SubscriptionGroupFactory(
                dispatcher,
                Monitor,
                LoggerFactory);

            var settings = SetupBusConfig();

            return subscriptionGroupFactory.Create(defaults, settings);
        }

        protected virtual Dictionary<string, SubscriptionGroupConfigBuilder> SetupBusConfig()
        {
            return new Dictionary<string, SubscriptionGroupConfigBuilder>
            {
                { "test", new SubscriptionGroupConfigBuilder("test").AddQueues(Queues) },
            };
        }

        protected static FakeSqsQueue CreateSuccessfulTestQueue(string queueName, params Message[] messages)
        {
            return CreateSuccessfulTestQueue(queueName, messages.ToList);
        }

        protected static FakeSqsQueue CreateSuccessfulTestQueue(
            string queueName,
            Func<IEnumerable<Message>> getMessages)
        {
            return CreateSuccessfulTestQueue(queueName,
                () => new ReceiveMessageResponse()
                {
                    Messages = getMessages().ToList()
                }.Infinite());
        }

        protected static FakeSqsQueue CreateSuccessfulTestQueue(
            string queueName,
            Func<IEnumerable<ReceiveMessageResponse>> getMessages)
        {
            var fakeClient = new FakeAmazonSqs(getMessages);

            var sqsQueue = new FakeSqsQueue(queueName,
                fakeClient);

            return sqsQueue;
        }

        public Task DisposeAsync()
        {
            LoggerFactory?.Dispose();

            return Task.CompletedTask;
        }

        protected class TestMessage : Message
        { }
    }
}
