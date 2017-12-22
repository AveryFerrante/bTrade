using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using Newtonsoft.Json;
using bTrade.Objects;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using bTrade.Objects.Socket;

namespace bTrade
{
    class Program
    {

        //static void Main(String[] args)
        //{
        //    var binanceClient = new BinanceClient(new ApiClient("test", "test"));
        //    IList<string> symbolList = new List<string>(); symbolList.Add("XRPETH"); /*symbolList.Add("TRXETH"); symbolList.Add("MANAETH"); symbolList.Add("XVGETH");*/
        //    int timeBetweenTradeCheck = 5000;
        //    bool match = false;

        //    var initialPercentageIncreaseThreshold = roundDecimal(-.02m);
        //    /* Initial Currency Finding Config*/
        //    int tradeIntervalInMinutes = 3;

        //    while (true)
        //    {
        //        while (!match)
        //        {
        //            foreach (var symbol in symbolList)
        //            {
        //                var trades = getAggTrades(binanceClient, symbol, tradeIntervalInMinutes).Result; // Oldest trade is first

        //                var perInc = getPercentagePriceIncrease(trades.Last().Price, trades.First().Price);
        //                if (perInc >= initialPercentageIncreaseThreshold)
        //                {
        //                    Console.WriteLine($"Match found for {symbol} - {perInc}, starting socket...");
        //                    binanceClient.ListenTradeEndpoint(symbol.ToLower(), AggregateTradesHandler);
        //                    match = true;
        //                }
        //            }

        //            Thread.Sleep(timeBetweenTradeCheck);
        //        }
        //        Console.ReadKey(true);
        //    }
        //}

        //static async Task<IEnumerable<AggregateTrade>> getAggTrades(BinanceClient bClient, string symbol, int tradeInterval)
        //{
        //    long endTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        //    long startTime = DateTimeOffset.Now.AddMinutes(-tradeInterval).ToUnixTimeMilliseconds();
        //    var trades = await bClient.GetAggregateTrades(symbol, 200);
        //    return trades;
        //}

        //static decimal getPercentagePriceIncrease(decimal newestTrade, decimal oldestTrade)
        //{
        //    return roundDecimal((newestTrade - oldestTrade) / oldestTrade);
        //}

        //static decimal roundDecimal(decimal number)
        //{
        //    return Math.Round(number, 8);
        //}

        //private static void AggregateTradesHandler(AggregateTradeMessage data)
        //{
        //    Console.WriteLine(data.Price);
        //}

        public static decimal getPercentagePriceIncrease(IList<AggregateTrade> trades)
        {
            if (trades.Count() > 0)
            {
                AggregateTrade mostRecentTrade = trades.First();
                AggregateTrade oldestTrade = trades.Last();
                return (mostRecentTrade.price - oldestTrade.price) / oldestTrade.price;
            }
            return 0;
        }

        public static IList<decimal> makePurchase(decimal wallet, decimal percentMarkup, IList<AggregateTrade> tradeInfo)
        {
            decimal endAmount = wallet + (wallet * percentMarkup);
            decimal amountBuy = Decimal.Truncate(wallet / tradeInfo.First().price);
            decimal amountSpending = amountBuy * tradeInfo.First().price;
            decimal purchaseFee = amountSpending * Convert.ToDecimal(.0005);

            decimal sellPrice = Math.Ceiling((endAmount + purchaseFee) / (amountBuy * (Convert.ToDecimal(1 - .0005)))*100000000)/ 100000000;
            Console.WriteLine($"Purchase Price: {tradeInfo.First().price} Buying: {amountBuy} = {amountSpending} sell price: {sellPrice} end amount {endAmount}");
            IList<decimal> retVals = new List<decimal>(); retVals.Add(sellPrice); retVals.Add(tradeInfo.First().price);
            return retVals;
        }

