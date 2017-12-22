using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bTrade.Objects.Socket
{
    class AggregateTrade
    {
        [JsonProperty("a")]
        public int tradeId { get; set; }
        [JsonProperty("p")]
        public decimal price { get; set; }
        [JsonProperty("q")]
        public decimal quantity { get; set; }
    }
}
