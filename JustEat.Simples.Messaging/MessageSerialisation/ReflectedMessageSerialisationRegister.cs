using System;
using System.Collections.Generic;
using JustEat.Simples.NotificationStack.Messaging.Messages;
using JustEat.Simples.NotificationStack.Messaging.Messages.CustomerCommunication;
using JustEat.Simples.NotificationStack.Messaging.Messages.OrderDispatch;

namespace JustEat.Simples.NotificationStack.Messaging.MessageSerialisation
{
    public class ReflectedMessageSerialisationRegister : IMessageSerialisationRegister
    {
        private readonly Dictionary<string, IMessageSerialiser<Message>> _map;

        public ReflectedMessageSerialisationRegister()
        {
            _map = new Dictionary<string, IMessageSerialiser<Message>>();

            // ToDo: reflect this somehow!

            //var list = from t in Assembly.GetExecutingAssembly().GetTypes()
            //           where t.BaseType == (typeof (Message)) && t.GetConstructor(Type.EmptyTypes) != null
            //           select new {Key = t, Value = new NewtonsoftSerialiser<t>()};

            _map.Add(typeof(CustomerOrderRejectionSms).Name, new ServiceStackSerialiser<CustomerOrderRejectionSms>());
            _map.Add(typeof(CustomerOrderRejectionSmsFailed).Name, new ServiceStackSerialiser<CustomerOrderRejectionSmsFailed>());

            _map.Add(typeof(OrderAccepted).Name, new ServiceStackSerialiser<OrderAccepted>());
            _map.Add(typeof(OrderAlternateTimeSuggested).Name, new ServiceStackSerialiser<OrderAlternateTimeSuggested>());
            _map.Add(typeof(OrderRejected).Name, new ServiceStackSerialiser<OrderRejected>());
        }

        public IMessageSerialiser<Message> GetSerialiser(string objectType)
        {
            return _map[objectType];
        }

        public IMessageSerialiser<Message> GetSerialiser(Type objectType)
        {
            return _map[objectType.Name];
        }
    }
}