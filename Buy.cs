// Copyright QUANTOWER LLC. © 2017-2023. All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using TradingPlatform.BusinessLayer;

namespace Buy
{
    public class Buy : Strategy
    {
        [InputParameter("Symbol MES", 10)]
        public Symbol SymbolMES { get; set; }

        [InputParameter("Symbol ES", 11)]
        public Symbol SymbolES { get; set; }

        [InputParameter("Account", 20)]
        public Account Account { get; set; }

        [InputParameter("Historical Period", 50)]
        public Period SelectedPeriod = Period.MIN1;

        [InputParameter("ATR Period", 60, 1, 100)]
        public int ATRPeriod = 13;

        [InputParameter("Stop Loss Multiplier (ATR)", 70)]
        public double StopLossMultiplier = 1.6;

        [InputParameter("Reward-Risk Ratio", 80)]
        public double RewardRiskRatio = 1.6;

        [InputParameter("Risk ($)", 90)]
        public double Risk = 200;

        private Indicator atr;
        private HistoricalData historicalData;

        private bool openPositionsES = false;
        private bool openPositionsMES = false;
        private double entryPrice;

        private Order tpOrderES = null;
        private Order slOrderES = null;
        private Order tpOrderMES = null;
        private Order slOrderMES = null;
        private string tpOrderCommentES;
        private string slOrderCommentES;
        private string tpOrderCommentMES;
        private string slOrderCommentMES;
        private string slOrderIdES;
        private string tpOrderIdES;
        private string slOrderIdMES;
        private string tpOrderIdMES;
        private int comment = 0;
        private double tpFilledQtyES = 0;
        private double slFilledQtyES = 0;
        private double tpFilledQtyMES = 0;
        private double slFilledQtyMES = 0;
        int qtyES = 0;
        int qtyMES = 0;

        // Metrics
        private double grossPnL = 0;
        private double netPnL = 0;
        private double grossProfit = 0;
        private double grossLoss = 0;
        private double totalFees = 0;
        private int totalTrades = 0;
        private double winningTrades = 0;
        private double losingTrades = 0;
        private double winRatio = 0;
        private double profitFactor = 0;
        private double maxLoss = 0;
        private double maxWin = 0;
        private double peakEquity = 0;
        private double troughEquity = 0;
        private double maxDrawdown = 0;

        public Buy()
            : base()
        {
            this.Name = "Buy";
            this.Description = "Auto-buy on start, stop after TP or SL.";
        }

        public override string[] MonitoringConnectionsIds => new string[] { this.SymbolMES?.ConnectionId, this.SymbolES?.ConnectionId, this.Account?.ConnectionId };

        protected override void OnRun()
        {
            if (SymbolMES == null || SymbolES == null || Account == null || SymbolMES.ConnectionId != Account.ConnectionId || SymbolES.ConnectionId != Account.ConnectionId)
            {
                Log("Incorrect input parameters... Symbol or Account are not specified or they have different connectionID.", StrategyLoggingLevel.Error);
                return;
            }

            this.SymbolMES = Core.GetSymbol(this.SymbolMES.CreateInfo());
            this.SymbolES = Core.GetSymbol(this.SymbolES.CreateInfo());

            historicalData = this.SymbolES.GetHistory(SelectedPeriod, this.SymbolES.HistoryType, DateTime.UtcNow.AddDays(-555));
            atr = Core.Indicators.BuiltIn.ATR(ATRPeriod, MaMode.EMA);
            historicalData.AddIndicator(atr);

            Core.TradeAdded += OnTradeAdded;
            Core.OrderAdded += OnOrderAdded;

            entryPrice = this.SymbolES.Last;
            PlaceOrder();
        }

        protected override void OnStop()
        {
            Core.TradeAdded -= OnTradeAdded;
            Core.OrderAdded -= OnOrderAdded;
            openPositionsES = false;
            openPositionsMES = false;
        }

