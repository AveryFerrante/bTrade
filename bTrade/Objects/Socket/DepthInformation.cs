using bTrade.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bTrade.Objects
{
    class DepthInformation
    {
        [JsonProperty("e")]
        public string eventType { get; set; }
        [JsonProperty("E")]
        public string eventTime { get; set; }
        [JsonProperty("s")]
        public string symbol { get; set; }
        [JsonProperty("u")]
        public int updateId { get; set; }
        [JsonProperty("b")]
        public IList<Order> bids { get; set; }
        [JsonProperty("a")]
        public IList<Order> asks { get; set; }

        public DateTime convertEventTime()
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(Convert.ToInt64(this.eventTime)).UtcDateTime.ToLocalTime();
        }
    }

    [JsonConverter(typeof(ObjectToArrayConverter<Order>))]
    class Order
    {
        [JsonProperty(Order = 1)]
        public decimal price { get; set; }
        [JsonProperty(Order = 2)]
        public decimal quantity { get; set; }
    }
}