        static void Main(string[] args)
        {
            int timeBetweenTradeCheck = 15000;
            int tradeIntervalInMinutes = 3;
            decimal initialPercentageIncreaseThreshold = Convert.ToDecimal(.025);
            decimal secondaryPercentageIncreaseThreshold = Convert.ToDecimal(.001);
            decimal sellPriceFallThreshold = Convert.ToDecimal(.038);
            decimal maxPercentDecreaseDuringBuying = Convert.ToDecimal(.025);
            int tradeStorageInterval = 50;
            IList<AggregateTrade> aggregateTrades = new List<AggregateTrade>();
            decimal sellPrice = Convert.ToDecimal(0);
            IList<string> symbolList = new List<string>(); symbolList.Add("XRPETH"); symbolList.Add("TRXETH"); symbolList.Add("MANAETH"); symbolList.Add("XVGETH");
            bool match = false;
            decimal wallet = Convert.ToDecimal(.25);
            decimal percentMarkup = Convert.ToDecimal(.003);

            WebSocket buyingTradesSocket = new WebSocket("wss://stream.binance.com:9443/ws/ethbtc@depth");

            WebSocket sellingTradesSocket = new WebSocket("wss://stream.binance.com:9443/ws/ethbtc@depth");





            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri("https://api.binance.com/api/v1/aggTrades");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            IList<AggregateTrade> trades = new List<AggregateTrade>();
            decimal percentageIncrease = 0;
            while (true)
            {
                while (!match)
                {
                    long currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    long intervalTime = DateTimeOffset.Now.AddMinutes(-tradeIntervalInMinutes).ToUnixTimeMilliseconds(); // 2 minute time intervals
                    foreach (string symbol in symbolList)
                    {
                        HttpResponseMessage response = client.GetAsync("?symbol=" + symbol + "&startTime=" + intervalTime + "&endTime=" + currentTime).Result;
                        if (response.IsSuccessStatusCode)
                        {
                            aggregateTrades.Clear(); // Clear previous failed buys / sells
                            trades = JsonConvert.DeserializeObject<List<AggregateTrade>>(response.Content.ReadAsStringAsync().Result);
                            trades.Reverse();// Newest trade comes last from the API
                            percentageIncrease = getPercentagePriceIncrease(trades);
                            if (percentageIncrease > initialPercentageIncreaseThreshold)
                            {
                                Console.WriteLine($"Found match for {symbol}. Newest Trade: {trades.First().price} at quantity {trades.First().quantity} Oldest Trade {trades.Last().price} at quantity {trades.Last().quantity}, starting socket");
                                /***********LOOKING TO START BUYING HERE*******************/
                                sellingTradesSocket = new WebSocket("wss://stream.binance.com:9443/ws/" + symbol.ToLower() + "@aggTrade");
                                sellingTradesSocket.Log.Output = (_, __) => { };
                                sellingTradesSocket.OnMessage += (sender, e) =>
                                {
                                    //if (aggregateTrades.Count() == tradeStorageInterval)
                                    //    aggregateTrades.Remove(aggregateTrades.Last());
                                    aggregateTrades.Insert(0, JsonConvert.DeserializeObject<AggregateTrade>(e.Data)); // Most recent data is positioned first

                                    decimal percentageIncreaseSingleCurrency = getPercentagePriceIncrease(aggregateTrades);
                                    Console.WriteLine($"Percentage Increase for {symbol}: {percentageIncreaseSingleCurrency}");
                                    if (percentageIncreaseSingleCurrency > secondaryPercentageIncreaseThreshold)
                                    {
                                        IList<decimal> purchaseInfo = makePurchase(wallet, percentMarkup, aggregateTrades);
                                        sellPrice = purchaseInfo.ElementAt(0);
                                        decimal buyPrice = purchaseInfo.ElementAt(1);
                                        Console.WriteLine($"Successfully Bought. Selling Price: {sellPrice}");
                                        sellingTradesSocket.Close();
                                        /*****************LOOKING TO START SELLING HERE *********************/
                                        buyingTradesSocket = new WebSocket(sellingTradesSocket.Url.ToString());
                                        buyingTradesSocket.Log.Output = (_, __) => { };
                                        buyingTradesSocket.OnMessage += (senderr, ee) =>
                                        {
                                            AggregateTrade trade = JsonConvert.DeserializeObject<AggregateTrade>(ee.Data);
                                            if (trade.price >= sellPrice)
                                            {
                                                wallet += (wallet * percentMarkup);
                                                Console.WriteLine($"SOLD! Wallet balance is now {wallet}");
                                                match = false;
                                                buyingTradesSocket.Close();
                                            }
                                            var pricePercentage = (trade.price - buyPrice) / trade.price;
                                            if (pricePercentage < -sellPriceFallThreshold) // Sell failed, abondon
                                            {
                                                wallet -= (wallet * sellPriceFallThreshold);
                                                Console.WriteLine($"Price fell too low to: {trade.price} - PANIC SELLING! New wallet balance {wallet}");
                                                match = false;
                                                buyingTradesSocket.Close();
                                            }

                                        };
                                        buyingTradesSocket.Connect();
                                    }
                                    else if (percentageIncreaseSingleCurrency < -maxPercentDecreaseDuringBuying)
                                    {
                                        Console.WriteLine($"{symbol} fell below the threshold {maxPercentDecreaseDuringBuying} during buying phase at {percentageIncreaseSingleCurrency} - ABONDON!");
                                        sellingTradesSocket.Close();
                                        match = false;
                                    }
                                };
                                sellingTradesSocket.Connect();
                                match = true;
                                break;
                            }
                        }
                        else
                        {
                            Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
                        }
                    }
                    Thread.Sleep(timeBetweenTradeCheck);
                }
            }


            //    //var ws = new WebSocket("wss://stream.binance.com:9443/ws/xrpeth@aggTrade");
            //    //IList<AggregateTrade> xrpTradeInfo = new List<AggregateTrade>();
            //    //decimal xrpPricePercentageIncreaseThreshold = Convert.ToDecimal(.008);
            //    //decimal wallet = Convert.ToDecimal(.25);
            //    //decimal percentMarkup = Convert.ToDecimal(.001);
            //    //decimal sellPrice = 1;
            //    //ws.OnMessage += (sender, e) =>
            //    //{
            //    //    if (xrpTradeInfo.Count() == 50) // Only store last 100 trades
            //    //        xrpTradeInfo.Remove(xrpTradeInfo.Last());
            //    //    xrpTradeInfo.Insert(0, JsonConvert.DeserializeObject<AggregateTrade>(e.Data)); // Most recent data is positioned first
            //    //    if (getPercentagePriceIncrease(xrpTradeInfo) > xrpPricePercentageIncreaseThreshold)
            //    //    {
            //    //       sellPrice = makePurchase(wallet, percentMarkup, xrpTradeInfo, ws);
            //    //    }

            //    //};
            //    //ws.ConnectAsync();


            //    //while(sellPrice == 1)
            //    //{

            //    //}
            //    //ws.CloseAsync();
            //    //var ws1 = new WebSocket("wss://stream.binance.com:9443/ws/xrpeth@aggTrade");
            //    //Console.WriteLine("Looking for purchase...");
            //    //ws1.OnMessage += (sender, e) =>
            //    //{
            //    //    Console.WriteLine("Looking for buyer");
            //    //    AggregateTrade trade = JsonConvert.DeserializeObject<AggregateTrade>(e.Data);
            //    //    if (trade.price >= sellPrice)
            //    //    {
            //    //        Console.WriteLine("SOLD!");
            //    //        ws1.CloseAsync();
            //    //    }
            //    //};
            //    //ws1.ConnectAsync();

            //    //var ws2 = new WebSocket("wss://stream.binance.com:9443/ws/mtheth@aggTrade");
            //    //IList<AggregateTrade> mthTradeInfo = new List<AggregateTrade>();
            //    //decimal mthPricePercentageIncreaseThreshold = Convert.ToDecimal(.02);
            //    //ws.OnMessage += (sender, e) =>
            //    //{
            //    //    Console.WriteLine("In MTH");
            //    //    if (xrpTradeInfo.Count() == 10) // Only store last 100 trades
            //    //        xrpTradeInfo.Remove(xrpTradeInfo.Last());

            //    //    mthTradeInfo.Insert(0, JsonConvert.DeserializeObject<AggregateTrade>(e.Data)); // Most recent data is positioned first

            //    //    if (getPercentagePriceIncrease(mthTradeInfo) > mthPricePercentageIncreaseThreshold)
            //    //    {
            //    //        Console.WriteLine("BUYING MTH");
            //    //    }

            //    //};
            //    //ws2.ConnectAsync();



            //    //Console.WriteLine($"Buying at {mostRecentTrade.price}");
            //    //decimal sellPrice = mostRecentTrade.price + (mostRecentTrade.price * Convert.ToDecimal(.01));
            //    //Console.WriteLine($"Attempting to sell at {sellPrice}");

            //    //while(mostRecentTrade.price < sellPrice)
            //    //{
            //    //    HttpResponseMessage response = client.GetAsync("?symbol=ETHBTC&limit=100").Result;
            //    //    if (response.IsSuccessStatusCode)
            //    //    {
            //    //        trades = JsonConvert.DeserializeObject<List<AggregateTrade>>(response.Content.ReadAsStringAsync().Result);
            //    //        mostRecentTrade = trades.Last();
            //    //        Console.WriteLine($"Most recent trade price: {mostRecentTrade.price}, Looking for {sellPrice}");
            //    //    }
            //    //    else
            //    //    {
            //    //        Console.WriteLine("{0} ({1})", (int)response.StatusCode, response.ReasonPhrase);
            //    //    }
            //    //    Thread.Sleep(2000);
            //    //}
            //}
        }
    }
}
