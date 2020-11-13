using System;
using JustSaying.Messaging.MessageHandling;
using JustSaying.Messaging.Middleware.Handle;
using Microsoft.Extensions.Logging;
using HandleMessageMiddleware = JustSaying.Messaging.Middleware.MiddlewareBase<JustSaying.Messaging.Middleware.Handle.HandleMessageContext, bool>;

namespace JustSaying.Messaging.Middleware.ExactlyOnce
{
    public static class HandlerMiddlewareBuilderExtensions
    {
        /// <summary>
        /// Adds an <see cref="ExactlyOnceMiddleware{T}"/> to the current pipeline.
        /// </summary>
        /// <param name="builder">The current <see cref="HandlerMiddlewareBuilder"/>.</param>
        /// <param name="lockKey">A unique key to identify this lock, e.g. the name of the handler.</param>
        /// <param name="lockDuration">The length of time to lock messages while handling them.</param>
        /// <typeparam name="TMessage">The type of the message that should be locked.</typeparam>
        /// <returns>The current <see cref="HandlerMiddlewareBuilder"/>.</returns>
        public static HandlerMiddlewareBuilder UseExactlyOnce<TMessage>(
            this HandlerMiddlewareBuilder builder,
            string lockKey,
            TimeSpan? lockDuration = null)
        {
            HandleMessageMiddleware CreateMiddleware()
            {
                var messageLock = builder.ServiceResolver.ResolveService<IMessageLockAsync>();
                var logger = builder.ServiceResolver.ResolveService<ILogger<ExactlyOnceMiddleware<TMessage>>>();

                return new ExactlyOnceMiddleware<TMessage>(messageLock,
                    lockDuration ?? TimeSpan.MaxValue,
                    lockKey,
                    logger);
            }

            builder.Use(CreateMiddleware);

            return builder;
        }
    }
}
