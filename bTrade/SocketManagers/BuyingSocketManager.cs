using bTrade.Objects.Socket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;

namespace bTrade.SocketManagers
{
    class BuyingSocketManager
    {
        public WebSocket socket;
        public IList<AggregateTrade> aggregateTrades;
        public int tradeStorageInterval { get; set; }
        public string symbol { get; set; }
        public decimal percentageIncreaseThreshold { get; set; }
        public decimal wallet { get; set; }
        public decimal percentMarkup { get; set; }
        public decimal sellPrice { get; set; }

        private string baseSocketUri = "wss://stream.binance.com:9443/ws/";
        private string socketEndPoint = "@aggTrade";

        BuyingSocketManager(string symbol, decimal percentageIncreaseThreshold, decimal wallet, decimal percentMarkup, int tradeStorageInterval = 25)
        {
            this.aggregateTrades = new List<AggregateTrade>();
            this.tradeStorageInterval = tradeStorageInterval;
            this.symbol = symbol;
            this.percentageIncreaseThreshold = percentageIncreaseThreshold;
            this.wallet = wallet;
            this.percentMarkup = percentMarkup;
            this.socket = new WebSocket(this.baseSocketUri + this.symbol + this.socketEndPoint);


            socket.OnMessage += (sender, e) =>
            {
                if (aggregateTrades.Count() == tradeStorageInterval)
                    aggregateTrades.Remove(aggregateTrades.Last());

                aggregateTrades.Insert(0, JsonConvert.DeserializeObject<AggregateTrade>(e.Data)); // Most recent data is positioned first
                decimal percentageIncreaseSingleCurrency = getPercentagePriceIncrease();
                Console.WriteLine($"Percentage Increase for {symbol}: {percentageIncreaseSingleCurrency}");
                if (percentageIncreaseSingleCurrency > percentageIncreaseThreshold)
                {
                    sellPrice = makePurchase();
                    Console.WriteLine($"Successfully Bought. Selling Price: {sellPrice}");
                    socket.Close();
                    // Return buying socket manager here?
                }
            };
        }

        private decimal getPercentagePriceIncrease()
        {
            if (this.aggregateTrades.Count() > 0)
            {
                AggregateTrade mostRecentTrade = this.aggregateTrades.First();
                AggregateTrade oldestTrade = this.aggregateTrades.Last();
                return (mostRecentTrade.price - oldestTrade.price) / oldestTrade.price;
            }
            return 0;
        }

        private decimal makePurchase()
        {
            decimal endAmount = wallet + (wallet * percentMarkup);
            decimal amountCanBuy = Decimal.Truncate(wallet / aggregateTrades.First().price);
            decimal purchaseFee = amountCanBuy * Convert.ToDecimal(.001);
            decimal purchasedAmount = amountCanBuy - purchaseFee;
            decimal amountToSell = Decimal.Truncate(purchasedAmount);
            decimal sellPrice = endAmount / (amountToSell * (Convert.ToDecimal(1 - .001)));
            Console.WriteLine($"Purchase Price: {aggregateTrades.First().price} Can Buy: {amountCanBuy} Amount Sell: {amountToSell} sell price: {sellPrice} end amount {endAmount}");
            return sellPrice;
        }
    }
}