        private void PlaceOrder()
        {
            double stopLossDistance = atr.GetValue() * StopLossMultiplier;
            double StopLoss = entryPrice - stopLossDistance;
            double TakeProfit = entryPrice + stopLossDistance * RewardRiskRatio;
            double valuePerContract = (stopLossDistance / this.SymbolES.TickSize) * this.SymbolES.GetTickCost(entryPrice);
            double Qty = (Risk / valuePerContract);

            qtyES = (int)Qty;
            qtyMES = (int)Math.Round((Qty - qtyES) * 10);
            Log($"ES: {qtyES}, MES: {qtyMES}", StrategyLoggingLevel.Info);

            tpOrderCommentES = GenerateComment("tpES");
            slOrderCommentES = GenerateComment("slES");
            tpOrderCommentMES = GenerateComment("tpMES");
            slOrderCommentMES = GenerateComment("slMES");

            if (qtyES > 0)
            {
                var BuyPositionES = Core.Instance.PlaceOrder(this.SymbolES, this.Account, Side.Buy, quantity: qtyES);
                if (BuyPositionES.Status == TradingOperationResultStatus.Success)
                    openPositionsES = true;

                var TPes = new PlaceOrderRequestParameters
                {
                    Symbol = this.SymbolES,
                    Account = this.Account,
                    Side = Side.Sell,
                    Quantity = qtyES,
                    Price = TakeProfit,
                    OrderTypeId = OrderType.Limit,
                    Comment = tpOrderCommentES,
                };
                var SLes = new PlaceOrderRequestParameters
                {
                    Symbol = this.SymbolES,
                    Account = this.Account,
                    Side = Side.Sell,
                    Quantity = qtyES,
                    TriggerPrice = StopLoss,
                    OrderTypeId = OrderType.Stop,
                    Comment = slOrderCommentES,
                };

                Core.PlaceOrder(TPes);
                Core.PlaceOrder(SLes);
            }

            if (qtyMES > 0)
            {
                var BuyPositionMES = Core.Instance.PlaceOrder(this.SymbolMES, this.Account, Side.Buy, quantity: qtyMES);
                if (BuyPositionMES.Status == TradingOperationResultStatus.Success)
                    openPositionsMES = true;

                var TPmes = new PlaceOrderRequestParameters
                {
                    Symbol = this.SymbolMES,
                    Account = this.Account,
                    Side = Side.Sell,
                    Quantity = qtyMES,
                    Price = TakeProfit,
                    OrderTypeId = OrderType.Limit,
                    Comment = tpOrderCommentMES,
                };
                var SLmes = new PlaceOrderRequestParameters
                {
                    Symbol = this.SymbolMES,
                    Account = this.Account,
                    Side = Side.Sell,
                    Quantity = qtyMES,
                    TriggerPrice = StopLoss,
                    OrderTypeId = OrderType.Stop,
                    Comment = slOrderCommentMES,
                };

                var tpOrder = Core.PlaceOrder(TPmes);
                Core.PlaceOrder(SLmes);

                this.Log("TP Order Result: " + tpOrder?.Status + " | " + tpOrder?.Message + " | OrderId: " + tpOrder?.OrderId);


            }
        }

        private void OnOrderAdded(Order order)
        {
            if (tpOrderES == null && order.Comment == tpOrderCommentES)
            {
                tpOrderES = order;
                tpOrderIdES = order.Id;
            }
            if (slOrderES == null && order.Comment == slOrderCommentES)
            {
                slOrderES = order;
                slOrderIdES = order.Id;
            }
            if (tpOrderMES == null && order.Comment == tpOrderCommentMES)
            {
                tpOrderMES = order;
                tpOrderIdMES = order.Id;
            }
            if (slOrderMES == null && order.Comment == slOrderCommentMES)
            {
                slOrderMES = order;
                slOrderIdMES = order.Id;
            }
        }

        private void OnTradeAdded(Trade trade)
        {
            if (trade.OrderId == tpOrderIdES || trade.OrderId == slOrderIdES || trade.OrderId == tpOrderIdMES || trade.OrderId == slOrderIdMES)
            {
                if (trade.OrderId == tpOrderIdES) { tpFilledQtyES += trade.Quantity; Core.CancelOrder((IOrder)slOrderES); }
                if (trade.OrderId == slOrderIdES) { slFilledQtyES += trade.Quantity; Core.CancelOrder((IOrder)tpOrderES); }
                if (trade.OrderId == tpOrderIdMES) { tpFilledQtyMES += trade.Quantity; Core.CancelOrder((IOrder)slOrderMES); }
                if (trade.OrderId == slOrderIdMES) { slFilledQtyMES += trade.Quantity; Core.CancelOrder((IOrder)tpOrderMES); } 

                bool mesClosed = (tpFilledQtyMES + slFilledQtyMES) >= qtyMES;
                bool esClosed = (tpFilledQtyES + slFilledQtyES) >= qtyES;

                if (mesClosed && esClosed)
                {
                    ResetOrderState();
                    this.Stop();
                }

                double tradePnL = trade.GrossPnl.Value;
                double tradeFee = trade.Fee.Value;
                double endPnL = trade.NetPnl.Value;

                grossPnL += tradePnL;
                totalFees += tradeFee * 2;
                netPnL += (endPnL + tradeFee);
                totalTrades++;

                if (tradePnL > 0)
                {
                    Log("Take profit hit");
                    winningTrades++;
                    winRatio = winningTrades / totalTrades;
                    grossProfit += tradePnL;
                }
                else
                {
                    Log("Stop loss hit");
                    losingTrades++;
                    grossLoss += tradePnL;
                    if (tradePnL < maxLoss) maxLoss = tradePnL;
                }

                if (tradePnL > maxWin) maxWin = tradePnL;

                if (grossPnL > peakEquity)
                {
                    peakEquity = grossPnL;
                    troughEquity = grossPnL;
                }

                if (grossPnL < troughEquity)
                {
                    troughEquity = grossPnL;
                    double currentDD = peakEquity - troughEquity;
                    if (currentDD > maxDrawdown)
                        maxDrawdown = currentDD;
                }

                profitFactor = grossLoss == 0 ? grossProfit : grossProfit / Math.Abs(grossLoss);
            }
        }

        private void ResetOrderState()
        {
            openPositionsES = false;
            openPositionsMES = false;

            tpOrderES = null; slOrderES = null;
            tpOrderMES = null; slOrderMES = null;
            tpOrderIdES = slOrderIdES = tpOrderIdMES = slOrderIdMES = null;
            tpOrderCommentES = slOrderCommentES = tpOrderCommentMES = slOrderCommentMES = null;
            qtyES = qtyMES = 0;
            tpFilledQtyES = slFilledQtyES = tpFilledQtyMES = slFilledQtyMES = 0;

            Log("OrderState Reset", StrategyLoggingLevel.Info);
        }

        private string GenerateComment(string descriptor)
        {
            comment++;
            DateTime time = TimeZoneInfo.ConvertTime(this.SymbolES.LastDateTime, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
            return $"{this.Name}-{descriptor}-{time}-{comment}";
        }

        protected override void OnInitializeMetrics(Meter meter)
        {
            base.OnInitializeMetrics(meter);

            meter.CreateObservableCounter("profit-factor", () => this.profitFactor);
            meter.CreateObservableCounter("max-drawdown", () => this.maxDrawdown);
            meter.CreateObservableCounter("max-loss", () => this.maxLoss);
            meter.CreateObservableCounter("max-win", () => this.maxWin);
            meter.CreateObservableCounter("fees", () => this.totalFees);
            meter.CreateObservableCounter("gross-loss", () => this.grossLoss);
            meter.CreateObservableCounter("gross-profit", () => this.grossProfit);
            meter.CreateObservableCounter("losing-trades", () => this.losingTrades);
            meter.CreateObservableCounter("winning-trades", () => this.winningTrades);
            meter.CreateObservableCounter("win-ratio", () => this.winRatio);
            meter.CreateObservableCounter("trades", () => this.totalTrades);
            meter.CreateObservableCounter("net-pnl", () => this.netPnL);
        }
    }
}
